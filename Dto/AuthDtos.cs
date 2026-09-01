namespace TraceProject.Dto;

// "Dto" = Data Transfer Object. This is the exact JSON shape the frontend
// sends in the request body for POST /api/users (Register). Notice it looks
// similar to the User model but isn't the same class — this one has a
// plain-text Password field (which the User model deliberately does NOT
// have; Identity only ever stores a PasswordHash).
public class UserRegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserLoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// The shape returned to the frontend after a successful login — this is
// what becomes `user` in authApi.login()'s response on the React side.
public class UserDetailsDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Permission { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
