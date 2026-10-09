using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Models;
using TraceProject.Services;

namespace TraceProject.Controllers;

/// <summary>
/// Computers running the desktop monitoring agent. Creating a device (or asking for a new code)
/// returns a pairing code that is typed into TraceAgent on the employee's laptop.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ActivityMonitoringOptions monitoring;
    private readonly ILogger<DevicesController> logger;

    public DevicesController(ApplicationDbContext dbContext, IOptions<ActivityMonitoringOptions> monitoring, ILogger<DevicesController> logger)
    {
        this.dbContext = dbContext;
        this.monitoring = monitoring.Value;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices([FromQuery] long? employeeId, CancellationToken cancellation)
    {
        try
        {
            var onlineSince = DateTime.UtcNow.AddSeconds(-monitoring.OfflineAfterSeconds);

            var devices = await dbContext.Devices.AsNoTracking()
                .Where(d => employeeId == null || d.EmployeeId == employeeId)
                .OrderBy(d => d.Employee.FullName).ThenBy(d => d.Name)
                .Select(d => new DeviceDto(
                    d.Id, d.EmployeeId, d.Employee.FullName, d.Name, d.MachineName, d.AgentVersion,
                    d.TokenHash != null,
                    d.IsActive && d.LastSeenAt != null && d.LastSeenAt >= onlineSince,
                    d.IsActive, d.PairedAt, d.LastSeenAt))
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: devices));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDevices), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddDevice(AddDeviceDto dto, CancellationToken cancellation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(CallResult.Error("Device name is required"));

            var employeeExists = await dbContext.Employees.AnyAsync(e => e.Id == dto.EmployeeId, cancellation);
            if (!employeeExists)
                return BadRequest(CallResult.Error("Employee not found"));

            var device = new Device
            {
                EmployeeId = dto.EmployeeId,
                Name = dto.Name.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            IssuePairingCode(device);

            dbContext.Devices.Add(device);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Device created. Enter the pairing code in TraceAgent on the laptop.",
                new PairingCodeDto(device.Id, device.PairingCode!, device.PairingCodeExpiresAt!.Value)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddDevice), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>Issues a new pairing code. Any agent already paired with this device is signed out.</summary>
    [HttpPost("{id}/pairing-code")]
    public async Task<IActionResult> NewPairingCode(long id, CancellationToken cancellation)
    {
        try
        {
            var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellation);
            if (device is null)
                return NotFound(CallResult.Error("Device not found"));

            IssuePairingCode(device);
            device.TokenHash = null;
            device.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new PairingCodeDto(device.Id, device.PairingCode!, device.PairingCodeExpiresAt!.Value)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(NewPairingCode), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/activation")]
    public async Task<IActionResult> DeviceActivation(long id, DeviceActivationDto dto, CancellationToken cancellation)
    {
        try
        {
            var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellation);
            if (device is null)
                return NotFound(CallResult.Error("Device not found"));

            device.IsActive = dto.Active;
            device.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(dto.Active ? "Device enabled" : "Device disabled; its agent stops recording until it is enabled again"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeviceActivation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>Deletes the device and all of its recorded activity sessions.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDevice(long id, CancellationToken cancellation)
    {
        try
        {
            var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellation);
            if (device is null)
                return NotFound(CallResult.Error("Device not found"));

            dbContext.Remove(device);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Device deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteDevice), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    private void IssuePairingCode(Device device)
    {
        device.PairingCode = DeviceTokens.NewPairingCode();
        device.PairingCodeExpiresAt = DateTime.UtcNow.AddMinutes(Math.Max(1, monitoring.PairingCodeMinutes));
    }
}
