using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services.Interfaces;
using PaginationDto = HlkHelpers.PaginationDto;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CamerasController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<CamerasController> logger;
    private readonly IHubContext<NotificationHub> hubContext;
    private readonly IVideoSource videoSource;
    private readonly ICameraMetricsService metricsService;
    private readonly IFrameProcessingService frameProcessingService;
    private readonly CameraOptions cameraOptions;

    public CamerasController(
        ApplicationDbContext dbContext,
        ILogger<CamerasController> logger,
        IHubContext<NotificationHub> hubContext,
        IVideoSource videoSource,
        ICameraMetricsService metricsService,
        IFrameProcessingService frameProcessingService,
        IOptions<CameraOptions> cameraOptions)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
        this.videoSource = videoSource;
        this.metricsService = metricsService;
        this.frameProcessingService = frameProcessingService;
        this.cameraOptions = cameraOptions.Value;
    }

    [HttpPost]
    public async Task<IActionResult> AddCamera(AddCameraDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.CameraName == dto.CameraName, cancellation);

            if (camera is not null) 
                return BadRequest(CallResult.Error("Camera already exists"));

            camera = new()
            {
                CameraName = dto.CameraName,
                IPAddress = dto.IPAddress,
                Location = dto.Location,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Add(camera);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Camera added successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCameras([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var searchQuery = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Cameras.AsNoTracking();

            var list = await PagedList<Camera>.CreateAsync(source, dto,
                orderBy: x => x.CameraName,
                selector: c => new { c.Id, c.CameraName, c.IPAddress, c.Location, c.IsActive, c.CreatedAt, c.UpdatedAt },
                whereClause: x => x.CameraName.ToLower().Contains(searchQuery) || x.IPAddress.ToLower().Contains(searchQuery) || x.Location.ToLower().Contains(searchQuery),
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetCameras), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCameraById(long id, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellation);

            if (camera is null) 
                return NotFound(CallResult.Error("Camera not found"));

            return Ok(CallResult.Ok(data: camera));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetCameraById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCamera(long id, UpdateCameraDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellation);

            if (camera is null) 
                return NotFound(CallResult.Error("Camera not found"));

            var exists = await dbContext.Cameras.AsNoTracking()
                .AnyAsync(c => c.CameraName == dto.CameraName && c.Id != id, cancellation);

            if (exists) 
                return BadRequest(CallResult.Error("Camera with this name already exists"));

            camera.CameraName = dto.CameraName;
            camera.IPAddress = dto.IPAddress;
            camera.Location = dto.Location;
            camera.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(camera);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Camera updated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/activation")]
    public async Task<IActionResult> ToggleCameraActivation(long id, CameraActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellation);

            if (camera is null) 
                return NotFound(CallResult.Error("Camera not found"));

            camera.IsActive = dto.active;
            camera.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(camera);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast camera status change event
            var eventName = dto.active ? "CameraOnline" : "CameraOffline";
            await hubContext.Clients.All.SendAsync(eventName, new
            {
                cameraId = camera.Id,
                cameraName = camera.CameraName
            }, cancellation);

            return Ok(CallResult.Ok("Camera updated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(ToggleCameraActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCamera(long id, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellation);

            if (camera is null) 
                return NotFound(CallResult.Error("Camera not found"));

            dbContext.Remove(camera);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Camera deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Phase 7: Get live camera status with pipeline metrics
    /// </summary>
    [HttpGet("status/live")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLiveStatus(CancellationToken cancellation)
    {
        try
        {
            // The live feed is the single camera configured in appsettings (Camera section).
            // Its Camera:Id links it to a row in the Cameras table, if one exists.
            var camera = await dbContext.Cameras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cameraOptions.Id, cancellation);

            // Get live metrics
            var isConnected = videoSource.ConnectionStatus == TraceProject.Services.Interfaces.CameraConnectionStatus.Connected;
            var isProcessing = frameProcessingService.IsRunning;
            var resolution = videoSource.CurrentResolution;
            var incomingFps = videoSource.CurrentFramesPerSecond;
            var latencyMs = metricsService.ApproximateLatencyMs;
            var lastFrameUtc = videoSource.LastFrameTimestampUtc;
            var (facesDetected, facesRecognized, unknownFaces) = frameProcessingService.GetPipelineMetrics();
            var lastError = videoSource.LastException?.Message;

            var status = new CameraStatusDto(
                Id: cameraOptions.Id,
                CameraName: camera?.CameraName ?? cameraOptions.Name,
                IsConnected: isConnected,
                IsProcessing: isProcessing,
                ConnectionStatus: videoSource.ConnectionStatus.ToString(),
                Resolution: resolution is { } r ? $"{r.Width}x{r.Height}" : null,
                IncomingFps: incomingFps,
                ProcessingFps: metricsService.ConsumableFramesPerSecond,
                LatencyMs: latencyMs,
                FramesReceived: metricsService.FramesReceived,
                FramesDropped: metricsService.FramesDropped,
                FacesDetected: facesDetected,
                FacesRecognized: facesRecognized,
                UnknownFaces: unknownFaces,
                LastFrameUtc: lastFrameUtc,
                LastError: lastError);

            return Ok(CallResult.Ok(data: status));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLiveStatus), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Live MJPEG stream of the configured camera, for use as an &lt;img src&gt;.
    /// Browsers can't set headers on &lt;img&gt;, so the JWT is accepted as ?access_token= (see Program.cs).
    /// </summary>
    [HttpGet("{id}/stream")]
    public async Task StreamLive(long id, CancellationToken cancellation)
    {
        if (id != cameraOptions.Id)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        const string boundary = "frame";
        Response.ContentType = $"multipart/x-mixed-replace; boundary={boundary}";
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var lastSentUtc = DateTime.MinValue;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var frame = videoSource.GetLatestFrame();
                if (frame is not null && frame.TimestampUtc != lastSentUtc)
                {
                    var jpeg = EncodeJpeg(frame);
                    if (jpeg is not null)
                    {
                        await Response.WriteAsync($"--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n", cancellation);
                        await Response.Body.WriteAsync(jpeg, cancellation);
                        await Response.WriteAsync("\r\n", cancellation);
                        await Response.Body.FlushAsync(cancellation);
                        lastSentUtc = frame.TimestampUtc;
                    }
                }

                await Task.Delay(100, cancellation); // ~10 fps is plenty for a monitoring view
            }
        }
        catch (OperationCanceledException)
        {
            // Client closed the stream
        }
    }

    /// <summary>
    /// Latest frame of the configured camera as a single JPEG.
    /// </summary>
    [HttpGet("{id}/snapshot")]
    public IActionResult GetSnapshot(long id)
    {
        if (id != cameraOptions.Id)
            return NotFound(CallResult.Error("No live feed for this camera"));

        var frame = videoSource.GetLatestFrame();
        var jpeg = frame is null ? null : EncodeJpeg(frame);
        if (jpeg is null)
            return NotFound(CallResult.Error("No frame available yet"));

        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        return File(jpeg, "image/jpeg");
    }

    private byte[]? EncodeJpeg(IVideoFrame frame)
    {
        if (frame.Data.Length == 0 || frame.Width <= 0 || frame.Height <= 0)
            return null;

        var matType = frame.PixelFormat.Equals("BGRA", StringComparison.OrdinalIgnoreCase) ? MatType.CV_8UC4 : MatType.CV_8UC3;
        if (frame.Data.Length < frame.Width * frame.Height * matType.Channels)
            return null;

        try
        {
            using var mat = Mat.FromPixelData(frame.Height, frame.Width, matType, frame.Data);
            Cv2.ImEncode(".jpg", mat, out var jpeg, new ImageEncodingParam(ImwriteFlags.JpegQuality, 75));
            return jpeg;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to encode camera frame as JPEG");
            return null;
        }
    }
}
