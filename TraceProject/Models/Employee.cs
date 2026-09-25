using HlkHelpers;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraceProject.Models;

public class Employee : Auditable
{
    public long DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public List<Attendance> Attendances { get; set; } = [];
    public List<EmployeeAccess> EmployeeLocations { get; set; } = [];
    public List<FaceEmbedding> FaceEmbeddings { get; set; } = [];
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!; 
    public string EmployeeNumber { get; set; } = null!;
    public string Position { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}



