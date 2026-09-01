using Microsoft.EntityFrameworkCore;

namespace TraceProject.Model;

[Index(nameof(EmployeeId), IsUnique = true)]
    public class Activity
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        public  bool IsActive { get; set; }
        public string? CurrentApp { get; set; }
        public DateTime ActiveTime { get; set; }
        public DateTime OffTime { get; set; }
        public DateTime LockTime { get; set; }

    }

