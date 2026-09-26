using TraceProject.Data;
using TraceProject.Models;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services.FaceRecognition;

/// <summary>
/// Outcome of an enrollment attempt: the stored embedding, or a message saying why it failed.
/// </summary>
public record EnrollmentResult(FaceEmbedding? Embedding, string? Error)
{
    public static EnrollmentResult Failed(string error) => new(null, error);
}

/// <summary>
/// Enrolls employee faces: detects/aligns the face, computes its SFace embedding and stores it
/// (with a JPEG thumbnail) so the live pipeline can recognize the employee.
/// </summary>
public class FaceEnrollmentService
{
    private readonly IFaceRecognizer recognizer;
    private readonly IFaceDetector detector;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<FaceEnrollmentService> logger;

    public FaceEnrollmentService(
        IFaceRecognizer recognizer,
        IFaceDetector detector,
        ApplicationDbContext dbContext,
        ILogger<FaceEnrollmentService> logger)
    {
        this.recognizer = recognizer;
        this.detector = detector;
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <summary>
    /// Detects the largest face in a frame (live camera frame or uploaded photo) and enrolls it.
    /// </summary>
    public async Task<EnrollmentResult> EnrollFromFrameAsync(
        long employeeId,
        IVideoFrame frame,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (!detector.IsReady)
            return EnrollmentResult.Failed("The face detector is not ready (check the YuNet model).");
        if (!recognizer.IsReady)
            return EnrollmentResult.Failed("The face recognizer is not ready (check the SFace model).");

        var faces = await detector.DetectFacesAsync(frame, cancellationToken);
        if (faces.Count == 0)
            return EnrollmentResult.Failed("No face found. Face the camera directly, with good lighting.");

        // The enrolled person should be the one closest to the camera, looking at it
        var face = faces.OrderByDescending(f => f.BoundingBox.Width * f.BoundingBox.Height).First();
        if (!face.IsFrontal)
            return EnrollmentResult.Failed("The face is turned sideways. Enroll a face looking straight at the camera — side profiles cause mix-ups between people.");
        var embedding = await EnrollDetectedFaceAsync(employeeId, face, source, cancellationToken);
        return embedding is null
            ? EnrollmentResult.Failed("Could not compute a face embedding for this image.")
            : new EnrollmentResult(embedding, faces.Count > 1 ? $"{faces.Count} faces found; enrolled the largest one." : null);
    }

    /// <summary>
    /// Enrolls a stored face JPEG (an aligned face, e.g. from the Unknown Faces list).
    /// </summary>
    public async Task<EnrollmentResult> EnrollFromFaceJpegAsync(
        long employeeId,
        byte[] faceJpeg,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (!recognizer.IsReady)
            return EnrollmentResult.Failed("The face recognizer is not ready (check the SFace model).");

        var decoded = FaceImageHelper.DecodeToBgr(faceJpeg);
        if (decoded is null)
            return EnrollmentResult.Failed("The stored face image could not be decoded.");

        var (bgr, width, height) = decoded.Value;
        var embedding = await EnrollFaceFromCropAsync(employeeId, bgr, width, height, source, cancellationToken);
        return embedding is null
            ? EnrollmentResult.Failed("Could not compute a face embedding for this face.")
            : new EnrollmentResult(embedding, null);
    }

    /// <summary>
    /// Enroll an employee's face by computing an embedding from face crop image data.
    /// Creates a FaceEmbedding record and stores it in the database.
    /// Can be called multiple times for different enrollment photos/angles.
    /// </summary>
    /// <param name="employeeId">The employee to enroll.</param>
    /// <param name="faceImageData">Raw BGR image bytes of the face crop (112x112 or larger).</param>
    /// <param name="faceImageWidth">Width of the face image.</param>
    /// <param name="faceImageHeight">Height of the face image.</param>
    /// <param name="source">Optional source descriptor (e.g., "front_profile_enrollment_20250922").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created FaceEmbedding, or null if enrollment failed.</returns>
    public async Task<FaceEmbedding?> EnrollFaceFromCropAsync(
        long employeeId,
        byte[] faceImageData,
        int faceImageWidth,
        int faceImageHeight,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (!recognizer.IsReady)
        {
            logger.LogError("Face recognizer not ready for enrollment");
            return null;
        }

        if (faceImageData == null || faceImageData.Length == 0)
        {
            logger.LogError("Face image data is empty");
            return null;
        }

        // Create a temporary DetectedFace from the crop
        var detectedFace = new DetectedFace
        {
            BoundingBox = (0, 0, faceImageWidth, faceImageHeight),
            Confidence = 1.0f,
            FaceImageData = faceImageData,
            FaceImageWidth = faceImageWidth,
            FaceImageHeight = faceImageHeight,
            DetectionTimestampUtc = DateTime.UtcNow
        };

        try
        {
            // Compute embedding using the recognizer's public API
            var embeddingBytes = await recognizer.ComputeEmbeddingAsync(detectedFace, cancellationToken);

            if (embeddingBytes == null || embeddingBytes.Length == 0)
            {
                logger.LogError("Failed to compute embedding for employee {employeeId}", employeeId);
                return null;
            }

            // Verify employee exists
            var employee = await dbContext.Employees.FindAsync(new object[] { employeeId }, cancellationToken: cancellationToken);
            if (employee == null)
            {
                logger.LogError("Employee {employeeId} not found", employeeId);
                return null;
            }

            // Create FaceEmbedding record
            var faceEmbedding = new FaceEmbedding
            {
                EmployeeId = employeeId,
                EmbeddingData = embeddingBytes,
                ModelName = "SFace",
                EmbeddingDimension = 128,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Source = source,
                FaceImage = FaceImageHelper.EncodeJpeg(faceImageData, faceImageWidth, faceImageHeight)
            };

            dbContext.FaceEmbeddings.Add(faceEmbedding);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "✓ Enrolled face for employee {employeeId} ({employeeName}). Source: {source}",
                employeeId, employee.FullName, source ?? "unknown");

            // Refresh recognizer cache to include the new embedding
            await recognizer.RefreshEmbeddingCacheAsync(cancellationToken);

            return faceEmbedding;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enrolling face for employee {employeeId}", employeeId);
            return null;
        }
    }

