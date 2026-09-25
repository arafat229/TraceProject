namespace TraceProject.Dto;


public record AddUserDto(string Email, string FullName, string Role);

public record LoginUserDto(string Email, string Password);

public record SetUserPasswordDto(string Email, string Code, string Password); 

public record UserActivation(long userId, bool activate);

public record UpdateUserDto(string? Email = null, string? FullName = null, string? Role = null);

public record ChangePasswordDto(string OldPassword, string NewPassword);
