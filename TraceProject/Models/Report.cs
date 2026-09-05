using System.Security.Cryptography.X509Certificates;

namespace TraceProject.Models
{
    public class Report
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }


    }

    public enum ReportType { 
    
        Daily,
        weekly,
        Monthly
    
    }

}
