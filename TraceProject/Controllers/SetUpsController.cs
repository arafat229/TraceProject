using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;

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
                IsActive = true
            };

            dbContext.Add(department);

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

            return Ok(CallResult.Ok("Department updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DepartmentActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("cameras")]
    public async Task<IActionResult> AddCamera(AddCameraDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.CameraName == dto.CameraName, cancellation);

            if (camera is not null) return BadRequest(CallResult.Error("Camera already exists"));

            camera = new()
            {
                CameraName = dto.CameraName,
                IPAddress = dto.IPAddress,
                Location = dto.Location,
                IsActive = true
            };

            dbContext.Add(camera);

            return Ok(CallResult.Ok("Camera added successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPut("cameras/{id}")]
    public async Task<IActionResult> UpdateCamera(long id, UpdateCameraDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellation);

            if (camera is null) return BadRequest(CallResult.Error("Camera not found"));

            var exists = await dbContext.Cameras.AnyAsync(c => c.CameraName == dto.CameraName && c.Id != id, cancellation);

            if (exists) return BadRequest(CallResult.Error("Camera with this name already exists"));

            camera.CameraName = dto.CameraName;
            camera.IPAddress = dto.IPAddress;
            camera.Location = dto.Location;
            camera.UpdatedAt = DateTime.UtcNow;
            dbContext.Update(camera);

            return Ok(CallResult.Ok("Camera updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateCamera), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("cameras/activation")]
    public async Task<IActionResult> CameraActivation(CameraActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var camera = await dbContext.Cameras.FirstOrDefaultAsync(c => c.Id == dto.id, cancellation);

            if (camera is null) return BadRequest(CallResult.Error("Camera not found"));

            camera.IsActive = dto.active;
            camera.UpdatedAt = DateTime.UtcNow;
            dbContext.Update(camera);

            return Ok(CallResult.Ok("Camera updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(CameraActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }



}
