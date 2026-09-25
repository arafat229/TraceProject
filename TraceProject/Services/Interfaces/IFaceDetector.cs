namespace TraceProject.Services.Interfaces;

using TraceProject.Models;

/// <summary>
/// Interface for face detection.
/// Phase 3: Abstraction over face detection implementation (YuNet via OpenCvSharp4).
/// Performs face detection on a video frame and returns detected faces with bounding boxes.
/// This is DETECTION ONLY — determines "is there a face and where" but NOT "whose face is it".
/// </summary>
public interface IFaceDetector : IAsyncDisposable
{
    /// <summary>
    /// Detect faces in the provided video frame.
    /// Runs detector on a copy of the frame; does not modify the original.
    /// </summary>
    /// <param name="frame">The video frame to analyze. May be in any format; detector handles conversion.</param>
    /// <param name="cancellationToken">Cancellation token to stop detection early.</param>
    /// <returns>
    /// List of detected faces, each with bounding box, confidence, and cropped face image.
    /// Returns empty list if no faces are detected.
    /// Only faces meeting the confidence threshold are returned.
    /// </returns>
    /// <remarks>
    /// Thread-safe. Can be called from multiple threads concurrently, but the underlying
    /// detector (e.g., OpenCV's FaceDetectorYN) should be called sequentially for best performance.
    /// Exceptions from native OpenCV code are caught, logged, and re-thrown as appropriate.
    /// </remarks>
    Task<IReadOnlyList<DetectedFace>> DetectFacesAsync(
        IVideoFrame frame,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the detector is ready (model loaded and initialized).
    /// </summary>
    bool IsReady { get; }
}
