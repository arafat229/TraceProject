using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Models;
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
    private readonly ILogger<EmployeeFacesController> logger;

    public EmployeeFacesController(
        ApplicationDbContext dbContext,
        FaceEnrollmentService enrollmentService,
        IFaceRecognizer recognizer,
        IVideoSource videoSource,
        ILogger<EmployeeFacesController> logger)
    {
        this.dbContext = dbContext;
        this.enrollmentService = enrollmentService;
        this.recognizer = recognizer;
        this.videoSource = videoSource;
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
