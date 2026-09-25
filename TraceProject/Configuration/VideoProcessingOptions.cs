namespace TraceProject.Configuration;

/// <summary>
/// Configuration for video frame processing and buffering.
/// Bind from appsettings.json "VideoProcessing" section.
/// Phase 1: Buffering and frame capture.
/// Phase 2: Metrics collection.
/// Phase 3: Face detection processing rate and resolution.
/// </summary>
public class VideoProcessingOptions
{
    /// <summary>
    /// Maximum number of frames to keep in the buffer.
    /// For Phase 1: keep it at 1 (latest frame only, drop stale).
    /// </summary>
    public int MaxBufferedFrames { get; set; } = 1;

    /// <summary>
    /// If true, drop old frames when buffer is full.
    /// If false, block (wait) until space is available.
    /// Recommended: true (drop-stale pattern for real-time monitoring).
    /// </summary>
    public bool DropOldFrames { get; set; } = true;

    /// <summary>
    /// Timeout in milliseconds to wait for a frame to be consumed before considering it stale.
    /// If 0, no timeout (use indefinitely).
    /// </summary>
    public int FrameTimeoutMs { get; set; } = 0;

    /// <summary>
    /// Interval in seconds between periodic metrics summary logs.
    /// Example: 10 = log summary every 10 seconds.
    /// Set to 0 to disable periodic logging.
    /// Used by Phase 2 metrics collection and Phase 3 detection metrics.
    /// </summary>
    public int MetricsSummaryIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Target frames per second for face detection processing (Phase 3).
    /// Independent of the camera's capture FPS.
    /// Example: 5 = process and detect faces at ~5 FPS regardless of camera stream rate.
    /// Set to 0 or negative to use a default of 5 FPS.
    /// Higher values consume more CPU; lower values reduce latency/bandwidth.
    /// </summary>
    public int TargetProcessingFps { get; set; } = 5;

    /// <summary>
    /// Width of resized frame for processing (Phase 3 face detection).
    /// Frames are resized DOWN to this width for efficient processing.
    /// The face detector will further resize to its internal size (416x416 for YuNet).
    /// Example: 640 = process at 640 pixel width.
    /// Original frame resolution is never permanently downscaled.
    /// </summary>
    public int ProcessingWidth { get; set; } = 640;

    /// <summary>
    /// Height of resized frame for processing (Phase 3 face detection).
    /// Frames are resized DOWN to this height for efficient processing.
    /// The face detector will further resize to its internal size (416x416 for YuNet).
    /// Example: 360 = process at 360 pixel height.
    /// Original frame resolution is never permanently downscaled.
    /// </summary>
    public int ProcessingHeight { get; set; } = 360;
}
