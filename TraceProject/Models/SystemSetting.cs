namespace TraceProject.Models;

/// <summary>
/// System-wide settings edited from the dashboard's Settings page (a single row).
/// </summary>
public class SystemSetting
{
    public long Id { get; set; }

    /// <summary>Minimum SFace cosine similarity to accept a face as an employee (OpenCV recommends 0.363).</summary>
    public float RecognitionThreshold { get; set; } = 0.363f;

    /// <summary>Local working-day start, "HH:mm".</summary>
    public string WorkStartTime { get; set; } = "08:00";

    /// <summary>Local working-day end, "HH:mm".</summary>
    public string WorkEndTime { get; set; } = "17:00";

    /// <summary>Minutes after WorkStartTime before a first check-in counts as late.</summary>
    public int LateGraceMinutes { get; set; } = 15;

    /// <summary>Minimum minutes between updates of an employee's check-out (last seen) time.</summary>
    public int AttendanceUpdateIntervalMinutes { get; set; } = 1;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
