using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using HlkHelpers;
using Microsoft.EntityFrameworkCore;

namespace TraceProject.Model;


[Index(nameof(Email), IsUnique = true)]
public class User : BasicUser
{
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Code { get; set; } = "";
    public DateTime? CodeExpiry { get; set; } 
    public bool IsActive { get; set; } = true;

}




  
