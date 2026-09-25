namespace TraceProject.Models;

/// <summary>
/// Result of a face recognition operation.
/// Phase 4: Output of IFaceRecognizer.RecognizeAsync().
/// Contains the matched employee ID (if found), confidence/similarity score, and match status.
/// </summary>
public class RecognitionResult
{
    /// <summary>
    /// The ID of the matched employee, or null if no match found.
    /// </summary>
    public long? EmployeeId { get; set; }

    /// <summary>
    /// The name of the matched employee (loaded from DB for convenience in logging).
    /// Null if no match found.
    /// </summary>
    public string? MatchedEmployeeName { get; set; }

    /// <summary>
    /// The similarity/confidence score for this match.
    /// Range: 0.0 to 1.0
    /// For cosine similarity: 1.0 = identical, 0.0 = completely different.
    /// For L2 distance (Euclidean): lower is better; converted to 0-1 scale where 1.0 = best match.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Whether this result represents a valid match.
    /// True if Confidence >= configured SimilarityThreshold AND EmployeeId is not null.
    /// False if no employee embedding exceeded the threshold (Unknown person).
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Optional: the matching embedding source (for debugging/audit).
    /// Shows which enrollment photo / angle this match came from.
    /// </summary>
    public string? MatchedEmbeddingSource { get; set; }

    /// <summary>
    /// Timestamp (UTC) when this recognition was computed.
    /// </summary>
    public DateTime RecognitionTimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// L2-normalized SFace embedding of the face (set for matches and unknowns when available).
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Create an "Unknown" result (no match found).
    /// </summary>
    public static RecognitionResult Unknown() => new()
    {
        EmployeeId = null,
        MatchedEmployeeName = null,
        Confidence = 0f,
        IsMatch = false,
        MatchedEmbeddingSource = null,
        RecognitionTimestampUtc = DateTime.UtcNow
    };

    /// <summary>
    /// Create a match result.
    /// </summary>
    public static RecognitionResult Match(long employeeId, string employeeName, float confidence, string? embeddingSource = null) => new()
    {
        EmployeeId = employeeId,
        MatchedEmployeeName = employeeName,
        Confidence = confidence,
        IsMatch = true,
        MatchedEmbeddingSource = embeddingSource,
        RecognitionTimestampUtc = DateTime.UtcNow
    };
}
