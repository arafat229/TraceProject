using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TraceProject.Model;

namespace TraceProject.Data;

public class ApplicationDbContext : IdentityDbContext<User, Role, long,
    IdentityUserClaim<long>, UserRole, IdentityUserLogin<long>,
    IdentityRoleClaim<long>, IdentityUserToken<long>>
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {

    }
    public DbSet<Camera> Cameras { get; set; } = null!;
    public DbSet<FaceProfil> FaceProfil { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<Attendance> Attendances { get; set; } = null!;
    public DbSet<UnknownFace> UnkownFaces { get; set; } = null!;
    public DbSet<CompanyUser> CompanyUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserRole>(userRole =>
        {
            userRole.HasKey(u => new { u.RoleId, u.UserId });

            userRole.HasOne(u => u.User)
              .WithMany(u => u.UserRoles)
              .HasForeignKey(u => u.UserId)
              .IsRequired();

            userRole.HasOne(u => u.Role)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(u => u.RoleId)
            .IsRequired();
        });

        builder.Entity<RoleClaim>(r =>
        {
            r.HasOne(r => r.Role)
            .WithMany(r => r.RoleClaims)
            .HasForeignKey(r => r.RoleId)
            .IsRequired();
        });

        builder.Entity<Attendance>(attendance =>
        {

            attendance
            .HasOne(x => x.Employee)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired();

            attendance
            .HasOne(x=>x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .IsRequired();

        });

        builder.Entity<EmployeeAccess>(employeeAccess =>
        {
           employeeAccess
            .HasOne(x => x.Employee)
            .WithMany(x => x.EmployeeLocations)
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired();
        });

        builder.Entity<UnknownFaceLocation>(unknownFaceLocation =>
        {

            unknownFaceLocation
            .HasOne(x => x.UnknownFace)
            .WithMany(x => x.UnknownFaceLocations)
            .HasForeignKey(x => x.UnknownFaceId)
            .IsRequired();

        });
    }
}



