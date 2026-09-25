using TraceProject.Configuration;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services;

/// <summary>
/// Manages RTSP camera connection lifecycle, reconnection logic, and backoff strategy.
/// Ensures only one connection attempt at a time and coordinates with IVideoSource.
/// </summary>
public class CameraConnectionManager : IAsyncDisposable
{
    private readonly IVideoSource videoSource;
    private readonly ConnectionOptions connectionOptions;
    private readonly ILogger<CameraConnectionManager> logger;

    private int reconnectAttempts;
    private DateTime lastConnectAttemptUtc = DateTime.MinValue;
    private CancellationTokenSource? reconnectCts;
    private Task? reconnectTask;
    private readonly SemaphoreSlim connectionSemaphore = new(1, 1);
    private readonly object scheduleLock = new();

    // Watchdog: catches failures that raise no LibVLC event (stuck connecting, frozen stream)
    private CancellationTokenSource? lifetimeCts;
    private Task? watchdogTask;
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FrameStallTimeout = TimeSpan.FromSeconds(15);
    // Real video always changes (sensor noise, the camera's on-screen clock); identical frames this long = frozen stream
    private static readonly TimeSpan PictureFrozenTimeout = TimeSpan.FromSeconds(20);

    public CameraConnectionManager(
        IVideoSource videoSource,
        ConnectionOptions connectionOptions,
        ILogger<CameraConnectionManager> logger)
    {
        this.videoSource = videoSource;
        this.connectionOptions = connectionOptions;
        this.logger = logger;

        // Hook into connection status changes
        videoSource.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    /// <summary>
    /// Start managing the connection (initial connect + reconnection logic).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting camera connection manager");
        reconnectCts = new CancellationTokenSource();
        reconnectAttempts = 0;
        lifetimeCts = new CancellationTokenSource();

        // First connection attempt
        await AttemptConnectAsync(cancellationToken);

        watchdogTask = WatchdogAsync(lifetimeCts.Token);
    }

    /// <summary>
    /// Stop managing and disconnect.
    /// </summary>
    public async Task StopAsync()
    {
        logger.LogInformation("Stopping camera connection manager");

        lifetimeCts?.Cancel();
        if (watchdogTask != null)
        {
            try { await watchdogTask; } catch (OperationCanceledException) { }
        }

        reconnectCts?.Cancel();
        if (reconnectTask != null)
        {
            try
            {
                await reconnectTask;
            }
            catch (OperationCanceledException) { }
        }

        await videoSource.StopAsync();
    }

