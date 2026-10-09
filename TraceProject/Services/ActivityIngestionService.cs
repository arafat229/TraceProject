using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Workers;

namespace TraceProject.Services;

/// <summary>
/// Stores reports from the desktop agent: upserts activity sessions, updates the employee's
/// live <see cref="Activity"/> row and broadcasts ActivityUpdated when the status changes.
/// </summary>
public class ActivityIngestionService(
    ApplicationDbContext dbContext,
    IHubContext<NotificationHub> hubContext,
    PresenceMonitor presenceMonitor,
    ILogger<ActivityIngestionService> logger)
{
    public const int MaxSessionsPerReport = 500;

    // Agent and server clocks can disagree a little; anything further in the future is dropped
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(2);

    public async Task<int> IngestAsync(long deviceId, long employeeId, AgentReportDto report, CancellationToken cancellation)
    {
        var now = DateTime.UtcNow;

        var device = await dbContext.Devices.FirstAsync(d => d.Id == deviceId, cancellation);
        device.LastSeenAt = now;
        if (!string.IsNullOrWhiteSpace(report.AgentVersion))
            device.AgentVersion = Truncate(report.AgentVersion, 50);

        var sessions = (report.Sessions ?? [])
            .Where(s => s.ClientId != Guid.Empty && s.State != ActivityState.Offline && Enum.IsDefined(s.State))
            .Select(s => s with { StartedAt = s.StartedAt.ToUniversalTime(), EndedAt = s.EndedAt.ToUniversalTime() })
            .Where(s => s.EndedAt >= s.StartedAt && s.EndedAt <= now + AllowedClockSkew)
            // The open session can appear twice in one batch (e.g. after a retry); keep its latest copy
            .GroupBy(s => s.ClientId)
            .Select(g => g.MaxBy(s => s.EndedAt)!)
            .ToList();

        if (sessions.Count > 0)
        {
            var clientIds = sessions.Select(s => s.ClientId).ToList();
            var existing = await dbContext.ActivitySessions
                .Where(s => s.DeviceId == deviceId && clientIds.Contains(s.ClientId))
                .ToDictionaryAsync(s => s.ClientId, cancellation);

            foreach (var s in sessions)
            {
                if (existing.TryGetValue(s.ClientId, out var row))
                {
                    // The agent is authoritative and reports in order. EndedAt can move back: when the
                    // user goes idle, the agent ends the active session at the last input, not at "now".
                    row.EndedAt = s.EndedAt;
                    row.WindowTitle = Truncate(s.WindowTitle, 512);
                }
                else
                {
                    dbContext.ActivitySessions.Add(new ActivitySession
                    {
                        DeviceId = deviceId,
                        EmployeeId = employeeId,
                        ClientId = s.ClientId,
                        WorkDate = WorkDay.Of(s.StartedAt),
                        State = s.State,
                        AppName = Truncate(s.AppName, 128),
                        WindowTitle = Truncate(s.WindowTitle, 512),
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                    });
                }
            }
        }

        var newEvents = await AddEventsAsync(deviceId, employeeId, report.Events, now, cancellation);

        // The newest event says whether the agent is stopping (closed, shutdown, sleep). Older events
        // queued while offline are history only: a later Started/Resumed means it is running again.
        var latestEvent = (report.Events ?? [])
            .Where(e => Enum.IsDefined(e.Kind) && e.Kind != AgentEventKind.ConnectionLost)
            .MaxBy(e => e.At);
        AgentEventKind? stopReason = latestEvent?.Kind is AgentEventKind.UserExit or AgentEventKind.Shutdown or AgentEventKind.Sleep
            ? latestEvent.Kind
            : null;

        var state = stopReason is not null ? ActivityState.Offline
            : report.CurrentState is ActivityState.Active or ActivityState.Idle or ActivityState.Locked ? report.CurrentState
            : ActivityState.Active;
        var currentApp = state == ActivityState.Offline ? null : Truncate(report.CurrentApp, 128);

        var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.EmployeeId == employeeId, cancellation);
        if (activity is null)
        {
            activity = new Activity { EmployeeId = employeeId, ActiveTime = now, OffTime = now, LockTime = now };
            dbContext.Activities.Add(activity);
        }

        var changed = activity.State != state || activity.CurrentApp != currentApp || activity.OfflineReason != stopReason;

        if (state == ActivityState.Active)
            activity.ActiveTime = now;
        else if (activity.State == ActivityState.Active)
            activity.OffTime = now;
        if (state == ActivityState.Locked && activity.State != ActivityState.Locked)
            activity.LockTime = now;

        activity.State = state;
        activity.IsActive = state == ActivityState.Active;
        activity.CurrentApp = currentApp;
        activity.OfflineReason = stopReason;
        activity.LastSeenAt = now;

        await dbContext.SaveChangesAsync(cancellation);

        if (changed)
        {
            await BroadcastAsync(hubContext, logger, activity, deviceId);
            presenceMonitor.Poke();
        }

        // Closing the agent stops the monitoring: tell the dashboards (not for exits queued long ago)
        var exit = newEvents.Where(e => e.Kind == AgentEventKind.UserExit && now - e.At < TimeSpan.FromMinutes(10)).MaxBy(e => e.At);
        if (exit is not null)
            await BroadcastAgentStoppedAsync(device, exit, cancellation);

        return sessions.Count;
    }

    /// <summary>Stores the report's lifecycle events not seen before; returns the new ones.</summary>
    private async Task<List<AgentEvent>> AddEventsAsync(long deviceId, long employeeId, List<AgentEventDto>? events, DateTime now, CancellationToken cancellation)
    {
        var incoming = (events ?? [])
            .Where(e => e.ClientId != Guid.Empty && Enum.IsDefined(e.Kind) && e.Kind != AgentEventKind.ConnectionLost)
            .Select(e => e with { At = e.At.ToUniversalTime() })
            .Where(e => e.At <= now + AllowedClockSkew)
            .DistinctBy(e => e.ClientId)
            .ToList();
        if (incoming.Count == 0)
            return [];

        var clientIds = incoming.Select(e => (Guid?)e.ClientId).ToList();
        var known = (await dbContext.AgentEvents
                .Where(e => e.DeviceId == deviceId && clientIds.Contains(e.ClientId))
                .Select(e => e.ClientId)
                .ToListAsync(cancellation))
            .ToHashSet();

        var added = incoming
            .Where(e => !known.Contains(e.ClientId))
            .Select(e => new AgentEvent
            {
                DeviceId = deviceId,
                EmployeeId = employeeId,
                ClientId = e.ClientId,
                Kind = e.Kind,
                At = e.At,
                WorkDate = WorkDay.Of(e.At),
            })
            .ToList();
        dbContext.AgentEvents.AddRange(added);
        return added;
    }

    private async Task BroadcastAgentStoppedAsync(Device device, AgentEvent exit, CancellationToken cancellation)
    {
        try
        {
            var employeeName = await dbContext.Employees.Where(e => e.Id == device.EmployeeId).Select(e => e.FullName).FirstOrDefaultAsync(cancellation);
            logger.LogInformation("{employee} closed TraceAgent on {device}", employeeName, device.Name);
            await hubContext.Clients.All.SendAsync("AgentStopped", new
            {
                device.EmployeeId,
                EmployeeName = employeeName,
                DeviceId = device.Id,
                DeviceName = device.Name,
                Reason = exit.Kind.ToString(),
                exit.At,
            }, cancellation);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast AgentStopped for device {deviceId}", device.Id);
        }
    }

    /// <summary>Sends ActivityUpdated to every dashboard; never throws.</summary>
    public static async Task BroadcastAsync(IHubContext<NotificationHub> hubContext, ILogger logger, Activity activity, long? deviceId)
    {
        try
        {
            await hubContext.Clients.All.SendAsync("ActivityUpdated", new
            {
                activity.EmployeeId,
                DeviceId = deviceId,
                State = activity.State.ToString(),
                activity.IsActive,
                activity.CurrentApp,
                activity.ActiveTime,
                activity.OffTime,
                activity.LockTime,
                activity.LastSeenAt,
                OfflineReason = activity.OfflineReason?.ToString(),
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast ActivityUpdated for employee {employeeId}", activity.EmployeeId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
