namespace TraceProject.Configuration;

/// <summary>
/// Face detection settings, from the "FaceDetection" section. See Services/FaceDetection/FaceQuality.cs
/// for how the recognition thresholds were chosen.
/// </summary>
public class FaceDetectionOptions
{
    public string ModelPath { get; set; } = "Models/FaceDetection/face_detection_yunet_2023mar.onnx";

    /// <summary>YuNet score above which a face is detected at all (counted, shown in metrics).</summary>
    public double ConfidenceThreshold { get; set; } = 0.6;

    /// <summary>
    /// Detections below this score are never recognized. Below ~0.85 YuNet also fires on hands
    /// and fabric, and those can land close to an employee's embedding.
    /// </summary>
    public float MinRecognitionScore { get; set; } = 0.85f;

    /// <summary>Smallest face side, in camera pixels, that is recognized.</summary>
    public int MinRecognitionFaceSize { get; set; } = 60;

    /// <summary>Largest share of the aligned face allowed outside the picture.</summary>
    public double MaxCutOffFraction { get; set; } = 0.15;

    /// <summary>Faces blurrier than this (variance of the Laplacian) are not recognized.</summary>
    public double MinSharpness { get; set; } = 100;
}
