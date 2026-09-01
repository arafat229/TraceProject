namespace TraceProject.Model;

public class Employee
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Foreign key + navigation property pair. EF Core sees "DepartmentId"
    // matches the "Id" on Department and automatically treats this as a
    // foreign key relationship — no extra configuration needed for the
    // simple cases (we'll do explicit configuration for the trickier
    // relationships, like User <-> Employee, in ApplicationDbContext).
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string JobRole { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // "Active" | "Inactive"
    public DateTime? HireDate { get; set; }

    public Guid? PrimaryFaceProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // The reverse side of the nullable link described in User.cs.
    public User? User { get; set; }
}
