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
    // Why it is Offline: "UserExit" | "Shutdown" | "Sleep" | "ConnectionLost"
    string? ComputerOfflineReason,
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
    bool IsLate,
    DateTime? FirstComputerActivity,
    DateTime? LastComputerActivity,
    double ActiveSeconds,
    double IdleSeconds,
    double LockedSeconds,
    // Active computer time after the camera check-in: work verified by both modules
    double VerifiedActiveSeconds,
    // Times the employee closed TraceAgent that day
    int AgentClosedByUser);

/// <summary>One employee over a period (GET /api/Presence/range). Absence counts working days (Mon–Fri) only.</summary>
public record PresenceEmployeeRangeDto(
    long EmployeeId,
    string EmployeeName,
    string? Department,
    int DaysVerified,
    int DaysPresentNoComputer,
    int DaysUnverified,
    int DaysAbsent,
    int DaysLate,
    // Mean local time of the camera check-in, "HH:mm"
    string? AverageCheckIn,
    double ActiveSeconds,
    double IdleSeconds,
    double LockedSeconds,
    double VerifiedActiveSeconds,
    int AgentClosedByUser);

/// <summary>All employees on one day of the period, for charts.</summary>
public record PresenceDayTotalsDto(
    DateOnly Date,
    bool IsWorkingDay,
    int Verified,
    int PresentNoComputer,
    int Unverified,
    int Absent,
    double ActiveSeconds,
    double VerifiedActiveSeconds);

public record PresenceRangeDto(
    DateOnly From,
    DateOnly To,
    int WorkingDays,
    List<PresenceEmployeeRangeDto> Employees,
    List<PresenceDayTotalsDto> Days);

/// <summary>One employee over a period, day by day (GET /api/Presence/employee/{id}).</summary>
public record PresenceEmployeeReportDto(
    DateOnly From,
    DateOnly To,
    int WorkingDays,
    PresenceEmployeeRangeDto Summary,
    List<PresenceDayDto> Days,
    // Active computer time per application over the whole period
    List<AppUsageDto> TopApps);
