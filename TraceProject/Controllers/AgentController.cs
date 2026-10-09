using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Services;

namespace TraceProject.Controllers;

/// <summary>
/// Endpoints called by the desktop monitoring agent (TraceAgent), authenticated with a
/// device token ("Authorization: Device &lt;token&gt;"), not the dashboard JWT.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = DeviceTokenAuthenticationHandler.SchemeName)]
public class AgentController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ActivityIngestionService ingestion;
    private readonly ActivityMonitoringOptions monitoring;
    private readonly ILogger<AgentController> logger;

    public AgentController(ApplicationDbContext dbContext, ActivityIngestionService ingestion,
        IOptions<ActivityMonitoringOptions> monitoring, ILogger<AgentController> logger)
    {
        this.dbContext = dbContext;
        this.ingestion = ingestion;
        this.monitoring = monitoring.Value;
        this.logger = logger;
    }

    /// <summary>Exchanges an admin-issued pairing code for this device's token (shown only once).</summary>
    [AllowAnonymous]
    [HttpPost("pair")]
    public async Task<IActionResult> Pair(AgentPairDto dto, CancellationToken cancellation)
    {
        try
        {
            var code = DeviceTokens.NormalizePairingCode(dto.PairingCode ?? "");
            var now = DateTime.UtcNow;

            var device = await dbContext.Devices
                .Include(d => d.Employee)
                .FirstOrDefaultAsync(d => d.PairingCode == code && d.PairingCodeExpiresAt > now && d.IsActive, cancellation);

            if (device is null)
            {
                logger.LogWarning("Pairing rejected: invalid or expired code from {ip}", HttpContext.Connection.RemoteIpAddress);
                return BadRequest(CallResult.Error("Invalid or expired pairing code. Ask the administrator for a new one."));
            }

            var token = DeviceTokens.NewToken();
            device.TokenHash = DeviceTokens.Hash(token);
            device.PairingCode = null;
            device.PairingCodeExpiresAt = null;
            device.PairedAt = now;
            device.LastSeenAt = now;
            device.MachineName = dto.MachineName?.Trim() is { Length: > 0 } name ? name[..Math.Min(name.Length, 100)] : null;
            device.AgentVersion = dto.AgentVersion?.Trim() is { Length: > 0 } version ? version[..Math.Min(version.Length, 50)] : null;
            device.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellation);

            logger.LogInformation("Device {deviceId} ({machine}) paired for employee {employeeId}", device.Id, device.MachineName, device.EmployeeId);
            return Ok(CallResult.Ok(data: new AgentPairResultDto(device.Id, device.Employee.FullName, token)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(Pair), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>The agent's identity and the monitoring settings it should use.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellation)
    {
        try
        {
            var deviceId = DeviceId();
            var device = await dbContext.Devices.AsNoTracking()
                .Where(d => d.Id == deviceId)
                .Select(d => new { d.Id, d.Name, EmployeeName = d.Employee.FullName })
                .FirstAsync(cancellation);

            return Ok(CallResult.Ok(data: new AgentConfigDto(
                device.Id, device.Name, device.EmployeeName,
                Math.Max(30, monitoring.IdleThresholdSeconds),
                Math.Clamp(monitoring.ReportIntervalSeconds, 5, 300),
                monitoring.CaptureWindowTitles)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(Me), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>Uploads finished sessions plus the open one, and the current status.</summary>
    [HttpPost("report")]
    public async Task<IActionResult> Report(AgentReportDto dto, CancellationToken cancellation)
    {
        try
        {
            if (User.FindFirst(DeviceTokenAuthenticationHandler.DeviceActiveClaim)?.Value != "true")
                return StatusCode(StatusCodes.Status403Forbidden, CallResult.Error("This computer has been disabled by the administrator"));

            if (dto.Sessions is { Count: > ActivityIngestionService.MaxSessionsPerReport })
                return BadRequest(CallResult.Error($"At most {ActivityIngestionService.MaxSessionsPerReport} sessions per report"));

            var accepted = await ingestion.IngestAsync(DeviceId(), EmployeeId(), dto, cancellation);
            return Ok(CallResult.Ok(data: new { accepted }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(Report), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    private long DeviceId() => long.Parse(User.FindFirst(DeviceTokenAuthenticationHandler.DeviceIdClaim)!.Value);

    private long EmployeeId() => long.Parse(User.FindFirst(DeviceTokenAuthenticationHandler.EmployeeIdClaim)!.Value);
}
