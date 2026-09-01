using Microsoft.AspNetCore.Identity;

namespace TraceProject.Model;

// Same pattern as User: IdentityRole<Guid> gives us Id and Name for free.
// We don't need extra fields on Role itself right now — the RolePermission
// table (added in a later installment) is where CanView/CanEdit/CanDelete
// per page actually live, keeping this class simple.
public class Role : IdentityRole<Guid>
{
}
