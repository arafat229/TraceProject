using System.Runtime.InteropServices;
using System.Text;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services.VideoSources;

/// <summary>
/// RTSP video source implementation using LibVLCSharp and native LibVLC.
/// Phase 2: Captures real frame dimensions and timestamps, measures actual FPS, tracks metrics.
/// Captures live video frames from an IP camera over RTSP/TCP and exposes them via IVideoSource.
/// Handles connection lifecycle, reconnection with exponential backoff, and frame delivery.
/// </summary>
public class RtspVideoSource : IVideoSource, IAsyncDisposable
{
    private readonly CameraOptions cameraOptions;
    private readonly VideoProcessingOptions videoOptions;
    private readonly ILogger<RtspVideoSource> logger;
    private readonly ICameraMetricsService metricsService;
    private readonly string rtspUrl;

    private LibVLC? libVlc;
    private Media? media;
    private MediaPlayer? mediaPlayer;
    private bool isRunning = false;
    private VideoFrameImpl? latestFrame;
    private readonly object frameLock = new();
    private CameraConnectionStatus connectionStatus = CameraConnectionStatus.Disconnected;
    private CancellationTokenSource? metricsLoggingTokenSource;
    private bool hasSeenFirstFrame = false;

    // LibVLC memory rendering. The delegates are kept in fields so the GC can't
    // collect them while native code still holds pointers to them.
    private const uint MaxCaptureWidth = 1280;
    private const int MaxCaptureFps = 15;
    private readonly MediaPlayer.LibVLCVideoFormatCb videoFormatCallback;
    private readonly MediaPlayer.LibVLCVideoCleanupCb videoCleanupCallback;
    private readonly MediaPlayer.LibVLCVideoLockCb videoLockCallback;
    private readonly MediaPlayer.LibVLCVideoDisplayCb videoDisplayCallback;
    private readonly object bufferLock = new();
    private IntPtr frameBuffer = IntPtr.Zero;
    private int bufferWidth;
    private int bufferHeight;
    private DateTime lastCaptureUtc = DateTime.MinValue;

    public bool IsRunning => isRunning;
    public CameraConnectionStatus ConnectionStatus => connectionStatus;
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public (int Width, int Height)? CurrentResolution { get; private set; }
    public double? CurrentFramesPerSecond { get; private set; }
    public DateTime? LastFrameTimestampUtc { get; private set; }
    public DateTime? LastFrameChangeUtc { get; private set; }
    private ulong lastFrameSignature;
    public Exception? LastException { get; private set; }

    public RtspVideoSource(
        IOptions<CameraOptions> cameraOptionsOptions,
        IOptions<VideoProcessingOptions> videoOptionsOptions,
        ICameraMetricsService metricsService,
        ILogger<RtspVideoSource> logger)
    {
        this.cameraOptions = cameraOptionsOptions.Value;
        this.videoOptions = videoOptionsOptions.Value;
        this.metricsService = metricsService;
        this.logger = logger;
        this.rtspUrl = BuildRtspUrl();

        videoFormatCallback = OnVideoFormat;
        videoCleanupCallback = OnVideoCleanup;
        videoLockCallback = OnVideoLock;
        videoDisplayCallback = OnVideoDisplay;
    }

    /// <summary>
    /// Builds the RTSP URL from configuration, substituting username and password placeholders.
    /// </summary>
    private string BuildRtspUrl()
    {
        var url = cameraOptions.RtspUrl ?? string.Empty;
        url = url.Replace("{username}", cameraOptions.Username, StringComparison.OrdinalIgnoreCase);
        url = url.Replace("{password}", cameraOptions.Password, StringComparison.OrdinalIgnoreCase);
        return url;
    }

    /// <summary>
    /// Sanitizes RTSP URL for logging by removing sensitive credentials.
    /// </summary>
    private string SanitizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "[empty URL]";

