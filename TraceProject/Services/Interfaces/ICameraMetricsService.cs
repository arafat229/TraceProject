namespace TraceProject.Services.Interfaces;

/// <summary>
/// Metrics for camera frame capture and processing.
/// Thread-safe read-only snapshots of real-time measurement data.
/// </summary>
public interface ICameraMetricsService
{
    /// <summary>
    /// Total number of frames decoded and received from the camera stream.
    /// Incremented each time a new frame arrives from LibVLC decoder.
    /// Resets to zero on connection loss/reset.
    /// </summary>
    long FramesReceived { get; }

    /// <summary>
    /// Number of frames that were overwritten before being consumed.
    /// Incremented when a new frame arrives but a previous frame was still in the buffer.
    /// With Phase 1's single-slot buffer, this happens frequently during normal playback.
    /// NOT an error condition — it's expected behavior for live monitoring.
    /// </summary>
    long FramesDropped { get; }

    /// <summary>
    /// Actual frames-per-second measured from decoded frame arrivals, updated continuously.
    /// Calculated as: (frames received in last window) / (window duration in seconds)
    /// Null if insufficient data or no frames yet.
    /// </summary>
    double? IncomingFramesPerSecond { get; }

    /// <summary>
    /// Frames per second that would be available to a consumer reading via GetLatestFrame().
    /// May be lower than IncomingFramesPerSecond due to drops.
    /// Calculated as: (frames not dropped) / (window duration in seconds)
    /// Null if insufficient data or no frames yet.
    /// </summary>
    double? ConsumableFramesPerSecond { get; }

    /// <summary>
    /// Approximate latency from frame arrival at decoder to availability for consumer (milliseconds).
    /// Calculated as average time between frame arrival and first potential read opportunity.
    /// Null if insufficient data.
    /// </summary>
    double? ApproximateLatencyMs { get; }

    /// <summary>
    /// Current decoded stream resolution (width, height in pixels).
    /// Read from actual LibVLC frame dimensions, not assumed.
    /// Null if unknown or not connected.
    /// </summary>
    (int Width, int Height)? CurrentResolution { get; }

    /// <summary>
    /// Timestamp (UTC) when the last frame event was recorded.
    /// Null if no frames yet.
    /// </summary>
    DateTime? LastFrameTimestampUtc { get; }

    /// <summary>
    /// Time elapsed since the last frame was recorded.
    /// Null if no frames yet.
    /// </summary>
    TimeSpan? TimeSinceLastFrame { get; }

    /// <summary>
    /// Get all metrics as a serializable snapshot.
    /// Safe to call concurrently; returns point-in-time values.
    /// </summary>
    CameraMetricsSnapshot GetSnapshot();

    /// <summary>
    /// Record that a frame was received and available for consumption.
    /// Called by RtspVideoSource each time FrameReceived fires.
    /// Timestamp should be the frame's arrival time at the decoder.
    /// </summary>
    void RecordFrameReceived(int width, int height, DateTime frameTimestampUtc);

    /// <summary>
    /// Record that a frame was discarded without being consumed.
    /// Called by RtspVideoSource when a new frame overwrites an existing one.
    /// </summary>
    void RecordFrameDropped();

    /// <summary>
    /// Reset all metrics to zero and update resolution.
    /// Called on connection loss or reconnect to avoid stale data.
    /// </summary>
    void Reset();

    /// <summary>
    /// Update the current resolution (called when stream properties become known).
    /// </summary>
    void UpdateResolution(int width, int height);
}

/// <summary>
/// Serializable snapshot of current camera metrics.
/// Represents a point-in-time capture; can be safely passed to APIs or logged.
/// </summary>
public class CameraMetricsSnapshot
{
    /// <summary>
    /// Total frames received so far in this connection.
    /// </summary>
    public long FramesReceived { get; set; }

    /// <summary>
    /// Total frames dropped (overwritten) so far in this connection.
    /// </summary>
    public long FramesDropped { get; set; }

    /// <summary>
    /// Incoming FPS from the camera stream (may be null).
    /// </summary>
    public double? IncomingFramesPerSecond { get; set; }

    /// <summary>
    /// Frames per second available to consumers (may be null).
    /// </summary>
    public double? ConsumableFramesPerSecond { get; set; }

    /// <summary>
    /// Approximate per-frame latency in milliseconds (may be null).
    /// </summary>
    public double? ApproximateLatencyMs { get; set; }

    /// <summary>
    /// Current stream resolution (may be null if unknown).
    /// </summary>
    public (int Width, int Height)? Resolution { get; set; }

    /// <summary>
    /// When was the last frame recorded (UTC, may be null).
    /// </summary>
    public DateTime? LastFrameTimestampUtc { get; set; }

    /// <summary>
    /// How long ago was the last frame recorded (may be null).
    /// </summary>
    public TimeSpan? TimeSinceLastFrame { get; set; }
}
