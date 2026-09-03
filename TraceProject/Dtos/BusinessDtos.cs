namespace TraceProject.Dtos;

public record AddEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record UpdateEmployeeDto(long DepartmentId, string FullName, string EmployeeNumber, string Position, string Email, string PhoneNumber);

public record EmployeeActivationDto(long id, bool activate);


