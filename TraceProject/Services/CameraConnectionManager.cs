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
    private DateTime lastReconnectAttempt = DateTime.MinValue;
    private CancellationTokenSource? reconnectCts;
    private Task? reconnectTask;
    private readonly SemaphoreSlim connectionSemaphore = new(1, 1);

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

        // First connection attempt
        await AttemptConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Stop managing and disconnect.
    /// </summary>
    public async Task StopAsync()
    {
        logger.LogInformation("Stopping camera connection manager");

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
            await videoSource.StartAsync(timeoutCts.Token);
            reconnectAttempts = 0; // Reset on success
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
        if (e.Status == Services.Interfaces.CameraConnectionStatus.Disconnected ||
            e.Status == Services.Interfaces.CameraConnectionStatus.Error)
        {
            logger.LogWarning("Camera disconnected or encountered error: {Message}", e.Message);

            // Check reconnection limits
            if (connectionOptions.MaxReconnectAttempts > 0 && 
                reconnectAttempts >= connectionOptions.MaxReconnectAttempts)
            {
                logger.LogError("Max reconnection attempts ({Max}) reached. Giving up.",
                    connectionOptions.MaxReconnectAttempts);
                return;
            }

            reconnectAttempts++;

            // Calculate backoff delay
            var delayMs = CalculateBackoffDelay(reconnectAttempts);
            logger.LogInformation(
                "Scheduling reconnection attempt {Attempt} in {DelayMs}ms",
                reconnectAttempts, delayMs);

            // Schedule reconnection
            reconnectCts?.Cancel();
            reconnectCts = new CancellationTokenSource();
            reconnectTask = ScheduleReconnectAsync(delayMs, reconnectCts.Token);
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
        connectionSemaphore?.Dispose();
        reconnectCts?.Dispose();
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
