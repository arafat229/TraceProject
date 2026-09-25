using TraceProject.Services.Interfaces;

namespace TraceProject.Services;

/// <summary>
/// Thread-safe implementation of ICameraMetricsService.
/// Tracks frame counts, FPS, latency, and resolution from RTSP stream.
/// </summary>
public class CameraMetricsService : ICameraMetricsService
{
    private readonly ILogger<CameraMetricsService> logger;
    private readonly object lockObject = new();

    // Frame tracking
    private long framesReceived = 0;
    private long framesDropped = 0;
    private (int Width, int Height)? currentResolution;
    private DateTime? lastFrameTimestampUtc;

    // FPS measurement window (sliding)
    private readonly Queue<FrameArrival> frameArrivalWindow = new();
    private const int MeasurementWindowSize = 100;  // Track last N frames for FPS calculation

    // Per-frame latency tracking
    private readonly Queue<double> latencyWindowMs = new();
    private const int LatencyWindowSize = 50;  // Track last N frame latencies

    public long FramesReceived => framesReceived;
    public long FramesDropped => framesDropped;
    public (int Width, int Height)? CurrentResolution => currentResolution;
    public DateTime? LastFrameTimestampUtc => lastFrameTimestampUtc;

    public double? IncomingFramesPerSecond
    {
        get
        {
            lock (lockObject)
            {
                if (frameArrivalWindow.Count < 2)
                    return null;

                var oldest = frameArrivalWindow.Peek();
                var newest = frameArrivalWindow.Last();
                var durationSeconds = (newest.Timestamp - oldest.Timestamp).TotalSeconds;

                if (durationSeconds <= 0)
                    return null;

                return frameArrivalWindow.Count / durationSeconds;
            }
        }
    }

    public double? ConsumableFramesPerSecond
    {
        get
        {
            lock (lockObject)
            {
                if (frameArrivalWindow.Count < 2)
                    return null;

                var oldest = frameArrivalWindow.Peek();
                var newest = frameArrivalWindow.Last();
                var durationSeconds = (newest.Timestamp - oldest.Timestamp).TotalSeconds;

                if (durationSeconds <= 0)
                    return null;

                // Consumable FPS = frames that weren't dropped in this window
                var consumedCount = framesReceived - framesDropped;
                if (consumedCount <= 0)
                    return 0;

                return consumedCount / durationSeconds;
            }
        }
    }

    public double? ApproximateLatencyMs
    {
        get
        {
            lock (lockObject)
            {
                if (latencyWindowMs.Count == 0)
                    return null;

                return latencyWindowMs.Average();
            }
        }
    }

    public TimeSpan? TimeSinceLastFrame
    {
        get
        {
            lock (lockObject)
            {
                if (lastFrameTimestampUtc == null)
                    return null;

                return DateTime.UtcNow - lastFrameTimestampUtc.Value;
            }
        }
    }

    public CameraMetricsService(ILogger<CameraMetricsService> logger)
    {
        this.logger = logger;
    }

    public void RecordFrameReceived(int width, int height, DateTime frameTimestampUtc)
    {
        lock (lockObject)
        {
            framesReceived++;
            lastFrameTimestampUtc = frameTimestampUtc;
            currentResolution = (width, height);

            // Add to sliding window for FPS calculation
            frameArrivalWindow.Enqueue(new FrameArrival { Timestamp = frameTimestampUtc });

            // Keep window size manageable
            while (frameArrivalWindow.Count > MeasurementWindowSize)
                frameArrivalWindow.Dequeue();

            // Record latency (time from frame arrival to "now")
            var latencyMs = (DateTime.UtcNow - frameTimestampUtc).TotalMilliseconds;
            latencyWindowMs.Enqueue(latencyMs);

            while (latencyWindowMs.Count > LatencyWindowSize)
                latencyWindowMs.Dequeue();
        }
    }

    public void RecordFrameDropped()
    {
        lock (lockObject)
        {
            framesDropped++;
        }
    }

    public void Reset()
    {
        lock (lockObject)
        {
            framesReceived = 0;
            framesDropped = 0;
            lastFrameTimestampUtc = null;
            currentResolution = null;
            frameArrivalWindow.Clear();
            latencyWindowMs.Clear();

            logger.LogInformation("Camera metrics reset");
        }
    }

    public void UpdateResolution(int width, int height)
    {
        lock (lockObject)
        {
            currentResolution = (width, height);
            logger.LogDebug("Camera resolution updated to {width}x{height}", width, height);
        }
    }

    public CameraMetricsSnapshot GetSnapshot()
    {
        lock (lockObject)
        {
            return new CameraMetricsSnapshot
            {
                FramesReceived = framesReceived,
                FramesDropped = framesDropped,
                IncomingFramesPerSecond = IncomingFramesPerSecond,
                ConsumableFramesPerSecond = ConsumableFramesPerSecond,
                ApproximateLatencyMs = ApproximateLatencyMs,
                Resolution = currentResolution,
                LastFrameTimestampUtc = lastFrameTimestampUtc,
                TimeSinceLastFrame = TimeSinceLastFrame
            };
        }
    }

    /// <summary>
    /// Internal struct for tracking frame arrival timestamps.
    /// </summary>
    private struct FrameArrival
    {
        public DateTime Timestamp { get; set; }
    }
}
