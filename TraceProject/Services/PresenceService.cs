using System.Globalization;
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
public class PresenceService(ApplicationDbContext dbContext, SystemSettingsService settings)
{
    /// <summary>Longest period GET /api/Presence/range accepts.</summary>
    public const int MaxRangeDays = 93;

    public static PresenceStatus StatusOf(bool checkedInToday, ActivityState? computerState) => (checkedInToday, computerState) switch
    {
        (true, ActivityState.Active) => PresenceStatus.Working,
        (true, ActivityState.Idle or ActivityState.Locked) => PresenceStatus.PresentIdle,
        (true, _) => PresenceStatus.PresentNoComputer,
        (false, ActivityState.Active) => PresenceStatus.Unverified,
        _ => PresenceStatus.Absent,
    };

    /// <summary>Absence is only counted on these days.</summary>
    public static bool IsWorkingDay(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>Live combined status of every active employee, for today.</summary>
    public async Task<List<EmployeePresenceDto>> GetLiveAsync(CancellationToken cancellation = default)
    {
        var day = WorkDay.Today;
        var data = await LoadRangeAsync(day, day, cancellation);

        var activities = await dbContext.Activities.AsNoTracking()
            .Select(a => new { a.EmployeeId, a.State, a.OfflineReason, a.CurrentApp, a.LastSeenAt })
            .ToDictionaryAsync(a => a.EmployeeId, cancellation);

        return data.Employees.Select(e =>
        {
            var att = data.AttendanceOf(e.Id, day);
            activities.TryGetValue(e.Id, out var act);
            var status = StatusOf(att is not null, act?.State);

            return new EmployeePresenceDto(
                e.Id, e.FullName, e.Department, status.ToString(),
                att?.CheckIn, att is null ? null : att.CheckOut ?? att.CheckIn, att?.Place,
                act?.State.ToString(),
                act?.State == ActivityState.Offline ? act.OfflineReason?.ToString() : null,
                act?.State is ActivityState.Active or ActivityState.Idle ? act.CurrentApp : null,
                act?.LastSeenAt,
                data.SessionsOf(e.Id, day).Where(s => s.State == ActivityState.Active).Sum(s => s.Seconds));
        })
        .OrderBy(p => Enum.Parse<PresenceStatus>(p.Status))
        .ThenBy(p => p.EmployeeName)
        .ToList();
    }

    /// <summary>Per-employee presence report for one (server-local) day.</summary>
    public async Task<List<PresenceDayDto>> GetDailyAsync(DateOnly day, CancellationToken cancellation = default)
    {
        var data = await LoadRangeAsync(day, day, cancellation);
        return data.Employees
            .Select(e => BuildDay(data, e, day))
            .OrderBy(r => Enum.Parse<PresenceOutcome>(r.Outcome))
            .ThenBy(r => r.EmployeeName)
            .ToList();
    }

    /// <summary>
    /// Totals per employee and per day over [from, to] (a week, a month). Days after today are left out;
    /// absence counts working days only.
    /// </summary>
    public async Task<PresenceRangeDto> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellation = default)
    {
        var lastDay = to > WorkDay.Today ? WorkDay.Today : to;
        var days = new List<DateOnly>();
        for (var d = from; d <= lastDay; d = d.AddDays(1))
            days.Add(d);

        var data = await LoadRangeAsync(from, lastDay, cancellation);
        var grid = data.Employees.ToDictionary(e => e.Id, e => days.Select(d => BuildDay(data, e, d)).ToList());

        var employees = data.Employees.Select(e =>
        {
            var rows = grid[e.Id];
            int Count(PresenceOutcome o) => rows.Count(r => r.Outcome == o.ToString());
            var checkIns = rows.Where(r => r.CheckIn is not null).Select(r => r.CheckIn!.Value.ToLocalTime().TimeOfDay.TotalMinutes).ToList();

            return new PresenceEmployeeRangeDto(
                e.Id, e.FullName, e.Department,
                Count(PresenceOutcome.Verified),
                Count(PresenceOutcome.PresentNoComputer),
                Count(PresenceOutcome.UnverifiedComputerUse),
                rows.Count(r => r.Outcome == nameof(PresenceOutcome.Absent) && IsWorkingDay(r.Date)),
                rows.Count(r => r.IsLate),
                checkIns.Count > 0 ? TimeSpan.FromMinutes(checkIns.Average()).ToString(@"hh\:mm") : null,
                rows.Sum(r => r.ActiveSeconds),
                rows.Sum(r => r.IdleSeconds),
                rows.Sum(r => r.LockedSeconds),
                rows.Sum(r => r.VerifiedActiveSeconds),
                rows.Sum(r => r.AgentClosedByUser));
        })
        .OrderBy(e => e.EmployeeName)
        .ToList();

        var totals = days.Select((d, i) =>
        {
            var rows = grid.Values.Select(r => r[i]).ToList();
            int Count(PresenceOutcome o) => rows.Count(r => r.Outcome == o.ToString());
            return new PresenceDayTotalsDto(
                d, IsWorkingDay(d),
                Count(PresenceOutcome.Verified),
                Count(PresenceOutcome.PresentNoComputer),
                Count(PresenceOutcome.UnverifiedComputerUse),
                IsWorkingDay(d) ? Count(PresenceOutcome.Absent) : 0,
                rows.Sum(r => r.ActiveSeconds),
                rows.Sum(r => r.VerifiedActiveSeconds));
        }).ToList();

        return new PresenceRangeDto(from, lastDay, days.Count(IsWorkingDay), employees, totals);
    }

