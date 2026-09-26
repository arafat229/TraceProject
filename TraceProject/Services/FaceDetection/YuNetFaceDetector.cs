using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Runtime.InteropServices;
using TraceProject.Configuration;
using TraceProject.Models;
using TraceProject.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace TraceProject.Services.FaceDetection;

/// <summary>
/// YuNet face detector implementation using OpenCvSharp4 with ONNX model.
/// Phase 3: Real face detection (detection only, not recognition).
/// Uses the YuNet ONNX model distributed with OpenCV for fast, lightweight face detection.
/// Loads the model once at startup and reuses the detector instance.
/// </summary>
public class YuNetFaceDetector : IFaceDetector
{
    private readonly ILogger<YuNetFaceDetector> logger;
    private readonly FaceDetectionOptions options;
    private readonly string modelPath;
    private FaceDetectorYN? detector;
    private Size detectorInputSize;
    private readonly object detectorLock = new();

    // FaceDetectorYN has no SetInputSize in OpenCvSharp, so frames are scaled to this width
    // (keeping aspect ratio) and the detector is recreated only if that size changes.
    private const int DetectionWidth = 640;
    private const float NmsThreshold = 0.3f;
    private const int TopK = 5000;

    // Reference landmarks of the 112x112 face template used by SFace / cv::FaceRecognizerSF::alignCrop
    // (right eye, left eye, nose tip, right mouth corner, left mouth corner).
    private static readonly Point2f[] AlignmentTemplate =
    {
        new(38.2946f, 51.6963f),
        new(73.5318f, 51.5014f),
        new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f),
        new(70.7299f, 92.2041f),
    };
    private const int AlignedFaceSize = 112;
    private bool isReady = false;
    private bool disposed = false;

    public bool IsReady => isReady && detector is not null && !disposed;

    public YuNetFaceDetector(
        IOptions<FaceDetectionOptions> optionsInput,
        ILogger<YuNetFaceDetector> logger)
    {
        this.options = optionsInput.Value;
        this.logger = logger;
        this.modelPath = options.ModelPath ?? "Models/FaceDetection/face_detection_yunet_2023mar.onnx";
    }

    /// <summary>
    /// Initialize the detector by loading the YuNet ONNX model.
    /// Called once at startup via hosted service.
    /// Fails loudly and clearly if the model cannot be loaded.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Run model loading off the UI thread
            await Task.Run(() =>
            {
                if (isReady)
                {
                    logger.LogWarning("YuNetFaceDetector is already initialized");
                    return;
                }

                // Verify model file exists
                if (!File.Exists(modelPath))
                {
                    var message = $"YuNet model file not found at: {Path.GetFullPath(modelPath)}. " +
                        $"Please download it from https://github.com/opencv/opencv_zoo/tree/master/models/face_detection_yunet " +
                        $"and place it in the Models/FaceDetection directory.";
                    logger.LogError(message);
                    throw new FileNotFoundException(message);
                }

                var fullPath = Path.GetFullPath(modelPath);
                var fileInfo = new FileInfo(fullPath);

                // Check if this is a placeholder file (created for development)
                if (fileInfo.Length < 1024)
                {
                    logger.LogWarning(
                        "⚠️  PLACEHOLDER MODEL DETECTED at {modelPath} ({size} bytes). " +
                        "This is a development placeholder, not the real YuNet model. " +
                        "Face detection will NOT work. " +
                        "To get the real model: " +
                        "1. Visit: https://github.com/opencv/opencv_zoo/tree/master/models/face_detection_yunet " +
                        "2. Download: face_detection_yunet_2023mar.onnx (should be ~230 KB) " +
                        "3. Replace the placeholder file. " +
                        "The detector will run in DEMO mode (returns empty results for testing).",
                        fullPath, fileInfo.Length);

                    // If it's a placeholder, we'll run in demo mode (no actual inference)
                    isReady = true;
                    logger.LogInformation(
                        "YuNet face detector initialized in DEMO MODE (placeholder model). " +
                        "ConfidenceThreshold: {threshold}, InputSize: 416x416. " +
                        "This will not detect real faces - for testing only.",
                        options.ConfidenceThreshold);
                    return;
                }

                logger.LogInformation("Loading YuNet face detector model from: {modelPath}", fullPath);

                // cv::FaceDetectorYN runs YuNet and does its anchor decoding, score filtering and NMS
                detector = CreateDetector(new Size(DetectionWidth, DetectionWidth * 9 / 16));

                if (detector == null)
                {
                    var message = $"Failed to load YuNet model from {modelPath}";
                    logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                isReady = true;
                logger.LogInformation(
                    "✓ YuNet face detector loaded successfully (FaceDetectorYN). " +
                    "ConfidenceThreshold: {threshold}",
                    options.ConfidenceThreshold);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize YuNet face detector");
            isReady = false;
            throw;
        }
    }

    /// <summary>
    /// Detect faces in a video frame.
    /// Converts frame to OpenCV Mat, runs detector, extracts results, and disposes Mat.
    /// </summary>
    public async Task<IReadOnlyList<DetectedFace>> DetectFacesAsync(
        IVideoFrame frame,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            logger.LogWarning("Face detector not ready; cannot process frame");
            return new List<DetectedFace>();
        }

        if (frame == null)
        {
            return new List<DetectedFace>();
        }

        // If running in demo mode (detector is null but isReady is true), return empty results
        if (detector == null)
        {
            return new List<DetectedFace>();
        }

        Mat? sourceMat = null;
        Mat? resizedMat = null;

        try
        {
            return await Task.Run(() =>
            {
                // Convert frame to OpenCV Mat
                sourceMat = ConvertFrameToMat(frame);
                if (sourceMat == null)
                {
                    logger.LogWarning("Failed to convert frame to Mat; skipping detection");
                    return new List<DetectedFace>();
                }

                // Scale down to the detection width, keeping the aspect ratio
                double scale = sourceMat.Width > DetectionWidth ? (double)DetectionWidth / sourceMat.Width : 1.0;
                var inputSize = new Size((int)Math.Round(sourceMat.Width * scale), (int)Math.Round(sourceMat.Height * scale));
                resizedMat = new Mat();
                Cv2.Resize(sourceMat, resizedMat, inputSize, interpolation: InterpolationFlags.Linear);

                using var detections = new Mat();
                lock (detectorLock)
                {
                    if (inputSize != detectorInputSize)
                    {
                        detector?.Dispose();
                        detector = CreateDetector(inputSize);
                    }
                    detector!.Detect(resizedMat, detections);
                }

                var detectedFaces = ExtractFaces(frame, sourceMat, detections, scale);
                return detectedFaces;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Face detection was cancelled");
            return new List<DetectedFace>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during face detection");
            return new List<DetectedFace>();
        }
        finally
        {
            // Clean up OpenCV Mat objects to avoid native memory leaks
            resizedMat?.Dispose();
            sourceMat?.Dispose();
        }
    }

    /// <summary>
    /// Convert an IVideoFrame to an OpenCV Mat.
    /// Handles I420 planar format (typical from LibVLC).
    /// Returns null if conversion fails.
    /// </summary>
    private Mat? ConvertFrameToMat(IVideoFrame frame)
    {
        try
        {
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Data == null)
            {
                logger.LogWarning("Invalid frame dimensions or data");
                return null;
            }

            // Handle I420 (YUV planar) format from LibVLC
            if (frame.PixelFormat.Equals("I420", StringComparison.OrdinalIgnoreCase))
            {
                return ConvertI420ToMat(frame);
            }

            // Handle YUYV422 (interleaved YUV) format
            if (frame.PixelFormat.Equals("YUYV422", StringComparison.OrdinalIgnoreCase) ||
                frame.PixelFormat.Equals("YUY2", StringComparison.OrdinalIgnoreCase))
            {
                return ConvertYUYV422ToMat(frame);
            }

            // BGRA from RtspVideoSource (LibVLC RV32)
            if (frame.PixelFormat.Equals("BGRA", StringComparison.OrdinalIgnoreCase))
            {
                GCHandle bgraHandle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
                try
                {
                    using var bgraMat = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC4, bgraHandle.AddrOfPinnedObject());
                    var bgrMat = new Mat();
                    Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                    return bgrMat;
                }
                finally
                {
                    bgraHandle.Free();
                }
            }

            // Assume raw BGR data as fallback
            // Packed BGR (e.g. StillImageFrame for uploaded photos) or an unknown format treated as BGR
            if (!frame.PixelFormat.Equals("BGR", StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("Unknown pixel format: {format}; attempting BGR interpretation", frame.PixelFormat);
            GCHandle handle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
            try
            {
                using var mat = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC3, handle.AddrOfPinnedObject());
                return mat.Clone();
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert frame to Mat");
            return null;
        }
    }

    /// <summary>
    /// Convert I420 (YUV planar) format to BGR Mat.
    /// I420 layout: Y plane (full resolution) + U plane (quarter resolution) + V plane (quarter resolution).
    /// </summary>
    private Mat? ConvertI420ToMat(IVideoFrame frame)
    {
        try
        {
            int width = frame.Width;
            int height = frame.Height;
            byte[] data = frame.Data;

            // I420: Y plane size = width * height, U and V planes = width * height / 4 each
            int ySize = width * height;
            int uvSize = ySize / 4;

            if (data.Length < ySize + 2 * uvSize)
            {
                logger.LogWarning("Frame data size mismatch for I420 conversion");
                return null;
            }

            // Create Mat from I420 data
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Mat yuvMat = Mat.FromPixelData(height + height / 2, width, MatType.CV_8UC1, handle.AddrOfPinnedObject());
                Mat bgrMat = new Mat();

                    // Convert YUV420 to BGR
                    Cv2.CvtColor(yuvMat, bgrMat, ColorConversionCodes.YUV420p2BGR);

                    yuvMat.Dispose();

                    return bgrMat;
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to convert I420 frame to Mat");
                return null;
        }
    }

    /// <summary>
    /// Convert YUYV422 (YUV interleaved) format to BGR Mat.
    /// </summary>
    private Mat? ConvertYUYV422ToMat(IVideoFrame frame)
    {
        try
        {
            // Create Mat from YUYV422 data
            GCHandle handle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
            try
            {
                Mat yuyvMat = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC2, handle.AddrOfPinnedObject());
                Mat bgrMat = new Mat();

                // Convert YUV422 to BGR
                Cv2.CvtColor(yuyvMat, bgrMat, ColorConversionCodes.YUV2BGR_YUY2);

                yuyvMat.Dispose();

                return bgrMat;
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert YUYV422 frame to Mat");
            return null;
        }
    }

    private FaceDetectorYN CreateDetector(Size inputSize)
    {
        detectorInputSize = inputSize;
        return FaceDetectorYN.Create(modelPath, "", inputSize, (float)options.ConfidenceThreshold,
            NmsThreshold, TopK, Backend.OPENCV, Target.CPU);
    }

    /// <summary>
    /// Convert FaceDetectorYN output to DetectedFace objects. Each row (CV_32F) is
    /// [x, y, w, h, x_re, y_re, x_le, y_le, x_nt, y_nt, x_rcm, y_rcm, x_lcm, y_lcm, score]
    /// in detection-image coordinates; they are scaled back to the original frame.
    /// FaceImageData holds the face aligned to SFace's 112x112 template as raw BGR bytes.
    /// </summary>
    private List<DetectedFace> ExtractFaces(IVideoFrame frame, Mat originalMat, Mat detections, double scale)
    {
        var faces = new List<DetectedFace>();
        if (detections.Empty())
            return faces;

        try
        {
            float inv = (float)(1.0 / scale);
            for (int i = 0; i < detections.Rows; i++)
            {
                float score = detections.At<float>(i, 14);

                int boxX = Math.Max(0, (int)(detections.At<float>(i, 0) * inv));
                int boxY = Math.Max(0, (int)(detections.At<float>(i, 1) * inv));
                int boxW = Math.Min((int)(detections.At<float>(i, 2) * inv), originalMat.Width - boxX);
                int boxH = Math.Min((int)(detections.At<float>(i, 3) * inv), originalMat.Height - boxY);
                if (boxW <= 0 || boxH <= 0)
                    continue;

                var landmarks = new Point2f[5];
                for (int k = 0; k < 5; k++)
                {
                    landmarks[k] = new Point2f(detections.At<float>(i, 4 + k * 2) * inv, detections.At<float>(i, 5 + k * 2) * inv);
                }

                var aligned = AlignFace(originalMat, landmarks);
                if (aligned is null)
                    continue;

                faces.Add(new DetectedFace
                {
                    IsFrontal = IsFrontal(landmarks, boxW),
                    BoundingBox = (boxX, boxY, boxW, boxH),
                    Confidence = score,
                    FaceImageData = aligned,
                    FaceImageWidth = AlignedFaceSize,
                    FaceImageHeight = AlignedFaceSize,
                    DetectionTimestampUtc = frame.TimestampUtc
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting faces from detector output");
        }

        return faces;
    }

    /// <summary>
    /// A face looks at the camera when both eyes are clearly apart (inter-eye distance at least a
    /// quarter of the face width; about 0.4 when frontal, near 0 in profile) and the nose tip lies
    /// between the eyes horizontally.
    /// </summary>
    private static bool IsFrontal(Point2f[] landmarks, int faceWidth)
    {
        var (rightEye, leftEye, nose) = (landmarks[0], landmarks[1], landmarks[2]);
        float eyeDistance = MathF.Abs(leftEye.X - rightEye.X);
        if (faceWidth <= 0 || eyeDistance / faceWidth < 0.25f)
            return false;

        float minX = MathF.Min(leftEye.X, rightEye.X), maxX = MathF.Max(leftEye.X, rightEye.X);
        float margin = eyeDistance * 0.1f;
        return nose.X > minX + margin && nose.X < maxX - margin;
    }

    /// <summary>
    /// Warp the face so its 5 landmarks match SFace's template (same idea as
    /// cv::FaceRecognizerSF::alignCrop). Returns 112x112 BGR pixels, or null on failure.
    /// </summary>
    private static byte[]? AlignFace(Mat image, Point2f[] landmarks)
    {
        using var src = InputArray.Create(landmarks);
        using var dst = InputArray.Create(AlignmentTemplate);
        using var transform = Cv2.EstimateAffinePartial2D(src, dst, null, RobustEstimationAlgorithms.LMEDS);
        if (transform is null || transform.Empty())
            return null;

        using var aligned = new Mat();
        Cv2.WarpAffine(image, aligned, transform, new Size(AlignedFaceSize, AlignedFaceSize));

        var bytes = new byte[AlignedFaceSize * AlignedFaceSize * 3];
        Marshal.Copy(aligned.Data, bytes, 0, bytes.Length);
        return bytes;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        try
        {
            detector?.Dispose();
            detector = null;
            isReady = false;
            disposed = true;

            await Task.CompletedTask;
            logger.LogInformation("YuNetFaceDetector disposed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing YuNetFaceDetector");
        }
    }
}
