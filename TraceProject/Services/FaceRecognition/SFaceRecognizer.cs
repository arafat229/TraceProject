using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Runtime.InteropServices;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Models;
using TraceProject.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TraceProject.Services.FaceRecognition;

/// <summary>
/// SFace face recognizer implementation using OpenCvSharp4 with ONNX model.
/// Phase 4: Face recognition via embedding-based similarity matching.
/// Loads the SFace ONNX model once at startup and reuses the instance.
/// Caches employee embeddings in memory for fast matching on every frame.
/// </summary>
public class SFaceRecognizer : IFaceRecognizer
{
    private readonly ILogger<SFaceRecognizer> logger;
    private readonly FaceRecognitionOptions options;
    private readonly string modelPath;

    private OpenCvSharp.Dnn.Net? recognizer;
    private bool isReady = false;
    private bool disposed = false;

    // Temporary dbContext storage for initialization and cache refresh
    private ApplicationDbContext? tempDbContext = null;

    /// <summary>
    /// In-memory cache of all active employee embeddings.
    /// Key: EmployeeId, Value: list of (EmbeddingId, EmbeddingData as float[])
    /// Refreshed periodically from the database.
    /// </summary>
    private Dictionary<long, List<(long EmbeddingId, float[] EmbeddingVector, string? Source)>> embeddingCache = new();
    private Dictionary<long, string> employeeNameCache = new(); // EmployeeId -> EmployeeName for logging
    private DateTime lastCacheRefreshUtc = DateTime.MinValue;
    private readonly object cacheLock = new();

    // SFace model constants
    private const int SFaceInputSize = 112; // SFace model input is 112x112
    private const int SFaceEmbeddingDimension = 128;

    public bool IsReady => isReady && recognizer is not null && !disposed;

    public SFaceRecognizer(
        IOptions<FaceRecognitionOptions> optionsInput,
        ILogger<SFaceRecognizer> logger)
    {
        this.options = optionsInput.Value;
        this.logger = logger;
        this.modelPath = options.ModelPath ?? "Models/FaceRecognition/face_recognition_sface_2021dec.onnx";
    }

    /// <summary>
    /// Initialize the recognizer by loading the SFace ONNX model and caching embeddings.
    /// Called once at startup via hosted service after app is built.
    /// </summary>
    public async Task InitializeAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        // Store dbContext temporarily for use in RefreshEmbeddingCacheAsync
        tempDbContext = dbContext;

