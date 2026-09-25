namespace TraceProject.Services;

/// <summary>
/// Attendance days are server-local calendar days; timestamps are stored in UTC.
/// </summary>
public static class WorkDay
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static DateOnly Of(DateTime utc) => DateOnly.FromDateTime(utc.ToLocalTime());
}
