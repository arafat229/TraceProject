namespace TraceProject.Dto;


public record AddUserDto(string Email, string FullName, string Role);

public record LoginUserDto(string Email, string Password);

public record SetUserPasswordDto(string Email, string Password); 

public record UserActivation(long userId, bool activate); 