    /// <summary>
    /// Enroll a detected face from the live camera stream.
    /// Convenience method that takes a DetectedFace (output from Phase 3) and enrolls it.
    /// </summary>
    public async Task<FaceEmbedding?> EnrollDetectedFaceAsync(
        long employeeId,
        DetectedFace detectedFace,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (detectedFace?.FaceImageData == null || detectedFace.FaceImageData.Length == 0)
        {
            logger.LogError("Detected face has no image data");
            return null;
        }

        return await EnrollFaceFromCropAsync(
            employeeId,
            detectedFace.FaceImageData,
            detectedFace.FaceImageWidth,
            detectedFace.FaceImageHeight,
            source,
            cancellationToken);
    }

    /// <summary>
    /// Deactivate all embeddings for an employee.
    /// Useful for re-enrollment (deactivate old, add new).
    /// </summary>
    public async Task DeactivateEmployeeEmbeddingsAsync(
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = dbContext.FaceEmbeddings.Where(fe => fe.EmployeeId == employeeId && fe.IsActive);
            foreach (var embedding in embeddings)
            {
                embedding.IsActive = false;
                embedding.UpdatedAtUtc = DateTime.UtcNow;
            }

            int count = await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Deactivated {count} embeddings for employee {employeeId}", count, employeeId);

            // Refresh cache
            await recognizer.RefreshEmbeddingCacheAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deactivating embeddings for employee {employeeId}", employeeId);
        }
    }

    /// <summary>
    /// Delete all embeddings for an employee.
    /// Permanent operation (no undo).
    /// </summary>
    public async Task DeleteEmployeeEmbeddingsAsync(
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = dbContext.FaceEmbeddings.Where(fe => fe.EmployeeId == employeeId);
            dbContext.FaceEmbeddings.RemoveRange(embeddings);

            int count = await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Deleted {count} embeddings for employee {employeeId}", count, employeeId);

            // Refresh cache
            await recognizer.RefreshEmbeddingCacheAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting embeddings for employee {employeeId}", employeeId);
        }
    }
}
