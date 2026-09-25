using TraceProject.Models;

namespace TraceProject.Services.Interfaces;

/// <summary>
/// Interface for face recognition.
/// Phase 4: Abstraction over face recognition implementation (SFace via OpenCvSharp4).
/// Matches a detected face against enrolled employee embeddings.
/// This is RECOGNITION — determines "whose face is this" given a detected face.
/// </summary>
public interface IFaceRecognizer : IAsyncDisposable
{
    /// <summary>
    /// Recognize who a detected face belongs to.
    /// Compares the face embedding against all active employee embeddings
    /// and returns the best match if it exceeds the similarity threshold.
    /// </summary>
    /// <param name="face">The detected face to recognize (includes cropped image).</param>
    /// <param name="cancellationToken">Cancellation token to stop recognition early.</param>
    /// <returns>
    /// RecognitionResult with EmployeeId (if matched), confidence score, and IsMatch flag.
    /// If no employee embedding exceeds the similarity threshold, IsMatch = false and EmployeeId = null (Unknown).
    /// </returns>
    /// <remarks>
    /// Thread-safe. Can be called from multiple threads concurrently.
    /// Exceptions from native OpenCV code are caught, logged, and re-thrown as appropriate.
    /// This method should be fast (typically under 50ms per face on CPU).
    /// </remarks>
    Task<RecognitionResult> RecognizeAsync(
        DetectedFace face,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute an embedding vector from a face crop image.
    /// Used by enrollment and recognition internally.
    /// </summary>
    /// <param name="face">The detected/cropped face to compute embedding for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding as a byte array (float32 values), or null if computation fails.</returns>
    Task<byte[]?> ComputeEmbeddingAsync(
        DetectedFace face,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the recognizer is ready (model loaded and embeddings cached).
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Force a refresh of the employee embedding cache from the database.
    /// Called periodically by the recognizer, but can be triggered manually if needed.
    /// Loads all active FaceEmbeddings for active employees into memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// This operation may be expensive if there are many employees/embeddings.
    /// Should not be called in the recognition loop; call on startup or via admin API.
    /// </remarks>
    Task RefreshEmbeddingCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statistics about the current embedding cache (for logging/debug).
    /// </summary>
    /// <returns>A tuple of (total embeddings cached, total employees with embeddings, last refresh time).</returns>
    (int TotalEmbeddings, int EmployeesWithEmbeddings, DateTime LastRefreshUtc) GetCacheStatistics();
}
