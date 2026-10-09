namespace TraceProject.Dtos;

/// <summary>Live combined status of one employee (GET /api/Presence).</summary>
public record EmployeePresenceDto(
    long EmployeeId,
    string EmployeeName,
    string? Department,
    string Status,
    // Camera: today's check-in and the last time a camera recognized them (attendance check-out)
    DateTime? CheckIn,
    DateTime? LastSeenByCamera,
    string? LocationName,
    // Computer: live agent state ("Active" | "Idle" | "Locked" | "Offline"), null when never reported
    string? ComputerState,
    string? CurrentApp,
    DateTime? ComputerLastSeen,
    double ActiveSecondsToday);

/// <summary>One employee's day in the presence report (GET /api/Presence/daily).</summary>
public record PresenceDayDto(
    long EmployeeId,
    string EmployeeName,
    string? Department,
    DateOnly Date,
    string Outcome,
    DateTime? CheckIn,
    DateTime? CheckOut,
    string? LocationName,
    DateTime? FirstComputerActivity,
    DateTime? LastComputerActivity,
    double ActiveSeconds,
    double IdleSeconds,
    double LockedSeconds,
    // Active computer time after the camera check-in: work verified by both modules
    double VerifiedActiveSeconds);
