namespace TraceProject.Models
{
    public class UnknownFace
    {
        public long Id { get; set; }
        /// <summary>JPEG of the aligned 112x112 face.</summary>
        public byte[]? face {  get; set; }
        public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
        public long? CameraId { get; set; }
        /// <summary>Face detector score (0-1).</summary>
        public float DetectionScore { get; set; }
        public List<UnknownFaceLocation>? UnknownFaceLocations { get; set; }
       
    }

    public class UnknownFaceLocation
    {
        public long Id { get; set; }
        public UnknownFace? UnknownFace { get; set; }
        public long UnknownFaceId { get; set; }
        public Location Location { get; set; } = null!;
        public long LocationId { get; set; }
    } 


}
