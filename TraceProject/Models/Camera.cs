using HlkHelpers;

namespace TraceProject.Models;

    public class Camera : Auditable 
    {
        public string CameraName { get; set; } = null!;
        public string IPAddress { get; set; } = null!;
        public string Location { get; set; } = null!;
        public bool IsActive { get; set; } 

    }
// Relations between camera and locations must be added
    


