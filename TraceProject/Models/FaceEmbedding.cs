using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraceProject.Models;

/// <summary>
/// Represents a face embedding vector for an employee.
/// Phase 4: Embedding-based face recognition.
/// Stores the fixed-length embedding vector computed from an enrolled face photo.
/// One Employee can have multiple embeddings from different enrollment angles/photos.
/// 
/// Note: FaceProfil (which stores raw per-angle photos) is kept for backward compatibility.
/// This entity is the new mechanism for face matching via embeddings.
/// </summary>
public class FaceEmbedding
{
    /// <summary>
    /// Unique identifier for this embedding record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to Employee.
    /// Supports one-to-many: one Employee can have multiple embeddings.
    /// </summary>
    [Required]
    public long EmployeeId { get; set; }

    /// <summary>
    /// Navigation property to the associated Employee.
    /// </summary>
    public Employee? Employee { get; set; }

    /// <summary>
    /// The embedding vector as raw bytes.
    /// Format: float32 array stored as bytea in PostgreSQL.
    /// Size: EmbeddingDimension * 4 bytes (for float32).
    /// Example: 128-dim SFace = 128 * 4 = 512 bytes.
    /// </summary>
    [Required]
    public byte[] EmbeddingData { get; set; } = null!;

    /// <summary>
    /// The name/version of the model used to generate this embedding.
    /// Example: "SFace", "SFace_v1", allows for future model changes and validation.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ModelName { get; set; } = "SFace";

    /// <summary>
    /// The dimensionality of the embedding vector.
    /// Typically 128 for SFace. Stored for validation when loading embeddings.
    /// </summary>
    [Required]
    public int EmbeddingDimension { get; set; }

    /// <summary>
    /// Whether this embedding is currently active for matching.
    /// False for deprecated or problematic embeddings.
    /// Allows soft-deletion or temporary disabling without losing historical data.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp (UTC) when this embedding was created.
    /// </summary>
    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp (UTC) when this embedding was last updated (if re-enrolled, marked inactive, etc.).
    /// </summary>
    [Required]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional: source info describing where this embedding came from.
    /// Example: "front_profile_enrollment_20250922", helps track enrollment context.
    /// </summary>
    [MaxLength(255)]
    public string? Source { get; set; }
}
