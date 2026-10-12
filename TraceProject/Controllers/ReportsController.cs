using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Models;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<ReportsController> logger;

    public ReportsController(ApplicationDbContext dbContext, ILogger<ReportsController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> GenerateReport(AddReportDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId, cancellation);

            if (employee is null) 
                return BadRequest(CallResult.Error("Employee not found"));

            if (!Enum.TryParse(dto.ReportType, true, out ReportType reportType))
                return BadRequest(CallResult.Error("Invalid report type. Allowed values: Daily, Weekly, Monthly"));

            var report = new Report
            {
                EmployeeId = dto.EmployeeId,
                ReportType = reportType,
                GeneratedAt = DateTime.UtcNow,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate
            };

            dbContext.Add(report);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new ReportDto(
                report.Id,
                report.EmployeeId,
                employee.FullName,
                report.ReportType.ToString(),
                report.GeneratedAt,
                report.StartDate,
                report.EndDate)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GenerateReport), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, 
        [FromQuery] long? employeeId = null, [FromQuery] string? reportType = null, CancellationToken cancellation = default)
    {
        try
        {
            var query = dbContext.Reports.AsNoTracking();

            if (employeeId.HasValue)
                query = query.Where(r => r.EmployeeId == employeeId);

            if (startDate.HasValue)
                query = query.Where(r => r.StartDate >= startDate);

            if (endDate.HasValue)
                query = query.Where(r => r.EndDate <= endDate);

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                if (Enum.TryParse(reportType, true, out ReportType type))
                    query = query.Where(r => r.ReportType == type);
            }

            var paginationDto = new HlkHelpers.PaginationDto { PageNumber = pageNumber, PageSize = pageSize };

            var list = await PagedList<Report>.CreateAsync(query, paginationDto,
                orderBy: x => x.GeneratedAt,
                selector: r => new { 
                    r.Id, 
                    r.EmployeeId, 
                    EmployeeName = dbContext.Employees.FirstOrDefault(e => e.Id == r.EmployeeId)!.FullName,
                    ReportType = r.ReportType.ToString(),
                    r.GeneratedAt, 
                    r.StartDate, 
                    r.EndDate 
                },
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetReports), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReportById(long id, CancellationToken cancellation)
    {
        try
        {
            var report = await dbContext.Reports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellation);

            if (report is null) 
                return NotFound(CallResult.Error("Report not found"));

            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == report.EmployeeId, cancellation);

            var dto = new ReportDto(
                report.Id,
                report.EmployeeId,
                employee?.FullName ?? "Unknown",
                report.ReportType.ToString(),
                report.GeneratedAt,
                report.StartDate,
                report.EndDate);

            return Ok(CallResult.Ok(data: dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetReportById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReport(long id, CancellationToken cancellation)
    {
        try
        {
            var report = await dbContext.Reports.FirstOrDefaultAsync(r => r.Id == id, cancellation);

            if (report is null) 
                return NotFound(CallResult.Error("Report not found"));

            dbContext.Remove(report);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Report deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteReport), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetEmployeeReports(long employeeId, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellation);

            if (employee is null) 
                return NotFound(CallResult.Error("Employee not found"));

            var reports = await dbContext.Reports
                .Where(r => r.EmployeeId == employeeId)
                .AsNoTracking()
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { 
                    r.Id, 
                    r.EmployeeId, 
                    Employee = employee.FullName,
                    ReportType = r.ReportType.ToString(),
                    r.GeneratedAt, 
                    r.StartDate, 
                    r.EndDate 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: reports));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeeReports), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("department/{departmentId}")]
    public async Task<IActionResult> GetDepartmentReports(long departmentId, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, cancellation);

            if (department is null) 
                return NotFound(CallResult.Error("Department not found"));

            var reports = await dbContext.Reports
                .Where(r => dbContext.Employees.Any(e => e.Id == r.EmployeeId && e.DepartmentId == departmentId))
                .AsNoTracking()
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { 
                    r.Id, 
                    r.EmployeeId, 
                    EmployeeName = dbContext.Employees.FirstOrDefault(e => e.Id == r.EmployeeId)!.FullName,
                    Department = department.Name,
                    ReportType = r.ReportType.ToString(),
                    r.GeneratedAt, 
                    r.StartDate, 
                    r.EndDate 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: reports));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDepartmentReports), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("by-date-range")]
    public async Task<IActionResult> GetReportsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellation)
    {
        try
        {
            if (startDate > endDate)
                return BadRequest(CallResult.Error("Start date must be before end date"));

            var reports = await dbContext.Reports
                .Where(r => r.StartDate >= startDate && r.EndDate <= endDate)
                .AsNoTracking()
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { 
                    r.Id, 
                    r.EmployeeId, 
                    EmployeeName = dbContext.Employees.FirstOrDefault(e => e.Id == r.EmployeeId)!.FullName,
                    ReportType = r.ReportType.ToString(),
                    r.GeneratedAt, 
                    r.StartDate, 
                    r.EndDate 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: reports));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetReportsByDateRange), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("by-type/{reportType}")]
    public async Task<IActionResult> GetReportsByType(string reportType, CancellationToken cancellation)
    {
        try
        {
            if (!Enum.TryParse(reportType, true, out ReportType type))
                return BadRequest(CallResult.Error("Invalid report type. Allowed values: Daily, Weekly, Monthly"));

            var reports = await dbContext.Reports
                .Where(r => r.ReportType == type)
                .AsNoTracking()
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { 
                    r.Id, 
                    r.EmployeeId, 
                    EmployeeName = dbContext.Employees.FirstOrDefault(e => e.Id == r.EmployeeId)!.FullName,
                    ReportType = r.ReportType.ToString(),
                    r.GeneratedAt, 
                    r.StartDate, 
                    r.EndDate 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: reports));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetReportsByType), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Phase 8: Get attendance statistics for an employee in a date range
    /// </summary>
    [HttpGet("attendance-stats/{employeeId}")]
    public async Task<IActionResult> GetAttendanceStats(
        long employeeId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellation)
    {
        try
        {
            // Verify employee exists
            var employee = await dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellation);

            if (employee is null)
                return NotFound(CallResult.Error("Employee not found"));

            // Query attendance records for the period
            var attendances = await dbContext.Attendances
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId &&
                            a.CheckIn >= startDate &&
                            a.CheckIn <= endDate)
                .ToListAsync(cancellation);

            if (attendances.Count == 0)
                return Ok(CallResult.Ok(data: new AttendanceStatsDto(
                    EmployeeId: employeeId,
                    EmployeeName: employee.FullName,
                    StartDate: startDate,
                    EndDate: endDate,
                    TotalCheckIns: 0,
                    UniqueAttendanceDays: 0,
                    AverageConfidence: 0.0,
                    FacesRecognized: 0,
                    FacesUnknown: 0)));

            // Calculate statistics
            int totalCheckIns = attendances.Count;
            int uniqueDays = attendances.Select(a => a.CheckIn.Date).Distinct().Count();
            double avgConfidence = attendances.Average(a => a.Confidence);

            // Recognition stats (we store recognized faces as Attendance, unknown as separate)
            // All Attendance records are recognized by definition
            int facesRecognized = totalCheckIns;
            int facesUnknown = 0;

            var stats = new AttendanceStatsDto(
                EmployeeId: employeeId,
                EmployeeName: employee.FullName,
                StartDate: startDate,
                EndDate: endDate,
                TotalCheckIns: totalCheckIns,
                UniqueAttendanceDays: uniqueDays,
                AverageConfidence: avgConfidence,
                FacesRecognized: facesRecognized,
                FacesUnknown: facesUnknown);

            return Ok(CallResult.Ok(data: stats));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetAttendanceStats), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Phase 8: Get daily attendance summary for organization
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> GetDailySummary(
        [FromQuery] DateTime? date = null,
        CancellationToken cancellation = default)
    {
        try
        {
            var queryDate = date?.Date ?? DateTime.UtcNow.Date;
            var startOfDay = queryDate.ToUniversalTime();
            var endOfDay = startOfDay.AddDays(1);

            // Get attendance for the day
            var dailyAttendance = await dbContext.Attendances
                .AsNoTracking()
                .Where(a => a.CheckIn >= startOfDay && a.CheckIn < endOfDay)
                .Include(a => a.Employee)
                .ToListAsync(cancellation);

            var summary = new
            {
                Date = queryDate,
                TotalCheckIns = dailyAttendance.Count,
                UniqueEmployees = dailyAttendance.Select(a => a.EmployeeId).Distinct().Count(),
                AverageConfidence = dailyAttendance.Any() ? dailyAttendance.Average(a => a.Confidence) : 0.0,
                ByLocation = dailyAttendance
                    .GroupBy(a => new { a.LocationId })
                    .Select(g => new
                    {
                        LocationId = g.Key.LocationId,
                        CheckIns = g.Count(),
                        UniqueEmployees = g.Select(a => a.EmployeeId).Distinct().Count()
                    })
                    .ToList(),
                Employees = dailyAttendance
                    .GroupBy(a => new { a.EmployeeId, a.Employee.FullName })
                    .Select(g => new
                    {
                        EmployeeId = g.Key.EmployeeId,
                        EmployeeName = g.Key.FullName,
                        CheckIns = g.Count(),
                        AverageConfidence = g.Average(a => a.Confidence)
                    })
                    .OrderByDescending(e => e.CheckIns)
                    .ToList()
            };

            return Ok(CallResult.Ok(data: summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDailySummary), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Phase 8: Get monthly attendance summary
    /// </summary>
    [HttpGet("monthly-summary")]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] int year = 0,
        [FromQuery] int month = 0,
        CancellationToken cancellation = default)
    {
        try
        {
            if (year == 0) year = DateTime.UtcNow.Year;
            if (month == 0) month = DateTime.UtcNow.Month;

            var startDate = new DateTime(year, month, 1).ToUniversalTime();
            var endDate = startDate.AddMonths(1);

            // Get attendance for the month
            var monthlyAttendance = await dbContext.Attendances
                .AsNoTracking()
                .Where(a => a.CheckIn >= startDate && a.CheckIn < endDate)
                .Include(a => a.Employee)
                .ToListAsync(cancellation);

            // Group by day
            var dailyData = monthlyAttendance
                .GroupBy(a => a.CheckIn.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    CheckIns = g.Count(),
                    UniqueEmployees = g.Select(a => a.EmployeeId).Distinct().Count(),
                    AverageConfidence = g.Average(a => a.Confidence)
                })
                .OrderBy(d => d.Date)
                .ToList();

            var summary = new
            {
                Year = year,
                Month = month,
                MonthName = new DateTime(year, month, 1).ToString("MMMM"),
                TotalCheckIns = monthlyAttendance.Count,
                UniqueEmployees = monthlyAttendance.Select(a => a.EmployeeId).Distinct().Count(),
                AttendanceRating = monthlyAttendance.Any() ? (monthlyAttendance.Average(a => a.Confidence) * 100).ToString("F2") + "%" : "0%",
                DailyBreakdown = dailyData
            };

            return Ok(CallResult.Ok(data: summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetMonthlySummary), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
