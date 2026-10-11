namespace TraceProject.Configuration;

/// <summary>
/// Configuration for camera connection resilience (reconnection strategy).
/// Bind from appsettings.json "Connection" section.
/// </summary>
public class ConnectionOptions
{
    /// <summary>
    /// Initial delay in seconds before first reconnection attempt.
    /// Example: 5 seconds after first disconnect.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay in seconds between reconnection attempts (exponential backoff cap).
    /// Example: 30 seconds max, so backoff stops at 5s, 10s, 20s, 30s, 30s, ...
    /// </summary>
    public int MaxReconnectDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Backoff multiplier for each reconnection attempt.
    /// Example: delay * 2 (5s → 10s → 20s → 30s).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum number of reconnection attempts before giving up.
    /// Set to 0 for infinite retries (recommended for always-on monitoring).
    /// Set to positive number to limit attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 0;

    /// <summary>
    /// Timeout in milliseconds for a single connection attempt.
    /// If connection doesn't complete within this time, it's considered failed.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// After this many failed attempts in a row (about 10 minutes at the 30 s cap), the camera is
    /// treated as off rather than glitching, and retries slow to OutageReconnectDelaySeconds.
    /// </summary>
    public int OutageAfterAttempts { get; set; } = 20;

    /// <summary>
    /// Delay between attempts during a long outage. Also how long a camera that comes back can
    /// wait before it is picked up again.
    /// </summary>
    public int OutageReconnectDelaySeconds { get; set; } = 120;
}