        try
        {
            await Task.Run(() =>
            {
                if (isReady)
                {
                    logger.LogWarning("SFaceRecognizer is already initialized");
                    return;
                }

                // Verify model file exists
                if (!File.Exists(modelPath))
                {
                    var message = $"SFace model file not found at: {Path.GetFullPath(modelPath)}. " +
                        $"Please download it from https://github.com/opencv/opencv_zoo/tree/master/models/face_recognition_sface " +
                        $"and place it in the Models/FaceRecognition directory.";
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
                        "This is a development placeholder, not the real SFace model. " +
                        "Face recognition will NOT work. " +
                        "To get the real model: " +
                        "1. Visit: https://github.com/opencv/opencv_zoo/tree/master/models/face_recognition_sface " +
                        "2. Download: face_recognition_sface_*.onnx (should be ~730 KB) " +
                        "3. Replace the placeholder file. " +
                        "The recognizer will run in DEMO MODE (all faces unknown for testing).",
                        fullPath, fileInfo.Length);

                    // If it's a placeholder, we'll run in demo mode (no actual inference)
                    isReady = true;
                    logger.LogInformation(
                        "SFace recognizer initialized in DEMO MODE (placeholder model). " +
                        "SimilarityThreshold: {threshold}. " +
                        "This will return all faces as Unknown - for testing only.",
                        options.SimilarityThreshold);
                    return;
                }

                logger.LogInformation("Loading SFace face recognition model from: {modelPath}", fullPath);

                // Load SFace recognizer as a DNN Net model (ONNX format)
                recognizer = CvDnn.ReadNetFromOnnx(modelPath);

                if (recognizer == null)
                {
                    var message = $"Failed to load SFace model from {modelPath}";
                    logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                isReady = true;
                logger.LogInformation(
                    "✓ SFace face recognition model loaded successfully. " +
                    "SimilarityThreshold: {threshold}, EmbeddingDimension: {dim}",
                    options.SimilarityThreshold, SFaceEmbeddingDimension);
            }, cancellationToken);

            // Load embeddings after model is ready
            await RefreshEmbeddingCacheAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize SFace recognizer");
            isReady = false;
            throw;
        }
    }

    /// <summary>
    /// Recognize a detected face against cached employee embeddings.
    /// Returns the best match if confidence >= threshold, or Unknown if no match.
    /// </summary>
    public async Task<RecognitionResult> RecognizeAsync(
        DetectedFace face,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            logger.LogWarning("Face recognizer not ready; cannot process face");
            return RecognitionResult.Unknown();
        }

        if (face == null || face.FaceImageData == null || face.FaceImageData.Length == 0)
        {
            return RecognitionResult.Unknown();
        }

        // If running in demo mode (recognizer is null but isReady is true), return unknown
        if (recognizer == null)
        {
            return RecognitionResult.Unknown();
        }

        Mat? faceMat = null;
        Mat? alignedFaceMat = null;
        Mat? embeddingMat = null;

        try
        {
            return await Task.Run(() =>
            {
                // Convert face crop to OpenCV Mat
                faceMat = ConvertFaceImageToMat(face);
                if (faceMat == null || faceMat.Empty())
                {
                    logger.LogWarning("Failed to convert face image to Mat; treating as unknown");
                    return RecognitionResult.Unknown();
                }

                // Align face crop to 112x112 (SFace input size)
                alignedFaceMat = new Mat();
                Cv2.Resize(faceMat, alignedFaceMat, new Size(SFaceInputSize, SFaceInputSize), interpolation: InterpolationFlags.Linear);

                // Same preprocessing as cv::FaceRecognizerSF::feature: raw 0-255 pixels, BGR->RGB.
                // The SFace model normalizes internally.
                using Mat blob = CvDnn.BlobFromImage(
                    alignedFaceMat,
                    1.0,
                    new Size(SFaceInputSize, SFaceInputSize),
                    new Scalar(0, 0, 0),
                    swapRB: true,
                    crop: false);

                recognizer!.SetInput(blob);

                // Run inference to compute embedding
                embeddingMat = recognizer!.Forward();

                if (embeddingMat == null || embeddingMat.Empty())
                {
                    logger.LogWarning("Failed to compute embedding from face; treating as unknown");
                    return RecognitionResult.Unknown();
                }

                // Convert embedding Mat to float array
                float[] embeddingVector = ConvertMatToFloatArray(embeddingMat);

                // Find best match in cached embeddings
                return FindBestMatchAsync(embeddingVector);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during face recognition");
            return RecognitionResult.Unknown();
        }
        finally
        {
            faceMat?.Dispose();
            alignedFaceMat?.Dispose();
            embeddingMat?.Dispose();
        }
    }

    /// <summary>
    /// Compute an embedding vector from a detected face.
    /// Used by enrollment and other services that need embeddings without recognition matching.
    /// Returns the embedding as a byte array of float32 values.
    /// </summary>
    public async Task<byte[]?> ComputeEmbeddingAsync(
        DetectedFace face,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            logger.LogWarning("Face recognizer not ready; cannot compute embedding");
            return null;
        }

        if (face == null || face.FaceImageData == null || face.FaceImageData.Length == 0)
        {
            return null;
        }

        // If running in demo mode, return null
        if (recognizer == null)
        {
            return null;
        }

        Mat? faceMat = null;
        Mat? alignedFaceMat = null;
        Mat? embeddingMat = null;

        try
        {
            return await Task.Run(() =>
            {
                // Convert face crop to OpenCV Mat
                faceMat = ConvertFaceImageToMat(face);
                if (faceMat == null || faceMat.Empty())
                {
                    logger.LogWarning("Failed to convert face image to Mat; cannot compute embedding");
                    return null;
                }

                // Align face crop to 112x112 (SFace input size)
                alignedFaceMat = new Mat();
                Cv2.Resize(faceMat, alignedFaceMat, new Size(SFaceInputSize, SFaceInputSize), interpolation: InterpolationFlags.Linear);

                // Same preprocessing as cv::FaceRecognizerSF::feature: raw 0-255 pixels, BGR->RGB.
                // The SFace model normalizes internally.
                using Mat blob = CvDnn.BlobFromImage(
                    alignedFaceMat,
                    1.0,
                    new Size(SFaceInputSize, SFaceInputSize),
                    new Scalar(0, 0, 0),
                    swapRB: true,
                    crop: false);

                recognizer!.SetInput(blob);

                // Run inference to compute embedding
                embeddingMat = recognizer!.Forward();

                if (embeddingMat == null || embeddingMat.Empty())
                {
                    logger.LogWarning("Failed to compute embedding from face");
                    return null;
                }

                // Convert embedding Mat to float array
                float[] embeddingVector = ConvertMatToFloatArray(embeddingMat);

                // Convert float array to bytes
                byte[] embeddingBytes = new byte[embeddingVector.Length * sizeof(float)];
                Buffer.BlockCopy(embeddingVector, 0, embeddingBytes, 0, embeddingBytes.Length);

                return embeddingBytes;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error computing embedding");
            return null;
        }
        finally
        {
            faceMat?.Dispose();
            alignedFaceMat?.Dispose();
            embeddingMat?.Dispose();
        }
    }

    /// <summary>
    /// Load all active employee embeddings from the database into memory cache.
    /// </summary>
    public async Task RefreshEmbeddingCacheAsync(CancellationToken cancellationToken = default)
    {
        // Use temporarily stored dbContext from initialization, or fail if not initialized
        if (tempDbContext == null)
        {
            logger.LogWarning("RefreshEmbeddingCacheAsync called before initialization or after disposed. Skipping refresh.");
            return;
        }

        try
        {
            logger.LogInformation("Refreshing face embedding cache from database...");

            // Fetch all active employees with their active embeddings
            var employeesWithEmbeddings = await tempDbContext.Employees
                .Where(e => e.IsActive)
                .Include(e => e.FaceEmbeddings)
                .ToListAsync(cancellationToken);

            lock (cacheLock)
            {
                embeddingCache.Clear();
                employeeNameCache.Clear();

                int totalEmbeddingsLoaded = 0;

                foreach (var employee in employeesWithEmbeddings)
                {
                    if (employee.FaceEmbeddings == null || employee.FaceEmbeddings.Count == 0)
                        continue;

                    // Filter to active embeddings
                    var activeEmbeddings = employee.FaceEmbeddings
                        .Where(fe => fe.IsActive && fe.EmbeddingData != null && fe.EmbeddingData.Length > 0)
                        .ToList();

                    if (activeEmbeddings.Count == 0)
                        continue;

                    // Apply max embeddings per employee limit if set
                    var embeddingsToCache = options.MaxEmbeddingsPerEmployee > 0
                        ? activeEmbeddings
                            .OrderByDescending(fe => fe.CreatedAtUtc)
                            .Take(options.MaxEmbeddingsPerEmployee)
                            .ToList()
                        : activeEmbeddings;

                    // Convert embeddings from bytes to float arrays
                    var embeddingVectors = embeddingsToCache.Select(fe => (
                        fe.Id,
                        ConvertBytesToFloatArray(fe.EmbeddingData, fe.EmbeddingDimension),
                        fe.Source
                    )).ToList();

                    embeddingCache[employee.Id] = embeddingVectors;
                    employeeNameCache[employee.Id] = employee.FullName;
                    totalEmbeddingsLoaded += embeddingVectors.Count;
                }

                lastCacheRefreshUtc = DateTime.UtcNow;
                logger.LogInformation(
                    "✓ Face embedding cache refreshed. Employees with embeddings: {empCount}, Total embeddings: {embCount}",
                    embeddingCache.Count, totalEmbeddingsLoaded);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh face embedding cache");
            throw;
        }
    }

    /// <summary>
    /// Get cache statistics for monitoring/debug.
    /// </summary>
    public (int TotalEmbeddings, int EmployeesWithEmbeddings, DateTime LastRefreshUtc) GetCacheStatistics()
    {
        lock (cacheLock)
        {
            int totalEmbeddings = embeddingCache.Values.Sum(list => list.Count);
            return (totalEmbeddings, embeddingCache.Count, lastCacheRefreshUtc);
        }
    }

    /// <summary>
    /// Find the best matching employee for a given embedding vector.
    /// Returns a match only if the best similarity exceeds the configured threshold.
    /// </summary>
    private RecognitionResult FindBestMatchAsync(float[] embeddingVector)
    {
        if (embeddingVector == null || embeddingVector.Length != SFaceEmbeddingDimension)
        {
            logger.LogWarning(
                "Invalid embedding dimension: expected {expected}, got {actual}",
                SFaceEmbeddingDimension, embeddingVector?.Length ?? 0);
            return RecognitionResult.Unknown();
        }

        lock (cacheLock)
        {
            if (embeddingCache.Count == 0)
            {
                return RecognitionResult.Unknown();
            }

            float bestSimilarity = 0f;
            long bestEmployeeId = -1;
            long bestEmbeddingId = -1;
            string? bestEmbeddingSource = null;

            foreach (var (employeeId, embeddingList) in embeddingCache)
            {
                foreach (var (embeddingId, cachedVector, source) in embeddingList)
                {
                    float similarity = ComputeCosineSimilarity(embeddingVector, cachedVector);

                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestEmployeeId = employeeId;
                        bestEmbeddingId = embeddingId;
                        bestEmbeddingSource = source;
                    }
                }
            }

            // Check if the best match exceeds the threshold
            if (bestSimilarity >= options.SimilarityThreshold && bestEmployeeId >= 0)
            {
                string? employeeName = employeeNameCache.TryGetValue(bestEmployeeId, out var name) ? name : null;
                return RecognitionResult.Match(bestEmployeeId, employeeName ?? "Unknown", bestSimilarity, bestEmbeddingSource);
            }

            return RecognitionResult.Unknown();
        }
    }

    /// <summary>
    /// Convert a face crop (as DetectedFace.FaceImageData) to an OpenCV Mat.
    /// Assumes BGR format as produced by YuNet detector.
    /// </summary>
    private Mat? ConvertFaceImageToMat(DetectedFace face)
    {
        try
        {
            if (face.FaceImageData == null || face.FaceImageData.Length == 0)
                return null;

            // Create Mat from raw BGR bytes
            GCHandle handle = GCHandle.Alloc(face.FaceImageData, GCHandleType.Pinned);
            try
            {
                // Copy, so the Mat does not keep pointing at the array after it is unpinned
                using Mat mat = Mat.FromPixelData(face.FaceImageHeight, face.FaceImageWidth, MatType.CV_8UC3, handle.AddrOfPinnedObject());
                return mat.Clone();
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert face image data to Mat");
            return null;
        }
    }

    /// <summary>
    /// Convert an OpenCV Mat to a float array.
    /// Extracts the embedding vector from the DNN output.
    /// </summary>
    private float[] ConvertMatToFloatArray(Mat mat)
    {
        try
        {
            if (mat == null || mat.Empty())
                return Array.Empty<float>();

            // The embedding output should be a 1D vector (1 x 1 x 128)
            mat = mat.Reshape(1, 1);
            int elemCount = (int)mat.Total();

            // Extract float values from Mat using unsafe pointer access
            float[] embedding = new float[elemCount];

            // Use Marshal to read data from unmanaged memory
            System.Runtime.InteropServices.Marshal.Copy(mat.Data, embedding, 0, elemCount);

            // Normalize embedding to unit length (L2 norm)
            float norm = (float)Math.Sqrt(embedding.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] /= norm;
                }
            }

            return embedding;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert Mat to float array");
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Convert raw bytes (stored in database) to a float array.
    /// </summary>
    private float[] ConvertBytesToFloatArray(byte[] bytes, int expectedDimension)
    {
        try
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<float>();

            int floatCount = bytes.Length / sizeof(float);
            if (floatCount != expectedDimension)
            {
                logger.LogWarning(
                    "Embedding dimension mismatch: expected {expected}, but got {actual} from {size} bytes",
                    expectedDimension, floatCount, bytes.Length);
                return Array.Empty<float>();
            }

            float[] floats = new float[floatCount];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert bytes to float array");
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Compute cosine similarity between two embedding vectors.
    /// Formula: dot(a, b) / (norm(a) * norm(b))
    /// Returns value in range [0, 1], where 1 = identical vectors.
    /// </summary>
    private float ComputeCosineSimilarity(float[] vec1, float[] vec2)
    {
        if (vec1 == null || vec2 == null || vec1.Length != vec2.Length)
            return 0f;

        float dotProduct = 0f;
        for (int i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
        }

        // Vectors should already be normalized (L2 norm = 1), so denominator = 1
        // But ensure it's in valid range [0, 1]
        return Math.Max(0, Math.Min(1, dotProduct));
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        isReady = false;

        try
        {
            // Dispose OpenCV resources
            recognizer?.Dispose();
            recognizer = null;

            lock (cacheLock)
            {
                embeddingCache.Clear();
                employeeNameCache.Clear();
            }

            logger.LogInformation("SFace recognizer disposed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing SFace recognizer");
        }

        GC.SuppressFinalize(this);
    }
}
