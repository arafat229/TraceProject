namespace TraceProject.Services.Interfaces;

using TraceProject.Models;

/// <summary>
/// Service for independent frame processing (detection, analytics, etc.).
/// Phase 3: Separates frame detection from the RTSP capture loop to keep camera responsive.
/// Processes frames at a configurable target rate (TargetProcessingFps), independent of the camera's stream FPS.
/// Phase 7: Exposes pipeline metrics for live status endpoints.
/// </summary>
public interface IFrameProcessingService : IAsyncDisposable
{
    /// <summary>
    /// Get the latest detection results (faces detected in the most recent processed frame).
    /// May be null if no frames have been processed yet.
    /// Returns the results without modifying internal state.
    /// </summary>
    (IReadOnlyList<DetectedFace> Faces, DateTime ProcessedAtUtc)? GetLatestDetectionResults();

    /// <summary>
    /// Indicates whether the frame processing service is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Phase 7: Get current pipeline metrics for status reporting.
    /// </summary>
    (ulong FacesDetected, ulong FacesRecognized, ulong UnknownFaces) GetPipelineMetrics();
}
