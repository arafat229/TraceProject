using Microsoft.AspNetCore.SignalR;
using TraceProject.Hubs;
using TraceProject.Services;

namespace TraceProject.Workers;

/// <summary>
/// Keeps every employee's combined presence status and broadcasts PresenceChanged when it changes.
/// Recomputes right away when poked (camera check-in, computer status change) and every
/// 15 s otherwise, which also covers manual edits and the day rollover at midnight.
/// Singleton: scoped services may call <see cref="Poke"/>.
/// </summary>
public class PresenceMonitor(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hubContext,
    ILogger<PresenceMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly Dictionary<long, string> lastStatus = new();
    private bool initialized;

    /// <summary>Something that can change a status happened: recompute now instead of at the next tick.</summary>
    public void Poke()
    {
        try
        {
            if (wake.CurrentCount == 0)
                wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already poked
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecomputeAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Presence recompute failed");
            }

            try
            {
                await wake.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RecomputeAsync(CancellationToken cancellation)
    {
        using var scope = scopeFactory.CreateScope();
        var presence = await scope.ServiceProvider.GetRequiredService<PresenceService>().GetLiveAsync(cancellation);

        foreach (var p in presence)
        {
            var previous = lastStatus.GetValueOrDefault(p.EmployeeId);
            lastStatus[p.EmployeeId] = p.Status;

            // The first pass only learns the current state; announcing it would flood the dashboards
            if (!initialized || previous == p.Status)
                continue;

            logger.LogInformation("Presence: {employee} {previous} -> {status}", p.EmployeeName, previous ?? "(new)", p.Status);
            await hubContext.Clients.All.SendAsync("PresenceChanged", new
            {
                p.EmployeeId,
                p.EmployeeName,
                p.Status,
                PreviousStatus = previous,
                p.ComputerState,
                p.CheckIn,
                At = DateTime.UtcNow,
            }, cancellation);
        }

        // Deactivated or deleted employees
        foreach (var gone in lastStatus.Keys.Except(presence.Select(p => p.EmployeeId)).ToList())
            lastStatus.Remove(gone);

        initialized = true;
    }
}
