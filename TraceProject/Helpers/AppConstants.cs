using HlkHelpers;

namespace TraceProject.Helpers;

public class AppConstants : Constants
{
    // User roles - used consistently across the application
    public const string AdministratorRole = "Administrator";
    public const string SuperAdminRole = "SuperAdmin";
    public const string ManagerRole = "HR Officer";
    public const string SupervisorRole = "Supervisor";
    public const string UserRole = "User";

    // Common status messages
    public const string SuccessMessage = "Operation completed successfully";
    public const string ErrorMessage = "An error occurred during the operation";
    public const string NotFoundMessage = "Resource not found";
    public const string UnauthorizedMessage = "You are not authorized to perform this action";
}
