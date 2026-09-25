using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Models;
using PaginationDto = HlkHelpers.PaginationDto;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<ActivitiesController> logger;

    public ActivitiesController(ApplicationDbContext dbContext, ILogger<ActivitiesController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> AddActivity(AddActivityDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId, cancellation);

            if (employee is null) 
                return BadRequest(CallResult.Error("Employee not found"));

            var activity = await dbContext.Activities
                .FirstOrDefaultAsync(a => a.EmployeeId == dto.EmployeeId, cancellation);

            if (activity is null)
            {
                activity = new Activity
                {
                    EmployeeId = dto.EmployeeId,
                    IsActive = dto.IsActive,
                    CurrentApp = dto.CurrentApp,
                    ActiveTime = DateTime.UtcNow,
                    OffTime = DateTime.UtcNow.AddHours(8),
                    LockTime = DateTime.UtcNow
                };

                dbContext.Add(activity);
            }
            else
            {
                activity.IsActive = dto.IsActive;
                activity.CurrentApp = dto.CurrentApp;
                activity.ActiveTime = DateTime.UtcNow;
                dbContext.Update(activity);
            }

            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new ActivityDto(
                activity.Id,
                activity.EmployeeId,
                activity.IsActive,
                activity.CurrentApp,
                activity.ActiveTime,
                activity.OffTime,
                activity.LockTime)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var source = dbContext.Activities.AsNoTracking();

            var list = await PagedList<Activity>.CreateAsync(source, dto,
                orderBy: x => x.Id,
                selector: a => new { 
                    a.Id, 
                    a.EmployeeId, 
                    a.IsActive, 
                    a.CurrentApp, 
                    a.ActiveTime, 
                    a.OffTime, 
                    a.LockTime 
                },
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetActivities), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetActivityById(long id, CancellationToken cancellation)
    {
        try
        {
            var activity = await dbContext.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (activity is null) 
                return NotFound(CallResult.Error("Activity not found"));

            var dto = new ActivityDto(
                activity.Id,
                activity.EmployeeId,
                activity.IsActive,
                activity.CurrentApp,
                activity.ActiveTime,
                activity.OffTime,
                activity.LockTime);

            return Ok(CallResult.Ok(data: dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetActivityById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetEmployeeActivity(long employeeId, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellation);

            if (employee is null) 
                return NotFound(CallResult.Error("Employee not found"));

            var activity = await dbContext.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId, cancellation);

            if (activity is null) 
                return NotFound(CallResult.Error("No activity found for this employee"));

            var dto = new ActivityDto(
                activity.Id,
                activity.EmployeeId,
                activity.IsActive,
                activity.CurrentApp,
                activity.ActiveTime,
                activity.OffTime,
                activity.LockTime);

            return Ok(CallResult.Ok(data: dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeeActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateActivity(long id, UpdateActivityDto dto, CancellationToken cancellation)
    {
        try
        {
            var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (activity is null) 
                return NotFound(CallResult.Error("Activity not found"));

            activity.IsActive = dto.IsActive;
            activity.CurrentApp = dto.CurrentApp;

            if (dto.OffTime.HasValue)
                activity.OffTime = dto.OffTime.Value;

            if (dto.LockTime.HasValue)
                activity.LockTime = dto.LockTime.Value;

            dbContext.Update(activity);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new ActivityDto(
                activity.Id,
                activity.EmployeeId,
                activity.IsActive,
                activity.CurrentApp,
                activity.ActiveTime,
                activity.OffTime,
                activity.LockTime)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteActivity(long id, CancellationToken cancellation)
    {
        try
        {
            var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (activity is null) 
                return NotFound(CallResult.Error("Activity not found"));

            dbContext.Remove(activity);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Activity deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivateActivity(long id, CancellationToken cancellation)
    {
        try
        {
            var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellation);

            if (activity is null) 
                return NotFound(CallResult.Error("Activity not found"));

            activity.IsActive = false;
            activity.OffTime = DateTime.UtcNow;

            dbContext.Update(activity);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Activity deactivated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeactivateActivity), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
