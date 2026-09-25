using TraceProject.Data;
using TraceProject.Models;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services.FaceRecognition;

/// <summary>
/// Service for enrolling employee faces and computing embeddings.
/// Phase 4: Non-public internal service (no controller endpoint yet).
/// Provides methods to take face crops (from enrollment photos or detected faces) and store embeddings.
/// </summary>
public class FaceEnrollmentService
{
    private readonly IFaceRecognizer recognizer;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<FaceEnrollmentService> logger;

    public FaceEnrollmentService(
        IFaceRecognizer recognizer,
        ApplicationDbContext dbContext,
        ILogger<FaceEnrollmentService> logger)
    {
        this.recognizer = recognizer;
        this.dbContext = dbContext;
        this.logger = logger;
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
                Source = source
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
