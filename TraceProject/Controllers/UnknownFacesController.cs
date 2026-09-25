using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Dtos;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services.FaceRecognition;
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
    private readonly FaceEnrollmentService enrollmentService;

    public UnknownFacesController(ApplicationDbContext dbContext, ILogger<UnknownFacesController> logger, IHubContext<NotificationHub> hubContext, FaceEnrollmentService enrollmentService)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.hubContext = hubContext;
        this.enrollmentService = enrollmentService;
    }

    [HttpPost]
    public async Task<IActionResult> AddUnknownFace(AddUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            if (dto.face == null || dto.face.Length == 0)
                return BadRequest(CallResult.Error("Face data is required"));

            var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Id == dto.LocationId, cancellation);

            var unknownFace = new UnknownFace
            {
                face = dto.face,
                DetectedAtUtc = DateTime.UtcNow,
                UnknownFaceLocations = location is null
                    ? new List<UnknownFaceLocation>()
                    : new List<UnknownFaceLocation> { new() { LocationId = location.Id } }
            };

            dbContext.Add(unknownFace);
            await dbContext.SaveChangesAsync(cancellation);

            // Broadcast unknown face detected event in real-time
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

    /// <summary>
    /// Newest first. Same paged shape as PagedList ({ items, totalCount, totalPages, currentPage, … });
    /// each item: { id, detectedAtUtc, detectionScore, cameraId, image (base64 JPEG), locations: [{ id, locationId, place }] }.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUnknownFaces([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();
            int pageNumber = Math.Max(1, dto.PageNumber);
            int pageSize = Math.Clamp(dto.PageSize, 1, 500);

            var query = dbContext.UnknownFaces.AsNoTracking();
            int totalCount = await query.CountAsync(cancellation);

            var rows = await query
                .OrderByDescending(u => u.DetectedAtUtc)
                .ThenByDescending(u => u.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.DetectedAtUtc,
                    u.DetectionScore,
                    u.CameraId,
                    u.face,
                    Locations = u.UnknownFaceLocations!.Select(l => new { l.Id, l.LocationId, l.Location.Place }).ToList()
                })
                .ToListAsync(cancellation);

            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var page = new
            {
                CurrentPage = pageNumber,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalCount = totalCount,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1,
                Items = rows.Select(u => new
                {
                    u.Id,
                    u.DetectedAtUtc,
                    u.DetectionScore,
                    u.CameraId,
                    Image = u.face is { Length: > 0 } ? Convert.ToBase64String(u.face) : null,
                    u.Locations
                })
            };

            return Ok(CallResult.Ok(data: page));
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

    /// <summary>
    /// Creates a new employee from an unknown face and enrolls that face for recognition.
    /// </summary>
    [HttpPost("{id}/register-employee")]
    public async Task<IActionResult> RegisterEmployee(long id, RegisterUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces.FirstOrDefaultAsync(uf => uf.Id == id, cancellation);
            if (unknownFace is null)
                return NotFound(CallResult.Error("Unknown face not found"));

            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId, cancellation);
            if (department is null)
                return BadRequest(CallResult.Error("Department not found"));

            if (await dbContext.Employees.AnyAsync(e => e.Email == dto.Email, cancellation))
                return BadRequest(CallResult.Error("An employee with this email already exists"));

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
            await dbContext.SaveChangesAsync(cancellation);

            await hubContext.Clients.All.SendAsync("EmployeeAdded", new
            {
                employeeId = employee.Id,
                fullName = employee.FullName,
                department = department.Name
            }, cancellation);

            var enrollment = await enrollmentService.EnrollFromFaceJpegAsync(employee.Id, unknownFace.face ?? [], $"unknown_face_{id}", cancellation);
            if (enrollment.Embedding is null)
            {
                // Keep the unknown face so it can still be matched to this employee later
                return Ok(CallResult.Ok($"Employee created, but the face could not be enrolled: {enrollment.Error}", new { employeeId = employee.Id, faceEnrolled = false }));
            }

            dbContext.Remove(unknownFace);
            await dbContext.SaveChangesAsync(cancellation);

            await hubContext.Clients.All.SendAsync("UnknownFaceResolved", new
            {
                unknownFaceId = id,
                employeeId = employee.Id,
                employeeName = employee.FullName
            }, cancellation);

            return Ok(CallResult.Ok("Employee registered and face enrolled", new { employeeId = employee.Id, faceEnrolled = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(RegisterEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    /// <summary>
    /// Enrolls an unknown face as an additional face of an existing employee.
    /// </summary>
    [HttpPost("{id}/match-employee")]
    public async Task<IActionResult> MatchEmployee(long id, MatchUnknownFaceDto dto, CancellationToken cancellation)
    {
        try
        {
            var unknownFace = await dbContext.UnknownFaces.FirstOrDefaultAsync(uf => uf.Id == id, cancellation);
            if (unknownFace is null)
                return NotFound(CallResult.Error("Unknown face not found"));

            var employee = await dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == dto.EmployeeId, cancellation);
            if (employee is null)
                return NotFound(CallResult.Error("Employee not found"));

            var enrollment = await enrollmentService.EnrollFromFaceJpegAsync(employee.Id, unknownFace.face ?? [], $"unknown_face_{id}", cancellation);
            if (enrollment.Embedding is null)
                return BadRequest(CallResult.Error(enrollment.Error ?? "The face could not be enrolled"));

            dbContext.Remove(unknownFace);
            await dbContext.SaveChangesAsync(cancellation);

            await hubContext.Clients.All.SendAsync("UnknownFaceResolved", new
            {
                unknownFaceId = id,
                employeeId = employee.Id,
                employeeName = employee.FullName
            }, cancellation);

            return Ok(CallResult.Ok($"Face enrolled for {employee.FullName}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(MatchEmployee), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }
}
