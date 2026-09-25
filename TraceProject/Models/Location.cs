namespace TraceProject.Models;

public class Location
{
    public long Id { get; set; }
    public string Place { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<Employee> EmployeeLocations { get; set; } = new List<Employee>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}
