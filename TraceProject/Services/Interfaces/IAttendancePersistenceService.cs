using Microsoft.EntityFrameworkCore;
using TraceProject.Data;
using TraceProject.Models;

namespace TraceProject.Services.Interfaces;

/// <summary>
/// Phase 5: Service for persisting face recognition results as attendance records.
/// - Creates Attendance records only when:
///   1. Face recognized (IsMatch = true, confidence >= threshold)
///   2. Employee is active (IsActive = true)
///   3. No database errors crash the pipeline
/// - Skips Attendance for Unknown faces (IsMatch = false)
/// </summary>
public interface IAttendancePersistenceService
{
    /// <summary>
    /// Persist recognition result as attendance record (if conditions are met).
    /// </summary>
    /// <returns>Created Attendance record, or null if conditions not met or error occurred</returns>
    Task<Attendance?> PersistRecognitionResultAsync(
        RecognitionResult result,
        long? cameraId,
        long? locationId,
        CancellationToken cancellationToken = default);
}
