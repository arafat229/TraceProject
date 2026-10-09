using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services;

namespace TraceProject.Workers;

/// <summary>
/// Marks an employee's live activity Offline when their agent stops reporting
/// (laptop closed, shut down or off the network), so the dashboard never shows a stale "Active".
/// </summary>
public class DeviceStatusWatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hubContext,
    IOptions<ActivityMonitoringOptions> options,
    ILogger<DeviceStatusWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await MarkStaleOfflineAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Device status check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task MarkStaleOfflineAsync(CancellationToken cancellation)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-Math.Max(30, options.Value.OfflineAfterSeconds));

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var stale = await dbContext.Activities
            .Where(a => a.State != ActivityState.Offline && a.LastSeenAt != null && a.LastSeenAt < cutoff)
            .ToListAsync(cancellation);
        if (stale.Count == 0)
            return;

        foreach (var activity in stale)
        {
            if (activity.State == ActivityState.Active)
                activity.OffTime = activity.LastSeenAt!.Value;
            activity.State = ActivityState.Offline;
            activity.IsActive = false;
        }
        await dbContext.SaveChangesAsync(cancellation);

        foreach (var activity in stale)
        {
            logger.LogInformation("Employee {employeeId} computer went offline (last report {lastSeen:u})", activity.EmployeeId, activity.LastSeenAt);
            await ActivityIngestionService.BroadcastAsync(hubContext, logger, activity, deviceId: null);
        }
    }
}
