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
/// Turns recognitions into attendance, per employee and per day:
/// - check-in: the first time the employee is recognized that day creates their one Attendance
///   row (unique on EmployeeId + WorkDate) and broadcasts EmployeeCheckedIn;
/// - check-out: every later recognition that day moves the employee's own CheckOut to that
///   moment ("last seen"), at most once per CheckOutUpdateInterval, and broadcasts AttendanceUpdated.
/// </summary>
public class AttendancePersistenceService : IAttendancePersistenceService
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AttendancePersistenceService> logger;
    private readonly IHubContext<NotificationHub> hubContext;
    private readonly CameraOptions cameraOptions;

    // Recognition runs several times per second; don't rewrite the check-out on every frame
    private static readonly TimeSpan CheckOutUpdateInterval = TimeSpan.FromMinutes(1);

    public AttendancePersistenceService(
        ApplicationDbContext dbContext,
        ILogger<AttendancePersistenceService> logger,
        IHubContext<NotificationHub> hubContext,
        IOptions<CameraOptions> cameraOptions)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
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

            var today = WorkDay.Today;
            var todaysRow = await dbContext.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == result.EmployeeId.Value && a.WorkDate == today, cancellationToken);
            if (todaysRow is not null)
                return await UpdateCheckOutAsync(todaysRow, result, cancellationToken);

            var employee = await dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == result.EmployeeId.Value, cancellationToken);

            if (employee is null)
            {
                logger.LogWarning("Skipping check-in: employee {EmployeeId} not found", result.EmployeeId);
                return null;
            }

            if (!employee.IsActive)
            {
                logger.LogInformation("Skipping check-in: employee {EmployeeName} is inactive", employee.FullName);
                return null;
            }

            var location = locationId.HasValue
                ? await dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId.Value, cancellationToken)
                : null;
            location ??= await CameraLocationResolver.ResolveAsync(dbContext, cameraOptions, cancellationToken);

            if (location is null)
            {
                logger.LogWarning("Skipping check-in for {EmployeeName}: no location exists. Create one on the Settings page.", employee.FullName);
                return null;
            }

            var now = DateTime.UtcNow;
            var attendance = new Attendance
            {
                EmployeeId = employee.Id,
                LocationId = location.Id,
                CameraId = cameraId,
                WorkDate = today,
                CheckIn = now,
                Confidence = result.Confidence,
                IsAllowed = true,
                CreatedAt = now
            };

            dbContext.Attendances.Add(attendance);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Checked in concurrently (unique EmployeeId + WorkDate); anything else is a real error
                if (await dbContext.Attendances.AsNoTracking()
                        .AnyAsync(a => a.EmployeeId == employee.Id && a.WorkDate == today, cancellationToken))
                    return null;
                throw;
            }

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

    /// <summary>
    /// The employee is already checked in today: their check-out becomes this sighting
    /// ("last seen"), written at most once per CheckOutUpdateInterval.
    /// </summary>
    private async Task<Attendance?> UpdateCheckOutAsync(Attendance row, RecognitionResult result, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (now - (row.CheckOut ?? row.CheckIn) < CheckOutUpdateInterval)
            return null;

        row.CheckOut = now;
        row.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var employeeName = result.MatchedEmployeeName
            ?? await dbContext.Employees.Where(e => e.Id == row.EmployeeId).Select(e => e.FullName).FirstOrDefaultAsync(cancellationToken);

        logger.LogInformation("Check-out: {EmployeeName} seen again at {CheckOut:HH:mm:ss} (similarity {Confidence:F3})", employeeName, now, result.Confidence);

        await hubContext.Clients.All.SendAsync("AttendanceUpdated", new
        {
            attendanceId = row.Id,
            employeeId = row.EmployeeId,
            employeeName,
            checkIn = row.CheckIn,
            checkOut = row.CheckOut
        }, cancellationToken);

        return row;
    }
}
