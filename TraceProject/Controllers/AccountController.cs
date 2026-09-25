using HlkHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TraceProject.Data;
using TraceProject.Dto;
using TraceProject.Helpers;
using TraceProject.Models;

namespace TraceProject.Controllers;
 
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly BasicUserManager<User> userManager;
    private readonly ILogger<AccountController> logger;
    private readonly IConfiguration config;


    public AccountController(ApplicationDbContext dbContext, BasicUserManager<User> userManager, ILogger<AccountController> logger, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.logger = logger;
        this.config = config;
    }


    [HttpPost("users/new")]
    public async Task<IActionResult> AddUser(AddUserDto dto, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellation);

            if (user is not null) return BadRequest(CallResult.Error("Account already exists"));

            user = new() 
            {
                Email = dto.Email,
                FullName = dto.FullName,
                Role = dto.Role,
                Code = Utils.GenerateDigitNumbers(5) ,// temp pass code for user to login
                CodeExpiry = DateTime.UtcNow.AddHours(1)
            };

            userManager.SetUserPassword(ref user, user.Code);

            dbContext.Add(user);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok($"Account created Successfully with user pass code: {user.Code}"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(AddUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("users/set-password")]
    [AllowAnonymous]
    public async Task<IActionResult> SetPasswordUser(SetUserPasswordDto dto, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellation);

            if (user is null) return NotFound(CallResult.Error("Account not found"));

            if (string.IsNullOrWhiteSpace(user.Code)) return BadRequest(CallResult.Error("Password is already set"));

            if (user.CodeExpiry < DateTime.UtcNow) return BadRequest(CallResult.Error("Password code has expired"));

            if (dto.Code != user.Code) return BadRequest(CallResult.Error("Invalid or incorrect password code"));

            user.Code = "";
            user.CodeExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            userManager.SetUserPassword(ref user, dto.Password);

            dbContext.Update(user);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Password updated successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(SetPasswordUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }


    [HttpPost("users/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginUserDto dto, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellation);

            if (user is null) return NotFound(CallResult.Error("Account not found"));

            if (!string.IsNullOrWhiteSpace(user.Code)) return BadRequest(CallResult.Error("Password is not set"));

            var isGood = userManager.CheckPassword(user, dto.Password);

            if(!isGood) return BadRequest(CallResult.Error("Invalid credentials"));

            if (!user.IsActive)
                return BadRequest(CallResult.Error("Account is DEACTIVATED, kindly contact admin for assistance"));

            var claims = new List<Claim>()
            {
                  new (ClaimTypes.NameIdentifier, user.Id.ToString()),
                  new (ClaimTypes.Role, user.Role),
            };

            var res = new
            {
                user = new { user.Id, user.Email, user.FullName, user.Role },
                token = Utils.GenerateJwtToken(config, claims, DateTime.UtcNow.AddHours(1))
            };

            return Ok(CallResult.Ok("Login successful", res));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(SetPasswordUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("users/activate")]
    public async Task<IActionResult> ActivateUser(UserActivation dto, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == dto.userId, cancellation);

            if (user is null) return NotFound(CallResult.Error("Account not found"));

            user.IsActive = dto.activate;
            user.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(user);

            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok($"User {(user.IsActive ? "activated" : "deactivated")} successfully"));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(SetPasswordUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUser([FromQuery] PaginationDto? dto, CancellationToken cancellation)
    {
        try
        {
            dto ??= new PaginationDto();

            var query = (dto.Query ?? "").Trim().ToLower();

            var source = dbContext.Users.AsNoTracking();

            var list = await PagedList<User>.CreateAsync(source, dto,
                orderBy: x => x.FullName,
                selector: u => new { u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt },
                whereClause: x => x.Email.ToLower().Contains(query) || x.FullName.ToLower().Contains(query),
               cancellation: cancellation);

            return Ok(CallResult.Ok(data: list));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUserById(long id, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (user is null) 
                return NotFound(CallResult.Error("User not found"));

            return Ok(CallResult.Ok(data: new { user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(GetUserById), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(long id, UpdateUserDto updateDto, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (user is null) 
                return NotFound(CallResult.Error("User not found"));

            // Check if email is already taken by another user
            if (!string.IsNullOrWhiteSpace(updateDto.Email) && updateDto.Email != user.Email)
            {
                var emailExists = await dbContext.Users.AsNoTracking()
                    .AnyAsync(u => u.Email == updateDto.Email && u.Id != id, cancellation);

                if (emailExists)
                    return BadRequest(CallResult.Error("Email already in use"));

                user.Email = updateDto.Email;
            }

            if (!string.IsNullOrWhiteSpace(updateDto.FullName))
                user.FullName = updateDto.FullName;

            if (!string.IsNullOrWhiteSpace(updateDto.Role))
                user.Role = updateDto.Role;

            user.UpdatedAt = DateTime.UtcNow;
            dbContext.Update(user);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok(data: new { user.Id, user.Email, user.FullName, user.Role, user.IsActive }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(UpdateUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(long id, CancellationToken cancellation)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (user is null) 
                return NotFound(CallResult.Error("User not found"));

            dbContext.Remove(user);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("User deleted successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(DeleteUser), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("users/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto changePasswordDto, CancellationToken cancellation)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId)) 
                return Unauthorized(CallResult.Error("User not authenticated"));

            if (!long.TryParse(userId, out var id))
                return BadRequest(CallResult.Error("Invalid user ID"));

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellation);

            if (user is null) 
                return NotFound(CallResult.Error("User not found"));

            var isOldPasswordValid = userManager.CheckPassword(user, changePasswordDto.OldPassword);

            if (!isOldPasswordValid)
                return BadRequest(CallResult.Error("Old password is incorrect"));

            userManager.SetUserPassword(ref user, changePasswordDto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            dbContext.Update(user);
            await dbContext.SaveChangesAsync(cancellation);

            return Ok(CallResult.Ok("Password changed successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(ChangePassword), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

    [HttpPost("users/logout")]
    public IActionResult Logout()
    {
        try
        {
            // SignalR disconnection or token invalidation would be handled on client side
            return Ok(CallResult.Ok("Logout successful"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{action} failed: {error}", nameof(Logout), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
    }

}

