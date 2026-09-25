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
public class LocationsController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<LocationsController> logger;

    public LocationsController(ApplicationDbContext dbContext, ILogger<LocationsController> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> AddLocation(AddLocationDto dto, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Place == dto.Place, cancellation);

            if (location is not null) 
                return BadRequest(CallResult.Error("Location already exists"));

            location = new()
            {
                Place = dto.Place,
            };

            dbContext.Add(location);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Location added successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddLocation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var searchQuery = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Locations.AsNoTracking();

            var list = await PagedList<Location>.CreateAsync(source, dto,
                orderBy: x => x.Place,
                selector: l => new { l.Id, l.Place },
                whereClause: x => x.Place.ToLower().Contains(searchQuery),
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLocations), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLocationById(long id, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id, cancellation);

            if (location is null) 
                return NotFound(CallResult.Error("Location not found"));

            return Ok(CallResult.Ok(data: new LocationDto(location.Id, location.Place)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetLocationById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLocation(long id, UpdateLocationDto dto, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellation);

            if (location is null) 
                return NotFound(CallResult.Error("Location not found"));

            var exists = await dbContext.Locations.AsNoTracking()
                .AnyAsync(l => l.Place == dto.Place && l.Id != id, cancellation);

            if (exists) 
                return BadRequest(CallResult.Error("Location with this name already exists"));

            location.Place = dto.Place;
            dbContext.Update(location);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Location updated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateLocation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(long id, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellation);

            if (location is null) 
                return NotFound(CallResult.Error("Location not found"));

            // Check if location is used in any attendance records
            var isUsed = await dbContext.Attendances.AsNoTracking()
                .AnyAsync(a => a.LocationId == id, cancellation);

            if (isUsed) 
                return BadRequest(CallResult.Error("Location is in use and cannot be deleted"));

            dbContext.Remove(location);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Location deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteLocation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
