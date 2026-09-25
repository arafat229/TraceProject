using Microsoft.EntityFrameworkCore;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Models;

namespace TraceProject.Services;

/// <summary>
/// Decides which Location records from the live camera are filed under:
/// Camera:LocationId (appsettings) → the Cameras row's LocationId → the first active location.
/// </summary>
public static class CameraLocationResolver
{
    public static async Task<Location?> ResolveAsync(ApplicationDbContext db, CameraOptions cameraOptions, CancellationToken cancellationToken = default)
    {
        if (cameraOptions.LocationId is long configured)
        {
            var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == configured, cancellationToken);
            if (location is not null)
                return location;
        }

        var cameraLocationId = await db.Cameras.AsNoTracking()
            .Where(c => c.Id == cameraOptions.Id)
            .Select(c => c.LocationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (cameraLocationId is long fromCamera)
        {
            var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == fromCamera, cancellationToken);
            if (location is not null)
                return location;
        }

        return await db.Locations.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
