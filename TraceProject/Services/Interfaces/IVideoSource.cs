namespace TraceProject.Services.Interfaces;

/// <summary>
/// Abstraction for video sources (RTSP streams, files, test images, etc.).
/// Implementations handle connection, frame extraction, and error recovery.
/// </summary>
public interface IVideoSource : IAsyncDisposable
{
    /// <summary>
    /// Start capturing frames from the video source.
    /// This should establish the connection and begin pulling frames.
    /// Non-blocking; actual capture runs on a background thread.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop capturing frames and close the video source.
    /// Must be idempotent (safe to call multiple times).
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Indicates whether the video source is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Current connection status of this video source.
    /// </summary>
    CameraConnectionStatus ConnectionStatus { get; }

    /// <summary>
    /// Raised whenever a new frame is successfully captured and decoded.
    /// Subscribers should process the frame quickly to avoid blocking the capture thread.
    /// </summary>
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    /// <summary>
    /// Raised when the connection status changes
    /// (Connecting → Connected, Connected → Disconnected, Disconnected → Reconnecting, etc.).
    /// </summary>
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Get the latest successfully captured frame, if available.
    /// Returns null if no frame has been captured yet.
    /// Frame data is ownership-transferred (caller must dispose if frame implements IDisposable).
    /// </summary>
    IVideoFrame? GetLatestFrame();

    /// <summary>
    /// Current resolution of the captured stream (if known).
    /// Returns null if no stream is active or resolution is unknown.
    /// </summary>
    (int Width, int Height)? CurrentResolution { get; }

    /// <summary>
    /// Estimated frames-per-second of the incoming stream.
    /// Returns null if unknown or not yet measured.
    /// </summary>
    double? CurrentFramesPerSecond { get; }

    /// <summary>
    /// Timestamp of the last successfully captured frame (UTC).
    /// Returns null if no frame has been captured yet.
    /// </summary>
    DateTime? LastFrameTimestampUtc { get; }

    /// <summary>
    /// If connection is in Error status, this holds the exception.
    /// Returns null otherwise.
    /// </summary>
    Exception? LastException { get; }
}
