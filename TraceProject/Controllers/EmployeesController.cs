using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dto;
using TraceProject.Dtos;
using TraceProject.Models;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<EmployeesController> logger;

    public EmployeesController(ApplicationDbContext dbContext, ILogger<EmployeesController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
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
            };

            dbContext.Add(employee);

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

            var query = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Employees.AsNoTracking();

            var list = await PagedList<Employee>.CreateAsync(source, dto,
                orderBy: x => x.FullName,
                selector: u => new { u.Id, u.Email, u.FullName, u.Position, u.PhoneNumber, u.EmployeeNumber, u.IsActive, u.CreatedAt, u.UpdatedAt, Department = u.Department.Name },
                whereClause: x => x.Email.ToLower().Contains(query) || x.FullName.ToLower().Contains(query) || x.PhoneNumber.ToLower().Contains(query)
                || x.EmployeeNumber.ToLower().Contains(query) || x.Position.ToLower().Contains(query) || x.Department.Name.ToLower().Contains(query),
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

            return Ok(CallResult.Ok("Employee deleted successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }




}
