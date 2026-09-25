using HlkHelpers;

namespace TraceProject.Models;

public class Camera : Auditable 
{
    public string CameraName { get; set; } = null!;
    public string IPAddress { get; set; } = null!;
    public string Location { get; set; } = null!;
    // Where attendance recorded by this camera is filed (FK to Locations; already a column)
    public long? LocationId { get; set; }
    public bool IsActive { get; set; }

    // Streaming properties (Phase 1: RTSP support)
    public string? RtspUrl { get; set; }
    public string? Username { get; set; }
    public string? Transport { get; set; } = "TCP";
    // Note: Password is NOT stored in DB; retrieved from appsettings/User Secrets at runtime

    // Relations
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
// Relations between camera and locations must be added



