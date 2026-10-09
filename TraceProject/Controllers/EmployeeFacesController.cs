using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Models;
using TraceProject.Services;
using TraceProject.Services.FaceDetection;
using TraceProject.Services.FaceRecognition;
using TraceProject.Services.Interfaces;
using TraceProject.Services.VideoSources;

namespace TraceProject.Controllers;

/// <summary>
/// Face enrollment for an employee: each enrolled face is a FaceEmbedding the live
/// recognizer matches against.
/// </summary>
[Route("api/Employees/{employeeId:long}/faces")]
[ApiController]
[Authorize]
public class EmployeeFacesController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly ApplicationDbContext dbContext;
    private readonly FaceEnrollmentService enrollmentService;
    private readonly IFaceRecognizer recognizer;
    private readonly IVideoSource videoSource;
    private readonly SystemSettingsService settings;
    private readonly ILogger<EmployeeFacesController> logger;

    public EmployeeFacesController(
        ApplicationDbContext dbContext,
        FaceEnrollmentService enrollmentService,
        IFaceRecognizer recognizer,
        IVideoSource videoSource,
        SystemSettingsService settings,
        ILogger<EmployeeFacesController> logger)
    {
        this.dbContext = dbContext;
        this.enrollmentService = enrollmentService;
        this.recognizer = recognizer;
        this.videoSource = videoSource;
        this.settings = settings;
        this.logger = logger;
    }

    /// <summary>
    /// Enrolled faces: [{ id, source, isActive, createdAtUtc, image (base64 JPEG or null) }]
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFaces(long employeeId, CancellationToken cancellation)
    {
        try
        {
            if (!await dbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellation))
                return NotFound(CallResult.Error("Employee not found"));

            var faces = await dbContext.FaceEmbeddings.AsNoTracking()
                .Where(f => f.EmployeeId == employeeId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .Select(f => new { f.Id, f.Source, f.IsActive, f.CreatedAtUtc, f.FaceImage })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: faces.Select(f => new
            {
                f.Id,
                f.Source,
                f.IsActive,
                f.CreatedAtUtc,
                Image = f.FaceImage is { Length: > 0 } ? Convert.ToBase64String(f.FaceImage) : null
            })));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetFaces), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Checks every enrolled face of the employee, using the stored embeddings the recognizer matches against:
    /// [{ faceId, verdict ("Good" | "Weak" | "CloseToOther" | "Unusable"), reasons[], sharpness, cutOffFraction,
    ///    ownSimilarity (mean vs the employee's other active faces, null if none), closestOtherEmployee,
    ///    closestOtherSimilarity }]. "Unusable" faces should be deleted; "CloseToOther" ones risk mix-ups.
    /// </summary>
    [HttpGet("audit")]
    public async Task<IActionResult> AuditFaces(long employeeId, CancellationToken cancellation)
    {
        try
        {
            if (!await dbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellation))
                return NotFound(CallResult.Error("Employee not found"));

            var all = await dbContext.FaceEmbeddings.AsNoTracking()
                .Where(f => f.IsActive && f.Employee.IsActive)
                .Select(f => new { f.Id, f.EmployeeId, EmployeeName = f.Employee.FullName, f.EmbeddingData, f.FaceImage })
                .ToListAsync(cancellation);

            var vectors = all.ToDictionary(f => f.Id, f => ToUnitVector(f.EmbeddingData));
            var threshold = settings.Current.RecognitionThreshold;

            var audit = all.Where(f => f.EmployeeId == employeeId).Select(f =>
            {
                var v = vectors[f.Id];
                var own = all.Where(o => o.EmployeeId == employeeId && o.Id != f.Id).Select(o => Dot(v, vectors[o.Id])).ToList();
                var closest = all.Where(o => o.EmployeeId != employeeId)
                    .Select(o => (o.EmployeeName, Similarity: Dot(v, vectors[o.Id])))
                    .DefaultIfEmpty((EmployeeName: (string?)null!, Similarity: 0f))
                    .MaxBy(x => x.Similarity);

                double sharpness = -1, cutOff = 0;
                if (f.FaceImage is { Length: > 0 } && FaceImageHelper.DecodeToBgr(f.FaceImage) is { } img)
                    (sharpness, cutOff) = FaceQuality.MeasureCrop(img.Bgr, img.Width, img.Height);

                float? ownMean = own.Count > 0 ? own.Average() : null;
                var reasons = new List<string>();
                var verdict = "Good";

                if (cutOff > 0.15 || (sharpness >= 0 && sharpness < 150) || (own.Count >= 2 && ownMean < 0.1f))
                {
                    verdict = "Unusable";
                    if (cutOff > 0.15) reasons.Add($"{cutOff:P0} of the face is outside the picture");
                    if (sharpness >= 0 && sharpness < 150) reasons.Add($"very blurry (sharpness {sharpness:F0})");
                    if (own.Count >= 2 && ownMean < 0.1f) reasons.Add($"does not look like this employee's other faces (similarity {ownMean:F2})");
                }
                else if (closest.EmployeeName is not null && closest.Similarity >= threshold - 0.05f)
                {
                    verdict = "CloseToOther";
                    reasons.Add($"close to {closest.EmployeeName} (similarity {closest.Similarity:F2}, match threshold {threshold:F2})");
                }
                else if (FaceQuality.CropProblem(cutOff, sharpness) is { } quality || (ownMean is { } m && m < 0.25f))
                {
                    verdict = "Weak";
                    if (cutOff > FaceQuality.MaxEnrollmentCutOff) reasons.Add($"{cutOff:P0} of the face is outside the picture");
                    if (sharpness >= 0 && sharpness < FaceQuality.MinEnrollmentSharpness) reasons.Add($"blurry (sharpness {sharpness:F0})");
                    if (ownMean is { } mean && mean < 0.25f) reasons.Add($"weak resemblance to this employee's other faces (similarity {mean:F2})");
                }

                return new
                {
                    FaceId = f.Id,
                    Verdict = verdict,
                    Reasons = reasons,
                    Sharpness = Math.Round(sharpness),
                    CutOffFraction = Math.Round(cutOff, 3),
                    OwnSimilarity = ownMean is { } om ? Math.Round(om, 3) : (double?)null,
                    ClosestOtherEmployee = closest.EmployeeName,
                    ClosestOtherSimilarity = Math.Round(closest.Similarity, 3),
                };
            }).ToList();

            return Ok(CallResult.Ok(data: audit));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AuditFaces), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    private static float[] ToUnitVector(byte[] bytes)
    {
        var v = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, v, 0, v.Length * sizeof(float));
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0)
            for (int i = 0; i < v.Length; i++) v[i] /= norm;
        return v;
    }

    private static float Dot(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    /// <summary>
    /// Enrolls the largest face currently visible on the live camera.
    /// </summary>
    [HttpPost("capture")]
    public async Task<IActionResult> CaptureFromCamera(long employeeId, CancellationToken cancellation)
    {
        try
        {
            if (!await dbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellation))
                return NotFound(CallResult.Error("Employee not found"));

            var frame = videoSource.GetLatestFrame();
            if (frame is null || frame.Data.Length == 0 || frame.Age > TimeSpan.FromSeconds(5))
                return BadRequest(CallResult.Error("The live camera has no recent frame. Check that it is connected."));

            var result = await enrollmentService.EnrollFromFrameAsync(employeeId, frame, $"camera_capture_{DateTime.UtcNow:yyyyMMdd_HHmmss}", cancellation);
            return ToResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(CaptureFromCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Enrolls the largest face in an uploaded photo (multipart form field "file").
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadPhoto(long employeeId, IFormFile file, CancellationToken cancellation)
    {
        try
        {
            if (!await dbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellation))
                return NotFound(CallResult.Error("Employee not found"));

            if (file is null || file.Length == 0)
                return BadRequest(CallResult.Error("Choose a photo to upload."));

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellation);

            var frame = StillImageFrame.FromEncodedImage(buffer.ToArray());
            if (frame is null)
                return BadRequest(CallResult.Error("That file is not a readable image (use JPEG or PNG)."));

            var result = await enrollmentService.EnrollFromFrameAsync(employeeId, frame, $"upload_{Path.GetFileName(file.FileName)}", cancellation);
            return ToResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UploadPhoto), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{faceId:long}")]
    public async Task<IActionResult> DeleteFace(long employeeId, long faceId, CancellationToken cancellation)
    {
        try
        {
            var face = await dbContext.FaceEmbeddings.FirstOrDefaultAsync(f => f.Id == faceId && f.EmployeeId == employeeId, cancellation);
            if (face is null)
                return NotFound(CallResult.Error("Face not found"));

            dbContext.FaceEmbeddings.Remove(face);
            await dbContext.SaveChangesAsync(cancellation);
            await recognizer.RefreshEmbeddingCacheAsync(cancellation);

            return Ok(CallResult.Ok("Face removed"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteFace), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    private IActionResult ToResponse(EnrollmentResult result)
    {
        if (result.Embedding is null)
            return BadRequest(CallResult.Error(result.Error ?? "Enrollment failed"));

        var face = result.Embedding;
        return Ok(CallResult.Ok(result.Error ?? "Face enrolled", new
        {
            face.Id,
            face.Source,
            face.IsActive,
            face.CreatedAtUtc,
            Image = face.FaceImage is { Length: > 0 } ? Convert.ToBase64String(face.FaceImage) : null
        }));
    }
}
