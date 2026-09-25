namespace TraceProject.Services.Interfaces;

/// <summary>
/// Represents a single video frame captured from the camera.
/// </summary>
public interface IVideoFrame
{
    /// <summary>
    /// Raw frame data (pixel data in the format specified by PixelFormat).
    /// </summary>
    byte[] Data { get; }

    /// <summary>
    /// Width of the frame in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the frame in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Timestamp when the frame was captured (UTC).
    /// </summary>
    DateTime TimestampUtc { get; }

    /// <summary>
    /// Duration since the frame was captured.
    /// </summary>
    TimeSpan Age => DateTime.UtcNow - TimestampUtc;

    /// <summary>
    /// Pixel format of the frame data (e.g., "I420", "YUYV422", "RGBA").
    /// Used to interpret the raw byte data.
    /// </summary>
    string PixelFormat { get; }

    /// <summary>
    /// Number of bytes per horizontal line (pitch/stride).
    /// Needed for memory-efficient frame manipulation.
    /// </summary>
    int Pitch { get; }
}

/// <summary>
/// EventArgs for FrameReceived events.
/// </summary>
public class FrameReceivedEventArgs : EventArgs
{
    public required IVideoFrame Frame { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// EventArgs for connection status changes.
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    public required CameraConnectionStatus Status { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Enumeration of camera connection states.
/// </summary>
public enum CameraConnectionStatus
{
    /// <summary>
    /// Not connected and not attempting to connect.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Currently establishing connection to RTSP stream.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Successfully connected, receiving frames.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Connection lost, waiting to retry.
    /// </summary>
    Reconnecting = 3,

    /// <summary>
    /// An error occurred (e.g., network unreachable, invalid credentials).
    /// </summary>
    Error = 4
}
