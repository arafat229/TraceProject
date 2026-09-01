using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TraceProject.Helpers;
using TraceProject.Model;

namespace TraceProject.Data;
    public class SeedData
    {
       static private readonly List<string> roles = new()
        {

            Privileges.canCreateUser,
            Privileges.canViewUser,
            Privileges.canUpdateUser,
            Privileges.canDeleteUser,
            Privileges.canCreateRole,
            Privileges.canViewRole,
            Privileges.canUpdateRole,
            Privileges.canDeleteRole,
            Privileges.canCreateSettings,
            Privileges.canViewSetttings,
            Privileges.canUpdateSettings,
            Privileges.canDeleteSettings,
            Privileges.canViewReports,
            Privileges.canViewDashBoard
 
        };


        public static async Task Initialize(IServiceProvider serviceProvider)
        {

            var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var log = scope.ServiceProvider.GetRequiredService<ILogger<SeedData>>();

                    try
                    {

                        if (!(userManager.Users.Any() && roleManager.Roles.Any()))
                        {
                           

                            await SetRole(context, roleManager, Constants.SuperAdminRole);

                            var user = new User
                            {
                                FullName = Constants.SuperAdminRole,
                                UserName = Constants.SuperAdminRole,
                                Email = Constants.SuperAdminEmail,
                                PhoneNumber = Constants.SuperAdminPhoneNumber,
                                IsActive = true,
                              
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                            };
                            var result = await userManager.FindByEmailAsync(user.Email);
                            if (result is null)
                            {

                                var res = await userManager.CreateAsync(user,"Admin@passw0rd");
                                

                                if (res.Succeeded)
                                {
                                    var roleToBeCreated = await roleManager.FindByNameAsync(Constants.SuperAdminRole);

                                    if(roleToBeCreated is not null)
                                     {
                                       await userManager.AddToRoleAsync(user, Constants.SuperAdminRole);

                                      await context.SaveChangesAsync(); 
                                     }
                                 
                                }

                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.StackTrace);
                        log.LogError(ex.Message,"Failed to seed the database");
                    }


        }
    

        private static async Task SetRole(ApplicationDbContext dBContext, RoleManager <Role> roleManager, string roleName)
        {
            var AdminRole = new Role { Name = roleName.ToTitleCase(), NormalizedName = roleName.ToUpper() };

            var existingRole =  await roleManager.FindByNameAsync(roleName.ToTitleCase());

            if (existingRole is null)
            {
                var res = await roleManager.CreateAsync(AdminRole);

                if (res.Succeeded)
                {
                    foreach(var role in roles.Distinct())
                    {
                        await roleManager.AddClaimAsync(AdminRole, new Claim(Constants.Privilege, role));
                    }
            
                }

            }
            else
            {

                foreach (var role in roles)
                {

               
                var existingClaim = await dBContext.RoleClaims.FirstOrDefaultAsync(x => (x.RoleId == existingRole.Id && x.ClaimValue == role));
                
                    
                //var existingClaim = existingRole.RoleClaims.FirstOrDefault(x => x.ClaimType == Constants.Privilege && x.ClaimValue == role);

                    if (existingClaim is null)
                    {
                        await roleManager.AddClaimAsync(existingRole, new Claim(Constants.Privilege, role));
                    }


                }

            }
            //await dBContext.SaveChangesAsync();
        }

    }

