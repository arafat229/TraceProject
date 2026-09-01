using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using HlkHelpers;
using Microsoft.EntityFrameworkCore;

namespace TraceProject.Model;


public class  CompanyUser : BasicUser
{
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime LastUsed { get; set; }

}

[Index(nameof(Email),IsUnique = true)]
    public  class User :IdentityUser<long> {
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; } 
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; } 
    public virtual ICollection<UserRole> UserRoles { get; set; } = [];
    public virtual ICollection<RoleClaim> RoleClaims { get; set; } = [];
    //public FaceProfil FaceProfil { get; set; } = null!;

    }

    public class Role: IdentityRole<long>{
    public virtual ICollection<UserRole> UserRoles { get; set; } = [];
    public virtual ICollection<RoleClaim> RoleClaims { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; }

    }

    public class RoleClaim : IdentityRoleClaim<long>
    {
        public Role Role { get; set; } = null!;
    }       

    public class UserRole : IdentityUserRole<long>
    {
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }

  
