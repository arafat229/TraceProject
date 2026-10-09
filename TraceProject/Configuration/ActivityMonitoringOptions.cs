namespace TraceProject.Configuration;

/// <summary>
/// Desktop monitoring agent (TraceAgent) settings, from the "ActivityMonitoring" section.
/// The agent downloads IdleThresholdSeconds, ReportIntervalSeconds and CaptureWindowTitles
/// from GET /api/Agent/me, so they are controlled centrally.
/// </summary>
public class ActivityMonitoringOptions
{
    /// <summary>No keyboard/mouse input for this long means Idle.</summary>
    public int IdleThresholdSeconds { get; set; } = 300;

    /// <summary>How often the agent uploads its sessions.</summary>
    public int ReportIntervalSeconds { get; set; } = 15;

    /// <summary>A device that has not reported for this long is shown as Offline.</summary>
    public int OfflineAfterSeconds { get; set; } = 90;

    /// <summary>Whether the agent sends window titles (they can contain private text such as email subjects).</summary>
    public bool CaptureWindowTitles { get; set; } = true;

    /// <summary>How long an admin-issued pairing code stays valid.</summary>
    public int PairingCodeMinutes { get; set; } = 15;
}
