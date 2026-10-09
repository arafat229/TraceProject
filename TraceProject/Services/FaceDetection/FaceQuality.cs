using System.Runtime.InteropServices;
using OpenCvSharp;
using TraceProject.Configuration;
using TraceProject.Models;

namespace TraceProject.Services.FaceDetection;

/// <summary>
/// Whether a detected face is good enough to recognize or to enroll. Poor faces give poor
/// embeddings: they do not match their owner (piling up as unknown faces) and, worse, can drift
/// toward someone else. Enrollment is stricter than recognition, because one bad enrolled face
/// keeps causing mistakes until it is removed.
///
/// Thresholds were set from this system's own data (196 stored unknown faces and the enrolled
/// gallery, October 2026): the blurriest 8 % of faces (sharpness below 100) were visibly smeared,
/// and a hand detected as a "face" at confidence 0.74 scored 0.355 against an employee, just
/// under the 0.363 match threshold.
/// </summary>
public static class FaceQuality
{
    // Enrollment: only clear, complete, confident faces go into the gallery
    public const float MinEnrollmentScore = 0.9f;
    public const int MinEnrollmentFaceSize = 80;
    // Enrolled faces with 6–9 % cut off at the top still matched their owner at 0.67, so allow up to 10 %
    public const double MaxEnrollmentCutOff = 0.10;
    public const double MinEnrollmentSharpness = 400;

    /// <summary>Variance of the Laplacian of a BGR image: a standard focus / motion-blur measure.</summary>
    public static double Sharpness(Mat bgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using var laplacian = new Mat();
        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stdDev);
        return stdDev.Val0 * stdDev.Val0;
    }

    /// <summary>Sharpness and black (outside-the-picture) share of a stored face crop, e.g. a decoded JPEG.</summary>
    public static (double Sharpness, double BlackFraction) MeasureCrop(byte[] bgr, int width, int height)
    {
        var handle = GCHandle.Alloc(bgr, GCHandleType.Pinned);
        try
        {
            using var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, handle.AddrOfPinnedObject());
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var black = new Mat();
            // Warp borders are pure black; JPEG keeps them within a few levels of 0
            Cv2.Threshold(gray, black, 3, 255, ThresholdTypes.BinaryInv);
            return (Sharpness(mat), Cv2.CountNonZero(black) / (double)(width * height));
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Why the face should not be recognized, or null. Used for every face the camera detects.</summary>
    public static string? RecognitionProblem(DetectedFace face, FaceDetectionOptions options)
    {
        if (!face.IsFrontal)
            return "side profile";
        if (face.Confidence < options.MinRecognitionScore)
            return $"low detection confidence ({face.Confidence:F2})";
        if (Math.Min(face.BoundingBox.Width, face.BoundingBox.Height) < options.MinRecognitionFaceSize)
            return $"too small ({face.BoundingBox.Width}px)";
        if (face.CutOffFraction > options.MaxCutOffFraction)
            return $"cut off by the frame edge ({face.CutOffFraction:P0})";
        if (face.Sharpness >= 0 && face.Sharpness < options.MinSharpness)
            return $"blurry (sharpness {face.Sharpness:F0})";
        return null;
    }

    /// <summary>Why the face should not be enrolled, as a message for the person enrolling, or null.</summary>
    public static string? EnrollmentProblem(DetectedFace face)
    {
        if (!face.IsFrontal)
            return "The face is turned sideways. Enroll a face looking straight at the camera — side profiles cause mix-ups between people.";
        if (face.Confidence < MinEnrollmentScore)
            return $"The face is not clear enough (detection confidence {face.Confidence:F2}, at least {MinEnrollmentScore:F2} needed). Face the camera in good light, without hands or objects in front of the face.";
        if (Math.Min(face.BoundingBox.Width, face.BoundingBox.Height) < MinEnrollmentFaceSize)
            return $"The face is too small ({face.BoundingBox.Width}px, at least {MinEnrollmentFaceSize}px needed). Come closer to the camera.";
        return CropProblem(face.CutOffFraction, face.Sharpness);
    }

    /// <summary>Enrollment check for a face crop that has no detection data (an unknown face, a stored thumbnail).</summary>
    public static string? CropProblem(double cutOffFraction, double sharpness)
    {
        if (cutOffFraction > MaxEnrollmentCutOff)
            return $"Part of the face is outside the picture ({cutOffFraction:P0}). The whole face must be visible.";
        if (sharpness >= 0 && sharpness < MinEnrollmentSharpness)
            return $"The face is blurry (sharpness {sharpness:F0}, at least {MinEnrollmentSharpness:F0} needed). Hold still, with good lighting.";
        return null;
    }
}