    private PresenceDayDto BuildDay(RangeData data, EmployeeRow e, DateOnly day)
    {
        var att = data.AttendanceOf(e.Id, day);
        var sessions = data.SessionsOf(e.Id, day);

        double Seconds(ActivityState state) => sessions.Where(s => s.State == state).Sum(s => s.Seconds);

        // Active time from the camera check-in onwards
        var verified = att is null ? 0 : sessions.Where(s => s.State == ActivityState.Active).Sum(s =>
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
            att is not null && IsLate(att.CheckIn),
            sessions.Count > 0 ? sessions.Min(s => s.StartedAt) : null,
            sessions.Count > 0 ? sessions.Max(s => s.EndedAt) : null,
            activeSeconds, Seconds(ActivityState.Idle), Seconds(ActivityState.Locked), verified,
            data.UserExits.GetValueOrDefault((e.Id, day)));
    }

    /// <summary>Same rule as the dashboard: first check-in after work start + grace (Settings).</summary>
    private bool IsLate(DateTime checkInUtc)
    {
        var s = settings.Current;
        if (!TimeOnly.TryParseExact(s.WorkStartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return false;
        var limit = start.ToTimeSpan() + TimeSpan.FromMinutes(s.LateGraceMinutes);
        return checkInUtc.ToLocalTime().TimeOfDay > limit;
    }

    private async Task<RangeData> LoadRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellation)
    {
        var employees = await dbContext.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new EmployeeRow(e.Id, e.FullName, e.Department.Name))
            .ToListAsync(cancellation);

        var attendance = await dbContext.Attendances.AsNoTracking()
            .Where(a => a.WorkDate >= from && a.WorkDate <= to)
            .Select(a => new AttendanceRow(a.EmployeeId, a.WorkDate, a.CheckIn, a.CheckOut, a.Location.Place))
            .ToListAsync(cancellation);

        var sessions = await dbContext.ActivitySessions.AsNoTracking()
            .Where(s => s.WorkDate >= from && s.WorkDate <= to)
            .Select(s => new SessionRow(s.EmployeeId, s.WorkDate, s.State, s.StartedAt, s.EndedAt))
            .ToListAsync(cancellation);

        var userExits = await dbContext.AgentEvents.AsNoTracking()
            .Where(e => e.Kind == AgentEventKind.UserExit && e.WorkDate >= from && e.WorkDate <= to)
            .GroupBy(e => new { e.EmployeeId, e.WorkDate })
            .Select(g => new { g.Key.EmployeeId, g.Key.WorkDate, Count = g.Count() })
            .ToListAsync(cancellation);

        return new RangeData(
            employees,
            // (EmployeeId, WorkDate) is unique, but stay safe if old data has duplicates
            attendance.GroupBy(a => (a.EmployeeId, a.WorkDate)).ToDictionary(g => g.Key, g => g.MinBy(a => a.CheckIn)!),
            sessions.GroupBy(s => (s.EmployeeId, s.WorkDate)).ToDictionary(g => g.Key, g => g.ToList()),
            userExits.ToDictionary(x => (x.EmployeeId, x.WorkDate), x => x.Count));
    }

    private sealed record EmployeeRow(long Id, string FullName, string? Department);
    private sealed record AttendanceRow(long EmployeeId, DateOnly WorkDate, DateTime CheckIn, DateTime? CheckOut, string? Place);
    private sealed record SessionRow(long EmployeeId, DateOnly WorkDate, ActivityState State, DateTime StartedAt, DateTime EndedAt)
    {
        public double Seconds => (EndedAt - StartedAt).TotalSeconds;
    }

    private sealed record RangeData(
        List<EmployeeRow> Employees,
        Dictionary<(long, DateOnly), AttendanceRow> Attendance,
        Dictionary<(long, DateOnly), List<SessionRow>> Sessions,
        Dictionary<(long, DateOnly), int> UserExits)
    {
        public AttendanceRow? AttendanceOf(long employeeId, DateOnly day) => Attendance.GetValueOrDefault((employeeId, day));
        public List<SessionRow> SessionsOf(long employeeId, DateOnly day) => Sessions.GetValueOrDefault((employeeId, day), []);
    }
}
