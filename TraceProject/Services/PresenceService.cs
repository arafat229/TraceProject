using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Models;

namespace TraceProject.Services;

/// <summary>
/// Combines the two modules per employee: the camera says who came in today (attendance
/// check-in), the desktop agent says who is working at their computer. A camera check-in
/// followed by computer activity verifies the employee is at their workstation.
/// </summary>
public class PresenceService(ApplicationDbContext dbContext)
{
    public static PresenceStatus StatusOf(bool checkedInToday, ActivityState? computerState) => (checkedInToday, computerState) switch
    {
        (true, ActivityState.Active) => PresenceStatus.Working,
        (true, ActivityState.Idle or ActivityState.Locked) => PresenceStatus.PresentIdle,
        (true, _) => PresenceStatus.PresentNoComputer,
        (false, ActivityState.Active) => PresenceStatus.Unverified,
        _ => PresenceStatus.Absent,
    };

    /// <summary>Live combined status of every active employee, for today.</summary>
    public async Task<List<EmployeePresenceDto>> GetLiveAsync(CancellationToken cancellation = default)
    {
        var day = WorkDay.Today;
        var data = await LoadDayAsync(day, cancellation);

        var activities = await dbContext.Activities.AsNoTracking()
            .Select(a => new { a.EmployeeId, a.State, a.CurrentApp, a.LastSeenAt })
            .ToDictionaryAsync(a => a.EmployeeId, cancellation);

        return data.Employees.Select(e =>
        {
            data.Attendance.TryGetValue(e.Id, out var att);
            activities.TryGetValue(e.Id, out var act);
            var sessions = data.Sessions.GetValueOrDefault(e.Id, []);
            var status = StatusOf(att is not null, act?.State);

            return new EmployeePresenceDto(
                e.Id, e.FullName, e.Department, status.ToString(),
                att?.CheckIn, att is null ? null : att.CheckOut ?? att.CheckIn, att?.Place,
                act?.State.ToString(),
                act?.State is ActivityState.Active or ActivityState.Idle ? act.CurrentApp : null,
                act?.LastSeenAt,
                sessions.Where(s => s.State == ActivityState.Active).Sum(s => (s.EndedAt - s.StartedAt).TotalSeconds));
        })
        .OrderBy(p => Enum.Parse<PresenceStatus>(p.Status))
        .ThenBy(p => p.EmployeeName)
        .ToList();
    }

    /// <summary>Per-employee presence report for one (server-local) day.</summary>
    public async Task<List<PresenceDayDto>> GetDailyAsync(DateOnly day, CancellationToken cancellation = default)
    {
        var data = await LoadDayAsync(day, cancellation);

        return data.Employees.Select(e =>
        {
            data.Attendance.TryGetValue(e.Id, out var att);
            var sessions = data.Sessions.GetValueOrDefault(e.Id, []);
            var active = sessions.Where(s => s.State == ActivityState.Active).ToList();

            double Seconds(ActivityState state) =>
                sessions.Where(s => s.State == state).Sum(s => (s.EndedAt - s.StartedAt).TotalSeconds);

            // Active time from the camera check-in onwards
            var verified = att is null ? 0 : active.Sum(s =>
                Math.Max(0, (s.EndedAt - (s.StartedAt > att.CheckIn ? s.StartedAt : att.CheckIn)).TotalSeconds));

            var activeSeconds = Seconds(ActivityState.Active);
            var outcome = (att is not null, activeSeconds > 0) switch
            {
                (true, true) => PresenceOutcome.Verified,
                (true, false) => PresenceOutcome.PresentNoComputer,
                (false, true) => PresenceOutcome.UnverifiedComputerUse,
                _ => PresenceOutcome.Absent,
            };

            return new PresenceDayDto(
                e.Id, e.FullName, e.Department, day, outcome.ToString(),
                att?.CheckIn, att?.CheckOut, att?.Place,
                sessions.Count > 0 ? sessions.Min(s => s.StartedAt) : null,
                sessions.Count > 0 ? sessions.Max(s => s.EndedAt) : null,
                activeSeconds, Seconds(ActivityState.Idle), Seconds(ActivityState.Locked), verified);
        })
        .OrderBy(r => Enum.Parse<PresenceOutcome>(r.Outcome))
        .ThenBy(r => r.EmployeeName)
        .ToList();
    }

    private async Task<DayData> LoadDayAsync(DateOnly day, CancellationToken cancellation)
    {
        var employees = await dbContext.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new EmployeeRow(e.Id, e.FullName, e.Department.Name))
            .ToListAsync(cancellation);

        var attendance = await dbContext.Attendances.AsNoTracking()
            .Where(a => a.WorkDate == day)
            .Select(a => new AttendanceRow(a.EmployeeId, a.CheckIn, a.CheckOut, a.Location.Place))
            .ToListAsync(cancellation);

        var sessions = await dbContext.ActivitySessions.AsNoTracking()
            .Where(s => s.WorkDate == day)
            .Select(s => new SessionRow(s.EmployeeId, s.State, s.StartedAt, s.EndedAt))
            .ToListAsync(cancellation);

        return new DayData(
            employees,
            // (EmployeeId, WorkDate) is unique, but stay safe if old data has duplicates
            attendance.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.MinBy(a => a.CheckIn)!),
            sessions.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.ToList()));
    }

    private sealed record EmployeeRow(long Id, string FullName, string? Department);
    private sealed record AttendanceRow(long EmployeeId, DateTime CheckIn, DateTime? CheckOut, string? Place);
    private sealed record SessionRow(long EmployeeId, ActivityState State, DateTime StartedAt, DateTime EndedAt);
    private sealed record DayData(
        List<EmployeeRow> Employees,
        Dictionary<long, AttendanceRow> Attendance,
        Dictionary<long, List<SessionRow>> Sessions);
}
