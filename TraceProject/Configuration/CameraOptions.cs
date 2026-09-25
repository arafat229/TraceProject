namespace TraceProject.Configuration;

/// <summary>
/// Configuration for RTSP camera connection.
/// Bind from appsettings.json "Camera" section.
/// Password should come from User Secrets (dev) or environment variables (prod).
/// </summary>
public class CameraOptions
{
    /// <summary>
    /// Camera primary key in database (for Phase 2 when we wire to DB).
    /// </summary>
    public long Id { get; set; } = 1;

    /// <summary>
    /// Human-readable camera name (e.g., "Main Entrance Camera").
    /// </summary>
    public string Name { get; set; } = "Default Camera";

    /// <summary>
    /// IP address of the camera (e.g., "192.168.1.10").
    /// </summary>
    public string IpAddress { get; set; } = "192.168.1.10";

    /// <summary>
    /// Full RTSP URL including auth (e.g., "rtsp://admin:admin@192.168.1.10:554/").
    /// NOTE: This can contain literal username:password, OR use {username}:{password} placeholders
    /// that will be replaced at runtime from Username/Password fields.
    /// </summary>
    public string RtspUrl { get; set; } = "rtsp://{username}:{password}@192.168.1.10:554/";

    /// <summary>
    /// RTSP username (kept separate for potential templating in RtspUrl).
    /// Never logged as plain text.
    /// </summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// RTSP password. Should be filled from User Secrets (dev) / env var (prod).
    /// Never logged as plain text.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Transport protocol for RTSP: "TCP" (preferred, more reliable) or "UDP".
    /// </summary>
    public string Transport { get; set; } = "TCP";

    /// <summary>
    /// Whether this camera is enabled for capture.
    /// Set to false to disable capture without removing config.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Location that attendance and unknown faces from this camera are filed under.
    /// If not set, the Cameras row (Id) LocationId is used, then the first active location.
    /// </summary>
    public long? LocationId { get; set; }
}