    /// <summary>
    /// Make a single connection attempt with timeout.
    /// </summary>
    private async Task AttemptConnectAsync(CancellationToken cancellationToken)
    {
        // Ensure only one connection attempt at a time
        if (!connectionSemaphore.Wait(0))
        {
            logger.LogWarning("Connection attempt already in progress, skipping");
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(connectionOptions.ConnectTimeoutMs);

            logger.LogDebug("Attempting to connect to camera (attempt {Attempt})", reconnectAttempts + 1);
            lastConnectAttemptUtc = DateTime.UtcNow;
            await videoSource.StartAsync(timeoutCts.Token);
            // Backoff resets when the stream actually reports Connected (OnConnectionStatusChanged)
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Connection attempt timed out after {TimeoutMs}ms",
                connectionOptions.ConnectTimeoutMs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection attempt failed");
        }
        finally
        {
            connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Called when the IVideoSource connection status changes.
    /// Triggered reconnection if disconnected and within max retry bounds.
    /// </summary>
    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        if (e.Status == Services.Interfaces.CameraConnectionStatus.Connected)
        {
            if (reconnectAttempts > 0)
                logger.LogInformation("Camera reconnected after {Attempts} attempt(s)", reconnectAttempts);
            reconnectAttempts = 0;
            return;
        }

        if (e.Status == Services.Interfaces.CameraConnectionStatus.Disconnected ||
            e.Status == Services.Interfaces.CameraConnectionStatus.Error)
        {
            logger.LogWarning("Camera disconnected or encountered error: {Message}", e.Message);
            ScheduleReconnect();
        }
    }

    /// <summary>
    /// Schedules the next reconnection attempt with exponential backoff, unless one is already pending.
    /// </summary>
    private void ScheduleReconnect()
    {
        lock (scheduleLock)
        {
            if (lifetimeCts?.IsCancellationRequested == true)
                return; // shutting down
            if (reconnectTask is { IsCompleted: false })
                return; // already waiting to reconnect

            // Check reconnection limits (0 = retry forever)
            if (connectionOptions.MaxReconnectAttempts > 0 &&
                reconnectAttempts >= connectionOptions.MaxReconnectAttempts)
            {
                logger.LogError("Max reconnection attempts ({Max}) reached. Giving up.",
                    connectionOptions.MaxReconnectAttempts);
                return;
            }

            reconnectAttempts++;

            var delayMs = CalculateBackoffDelay(reconnectAttempts);
            logger.LogInformation(
                "Scheduling reconnection attempt {Attempt} in {DelayMs}ms",
                reconnectAttempts, delayMs);

            reconnectCts?.Cancel();
            reconnectCts = new CancellationTokenSource();
            reconnectTask = ScheduleReconnectAsync(delayMs, reconnectCts.Token);
        }
    }

    /// <summary>
    /// Every few seconds, forces a reconnect if the stream is stuck: connecting for longer than
    /// ConnectTimeoutMs, "connected" but without frames for FrameStallTimeout, or disconnected with
    /// no retry pending.
    /// </summary>
    private async Task WatchdogAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(WatchdogInterval, cancellationToken);

                if (reconnectTask is { IsCompleted: false })
                    continue; // a retry is already scheduled

                var now = DateTime.UtcNow;
                var sinceAttempt = now - lastConnectAttemptUtc;
                var lastActivity = videoSource.LastFrameTimestampUtc is DateTime f && f > lastConnectAttemptUtc ? f : lastConnectAttemptUtc;
                var lastChange = videoSource.LastFrameChangeUtc is DateTime c && c > lastConnectAttemptUtc ? c : lastConnectAttemptUtc;

                string? reason = videoSource.ConnectionStatus switch
                {
                    CameraConnectionStatus.Connecting when sinceAttempt > TimeSpan.FromMilliseconds(connectionOptions.ConnectTimeoutMs)
                        => $"still connecting after {sinceAttempt.TotalSeconds:F0}s",
                    CameraConnectionStatus.Connected when now - lastActivity > FrameStallTimeout
                        => $"no frames for {(now - lastActivity).TotalSeconds:F0}s",
                    CameraConnectionStatus.Connected when now - lastChange > PictureFrozenTimeout
                        => $"picture frozen for {(now - lastChange).TotalSeconds:F0}s",
                    CameraConnectionStatus.Disconnected or CameraConnectionStatus.Error
                        => "disconnected with no retry pending",
                    _ => null
                };

                if (reason != null)
                {
                    logger.LogWarning("Camera watchdog: {Reason}; reconnecting", reason);
                    ScheduleReconnect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Camera watchdog stopped unexpectedly");
        }
    }

    /// <summary>
    /// Calculate exponential backoff delay with maximum cap.
    /// Formula: min(initialDelay * (multiplier ^ (attempts - 1)), maxDelay)
    /// </summary>
    private int CalculateBackoffDelay(int attemptNumber)
    {
        var baseDelayMs = connectionOptions.ReconnectDelaySeconds * 1000;
        var maxDelayMs = connectionOptions.MaxReconnectDelaySeconds * 1000;
        var multiplier = connectionOptions.BackoffMultiplier;

        var delayMs = (int)(baseDelayMs * Math.Pow(multiplier, attemptNumber - 1));
        return Math.Min(delayMs, maxDelayMs);
    }

    /// <summary>
    /// Wait and then attempt to reconnect.
    /// </summary>
    private async Task ScheduleReconnectAsync(int delayMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMs, cancellationToken);
            logger.LogInformation("Attempting reconnection (attempt {Attempt})", reconnectAttempts);
            await AttemptConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Reconnection attempt cancelled");
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        connectionSemaphore?.Dispose();
        reconnectCts?.Dispose();
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
