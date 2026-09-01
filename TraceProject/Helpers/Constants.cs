namespace TraceProject.Helpers;

    public class Constants
    {
        //Roles
        public const string SuperAdminRole = "Administrator";
        public const string ManagerRole = "HR Officer";
        public const string SupervisorRole = "Supervisor";

        //Constants
        public const string SuperAdmin = "superAdmin";
        public const string SuperAdminPhoneNumber = "00000000001";
        public const string SuperAdminEmail = "superAdmin@App.com";
        public const string Manager = "manager";
        public const string ManagerEmail = "manager@App.com";
        public const string SuperAdminPhotoUrl = "manager@App.com";
        public const string SuperAdminPassword = "es12_dcn-sksaSLM+"; 
        public const string Privilege = "privilege";
        public const string FullName = "fullName";
        public const string FirstName = "lastName";
        public const string Email = "email";
        public const string PhoneNumber = "PhoneNumber";
        public const string sex = "sex";
        public const string AllInRole = "allIniRole";
    public const string Department = "AdminDepartment"; 


        public const string SuccessMessage = "Operation Executed Sucessfully";
        public const string FailedMessage = "Operation Failed";

        public const string IsAllowed = "IsAllowed";

        public List<string> BaseRole = new() { SuperAdminRole.ToLower(), ManagerRole.ToLower(), SuperAdminRole.ToLower() };


    }
    public class Privileges
    {
        //Privileges on User Model 
        public const string canViewUser = "canViewUser";
        public const string canCreateUser = "canCreateUser";
        public const string canUpdateUser = "canUpdateUser";
        public const string canDeleteUser = "canDeleteUser";


        //privileges on Role Model 
        public const string canViewRole = "canViewRole";
        public const string canCreateRole = "canCreateRole";
        public const string canUpdateRole = "canUpdateRole";
        public const string canDeleteRole = "canDeleteRole";

        //privileges on Settings 
        public const string canViewSetttings = "canViewSettings";
        public const string canCreateSettings = "canCreateSettings";
        public const string canUpdateSettings = "canUpdateSettings";
        public const string canDeleteSettings = "canDeleteSettings";

        public const string canViewDashBoard = "canViewDashBoard";
        public const string canViewReports = "canViewReport";
        public const string canCreateReports = "canCreateReport";



    }

