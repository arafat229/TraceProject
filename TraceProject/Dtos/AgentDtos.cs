using TraceProject.Models;

namespace TraceProject.Dtos;

// Admin side (dashboard): devices
public record AddDeviceDto(long EmployeeId, string Name);

public record DeviceDto(
    long Id,
    long EmployeeId,
    string EmployeeName,
    string Name,
    string? MachineName,
    string? AgentVersion,
    bool IsPaired,
    bool IsOnline,
    bool IsActive,
    DateTime? PairedAt,
    DateTime? LastSeenAt);

public record PairingCodeDto(long DeviceId, string PairingCode, DateTime ExpiresAt);

public record DeviceActivationDto(bool Active);

// Agent side (TraceAgent). TraceAgent/AgentApi.cs holds a copy of these contracts; keep them in sync.
public record AgentPairDto(string PairingCode, string? MachineName, string? AgentVersion);

public record AgentPairResultDto(long DeviceId, string EmployeeName, string DeviceToken);

public record AgentConfigDto(long DeviceId, string DeviceName, string EmployeeName, int IdleThresholdSeconds, int ReportIntervalSeconds, bool CaptureWindowTitles);

/// <summary>One session as the agent sees it; the open session is resent with a growing EndedAt.</summary>
public record AgentSessionDto(Guid ClientId, ActivityState State, string? AppName, string? WindowTitle, DateTime StartedAt, DateTime EndedAt);

/// <summary>An agent lifecycle event (started, closed by the user, shutdown, sleep, resumed).</summary>
public record AgentEventDto(Guid ClientId, AgentEventKind Kind, DateTime At);

/// <summary>
/// When the newest event in a report is UserExit, Shutdown or Sleep, the agent is stopping:
/// the computer is shown Offline at once, with that reason.
/// SentAt is the agent's clock when it sent the report (agent 1.1.0+); the server uses it to
/// correct the report's times when that computer's clock is off.
/// </summary>
public record AgentReportDto(List<AgentSessionDto> Sessions, ActivityState CurrentState, string? CurrentApp, string? AgentVersion, List<AgentEventDto>? Events = null, DateTime? SentAt = null);

public record AgentEventViewDto(long Id, long DeviceId, string Kind, DateTime At);

// Dashboard: activity history
public record ActivitySessionDto(long Id, long DeviceId, string State, string? AppName, string? WindowTitle, DateTime StartedAt, DateTime EndedAt, double Seconds);

public record AppUsageDto(string AppName, double Seconds);

public record ActivitySummaryDto(
    long EmployeeId,
    DateOnly Date,
    double ActiveSeconds,
    double IdleSeconds,
    double LockedSeconds,
    DateTime? FirstActivity,
    DateTime? LastActivity,
    List<AppUsageDto> TopApps);
