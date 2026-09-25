using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AttendancesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AttendancesController> logger;
    private readonly IHubContext<NotificationHub> hubContext;

    public AttendancesController(ApplicationDbContext dbContext, ILogger<AttendancesController> logger, IHubContext<NotificationHub> hubContext)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> AddAttendance(AddAttendanceDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId, cancellation);

            if (employee is null) 
                return BadRequest(CallResult.Error("Employee not found"));

            var location = await dbContext.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == dto.LocationId, cancellation);

            if (location is null) 
                return BadRequest(CallResult.Error("Location not found"));

            var attendance = new Attendance
            {
                EmployeeId = dto.EmployeeId,
                LocationId = dto.LocationId,
                CheckIn = dto.CheckIn,
                Confidence = dto.Confidence,
                IsAllowed = dto.IsAllowed
            };

            dbContext.Add(attendance);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast employee checked in event in real-time
            await hubContext.Clients.All.SendAsync("EmployeeCheckedIn", new
            {
                employeeId = employee.Id,
                employeeName = employee.FullName,
                locationName = location.Place
            }, cancellation);

            return Ok(CallResult.Ok(data: new AttendanceDto(
                attendance.Id, 
                attendance.EmployeeId, 
                employee.FullName, 
                attendance.LocationId, 
                location.Place, 
                attendance.CheckIn, 
                attendance.CheckOut, 
                attendance.Confidence, 
                attendance.IsAllowed)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddAttendance), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAttendances([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, 
        [FromQuery] long? employeeId = null, [FromQuery] long? locationId = null, CancellationToken cancellation = default)
    {
        try
        {
            var query = dbContext.Attendances.AsNoTracking();

            if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId);

            if (locationId.HasValue)
                query = query.Where(a => a.LocationId == locationId);

            if (startDate.HasValue)
                query = query.Where(a => a.CheckIn >= startDate);

            if (endDate.HasValue)
                query = query.Where(a => a.CheckIn <= endDate);

            var paginationDto = new HlkHelpers.PaginationDto { PageNumber = pageNumber, PageSize = pageSize };

            var list = await PagedList<Attendance>.CreateAsync(query, paginationDto,
                orderBy: x => x.CheckIn,
                selector: a => new { 
                    a.Id, 
                    a.EmployeeId, 
                    EmployeeName = a.Employee.FullName, 
                    a.LocationId, 
                    LocationName = a.Location.Place,
                    a.CheckIn, 
                    a.CheckOut, 
                    a.Confidence, 
                    a.IsAllowed 
                },
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetAttendances), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAttendanceById(long id, CancellationToken cancellation)
    {
        try
        {
            var attendance = await dbContext.Attendances
                .Include(a => a.Employee)
                .Include(a => a.Location)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (attendance is null) 
                return NotFound(CallResult.Error("Attendance record not found"));

            var dto = new AttendanceDto(
                attendance.Id,
                attendance.EmployeeId,
                attendance.Employee.FullName,
                attendance.LocationId,
                attendance.Location.Place,
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.Confidence,
                attendance.IsAllowed);

            return Ok(CallResult.Ok(data: dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetAttendanceById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAttendance(long id, UpdateAttendanceDto dto, CancellationToken cancellation)
    {
        try
        {
            var attendance = await dbContext.Attendances
                .Include(a => a.Employee)
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (attendance is null) 
                return NotFound(CallResult.Error("Attendance record not found"));

            var hadCheckOut = attendance.CheckOut.HasValue;

            if (dto.CheckOut.HasValue)
                attendance.CheckOut = dto.CheckOut.Value;

            attendance.Confidence = dto.Confidence;
            attendance.IsAllowed = dto.IsAllowed;

            dbContext.Update(attendance);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast employee checked out event if CheckOut was just set
            if (!hadCheckOut && attendance.CheckOut.HasValue)
            {
                await hubContext.Clients.All.SendAsync("EmployeeCheckedOut", new
                {
                    employeeId = attendance.EmployeeId,
                    employeeName = attendance.Employee.FullName
                }, cancellation);
            }

            return Ok(CallResult.Ok(data: new AttendanceDto(
                attendance.Id,
                attendance.EmployeeId,
                attendance.Employee.FullName,
                attendance.LocationId,
                attendance.Location.Place,
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.Confidence,
                attendance.IsAllowed)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateAttendance), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAttendance(long id, CancellationToken cancellation)
    {
        try
        {
            var attendance = await dbContext.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (attendance is null) 
                return NotFound(CallResult.Error("Attendance record not found"));

            dbContext.Remove(attendance);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Attendance record deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteAttendance), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetEmployeeAttendance(long employeeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellation);

            if (employee is null) 
                return NotFound(CallResult.Error("Employee not found"));

            var query = dbContext.Attendances
                .Where(a => a.EmployeeId == employeeId)
                .AsNoTracking();

            if (startDate.HasValue)
                query = query.Where(a => a.CheckIn >= startDate);

            if (endDate.HasValue)
                query = query.Where(a => a.CheckIn <= endDate);

            var attendances = await query
                .Include(a => a.Location)
                .OrderByDescending(a => a.CheckIn)
                .Select(a => new { 
                    a.Id, 
                    a.EmployeeId, 
                    Employee = employee.FullName, 
                    a.LocationId, 
                    Location = a.Location.Place,
                    a.CheckIn, 
                    a.CheckOut, 
                    a.Confidence, 
                    a.IsAllowed 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: attendances));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeeAttendance), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("location/{locationId}")]
    public async Task<IActionResult> GetLocationAttendance(long locationId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == locationId, cancellation);

            if (location is null) 
                return NotFound(CallResult.Error("Location not found"));

            var query = dbContext.Attendances
                .Where(a => a.LocationId == locationId)
                .AsNoTracking();

            if (startDate.HasValue)
                query = query.Where(a => a.CheckIn >= startDate);

            if (endDate.HasValue)
                query = query.Where(a => a.CheckIn <= endDate);

            var attendances = await query
                .Include(a => a.Employee)
                .OrderByDescending(a => a.CheckIn)
                .Select(a => new { 
                    a.Id, 
                    a.EmployeeId, 
                    Employee = a.Employee.FullName, 
                    a.LocationId, 
                    Location = location.Place,
                    a.CheckIn, 
                    a.CheckOut, 
                    a.Confidence, 
                    a.IsAllowed 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: attendances));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLocationAttendance), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