        // Remove credentials from rtsp://user:pass@host pattern
        return System.Text.RegularExpressions.Regex.Replace(
            url,
            @"rtsp://[^:]+:[^@]+@",
            "rtsp://***:***@",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Starts the video source: initializes LibVLC, opens media, and begins frame capture.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (isRunning)
            {
                // A reconnect after the stream dropped: stop the player, keep it and the LibVLC engine
                logger.LogInformation("Restarting RTSP video source");
                StopPlayback();
            }

            logger.LogInformation("Starting RTSP video source at {sanitizedUrl}", SanitizeUrl(rtspUrl));

            // Reset metrics on new connection
            metricsService.Reset();
            hasSeenFirstFrame = false;

            EnsurePlayer();

            // A new Media per attempt is cheap; the engine and the player are reused
            media?.Dispose();
            media = new Media(libVlc!, rtspUrl, FromType.FromLocation);

            // Configure RTSP options for TCP transport and low latency
            media.AddOption(":rtsp-tcp=yes");  // Use TCP instead of UDP
            media.AddOption(":network-caching=500");  // Reduce latency
            media.AddOption(":live-caching=500");

            mediaPlayer!.Media = media;
            mediaPlayer.Play();

            isRunning = true;
            UpdateConnectionStatus(CameraConnectionStatus.Connecting);

            // Start periodic metrics logging
            metricsLoggingTokenSource = new CancellationTokenSource();
            _ = LogMetricsPeriodicallyAsync(metricsLoggingTokenSource.Token);

            logger.LogInformation("RTSP video source started");
        }
        catch (Exception ex)
        {
            LastException = ex;
            logger.LogError(ex, "Error starting RTSP video source");
            UpdateConnectionStatus(CameraConnectionStatus.Error);
            throw;
        }
    }

    /// <summary>
    /// Creates the LibVLC engine and the media player on first use and keeps them for the life of
    /// this source. Disposing and recreating the engine on every reconnect made LibVLC abort() the
    /// whole process (exit code 3) after an hour or so of retries against an unreachable camera.
    /// </summary>
    private void EnsurePlayer()
    {
        if (mediaPlayer != null)
            return;

        Core.Initialize();
        libVlc = new LibVLC(enableDebugLogs: false);
        // Subscribing replaces LibVLC's own console output, which printed the camera login in the
        // stream address. Its messages go through OnLibVlcLog instead, with the login masked.
        libVlc.Log += OnLibVlcLog;

        mediaPlayer = new MediaPlayer(libVlc);
        mediaPlayer.Playing += OnMediaPlayerPlaying;
        mediaPlayer.Stopped += OnMediaPlayerStopped;
        mediaPlayer.EncounteredError += OnMediaPlayerError;

        // Decode frames into our own memory buffer (BGRA) instead of a window. Must be set before Play().
        mediaPlayer.SetVideoFormatCallbacks(videoFormatCallback, videoCleanupCallback);
        mediaPlayer.SetVideoCallbacks(videoLockCallback, null, videoDisplayCallback);
    }

    private void OnLibVlcLog(object? sender, LogEventArgs e)
    {
        try
        {
            // Errors repeat on every failed reconnect; keep them visible but out of the error count
            if (e.Level == LibVLCSharp.Shared.LogLevel.Error)
                logger.LogWarning("LibVLC {module}: {message}", e.Module, SanitizeUrl(e.Message));
            else if (e.Level == LibVLCSharp.Shared.LogLevel.Warning)
                logger.LogDebug("LibVLC {module}: {message}", e.Module, SanitizeUrl(e.Message));
        }
        catch
        {
            // Never let an exception escape into native LibVLC code
        }
    }

    /// <summary>
    /// LibVLC format callback: fixes the decoded pixel format to RV32 (BGRA in memory),
    /// caps the width at MaxCaptureWidth (keeping aspect ratio) and allocates the frame buffer.
    /// </summary>
    private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        try
        {
            Marshal.Copy(Encoding.ASCII.GetBytes("RV32"), 0, chroma, 4);

            if (width > MaxCaptureWidth)
            {
                height = (uint)((ulong)height * MaxCaptureWidth / width) & ~1u;
                width = MaxCaptureWidth;
            }

            pitches = width * 4;
            lines = height;

            lock (bufferLock)
            {
                FreeFrameBuffer();
                frameBuffer = Marshal.AllocHGlobal((int)(pitches * lines));
                bufferWidth = (int)width;
                bufferHeight = (int)height;
            }

            CurrentResolution = ((int)width, (int)height);
            metricsService.UpdateResolution((int)width, (int)height);
            logger.LogInformation("Decoding RTSP stream at {width}x{height} (BGRA)", width, height);

            return 1; // one picture buffer
        }
        catch (Exception ex)
        {
            LastException = ex;
            logger.LogError(ex, "Error in LibVLC video format callback");
            return 0; // tells LibVLC to abort video output
        }
    }

    private void OnVideoCleanup(ref IntPtr opaque)
    {
        lock (bufferLock)
        {
            FreeFrameBuffer();
        }
    }

    /// <summary>
    /// LibVLC lock callback: hands LibVLC our buffer to decode the next picture into.
    /// </summary>
    private IntPtr OnVideoLock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, frameBuffer);
        return IntPtr.Zero;
    }

    /// <summary>
    /// LibVLC display callback: a decoded picture is ready in the buffer. Copies it into a
    /// managed frame (throttled to MaxCaptureFps) and publishes it as the latest frame.
    /// </summary>
    private void OnVideoDisplay(IntPtr opaque, IntPtr picture)
    {
        try
        {
            var now = DateTime.UtcNow;
            if ((now - lastCaptureUtc).TotalMilliseconds < 1000.0 / MaxCaptureFps)
                return;

            VideoFrameImpl frame;
            lock (bufferLock)
            {
                if (frameBuffer == IntPtr.Zero)
                    return;

                var data = new byte[bufferWidth * bufferHeight * 4];
                Marshal.Copy(frameBuffer, data, 0, data.Length);

                frame = new VideoFrameImpl
                {
                    Data = data,
                    Width = bufferWidth,
                    Height = bufferHeight,
                    TimestampUtc = now,
                    PixelFormat = "BGRA",
                    Pitch = bufferWidth * 4
                };
            }

            lastCaptureUtc = now;

            var signature = ComputeSignature(frame.Data);
            if (signature != lastFrameSignature)
            {
                lastFrameSignature = signature;
                LastFrameChangeUtc = now;
            }

            lock (frameLock)
            {
                latestFrame = frame;
            }

            LastFrameTimestampUtc = now;
            hasSeenFirstFrame = true;
            metricsService.RecordFrameReceived(frame.Width, frame.Height, now);
            CurrentFramesPerSecond = metricsService.IncomingFramesPerSecond;

            FrameReceived?.Invoke(this, new FrameReceivedEventArgs { Frame = frame, ReceivedAt = now });
        }
        catch (Exception ex)
        {
            // Never let an exception escape into native LibVLC code
            LastException = ex;
            logger.LogError(ex, "Error in LibVLC video display callback");
        }
    }

    /// <summary>
    /// Cheap fingerprint of a frame (FNV-1a over a sparse pixel sample) to detect a frozen picture.
    /// </summary>
    private static ulong ComputeSignature(byte[] data)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < data.Length; i += 97)
        {
            hash ^= data[i];
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private void FreeFrameBuffer()
    {
        if (frameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(frameBuffer);
            frameBuffer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Periodic background task that logs metrics summary.
    /// Fires every N seconds (configurable via VideoProcessing:MetricsSummaryIntervalSeconds).
    /// </summary>
    private async Task LogMetricsPeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (videoOptions.MetricsSummaryIntervalSeconds <= 0)
            {
                logger.LogDebug("Metrics summary logging disabled (interval <= 0)");
                return;
            }

            var interval = TimeSpan.FromSeconds(videoOptions.MetricsSummaryIntervalSeconds);

            while (!cancellationToken.IsCancellationRequested && isRunning)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);

                    if (!isRunning || connectionStatus != CameraConnectionStatus.Connected)
                        continue;

                    var metrics = metricsService.GetSnapshot();

                    logger.LogInformation(
                        "Camera Metrics: FPS ~{incomingFps:F1} | Consumable ~{consumableFps:F1} | " +
                        "Received: {received} | Dropped: {dropped} | Resolution: {res} | " +
                        "Latency ~{latency:F1}ms",
                        metrics.IncomingFramesPerSecond ?? 0,
                        metrics.ConsumableFramesPerSecond ?? 0,
                        metrics.FramesReceived,
                        metrics.FramesDropped,
                        metrics.Resolution?.ToString() ?? "Unknown",
                        metrics.ApproximateLatencyMs ?? 0);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in metrics logging task");
                }
            }

            logger.LogInformation("Metrics logging task ended");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in metrics logging task");
        }
    }

    /// <summary>
    /// Stops playback before reconnecting, keeping the player and the engine. Its Stopped event is
    /// muted meanwhile so the reconnect it is part of doesn't schedule another one.
    /// </summary>
    private void StopPlayback()
    {
        try
        {
            metricsLoggingTokenSource?.Cancel();
            metricsLoggingTokenSource?.Dispose();
            metricsLoggingTokenSource = null;

            if (mediaPlayer != null)
            {
                mediaPlayer.Stopped -= OnMediaPlayerStopped;
                // Synchronous: LibVLC has finished with the frame buffer (cleanup callback) when it returns
                mediaPlayer.Stop();
                mediaPlayer.Stopped += OnMediaPlayerStopped;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error stopping the previous LibVLC stream");
        }
        finally
        {
            isRunning = false;
        }
    }

    /// <summary>
    /// Stops the video source; the player and engine are released in DisposeAsync.
    /// </summary>

    public async Task StopAsync()
    {
        try
        {
            logger.LogInformation("Stopping RTSP video source");

            // Stop metrics logging
            metricsLoggingTokenSource?.Cancel();
            metricsLoggingTokenSource?.Dispose();
            metricsLoggingTokenSource = null;

            if (mediaPlayer != null)
            {
                mediaPlayer.Playing -= OnMediaPlayerPlaying;
                mediaPlayer.Stopped -= OnMediaPlayerStopped;
                mediaPlayer.EncounteredError -= OnMediaPlayerError;
                mediaPlayer.Stop();
                // A later StartAsync (StopAsync then StartAsync) reuses the player, so re-attach
                mediaPlayer.Playing += OnMediaPlayerPlaying;
                mediaPlayer.Stopped += OnMediaPlayerStopped;
                mediaPlayer.EncounteredError += OnMediaPlayerError;
            }

            isRunning = false;
            UpdateConnectionStatus(CameraConnectionStatus.Disconnected);

            logger.LogInformation("RTSP video source stopped");
        }
        catch (Exception ex)
        {
            LastException = ex;
            logger.LogError(ex, "Error stopping RTSP video source");
        }
    }

    /// <summary>
    /// Gets the latest captured video frame without blocking.
    /// Returns null if no frame has been captured yet.
    /// </summary>
    public IVideoFrame? GetLatestFrame()
    {
        lock (frameLock)
        {
            return latestFrame;
        }
    }

    /// <summary>
    /// Called when media player starts playing.
    /// </summary>
    private void OnMediaPlayerPlaying(object? sender, EventArgs e)
    {
        try
        {
            logger.LogInformation("Media player playing");
            UpdateConnectionStatus(CameraConnectionStatus.Connected);

            // Resolution is reported by OnVideoFormat once LibVLC knows the stream size
        }
        catch (Exception ex)
        {
            LastException = ex;
            logger.LogError(ex, "Error in OnMediaPlayerPlaying");
        }
    }

    /// <summary>
    /// Called when media player stops.
    /// </summary>
    private void OnMediaPlayerStopped(object? sender, EventArgs e)
    {
        logger.LogInformation("Media player stopped");
        UpdateConnectionStatus(CameraConnectionStatus.Disconnected);
    }

    /// <summary>
    /// Called when media player encounters an error.
    /// </summary>
    private void OnMediaPlayerError(object? sender, EventArgs e)
    {
        logger.LogError("Media player error encountered");
        UpdateConnectionStatus(CameraConnectionStatus.Error);
    }

    /// <summary>
    /// Updates the connection status and raises the ConnectionStatusChanged event.
    /// Resets metrics on connection state changes to avoid stale data.
    /// </summary>
    private void UpdateConnectionStatus(CameraConnectionStatus newStatus)
    {
        if (connectionStatus != newStatus)
        {
            var oldStatus = connectionStatus;
            connectionStatus = newStatus;

            // Reset metrics boundaries on reconnect or connection loss
            if ((oldStatus == CameraConnectionStatus.Connected && newStatus != CameraConnectionStatus.Connected) ||
                (oldStatus != CameraConnectionStatus.Connected && newStatus == CameraConnectionStatus.Connected))
            {
                metricsService.Reset();
                hasSeenFirstFrame = false;
            }

            var eventArgs = new ConnectionStatusChangedEventArgs 
            { 
                Status = newStatus,
                Message = $"Status changed from {oldStatus} to {newStatus}"
            };
            ConnectionStatusChanged?.Invoke(this, eventArgs);
            logger.LogInformation("Camera connection status changed: {oldStatus} -> {newStatus}", oldStatus, newStatus);
        }
    }

    /// <summary>
    /// Disposes of all resources associated with the video source.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();

            mediaPlayer?.Dispose();
            mediaPlayer = null;
            media?.Dispose();
            media = null;
            if (libVlc != null)
                libVlc.Log -= OnLibVlcLog;
            libVlc?.Dispose();
            libVlc = null;

            logger.LogInformation("RtspVideoSource disposed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing RtspVideoSource");
        }
    }
}

/// <summary>
/// Concrete implementation of IVideoFrame for capturing RTSP frames.
/// </summary>
internal class VideoFrameImpl : IVideoFrame
{
    public required byte[] Data { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required string PixelFormat { get; init; }
    public required int Pitch { get; init; }
}
