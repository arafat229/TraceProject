using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Models;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<DashboardController> logger;

    public DashboardController(ApplicationDbContext dbContext, ILogger<DashboardController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellation)
    {
        try
        {
            var totalEmployees = await dbContext.Employees.CountAsync(cancellation);
            var activeEmployees = await dbContext.Employees.CountAsync(e => e.IsActive, cancellation);
            var inactiveEmployees = totalEmployees - activeEmployees;

            var totalDepartments = await dbContext.Departments.CountAsync(cancellation);
            var activeDepartments = await dbContext.Departments.CountAsync(d => d.IsActive, cancellation);

            var totalCameras = await dbContext.Cameras.CountAsync(cancellation);
            var activeCameras = await dbContext.Cameras.CountAsync(c => c.IsActive, cancellation);

            var totalUsers = await dbContext.Users.CountAsync(cancellation);
            var activeUsers = await dbContext.Users.CountAsync(u => u.IsActive, cancellation);

            var todayAttendance = await dbContext.Attendances
                .CountAsync(a => a.CheckIn.Date == DateTime.UtcNow.Date, cancellation);

            var totalLocations = await dbContext.Locations.CountAsync(cancellation);

            var unknownFaces = await dbContext.UnknownFaces.CountAsync(cancellation);

            var summary = new
            {
                Employees = new
                {
                    Total = totalEmployees,
                    Active = activeEmployees,
                    Inactive = inactiveEmployees
                },
                Departments = new
                {
                    Total = totalDepartments,
                    Active = activeDepartments
                },
                Cameras = new
                {
                    Total = totalCameras,
                    Active = activeCameras
                },
                Users = new
                {
                    Total = totalUsers,
                    Active = activeUsers
                },
                Attendance = new
                {
                    TodayCount = todayAttendance
                },
                Locations = totalLocations,
                UnknownFaces = unknownFaces,
                Timestamp = DateTime.UtcNow
            };

            return Ok(CallResult.Ok(data: summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDashboardSummary), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("attendance-trends")]
    public async Task<IActionResult> GetAttendanceTrends([FromQuery] int days = 30, CancellationToken cancellation = default)
    {
        try
        {
            if (days <= 0 || days > 365)
                return BadRequest(CallResult.Error("Days must be between 1 and 365"));

            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            var trends = await dbContext.Attendances
                .Where(a => a.CheckIn >= startDate && a.CheckIn <= endDate)
                .GroupBy(a => a.CheckIn.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalCheckIns = g.Count(),
                    CheckOuts = g.Count(a => a.CheckOut.HasValue),
                    AllowedCount = g.Count(a => a.IsAllowed)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: trends));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetAttendanceTrends), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("department-statistics")]
    public async Task<IActionResult> GetDepartmentStatistics(CancellationToken cancellation)
    {
        try
        {
            var statistics = await dbContext.Departments
                .Select(d => new
                {
                    DepartmentId = d.Id,
                    DepartmentName = d.Name,
                    EmployeeCount = d.Employees.Count,
                    ActiveEmployees = d.Employees.Count(e => e.IsActive),
                    IsActive = d.IsActive
                })
                .OrderByDescending(x => x.EmployeeCount)
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: statistics));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDepartmentStatistics), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("employee-attendance-summary")]
    public async Task<IActionResult> GetEmployeeAttendanceSummary([FromQuery] int days = 30, CancellationToken cancellation = default)
    {
        try
        {
            if (days <= 0 || days > 365)
                return BadRequest(CallResult.Error("Days must be between 1 and 365"));

            var startDate = DateTime.UtcNow.AddDays(-days);

            var summary = await dbContext.Employees
                .Select(e => new
                {
                    EmployeeId = e.Id,
                    EmployeeName = e.FullName,
                    Department = e.Department.Name,
                    TotalCheckIns = e.Attendances.Count(a => a.CheckIn >= startDate),
                    CheckOuts = e.Attendances.Count(a => a.CheckOut.HasValue && a.CheckIn >= startDate),
                    AllowedCount = e.Attendances.Count(a => a.IsAllowed && a.CheckIn >= startDate),
                    DeniedCount = e.Attendances.Count(a => !a.IsAllowed && a.CheckIn >= startDate),
                    AverageConfidence = e.Attendances.Where(a => a.CheckIn >= startDate).Average(a => a.Confidence)
                })
                .Where(x => x.TotalCheckIns > 0)
                .OrderByDescending(x => x.TotalCheckIns)
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeeAttendanceSummary), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("location-activity")]
    public async Task<IActionResult> GetLocationActivity([FromQuery] int days = 30, CancellationToken cancellation = default)
    {
        try
        {
            if (days <= 0 || days > 365)
                return BadRequest(CallResult.Error("Days must be between 1 and 365"));

            var startDate = DateTime.UtcNow.AddDays(-days);

            var activity = await dbContext.Locations
                .Select(l => new
                {
                    LocationId = l.Id,
                    LocationName = l.Place,
                    TotalCheckIns = dbContext.Attendances.Count(a => a.LocationId == l.Id && a.CheckIn >= startDate),
                    UnknownFaceCount = dbContext.UnknownFaceLocations.Count(ufl => ufl.LocationId == l.Id),
                    CameraCount = dbContext.Cameras.Count(c => c.Location == l.Place)
                })
                .OrderByDescending(x => x.TotalCheckIns)
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: activity));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLocationActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("activity-status")]
    public async Task<IActionResult> GetActivityStatus(CancellationToken cancellation)
    {
        try
        {
            var activeEmployees = await dbContext.Activities
                .Where(a => a.IsActive)
                .Select(a => new
                {
                    EmployeeId = a.EmployeeId,
                    CurrentApp = a.CurrentApp,
                    ActiveTime = a.ActiveTime,
                    IsActive = a.IsActive
                })
                .ToListAsync(cancellation);

            var summary = new
            {
                TotalActiveEmployees = activeEmployees.Count,
                ActiveDetails = activeEmployees,
                Timestamp = DateTime.UtcNow
            };

            return Ok(CallResult.Ok(data: summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetActivityStatus), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("security-alerts")]
    public async Task<IActionResult> GetSecurityAlerts([FromQuery] int days = 7, CancellationToken cancellation = default)
    {
        try
        {
            if (days <= 0 || days > 90)
                return BadRequest(CallResult.Error("Days must be between 1 and 90"));

            var startDate = DateTime.UtcNow.AddDays(-days);

            var deniedAccess = await dbContext.Attendances
                .Where(a => !a.IsAllowed && a.CheckIn >= startDate)
                .Select(a => new
                {
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee.FullName,
                    LocationId = a.LocationId,
                    LocationName = a.Location.Place,
                    CheckInTime = a.CheckIn,
                    Confidence = a.Confidence
                })
                .OrderByDescending(x => x.CheckInTime)
                .ToListAsync(cancellation);

            var unknownFacesDetected = await dbContext.UnknownFaces
                .Select(u => new
                {
                    UnknownFaceId = u.Id,
                    LocationCount = u.UnknownFaceLocations.Count,
                    Locations = u.UnknownFaceLocations.Select(ufl => ufl.Location.Place).ToList()
                })
                .ToListAsync(cancellation);

            var alerts = new
            {
                DeniedAccessCount = deniedAccess.Count,
                DeniedAccessDetails = deniedAccess,
                UnknownFacesCount = unknownFacesDetected.Count,
                UnknownFacesDetails = unknownFacesDetected,
                PeriodDays = days,
                Timestamp = DateTime.UtcNow
            };

            return Ok(CallResult.Ok(data: alerts));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetSecurityAlerts), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("user-activity-log")]
    public async Task<IActionResult> GetUserActivityLog([FromQuery] int days = 30, CancellationToken cancellation = default)
    {
        try
        {
            if (days <= 0 || days > 365)
                return BadRequest(CallResult.Error("Days must be between 1 and 365"));

            var startDate = DateTime.UtcNow.AddDays(-days);

            var log = await dbContext.Users
                .Where(u => u.UpdatedAt >= startDate || u.CreatedAt >= startDate)
                .Select(u => new
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    LastUpdated = u.UpdatedAt
                })
                .OrderByDescending(x => x.LastUpdated)
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: log));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUserActivityLog), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
