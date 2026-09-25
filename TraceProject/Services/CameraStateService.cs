using TraceProject.Services.Interfaces;

namespace TraceProject.Services;

/// <summary>
/// Provides queryable camera state information (connection status, resolution, FPS, errors).
/// Singleton service that can be injected into controllers/services for monitoring.
/// </summary>
public class CameraStateService
{
    private readonly IVideoSource videoSource;
    private readonly ILogger<CameraStateService> logger;

    public CameraStateService(IVideoSource videoSource, ILogger<CameraStateService> logger)
    {
        this.videoSource = videoSource;
        this.logger = logger;
    }

    /// <summary>
    /// Get the current connection status.
    /// </summary>
    public CameraConnectionStatus ConnectionStatus => videoSource.ConnectionStatus;

    /// <summary>
    /// Get a human-readable status string.
    /// </summary>
    public string ConnectionStatusText => ConnectionStatus switch
    {
        CameraConnectionStatus.Disconnected => "Disconnected",
        CameraConnectionStatus.Connecting => "Connecting...",
        CameraConnectionStatus.Connected => "Connected",
        CameraConnectionStatus.Reconnecting => "Reconnecting...",
        CameraConnectionStatus.Error => "Error",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether the camera is actively capturing frames.
    /// </summary>
    public bool IsConnected => ConnectionStatus == CameraConnectionStatus.Connected;

    /// <summary>
    /// Current resolution of the stream (Width, Height).
    /// Null if unknown or not connected.
    /// </summary>
    public (int Width, int Height)? Resolution => videoSource.CurrentResolution;

    /// <summary>
    /// Estimated frames per second.
    /// Null if unknown or insufficient data.
    /// </summary>
    public double? FramesPerSecond => videoSource.CurrentFramesPerSecond;

    /// <summary>
    /// Timestamp of the last captured frame (UTC).
    /// Null if no frame has been captured yet.
    /// </summary>
    public DateTime? LastFrameTimestampUtc => videoSource.LastFrameTimestampUtc;

    /// <summary>
    /// Time elapsed since the last frame was captured.
    /// Null if no frame has been captured yet.
    /// </summary>
    public TimeSpan? TimeSinceLastFrame
    {
        get
        {
            var lastTime = LastFrameTimestampUtc;
            return lastTime.HasValue ? DateTime.UtcNow - lastTime.Value : null;
        }
    }

    /// <summary>
    /// The exception from the last error, if any.
    /// Null if no error or after successful recovery.
    /// </summary>
    public Exception? LastException => videoSource.LastException;

    /// <summary>
    /// Get detailed status as a serializable object (useful for API responses).
    /// </summary>
    public CameraStatusDto GetStatus() => new()
    {
        ConnectionStatus = ConnectionStatusText,
        IsConnected = IsConnected,
        Resolution = Resolution,
        FramesPerSecond = FramesPerSecond,
        LastFrameTimestampUtc = LastFrameTimestampUtc,
        TimeSinceLastFrameMs = TimeSinceLastFrame?.TotalMilliseconds ?? -1,
        LastErrorMessage = LastException?.Message
    };

    /// <summary>
    /// Get the latest captured frame if available.
    /// </summary>
    public IVideoFrame? GetLatestFrame() => videoSource.GetLatestFrame();

    /// <summary>
    /// Log current camera state (useful for debugging).
    /// </summary>
    public void LogStatus()
    {
        var status = GetStatus();
        logger.LogInformation(
            "Camera Status: {Status} | Resolution: {Resolution} | FPS: {Fps} | Last Frame: {LastFrame} ago",
            status.ConnectionStatus,
            status.Resolution?.ToString() ?? "Unknown",
            status.FramesPerSecond?.ToString("F1") ?? "Unknown",
            status.TimeSinceLastFrameMs >= 0 ? $"{status.TimeSinceLastFrameMs:F0}ms" : "Never");
    }
}

/// <summary>
/// Serializable camera status DTO (for API endpoints, logging, etc.).
/// </summary>
public class CameraStatusDto
{
    public string ConnectionStatus { get; set; } = "Unknown";
    public bool IsConnected { get; set; }
    public (int Width, int Height)? Resolution { get; set; }
    public double? FramesPerSecond { get; set; }
    public DateTime? LastFrameTimestampUtc { get; set; }
    public double TimeSinceLastFrameMs { get; set; } = -1;
    public string? LastErrorMessage { get; set; }
}
