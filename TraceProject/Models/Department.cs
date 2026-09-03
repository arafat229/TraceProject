using HlkHelpers;

namespace TraceProject.Models
{
    public class Department : Auditable
    {
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } 
        public virtual List<Employee> Employees { get; set; } = [];
    }
}
