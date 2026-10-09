using TraceProject.Data;
using TraceProject.Models;
using Microsoft.EntityFrameworkCore;
using TraceProject.Services.FaceDetection;
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
        if (FaceQuality.EnrollmentProblem(face) is { } problem)
            return EnrollmentResult.Failed(problem);

        var result = await EnrollCropAsync(employeeId, face.FaceImageData, face.FaceImageWidth, face.FaceImageHeight, source, cancellationToken);
        if (result.Embedding is not null && faces.Count > 1)
            return result with { Error = JoinNotes($"{faces.Count} faces found; enrolled the largest one.", result.Error) };
        return result;
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
        // No detection data for a stored crop: judge it by its pixels (black = outside the picture)
        var (sharpness, blackFraction) = FaceQuality.MeasureCrop(bgr, width, height);
        if (FaceQuality.CropProblem(blackFraction, sharpness) is { } problem)
            return EnrollmentResult.Failed(problem);

        return await EnrollCropAsync(employeeId, bgr, width, height, source, cancellationToken);
    }

    // A new face this close to another employee (just under the match threshold) is enrolled with a warning
    private const float NearOtherEmployeeMargin = 0.05f;
    // Below this, the face hardly resembles the employee's other enrolled faces
    private const float MinOwnSimilarity = 0.2f;

    /// <summary>
    /// Computes the embedding of an aligned face, refuses it when it already matches another
    /// employee (enrolling it would make the two be confused), and stores it. Warnings
    /// (close to someone else, unlike the employee's other faces) come back in Error.
    /// </summary>
    private async Task<EnrollmentResult> EnrollCropAsync(long employeeId, byte[] bgr, int width, int height, string? source, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        if (employee is null)
            return EnrollmentResult.Failed("Employee not found.");

        var crop = new DetectedFace
        {
            BoundingBox = (0, 0, width, height),
            Confidence = 1.0f,
            FaceImageData = bgr,
            FaceImageWidth = width,
            FaceImageHeight = height,
            DetectionTimestampUtc = DateTime.UtcNow
        };
        var embeddingBytes = await recognizer.ComputeEmbeddingAsync(crop, cancellationToken);
        if (embeddingBytes is null || embeddingBytes.Length == 0)
            return EnrollmentResult.Failed("Could not compute a face embedding for this face.");

        var comparison = recognizer.CompareWithGallery(embeddingBytes, employeeId);
        if (comparison.OtherEmployeeId is not null && comparison.OtherBest >= comparison.Threshold)
        {
            logger.LogWarning("Enrollment for {employee} refused: matches {other} ({similarity:F3})",
                employee.FullName, comparison.OtherEmployeeName, comparison.OtherBest);
            return EnrollmentResult.Failed(
                $"This face already matches {comparison.OtherEmployeeName} (similarity {comparison.OtherBest:F2}, match threshold {comparison.Threshold:F2}). " +
                $"Enrolling it for {employee.FullName} would make the two be confused. Check that the right person is in front of the camera.");
        }

        var warnings = new List<string>();
        if (comparison.OtherEmployeeId is not null && comparison.OtherBest >= comparison.Threshold - NearOtherEmployeeMargin)
            warnings.Add($"This face is close to {comparison.OtherEmployeeName} (similarity {comparison.OtherBest:F2}); add more clear photos of {employee.FullName} to keep them apart.");
        if (comparison.OwnBest is { } own && own < MinOwnSimilarity)
            warnings.Add($"This face looks unlike {employee.FullName}'s other enrolled faces (similarity {own:F2}). Check it is the same person.");

        var stored = await StoreEmbeddingAsync(employee, embeddingBytes, bgr, width, height, source, cancellationToken);
        return stored is null
            ? EnrollmentResult.Failed("The face could not be saved.")
            : new EnrollmentResult(stored, warnings.Count > 0 ? string.Join(" ", warnings) : null);
    }

    private static string JoinNotes(string first, string? second) => second is null ? first : $"{first} {second}";

    /// <summary>
    /// Saves an embedding (with a JPEG thumbnail of the aligned face) and refreshes the recognizer
    /// cache so the live pipeline uses it right away. Returns null if saving failed.
    /// </summary>
    private async Task<FaceEmbedding?> StoreEmbeddingAsync(
        Employee employee, byte[] embeddingBytes, byte[] bgr, int width, int height, string? source, CancellationToken cancellationToken)
    {
        try
        {
            var faceEmbedding = new FaceEmbedding
            {
                EmployeeId = employee.Id,
                EmbeddingData = embeddingBytes,
                ModelName = "SFace",
                EmbeddingDimension = 128,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Source = source,
                FaceImage = FaceImageHelper.EncodeJpeg(bgr, width, height)
            };

            dbContext.FaceEmbeddings.Add(faceEmbedding);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "✓ Enrolled face for employee {employeeId} ({employeeName}). Source: {source}",
                employee.Id, employee.FullName, source ?? "unknown");

            await recognizer.RefreshEmbeddingCacheAsync(cancellationToken);
            return faceEmbedding;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enrolling face for employee {employeeId}", employee.Id);
            return null;
        }
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
