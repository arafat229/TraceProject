using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using TraceProject.Data;
using TraceProject.Dto;
using TraceProject.Helpers;
using TraceProject.Model;

namespace TraceProject.Controllers;
 
[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly UserManager<User> userManager;
    private readonly RoleManager<Role> roleManager;
    private readonly SignInManager<User> signInManager;
    private readonly ILogger<AccountController> log;
    private readonly IConfiguration config;


    public AccountController(ApplicationDbContext dbContext, UserManager<User> userManager, RoleManager<Role> roleManager, SignInManager<User> signInManager, ILogger<AccountController> log, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.roleManager = roleManager;
        this.signInManager = signInManager;
        this.log = log;
        this.config = config;
    }


     [HttpPost("users")]
     [Authorize("AdminOrManager")]
     public async Task<IActionResult> Register(UserRegisterDto dto, CancellationToken ct)
     {
         try
         {
             dto.FullName = dto.FullName.Trim();
             dto.PhoneNumber = dto.PhoneNumber.Trim();
             dto.Email = dto.Email.Trim();
             dto.Password = dto.Password;
             dto.Role = dto.Role.Trim().ToTitleCase();


             var existingUser = await userManager.FindByEmailAsync(dto.Email);

             if (existingUser is not null)
                 return BadRequest(CallResult.Error("Account already exists "));


             var result = await roleManager.FindByNameAsync(dto.Role);

             if (result is null)
                 return NotFound(CallResult.Error("Role not found"));


             var userToCreate = new User()
             {
                 FullName = dto.FullName,
                 UserName = dto.UserName,
                 Email = dto.Email,
                 PhoneNumber = dto.PhoneNumber,
                 CreatedAt = DateTime.Now.ToUniversalTime(),
                 UpdatedAt = DateTime.Now.ToUniversalTime(),
                 IsActive = true

             };

             var res = await userManager.CreateAsync(userToCreate, dto.Password);


             if (!res.Succeeded)
                 return BadRequest(CallResult.Error("Failed to create the user", res.Errors.Select(x => x.Description)));


             var createdUser =  await dbContext.Users.FirstOrDefaultAsync(x => x.PhoneNumber == dto.PhoneNumber);


             var roleResult = await userManager.AddToRoleAsync(createdUser, dto.Role);


             if (!roleResult.Succeeded)
             {
                 return BadRequest(CallResult.Error("Fail to assign a role to the user", roleResult.Errors.Select(x => x.Description)));
             }

             await dbContext.SaveChangesAsync();

             return Ok(CallResult.Ok($"user {createdUser.FullName} account is created successfully"));

         }
         catch (Exception ex)
         {
             //Console.WriteLine(ex.StackTrace);
             log.LogError(ex, "{action} failed : {error}", nameof(Register), ex.GetFullErrorMessage());
             return BadRequest(CallResult.Error(ex));
         }


     }

     



    [HttpPost("login")]
    public async Task<IActionResult> Login(UserLoginDto dto)
    {
        try
        {
            var existingUser = dbContext.Users.FirstOrDefault(x => x.Email == dto.Email);

            if (existingUser is null)
                return NotFound(CallResult.Error("Invalid Credentials"));

            if (existingUser is { IsActive: false })
                return Unauthorized(CallResult.Error("User is not active"));


            var result = await signInManager.CheckPasswordSignInAsync(existingUser, dto.Password, true);

            if (!result.Succeeded)
            {
                return Unauthorized(CallResult.Error("Invalid email or password"));
            }

            List<Claim> claims = new List<Claim>{
                new Claim(ClaimTypes.NameIdentifier, existingUser.Id.ToString()),
                new Claim (ClaimTypes.Name, existingUser.FullName),
                new Claim(ClaimTypes.Email, existingUser.Email??""),
                new Claim(ClaimTypes.MobilePhone, existingUser.PhoneNumber??"")

            };

            var roles = await userManager.GetRolesAsync(existingUser);
            var role = "";


            if (roles.Count >= 1)
            {

                role = roles[0];
                var existingRole = await roleManager.FindByNameAsync(role);

                if (existingRole is not null)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                    claims.AddRange(await roleManager.GetClaimsAsync(existingRole));
                }
            }

            List<string> roleClaims = new();

            foreach (var claim in claims)
            {
                //if(claim.Type == Constants.Privilege && claim.Value != null) {
                //  roleClaims.Add(claim.Value);

                //};

                if (claim is { Type: Constants.Privilege, Value: not null })
                {
                    roleClaims.Add(claim.Value);
                }
            };
            var userObj = new UserDetailsDto
            {
                Id = existingUser.Id,
                FullName = existingUser.FullName,
                Email = existingUser.Email,
                PhoneNumber = existingUser.PhoneNumber,
                Role = role,
                Permission = roleClaims,
                IsActive = existingUser.IsActive,
                CreatedAt = existingUser.CreatedAt,
            };


            var data = new
            {
                User = userObj,
                Token = Helpers.Utilities.GenerateJwtToken(config, claims, DateTime.UtcNow.AddDays(2))

            };

            return Ok(CallResult.Ok("Login Succesfully", data));


        }
        catch (Exception ex)
        {

            log.LogError(ex, "{action} failed: {error}", nameof(Login), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }

    }

    [Authorize("AdminOnly")]
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(RoleDto dto)
    {
        try
        {

            dto.Name = dto.Name.Trim().ToTitleCase();


            var existingRole = await dbContext.Roles.FirstOrDefaultAsync(x => x.Name == dto.Name);

            if (existingRole is not null)
                return BadRequest(CallResult.Error("Role already exists", HttpStatusCode.BadRequest));


            var role = new Role { Name = dto.Name.Trim().ToTitleCase(), NormalizedName = dto.Name.Trim().ToUpper() };

            var res = await roleManager.CreateAsync(role);

            if (res.Succeeded)
            {

                    foreach (var claim in dto.Claims.Distinct())
                    {
                        await roleManager.AddClaimAsync(role, new Claim(Constants.Privilege, claim));
                        await dbContext.Roles.AddAsync(role);


                    if (await dbContext.SaveChangesAsync() <= 0)
                        return BadRequest(CallResult.Error("Failed to save role"));
                    

                }
                return CreatedAtAction(nameof(CreateRole), new { id = role.Id }, CallResult.Ok("Role created successfully", role));

            }
            else
            {
                return BadRequest(CallResult.Error("Failed to create role", res.Errors.Select(e => e.Description)));
            }
               
        }
        catch (Exception ex)
        {
            log.LogError(ex, "{action} failed: {error}", nameof(CreateRole), ex.GetFullErrorMessage());
            return BadRequest(CallResult.Error(ex));
        }
     
    }

}

 