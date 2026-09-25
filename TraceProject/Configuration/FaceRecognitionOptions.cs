namespace TraceProject.Configuration;

/// <summary>
/// Configuration options for face recognition (Phase 4).
/// Loaded from appsettings.json under the "FaceRecognition" section.
/// </summary>
public class FaceRecognitionOptions
{
    /// <summary>
    /// Path to the SFace ONNX model file.
    /// Default: "Models/FaceRecognition/face_recognition_sface_2021dec.onnx"
    /// Relative to the application base directory.
    /// </summary>
    public string ModelPath { get; set; } = "Models/FaceRecognition/face_recognition_sface_2021dec.onnx";

    /// <summary>
    /// Similarity threshold for a face to be considered a match.
    /// Range: 0.0 to 1.0
    /// Default: 0.6
    /// If cosine similarity >= threshold, the face is matched to an employee.
    /// If below threshold, the face is considered Unknown.
    /// Lower threshold = more permissive (false positives), higher = more strict (false negatives).
    /// </summary>
    /// 0.363 is the cosine threshold OpenCV recommends for SFace.
    public float SimilarityThreshold { get; set; } = 0.363f;

    /// <summary>
    /// How often to refresh the embedding cache from the database (in seconds).
    /// Default: 60 seconds.
    /// The recognizer loads all active employee embeddings into memory and periodically
    /// refreshes this cache to pick up newly enrolled faces or deactivated embeddings.
    /// Set to 0 to disable periodic refresh (only load once at startup).
    /// </summary>
    public int EmbeddingCacheRefreshSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of embeddings per employee to use for matching.
    /// If an employee has more embeddings than this, the most recent N will be used.
    /// Set to 0 for unlimited.
    /// Default: 0 (no limit).
    /// </summary>
    public int MaxEmbeddingsPerEmployee { get; set; } = 0;
}
