namespace TraceProject.Model;
    public class Camera
    {
        public long Id { get; set; }
        public string CameraName { get; set; } = null!;
        public long IPAddress { get; set; }
        public string Location { get; set; } = null!;
        //public List<EmployeeCamera> EmployeeCameras { get; set; } = [];

    }
// Relations between camera and locations must be added
    


