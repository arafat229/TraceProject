namespace TraceProject.Models;

public class Attendance
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public long LocationId { get; set; }
    /// <summary>
    /// Server-local calendar day of the check-in. (EmployeeId, WorkDate) is unique:
    /// one attendance row per employee per day.
    /// </summary>
    public DateOnly WorkDate { get; set; }

    public DateTime CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public long? CameraId { get; set; }
    public double Confidence { get; set; }
    public bool IsAllowed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Employee Employee { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public Camera? Camera { get; set; }

    // Computed property with proper null-handling
    public TimeSpan? Duration => CheckOut.HasValue ? CheckOut.Value - CheckIn : null;
}

