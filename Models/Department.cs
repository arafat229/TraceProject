namespace TraceProject.Model;

// A plain entity — no Identity involved here, just a regular EF Core table.
// Notice this does NOT inherit from anything: that's the normal case for
// most of our models. Only User and Role are special because Identity
// needs them to be.
public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation property: lets you write department.Employees in code and
    // have EF Core automatically fetch every Employee whose DepartmentId
    // matches this department's Id. It doesn't create a database column —
    // it's purely a convenience EF Core builds from the foreign key below.
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
