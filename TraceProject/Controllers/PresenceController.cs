using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceProject.Services;

namespace TraceProject.Controllers;

/// <summary>
/// Camera presence and computer activity combined per employee (objective iii).
/// Live changes are pushed as the PresenceChanged SignalR event.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PresenceController : ControllerBase
{
    private readonly PresenceService presence;
    private readonly ILogger<PresenceController> logger;

    public PresenceController(PresenceService presence, ILogger<PresenceController> logger)
    {
        this.presence = presence;
        this.logger = logger;
    }

    /// <summary>Every active employee's combined status right now.</summary>
    [HttpGet]
    public async Task<IActionResult> GetLive(CancellationToken cancellation)
    {
        try
        {
            return Ok(CallResult.Ok(data: await presence.GetLiveAsync(cancellation)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLive), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Presence report for a server-local work day (yyyy-MM-dd, default today): camera check-in/out,
    /// computer active/idle/locked time, and the active time verified by a camera check-in.
    /// </summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily([FromQuery] DateOnly? date, CancellationToken cancellation)
    {
        try
        {
            return Ok(CallResult.Ok(data: await presence.GetDailyAsync(date ?? WorkDay.Today, cancellation)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetDaily), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Weekly / monthly report: per-employee totals and per-day totals over [from, to]
    /// (yyyy-MM-dd, server-local work days, at most 93 days). Days after today are left out.
    /// </summary>
    [HttpGet("range")]
    public async Task<IActionResult> GetRange([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellation)
    {
        try
        {
            if (to < from)
                return BadRequest(CallResult.Error("'to' must not be before 'from'"));
            if (to.DayNumber - from.DayNumber + 1 > PresenceService.MaxRangeDays)
                return BadRequest(CallResult.Error($"A report covers at most {PresenceService.MaxRangeDays} days"));
            if (from > WorkDay.Today)
                return BadRequest(CallResult.Error("The period starts in the future"));

            return Ok(CallResult.Ok(data: await presence.GetRangeAsync(from, to, cancellation)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetRange), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
