namespace TraceProject.Models;

/// <summary>
/// Represents a detected face in a video frame.
/// Phase 3: Detection results from the YuNet face detector.
/// Contains bounding box, confidence score, and cropped face image region.
/// </summary>
public class DetectedFace
{
    /// <summary>
    /// Bounding box coordinates and size.
    /// (X, Y, Width, Height) in original frame pixel coordinates.
    /// X, Y = top-left corner of the box.
    /// </summary>
    public required (int X, int Y, int Width, int Height) BoundingBox { get; init; }

    /// <summary>
    /// Detection confidence score.
    /// Range: 0.0 to 1.0, where 1.0 = 100% confidence.
    /// Filtered by ConfidenceThreshold before returning from detector.
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Cropped face region image data.
    /// Raw bytes in BGR format (OpenCV standard), ready for further processing.
    /// Dimensions: width × height of the bounding box.
    /// </summary>
    public required byte[] FaceImageData { get; init; }

    /// <summary>
    /// Width of the cropped face image in pixels.
    /// </summary>
    public required int FaceImageWidth { get; init; }

    /// <summary>
    /// Height of the cropped face image in pixels.
    /// </summary>
    public required int FaceImageHeight { get; init; }

    /// <summary>
    /// Timestamp (UTC) when this face was detected.
    /// Copied from the source frame's TimestampUtc.
    /// </summary>
    public required DateTime DetectionTimestampUtc { get; init; }
}
