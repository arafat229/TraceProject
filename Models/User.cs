using Microsoft.AspNetCore.Identity;

namespace TraceProject.Model;

// Inherits from IdentityUser<Guid> instead of the plain IdentityUser.
// IdentityUser already gives us: Id, UserName, Email, PasswordHash,
// PhoneNumber, and all the login/lockout plumbing — for free, already
// battle-tested, so we never touch password hashing ourselves.
//
// The <Guid> tells Identity to use a Guid as the primary key type instead of
// its default string. This matches Guid Id usage in your AccountController
// (existingUser.Id.ToString() would work with either, but Guid keys avoid
// accidental key collisions and match the rest of our entities below).
//
// Everything below the inherited properties is what WE added on top —
// these map directly to the "ApplicationUser" fields we designed together.
public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    // Nullable on purpose: not every login account corresponds to a tracked
    // Employee (e.g. a pure IT Administrator), and not every Employee has
    // login access. This is the same design decision from our ER diagram.
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
