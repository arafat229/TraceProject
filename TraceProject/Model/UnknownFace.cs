namespace TraceProject.Model
{
    public class UnknownFace
    {
        public long Id { get; set; }
        public byte[]? face {  get; set; }
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
