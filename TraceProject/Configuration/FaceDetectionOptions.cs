namespace TraceProject.Configuration;

/// <summary>
/// Configuration for face detection (Phase 3).
/// Bind from appsettings.json "FaceDetection" section.
/// Controls model loading, confidence threshold, and detection behavior.
/// </summary>
public class FaceDetectionOptions
{
    /// <summary>
    /// Path to the YuNet ONNX model file.
    /// Can be relative to the application's working directory or absolute.
    /// Example: "Models/FaceDetection/face_detection_yunet_2023mar.onnx"
    /// The model file must be downloaded from:
    /// https://github.com/opencv/opencv_zoo/raw/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx
    /// </summary>
    public string ModelPath { get; set; } = "Models/FaceDetection/face_detection_yunet_2023mar.onnx";

    /// <summary>
    /// Confidence threshold for face detection (0.0 to 1.0).
    /// Faces with confidence below this threshold are filtered out before returning.
    /// Example: 0.6 = 60% confidence minimum.
    /// Higher values (e.g., 0.7+) reduce false positives but may miss valid faces.
    /// Lower values (e.g., 0.5-) may detect more faces but with more false positives.
    /// Recommended range: 0.5 to 0.7.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.6;
}
