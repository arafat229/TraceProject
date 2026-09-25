using System.Globalization;
using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceProject.Dtos;
using TraceProject.Services;

namespace TraceProject.Controllers;

/// <summary>
/// System-wide settings (recognition threshold, working hours, attendance update interval).
/// Changes apply immediately to the live pipeline.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SystemSettingsService settings;
    private readonly ILogger<SettingsController> logger;

    public SettingsController(SystemSettingsService settings, ILogger<SettingsController> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    [HttpGet]
    public IActionResult GetSettings()
    {
        var s = settings.Current;
        return Ok(CallResult.Ok(data: new SystemSettingsDto(s.RecognitionThreshold, s.WorkStartTime, s.WorkEndTime, s.LateGraceMinutes, s.AttendanceUpdateIntervalMinutes)));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings(SystemSettingsDto dto, CancellationToken cancellation)
    {
        try
        {
            if (dto.RecognitionThreshold is < 0.2f or > 0.9f)
                return BadRequest(CallResult.Error("Recognition threshold must be between 0.20 and 0.90"));
            if (!IsTime(dto.WorkStartTime) || !IsTime(dto.WorkEndTime))
                return BadRequest(CallResult.Error("Working hours must be in HH:mm format"));
            if (dto.LateGraceMinutes is < 0 or > 240)
                return BadRequest(CallResult.Error("Grace period must be between 0 and 240 minutes"));
            if (dto.AttendanceUpdateIntervalMinutes is < 0 or > 120)
                return BadRequest(CallResult.Error("Attendance update interval must be between 0 and 120 minutes"));

            var updated = await settings.UpdateAsync(s =>
            {
                s.RecognitionThreshold = dto.RecognitionThreshold;
                s.WorkStartTime = dto.WorkStartTime;
                s.WorkEndTime = dto.WorkEndTime;
                s.LateGraceMinutes = dto.LateGraceMinutes;
                s.AttendanceUpdateIntervalMinutes = dto.AttendanceUpdateIntervalMinutes;
            }, cancellation);

            return Ok(CallResult.Ok("Settings saved", new SystemSettingsDto(updated.RecognitionThreshold, updated.WorkStartTime, updated.WorkEndTime, updated.LateGraceMinutes, updated.AttendanceUpdateIntervalMinutes)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateSettings), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    private static bool IsTime(string? value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
