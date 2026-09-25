using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services;

/// <summary>
/// Turns recognitions into attendance: one Attendance row per employee per (server-local) day.
/// The first sighting creates it (CheckIn); later sightings move CheckOut forward ("last seen"),
/// at most once per SystemSettings.AttendanceUpdateIntervalMinutes. Broadcasts
/// EmployeeCheckedIn / AttendanceUpdated on the NotificationHub.
/// </summary>
public class AttendancePersistenceService : IAttendancePersistenceService
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AttendancePersistenceService> logger;
    private readonly IHubContext<NotificationHub> hubContext;
    private readonly SystemSettingsService settings;
    private readonly CameraOptions cameraOptions;

    public AttendancePersistenceService(
        ApplicationDbContext dbContext,
        ILogger<AttendancePersistenceService> logger,
        IHubContext<NotificationHub> hubContext,
        SystemSettingsService settings,
        IOptions<CameraOptions> cameraOptions)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
        this.settings = settings;
        this.cameraOptions = cameraOptions.Value;
    }

    public async Task<Attendance?> PersistRecognitionResultAsync(
        RecognitionResult result,
        long? cameraId,
        long? locationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!result.IsMatch || !result.EmployeeId.HasValue)
                return null;

            var employee = await dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == result.EmployeeId.Value, cancellationToken);

            if (employee is null)
            {
                logger.LogWarning("Skipping attendance: employee {EmployeeId} not found", result.EmployeeId);
                return null;
            }

            if (!employee.IsActive)
            {
                logger.LogInformation("Skipping attendance: employee {EmployeeName} is inactive", employee.FullName);
                return null;
            }

            var location = locationId.HasValue
                ? await dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId.Value, cancellationToken)
                : null;
            location ??= await CameraLocationResolver.ResolveAsync(dbContext, cameraOptions, cancellationToken);

            if (location is null)
            {
                logger.LogWarning("Skipping attendance for {EmployeeName}: no location exists. Create one on the Settings page.", employee.FullName);
                return null;
            }

            var now = DateTime.UtcNow;
            var dayStartUtc = DateTime.Now.Date.ToUniversalTime();

            var today = await dbContext.Attendances
                .Where(a => a.EmployeeId == employee.Id && a.CheckIn >= dayStartUtc)
                .OrderByDescending(a => a.CheckIn)
                .FirstOrDefaultAsync(cancellationToken);

            if (today is null)
            {
                var attendance = new Attendance
                {
                    EmployeeId = employee.Id,
                    LocationId = location.Id,
                    CameraId = cameraId,
                    CheckIn = now,
                    Confidence = result.Confidence,
                    IsAllowed = true,
                    CreatedAt = now
                };

                dbContext.Attendances.Add(attendance);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Check-in: {EmployeeName} at {LocationName} (similarity {Confidence:F3})",
                    employee.FullName, location.Place, result.Confidence);

                await hubContext.Clients.All.SendAsync("EmployeeCheckedIn", new
                {
                    attendanceId = attendance.Id,
                    employeeId = employee.Id,
                    employeeName = employee.FullName,
                    locationName = location.Place,
                    checkIn = attendance.CheckIn,
                    confidence = attendance.Confidence
                }, cancellationToken);

                return attendance;
            }

            // Already checked in today: move "last seen" forward, not on every frame
            var lastSeen = today.CheckOut ?? today.CheckIn;
            var interval = TimeSpan.FromMinutes(Math.Max(0, settings.Current.AttendanceUpdateIntervalMinutes));
            if (now - lastSeen < interval)
                return null;

            today.CheckOut = now;
            today.Confidence = Math.Max(today.Confidence, result.Confidence);
            today.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Last seen updated: {EmployeeName}", employee.FullName);

            await hubContext.Clients.All.SendAsync("AttendanceUpdated", new
            {
                attendanceId = today.Id,
                employeeId = employee.Id,
                employeeName = employee.FullName,
                checkOut = today.CheckOut
            }, cancellationToken);

            return today;
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "Database error persisting attendance (not crashing pipeline)");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error persisting attendance (not crashing pipeline)");
            return null;
        }
    }
}
