using HlkHelpers;
using TraceProject.Helpers;
using TraceProject.Models;

namespace TraceProject.Data;

public class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {

        var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<BasicUserManager<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedData>>();

        try
        {

            if (!db.Users.Any())
            {
                var user = new User
                {
                    Email = "admin@trace.com",
                    FullName = "Admin User",
                    IsActive = true,
                    Role = AppConstants.AdministratorRole, 
                };
                userManager.SetUserPassword(ref user, "P@ssw0rd");
                db.Add(user);
                await db.SaveChangesAsync();

            }
        } catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
            logger.LogError(ex.Message, "Failed to seed the database");
        }

    }

}

