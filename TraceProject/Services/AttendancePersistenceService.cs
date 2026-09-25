using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Models;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services;

/// <summary>
/// Phase 5 + 6 implementation of attendance persistence with deduplication.
/// Creates Attendance records from face recognition results with error handling.
/// Phase 6: Prevents duplicate records within cooldown window.
/// </summary>
public class AttendancePersistenceService : IAttendancePersistenceService
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AttendancePersistenceService> logger;
    private readonly int cooldownMinutes;

    public AttendancePersistenceService(
        ApplicationDbContext dbContext,
        ILogger<AttendancePersistenceService> logger,
        IOptions<AttendanceOptions>? attendanceOptions = null)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.cooldownMinutes = attendanceOptions?.Value?.CooldownMinutes ?? 15;
    }

    /// <summary>
    /// Persist recognition result as attendance if all conditions are met.
    /// Conditions:
    /// 1. Recognition was a match (IsMatch = true)
    /// 2. Employee is active (IsActive = true)
    /// 3. No recent Attendance record within cooldown window (Phase 6)
    /// 4. No database errors
    /// </summary>
    public async Task<Attendance?> PersistRecognitionResultAsync(
        RecognitionResult result,
        long? cameraId,
        long? locationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Condition 1: Skip unknown faces
            if (!result.IsMatch)
            {
                logger.LogDebug("Skipping Attendance: Unknown face (no employee match)");
                return null;
            }

            // Condition 2: Validate EmployeeId
            if (!result.EmployeeId.HasValue)
            {
                logger.LogWarning("Skipping Attendance: RecognitionResult has no EmployeeId");
                return null;
            }

            // Condition 3: Verify employee exists and is active
            var employee = await dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == result.EmployeeId.Value, cancellationToken);

            if (employee is null)
            {
                logger.LogWarning("Skipping Attendance: Employee {EmployeeId} not found", result.EmployeeId);
                return null;
            }

            if (!employee.IsActive)
            {
                logger.LogInformation("Skipping Attendance: Employee {EmployeeName} is inactive", employee.FullName);
                return null;
            }

            // Condition 4: LocationId required
            if (!locationId.HasValue)
            {
                logger.LogWarning("Skipping Attendance: No LocationId provided");
                return null;
            }

            // Verify location exists
            var location = await dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == locationId.Value, cancellationToken);

            if (location is null)
            {
                logger.LogWarning("Skipping Attendance: Location {LocationId} not found", locationId);
                return null;
            }

            // Phase 6: Check cooldown - skip if recent attendance within window
            var cooldownStart = DateTime.UtcNow.AddMinutes(-cooldownMinutes);
            var recentAttendance = await dbContext.Attendances
                .AsNoTracking()
                .Where(a => a.EmployeeId == result.EmployeeId.Value &&
                            a.CheckIn >= cooldownStart)
                .OrderByDescending(a => a.CheckIn)
                .FirstOrDefaultAsync(cancellationToken);

            if (recentAttendance != null)
            {
                var minutesAgo = (DateTime.UtcNow - recentAttendance.CheckIn).TotalMinutes;
                logger.LogInformation(
                    "Skipping Attendance (cooldown): Employee {EmployeeName} checked in {MinutesAgo:F1} minutes ago (cooldown: {CooldownMinutes}m)",
                    employee.FullName,
                    minutesAgo,
                    cooldownMinutes);
                return null;
            }

            // Create Attendance record
            var attendance = new Attendance
            {
                EmployeeId = result.EmployeeId.Value,
                LocationId = locationId.Value,
                CameraId = cameraId,
                CheckIn = DateTime.UtcNow,
                Confidence = result.Confidence,
                IsAllowed = employee.IsActive, // Allow if employee is active
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Attendances.Add(attendance);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Attendance created: Employee {EmployeeName} at {LocationName} (confidence: {Confidence:P})",
                employee.FullName,
                location.Place,
                result.Confidence);

            return attendance;
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "Database error persisting attendance (not crashing pipeline)");
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Attendance persistence cancelled");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error persisting attendance (not crashing pipeline)");
            return null;
        }
    }
}
