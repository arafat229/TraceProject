using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
public class SetUpsController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<SetUpsController> logger;

    public SetUpsController(ApplicationDbContext dbContext, ILogger<SetUpsController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }


    [HttpPost("departments")]
    public async Task<IActionResult> AddDepartment(DepartmentDto dto, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Name == dto.Name, cancellation);

            if (department is not null) return BadRequest(CallResult.Error("Department already exists"));

            department = new()
            {
                Name = dto.Name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Add(department);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Department added successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddDepartment), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(long id, DepartmentDto dto, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellation);

            if (department is null) return BadRequest(CallResult.Error("Department not found"));

            var exists = await dbContext.Departments.AnyAsync(d => d.Name == dto.Name && d.Id != id, cancellation);

            if (exists) return BadRequest(CallResult.Error("Department with this name already exists"));

            department.Name = dto.Name;
            department.UpdatedAt = DateTime.UtcNow;
            dbContext.Update(department);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Department updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateDepartment), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("departments/activation")]
    public async Task<IActionResult> DepartmentActivation(DepartmentActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == dto.id, cancellation);

            if (department is null) return BadRequest(CallResult.Error("Department not found"));

            department.IsActive = dto.active;
            department.UpdatedAt = DateTime.UtcNow; 
            dbContext.Update(department);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Department updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DepartmentActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var searchQuery = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Departments.AsNoTracking();

            var list = await PagedList<Department>.CreateAsync(source, dto,
                orderBy: x => x.Name,
                selector: d => new { d.Id, d.Name, d.IsActive, d.CreatedAt, d.UpdatedAt },
                whereClause: x => x.Name.ToLower().Contains(searchQuery),
               cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDepartments), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("departments/{id}")]
    public async Task<IActionResult> GetDepartmentById(long id, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, cancellation);

            if (department is null)
                return NotFound(CallResult.Error("Department not found"));

            var employeeCount = await dbContext.Employees
                .CountAsync(e => e.DepartmentId == id, cancellation);

            return Ok(CallResult.Ok(data: new { 
                department.Id, 
                department.Name, 
                department.IsActive, 
                department.CreatedAt, 
                department.UpdatedAt, 
                EmployeeCount = employeeCount 
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDepartmentById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("departments/{id}")]
    public async Task<IActionResult> DeleteDepartment(long id, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellation);

            if (department is null)
                return NotFound(CallResult.Error("Department not found"));

            // Check if department has employees
            var hasEmployees = await dbContext.Employees
                .AnyAsync(e => e.DepartmentId == id, cancellation);

            if (hasEmployees)
                return BadRequest(CallResult.Error("Cannot delete department with employees"));

            dbContext.Remove(department);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Department deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteDepartment), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

}
