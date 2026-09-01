using System.ComponentModel.DataAnnotations.Schema;

namespace TraceProject.Model;
    public class Employee
    {
        public long Id { get; set; }
        public long DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public List<Attendance> Attendances { get; set; } = [];
        public List<EmployeeAccess> EmployeeLocations { get; set; } = [];  
        public string EmployeeNumber { get; set; } = null!;
        public string Position { get; set; } = null!;
        public bool IsActive { get; set; } 

    }

    public class Attendance
    {
    public Employee Employee { get; set; } = null!;
    public long EmployeeId { get; set; }
    public long Id { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    [NotMapped]
    public TimeSpan Duration => CheckOut - CheckIn;
    public double Confidence { get; set; }
    public bool IsAllowed { get; set; }
    public long LocationId { get; set; }
    public Location Location { get; set; } = null!;

    }

public class EmployeeAccess
{
    public long Id { get; set; }
    public DateTime AddedOn { get; set; }
    public Employee Employee { get; set; } = null!;
    public long EmployeeId { get; set; }
    public long LocationId { get; set; }
    public Location Location { get; set; } = null!;

}

public class Location
{
    public long Id { get; set; }
    public string Place { get; set; } = null!;

}



