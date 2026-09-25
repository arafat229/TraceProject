using HlkHelpers;

namespace TraceProject.Models;

public class EmployeeAccess : Auditable
{
    public Employee Employee { get; set; } = null!;
    public long EmployeeId { get; set; }
    public long LocationId { get; set; }
    public Location Location { get; set; } = null!;
}
