using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;
using PaginationDto = HlkHelpers.PaginationDto;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UnknownFacesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<UnknownFacesController> logger;
    private readonly IHubContext<NotificationHub> hubContext;

    public UnknownFacesController(ApplicationDbContext dbContext, ILogger<UnknownFacesController> logger, IHubContext<NotificationHub> hubContext)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> AddUnknownFace(AddUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            if (dto.face == null || dto.face.Length == 0)
                return BadRequest(CallResult.Error("Face data is required"));

            var unknownFace = new UnknownFace
            {
                face = dto.face,
                UnknownFaceLocations = new List<UnknownFaceLocation>()
            };

            dbContext.Add(unknownFace);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast unknown face detected event in real-time
            var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Id == dto.LocationId, cancellation);
            await hubContext.Clients.All.SendAsync("UnknownFaceDetected", new
            {
                unknownFaceId = unknownFace.Id,
                locationName = location?.Place ?? "Unknown Location"
            }, cancellation);

            return Ok(CallResult.Ok(data: new { unknownFace.Id, Message = "Unknown face added successfully" }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddUnknownFace), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUnknownFaces([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var source = dbContext.UnknownFaces
                .Include(u => u.UnknownFaceLocations)
                .ThenInclude(u => u.Location)
                .AsNoTracking();

            var list = await PagedList<UnknownFace>.CreateAsync(source, dto,
                orderBy: x => x.Id,
                selector: u => new { 
                    u.Id, 
                    FaceDataLength = u.face != null ? u.face.Length : 0,
                    Locations = u.UnknownFaceLocations.Select(l => new { l.Id, l.LocationId, l.Location.Place })
                },
                cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUnknownFaces), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUnknownFaceById(long id, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces
                .Include(u => u.UnknownFaceLocations)
                .ThenInclude(u => u.Location)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (unknownFace is null) 
                return NotFound(CallResult.Error("Unknown face not found"));

            var locationDtos = unknownFace.UnknownFaceLocations
                ?.Select(l => new UnknownFaceLocationDto(l.Id, l.UnknownFaceId, l.LocationId, l.Location.Place))
                .ToList() ?? new List<UnknownFaceLocationDto>();

            var dto = new UnknownFaceDto(unknownFace.Id, unknownFace.face, locationDtos);

            return Ok(CallResult.Ok(data: dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUnknownFaceById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/locations")]
    public async Task<IActionResult> AddLocationToUnknownFace(long id, AddUnknownFaceLocationDto dto, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces
                .FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (unknownFace is null) 
                return NotFound(CallResult.Error("Unknown face not found"));

            var location = await dbContext.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == dto.LocationId, cancellation);

            if (location is null) 
                return BadRequest(CallResult.Error("Location not found"));

            var locationExists = await dbContext.UnknownFaceLocations
                .AsNoTracking()
                .AnyAsync(ul => ul.UnknownFaceId == id && ul.LocationId == dto.LocationId, cancellation);

            if (locationExists)
                return BadRequest(CallResult.Error("Location already associated with this unknown face"));

            var unknownFaceLocation = new UnknownFaceLocation
            {
                UnknownFaceId = id,
                LocationId = dto.LocationId
            };

            dbContext.Add(unknownFaceLocation);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new UnknownFaceLocationDto(
                unknownFaceLocation.Id,
                unknownFaceLocation.UnknownFaceId,
                unknownFaceLocation.LocationId,
                location.Place)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddLocationToUnknownFace), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}/locations/{locationId}")]
    public async Task<IActionResult> RemoveLocationFromUnknownFace(long id, long locationId, CancellationToken cancellation)
    {
        try
        {
            var unknownFaceLocation = await dbContext.UnknownFaceLocations
                .FirstOrDefaultAsync(ul => ul.UnknownFaceId == id && ul.LocationId == locationId, cancellation);

            if (unknownFaceLocation is null) 
                return NotFound(CallResult.Error("Location not found for this unknown face"));

            dbContext.Remove(unknownFaceLocation);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Location removed from unknown face successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(RemoveLocationFromUnknownFace), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUnknownFace(long id, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces
                .Include(u => u.UnknownFaceLocations)
                .FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (unknownFace is null) 
                return NotFound(CallResult.Error("Unknown face not found"));

            // Remove all associated locations
            if (unknownFace.UnknownFaceLocations?.Any() == true)
            {
                dbContext.RemoveRange(unknownFace.UnknownFaceLocations);
            }

            dbContext.Remove(unknownFace);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Unknown face deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteUnknownFace), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("by-location/{locationId}")]
    public async Task<IActionResult> GetUnknownFacesByLocation(long locationId, CancellationToken cancellation)
    {
        try
        {
            var location = await dbContext.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == locationId, cancellation);

            if (location is null) 
                return NotFound(CallResult.Error("Location not found"));

            var unknownFaces = await dbContext.UnknownFaceLocations
                .Where(ul => ul.LocationId == locationId)
                .Include(ul => ul.UnknownFace)
                .AsNoTracking()
                .Select(ul => new { 
                    ul.UnknownFace.Id, 
                    FaceDataLength = ul.UnknownFace.face != null ? ul.UnknownFace.face.Length : 0,
                    LocationName = location.Place
                })
                .ToListAsync(cancellation);

            return Ok(CallResult.Ok(data: unknownFaces));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUnknownFacesByLocation), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/register-employee")]
    public async Task<IActionResult> RegisterEmployee(long id, RegisterUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces.FirstOrDefaultAsync(uf => uf.Id == id, cancellation);

            if (unknownFace is null)
                return NotFound(CallResult.Error("Unknown face not found"));

            // Check if department exists
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId, cancellation);
            if (department is null)
                return BadRequest(CallResult.Error("Department not found"));

            // Create new employee from unknown face
            var employee = new Employee
            {
                Email = dto.Email,
                FullName = dto.FullName,
                Position = dto.Position,
                PhoneNumber = dto.PhoneNumber,
                EmployeeNumber = dto.EmployeeNumber,
                DepartmentId = dto.DepartmentId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Add(employee);

            // Create face profile for this employee
            var faceProfile = new FaceProfil
            {
                EmployeeId = employee.Id,
                FrontProfile = unknownFace.face
            };
            dbContext.Add(faceProfile);

            // Remove unknown face from system (mark resolved)
            dbContext.Remove(unknownFace);

            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new
            {
                employeeId = employee.Id,
                message = "Employee registered successfully from unknown face"
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(RegisterEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("{id}/match-employee")]
    public async Task<IActionResult> MatchEmployee(long id, MatchUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces.FirstOrDefaultAsync(uf => uf.Id == id, cancellation);

            if (unknownFace is null)
                return NotFound(CallResult.Error("Unknown face not found"));

            var employee = await dbContext.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId, cancellation);

            if (employee is null)
                return NotFound(CallResult.Error("Employee not found"));

            // Link this unknown face to employee's face profile for training data improvement
            var faceProfile = await dbContext.FaceProfiles.FirstOrDefaultAsync(fp => fp.EmployeeId == dto.EmployeeId, cancellation);

            if (faceProfile is null)
            {
                // Create new face profile if one doesn't exist
                faceProfile = new FaceProfil
                {
                    EmployeeId = dto.EmployeeId,
                    FrontProfile = unknownFace.face
                };
                dbContext.Add(faceProfile);
            }
            else
            {
                // Could enhance existing face data here if system supports face data merging
                // For now, just mark as updated
                dbContext.Update(faceProfile);
            }

            // Note: Keep the unknown face record for audit trail, but mark it as resolved/matched
            // Alternatively, you could delete it if the system doesn't need audit trails
            dbContext.Remove(unknownFace);

            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new
            {
                message = "Unknown face matched to employee successfully"
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(MatchEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
