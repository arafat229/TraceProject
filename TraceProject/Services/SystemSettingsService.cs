using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Models;

namespace TraceProject.Services;

/// <summary>
/// In-memory copy of the single SystemSettings row, so hot paths (recognition, attendance)
/// can read settings without a database query. Loaded at startup, updated by SettingsController.
/// </summary>
public class SystemSettingsService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<SystemSettingsService> logger;
    private volatile SystemSetting current = new();

    public SystemSettingsService(IServiceScopeFactory scopeFactory, ILogger<SystemSettingsService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public SystemSetting Current => current;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.SystemSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            current = row;
            logger.LogInformation("System settings loaded (recognition threshold {threshold})", row.RecognitionThreshold);
        }
    }

    public async Task<SystemSetting> UpdateAsync(Action<SystemSetting> apply, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new SystemSetting();
            db.SystemSettings.Add(row);
        }

        apply(row);
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        current = row;
        return row;
    }
}
