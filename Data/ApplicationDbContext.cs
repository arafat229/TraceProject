using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TraceProject.Model;

namespace TraceProject.Data;

// IdentityDbContext<User, Role, Guid> instead of plain DbContext:
// this one line automatically creates all the Identity tables you never
// have to think about — AspNetUsers, AspNetRoles, AspNetUserRoles,
// AspNetUserClaims, AspNetRoleClaims, etc. Our own tables (below) sit
// alongside them in the same database.
public class ApplicationDbContext : IdentityDbContext<User, Role, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Each DbSet<T> becomes one table. This is what lets you write
    // dbContext.Employees, dbContext.RefreshTokens, etc. in your controllers.
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // More DbSets (FaceProfile, Attendance, Camera, UnknownFace, ActivityLog,
    // ApplicationUsage, Report, SystemSettings, RolePermission, UserPreference)
    // will be added here in the next installment, once those models exist.

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // IMPORTANT: always call the base implementation first when
        // inheriting from IdentityDbContext — it sets up all the Identity
        // table configuration. Forgetting this is a common source of very
        // confusing EF Core errors.
        base.OnModelCreating(builder);

        // Configure the nullable one-to-one relationship between User and
        // Employee explicitly, because EF Core can't safely guess this one
        // on its own (a plain nullable foreign key on User could just as
        // easily be a one-to-many). This says: "one User optionally has one
        // Employee, linked via User.EmployeeId, and if that Employee is
        // deleted, don't delete the User along with it" (Restrict instead
        // of the default Cascade, which could otherwise silently delete a
        // login account just because someone deleted an employee record).
        builder.Entity<User>()
            .HasOne(u => u.Employee)
            .WithOne(e => e.User)
            .HasForeignKey<User>(u => u.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Renaming Identity's default table names from "AspNetUsers" etc. to
        // something cleaner is optional — skipped here so the defaults just
        // work. If you want custom table names later, this is where they'd go.
    }
}
