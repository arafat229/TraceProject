namespace TraceProject.Configuration;

/// <summary>
/// Phase 6: Attendance deduplication configuration.
/// Controls cooldown period to prevent duplicate check-ins for same employee.
/// </summary>
public class AttendanceOptions
{
    /// <summary>
    /// Cooldown period in minutes. If an employee has a recent Attendance record
    /// within this window, a new recognition won't create a duplicate.
    /// Default: 15 minutes
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;
}
