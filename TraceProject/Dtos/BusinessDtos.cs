namespace TraceProject.Dtos;

public record AddEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record UpdateEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record EmployeeActivationDto(long id, bool activate);


public record DepartmentDto(string Name);

public record DepartmentActivationDto(long id, bool active);


public record AddCameraDto(string CameraName, string IPAddress, string Location, long? LocationId = null);

public record UpdateCameraDto(string CameraName, string IPAddress, string Location, long? LocationId = null);

// Settings (single row, edited from the dashboard Settings page)
public record SystemSettingsDto(float RecognitionThreshold, string WorkStartTime, string WorkEndTime, int LateGraceMinutes);

public record CameraActivationDto(long id, bool active);

/// <summary>
/// Phase 7: Live camera status with pipeline metrics
/// </summary>
public record CameraStatusDto(
    long Id,
    string CameraName,
    bool IsConnected,
    bool IsProcessing,
    string ConnectionStatus,
    string? Resolution,
    double? IncomingFps,
    double? ProcessingFps,
    double? LatencyMs,
    long FramesReceived,
    long FramesDropped,
    ulong FacesDetected,
    ulong FacesRecognized,
    ulong UnknownFaces,
    DateTime? LastFrameUtc,
    string? LastError);

// Location DTOs
public record AddLocationDto(string Place);

public record UpdateLocationDto(string Place);

public record LocationDto(long Id, string Place);

// Attendance DTOs
public record AddAttendanceDto(long EmployeeId, long LocationId, DateTime CheckIn, double Confidence, bool IsAllowed);

public record UpdateAttendanceDto(DateTime? CheckOut, double Confidence, bool IsAllowed);

public record AttendanceDto(long Id, long EmployeeId, string EmployeeName, long LocationId, string LocationName, DateTime CheckIn, DateTime? CheckOut, double Confidence, bool IsAllowed);

public record AttendanceFilterDto(int PageNumber = 1, int PageSize = 10, DateTime? StartDate = null, DateTime? EndDate = null, long? EmployeeId = null, long? LocationId = null, string? Query = null);

// Activity DTOs
public record AddActivityDto(long EmployeeId, bool IsActive, string? CurrentApp);

public record UpdateActivityDto(bool IsActive, string? CurrentApp, DateTime? OffTime, DateTime? LockTime);

public record ActivityDto(long Id, long EmployeeId, bool IsActive, string? CurrentApp, DateTime ActiveTime, DateTime OffTime, DateTime LockTime, string State, DateTime? LastSeenAt);

// FaceProfil DTOs
public record AddFaceProfilDto(long EmployeeId, byte[]? RightProfile, byte[]? FrontProfile, byte[]? LeftProfile, byte[]? SmileProfile, byte[]? GlassesProfile);

public record UpdateFaceProfilDto(byte[]? RightProfile, byte[]? FrontProfile, byte[]? LeftProfile, byte[]? SmileProfile, byte[]? GlassesProfile);

public record FaceProfilDto(long Id, long EmployeeId, byte[]? RightProfile, byte[]? FrontProfile, byte[]? LeftProfile, byte[]? SmileProfile, byte[]? GlassesProfile);

// UnknownFace DTOs
public record AddUnknownFaceDto(byte[] face, long LocationId);

public record UnknownFaceDto(long Id, byte[]? face, List<UnknownFaceLocationDto> UnknownFaceLocations);

public record UnknownFaceLocationDto(long Id, long UnknownFaceId, long LocationId, string LocationName);

public record AddUnknownFaceLocationDto(long UnknownFaceId, long LocationId);

public record RegisterUnknownFaceDto(string Email, string FullName, string Position, string PhoneNumber, string EmployeeNumber, long DepartmentId);

public record MatchUnknownFaceDto(long EmployeeId);

// Report DTOs
public record AddReportDto(long EmployeeId, string ReportType, DateTime StartDate, DateTime EndDate);

public record ReportDto(long Id, long EmployeeId, string EmployeeName, string ReportType, DateTime GeneratedAt, DateTime StartDate, DateTime EndDate);

public record ReportFilterDto(int PageNumber = 1, int PageSize = 10, DateTime? StartDate = null, DateTime? EndDate = null, long? EmployeeId = null, long? DepartmentId = null, string? ReportType = null);

/// <summary>
/// Phase 8: Attendance statistics for a period
/// </summary>
public record AttendanceStatsDto(
    long EmployeeId,
    string EmployeeName,
    DateTime StartDate,
    DateTime EndDate,
    int TotalCheckIns,
    int UniqueAttendanceDays,
    double AverageConfidence,
    int FacesRecognized,
    int FacesUnknown);

/// <summary>
/// Phase 8: Camera statistics for a period
/// </summary>
public record CameraStatsDto(
    long CameraId,
    string CameraName,
    DateTime StartDate,
    DateTime EndDate,
    long TotalFramesReceived,
    long TotalFramesDropped,
    ulong TotalFacesDetected,
    ulong TotalFacesRecognized,
    ulong TotalUnknownFaces,
    int UniqueEmployeesDetected,
    double AverageRecognitionConfidence);

// EmployeeAccess DTOs
public record AddEmployeeAccessDto(long EmployeeId, long LocationId);

public record UpdateEmployeeAccessDto(long LocationId);

public record EmployeeAccessDto(long Id, long EmployeeId, string EmployeeName, long LocationId, string LocationName);
