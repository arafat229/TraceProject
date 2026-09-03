namespace TraceProject.Dtos;

public record AddEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record UpdateEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record EmployeeActivationDto(long id, bool activate);


public record DepartmentDto(string Name);  

public record DepartmentActivationDto(long id, bool active);


public record AddCameraDto(string CameraName, string IPAddress, string Location);

public record UpdateCameraDto(string CameraName, string IPAddress, string Location);

public record CameraActivationDto(long id, bool active); 
