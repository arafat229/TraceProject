using HlkHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Helpers;
using TraceProject.Models;

namespace TraceProject.Data;

public class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<BasicUserManager<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedData>>();
        var cameraOptions = scope.ServiceProvider.GetRequiredService<IOptions<CameraOptions>>().Value;
        var recognitionOptions = scope.ServiceProvider.GetRequiredService<IOptions<FaceRecognitionOptions>>().Value;

        try
        {
            if (!db.Users.Any())
            {
                var user = new User
                {
                    Email = "admin@trace.com",
                    FullName = "Admin User",
                    IsActive = true,
                    Role = AppConstants.AdministratorRole,
                };
                userManager.SetUserPassword(ref user, "P@ssw0rd");
                db.Add(user);
                await db.SaveChangesAsync();
            }

            // Employees need a department
            if (!await db.Departments.AnyAsync())
            {
                db.Departments.Add(new Department { Name = "General", IsActive = true, CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded default department 'General'");
            }

            // Attendance and unknown faces are filed under a location
            if (!await db.Locations.AnyAsync())
            {
                db.Locations.Add(new Location { Place = "Main Entrance", IsActive = true });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded default location 'Main Entrance'");
            }

            await EnsureLiveCameraRowAsync(db, cameraOptions, logger);

            if (!await db.SystemSettings.AnyAsync())
            {
                db.SystemSettings.Add(new SystemSetting { RecognitionThreshold = recognitionOptions.SimilarityThreshold });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded default system settings");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed the database");
        }
    }

    /// <summary>
    /// The live camera is identified by Camera:Id; make sure a Cameras row with that id exists so
    /// it shows up on the Cameras page and can be given a location.
    /// </summary>
    private static async Task EnsureLiveCameraRowAsync(ApplicationDbContext db, CameraOptions cameraOptions, ILogger logger)
    {
        if (await db.Cameras.AnyAsync(c => c.Id == cameraOptions.Id))
            return;

        var location = await db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Id).FirstOrDefaultAsync();

        db.Cameras.Add(new Camera
        {
            Id = cameraOptions.Id,
            CameraName = await db.Cameras.AnyAsync(c => c.CameraName == cameraOptions.Name)
                ? $"{cameraOptions.Name} ({cameraOptions.Id})"
                : cameraOptions.Name,
            IPAddress = cameraOptions.IpAddress,
            Location = location?.Place ?? "",
            LocationId = location?.Id,
            Transport = cameraOptions.Transport,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Inserting an explicit Id doesn't advance the identity sequence; move it past the max Id
        // so cameras added later don't collide.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"Cameras\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Cameras\"))");

        logger.LogInformation("Seeded Cameras row {id} for the live camera '{name}'", cameraOptions.Id, cameraOptions.Name);
    }
}
