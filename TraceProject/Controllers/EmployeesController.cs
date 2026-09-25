using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dto;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;
using PaginationDto = HlkHelpers.PaginationDto;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<EmployeesController> logger;
    private readonly IHubContext<NotificationHub> hubContext;

    public EmployeesController(ApplicationDbContext dbContext, ILogger<EmployeesController> logger, IHubContext<NotificationHub> hubContext)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
    }


    [HttpPost]
    public async Task<IActionResult> AddEmployee(AddEmployeeDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellation);

            if (employee is not null) return BadRequest(CallResult.Error("Account already emailExists"));

            employee = new()
            {
                DepartmentId = dto.DepartmentId,
                Email = dto.Email,
                FullName = dto.FullName,
                Position = dto.Position,
                PhoneNumber = dto.PhoneNumber,
                EmployeeNumber = dto.EmployeeNumber,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Add(employee);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast employee added event in real-time
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == employee.DepartmentId, cancellation);
            await hubContext.Clients.All.SendAsync("EmployeeAdded", new
            {
                employeeId = employee.Id,
                fullName = employee.FullName,
                department = department?.Name ?? "Unknown"
            }, cancellation);

            return Ok(CallResult.Ok("Employee added successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetEmployees([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var searchQuery = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Employees.AsNoTracking();

            var list = await PagedList<Employee>.CreateAsync(source, dto,
                orderBy: x => x.FullName,
                selector: u => new { u.Id, u.Email, u.FullName, u.Position, u.PhoneNumber, u.EmployeeNumber, u.IsActive, u.CreatedAt, u.UpdatedAt, Department = u.Department.Name },
                whereClause: x => x.Email.ToLower().Contains(searchQuery) || x.FullName.ToLower().Contains(searchQuery) || x.PhoneNumber.ToLower().Contains(searchQuery)
                || x.EmployeeNumber.ToLower().Contains(searchQuery) || x.Position.ToLower().Contains(searchQuery) || x.Department.Name.ToLower().Contains(searchQuery),
               cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployees), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmployee(long id, UpdateEmployeeDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (employee is null) return NotFound(CallResult.Error("Employee not found"));

            var emailExists = await dbContext.Employees.AsNoTracking().AnyAsync(u => u.Email == dto.Email && u.Id != id, cancellation);  

            if (emailExists) return BadRequest(CallResult.Error("Employee with this email already exists"));

            var empNumberExists = await dbContext.Employees.AsNoTracking().AnyAsync(u => u.EmployeeNumber == dto.EmployeeNumber && u.Id != id, cancellation);

            if (empNumberExists) return BadRequest(CallResult.Error("Employee with this employee number already exists"));

            employee.DepartmentId = dto.DepartmentId;
            employee.Email = dto.Email;  
            employee.FullName = dto.FullName;
            employee.Position = dto.Position;
            employee.PhoneNumber = dto.PhoneNumber;
            employee.EmployeeNumber = dto.EmployeeNumber;
            employee.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(employee);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Employee updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("activation")]
    public async Task<IActionResult> EmployeeActivation(long id, EmployeeActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (employee is null) return NotFound(CallResult.Error("Employee not found"));

            employee.IsActive = dto.activate;
            employee.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(employee);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Employee updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(EmployeeActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(long id, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (employee is null) return NotFound(CallResult.Error("Employee not found"));

            dbContext.Remove(employee);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Employee deleted successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployeeById(long id, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees
                .Include(e => e.Department)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, cancellation);

            if (employee is null) 
                return NotFound(CallResult.Error("Employee not found"));

            return Ok(CallResult.Ok(data: new { 
                employee.Id, 
                employee.Email, 
                employee.FullName, 
                employee.Position, 
                employee.PhoneNumber, 
                employee.EmployeeNumber, 
                employee.IsActive, 
                employee.CreatedAt, 
                employee.UpdatedAt, 
                Department = employee.Department.Name 
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeeById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("department/{departmentId}")]
    public async Task<IActionResult> GetEmployeesByDepartment(long departmentId, [FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            var department = await dbContext.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, cancellation);

            if (department is null) 
                return NotFound(CallResult.Error("Department not found"));

            dto ??= new PaginationDto();

            var searchQuery = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Employees.Where(e => e.DepartmentId == departmentId).AsNoTracking();

            var list = await PagedList<Employee>.CreateAsync(source, dto,
                orderBy: x => x.FullName,
                selector: u => new { 
                    u.Id, 
                    u.Email, 
                    u.FullName, 
                    u.Position, 
                    u.PhoneNumber, 
                    u.EmployeeNumber, 
                    u.IsActive, 
                    u.CreatedAt, 
                    u.UpdatedAt, 
                    Department = u.Department.Name 
                },
                whereClause: x => x.Email.ToLower().Contains(searchQuery) || x.FullName.ToLower().Contains(searchQuery) 
                    || x.PhoneNumber.ToLower().Contains(searchQuery) || x.EmployeeNumber.ToLower().Contains(searchQuery) 
                    || x.Position.ToLower().Contains(searchQuery),
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetEmployeesByDepartment), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/activation")]
    public async Task<IActionResult> ToggleEmployeeActivation(long id, EmployeeActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var employee = await dbContext.Employees.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (employee is null) 
                return NotFound(CallResult.Error("Employee not found"));

            employee.IsActive = dto.activate;
            employee.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(employee);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok($"Employee {(employee.IsActive ? "activated" : "deactivated")} successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(ToggleEmployeeActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchEmployees([FromQuery] string? query, CancellationToken cancellation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(CallResult.Error("Search query is required"));

            var searchQuery = query.Trim().ToLower();

            var employees = await dbContext.Employees
                .Where(e => e.Email.ToLower().Contains(searchQuery) 
                    || e.FullName.ToLower().Contains(searchQuery) 
                    || e.EmployeeNumber.ToLower().Contains(searchQuery)
                    || e.Position.ToLower().Contains(searchQuery))
                .AsNoTracking()
                .Select(e => new { 
                    e.Id, 
                    e.Email, 
                    e.FullName, 
                    e.Position, 
                    e.PhoneNumber, 
                    e.EmployeeNumber, 
                    e.IsActive,
                    Department = e.Department.Name 
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: employees));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(SearchEmployees), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }



}
