
using Microsoft.EntityFrameworkCore;
using TraceProject.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TraceProject.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{

    public DbSet<Camera> Cameras { get; set; } = null!;
    public DbSet<FaceProfil> FaceProfiles { get; set; } = null!;
    public DbSet<FaceEmbedding> FaceEmbeddings { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<Attendance> Attendances { get; set; } = null!;
    public DbSet<UnknownFace> UnknownFaces { get; set; } = null!;
    public DbSet<UnknownFaceLocation> UnknownFaceLocations { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<EmployeeAccess> EmployeeAccesses { get; set; } = null!;
    public DbSet<Activity> Activities { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Employee>().HasMany(x => x.Attendances).WithOne(f => f.Employee).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Employee>().HasMany(x => x.EmployeeLocations).WithOne(f => f.Employee).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Attendance>(attendance =>
        {
            attendance
            .HasOne(x => x.Employee)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired();

            attendance
            .HasOne(x => x.Location)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.LocationId)
            .IsRequired();

            // Phase 5: Indexes for efficient attendance queries
            attendance.HasIndex(x => new { x.EmployeeId, x.CheckIn });
            attendance.HasIndex(x => new { x.CameraId, x.CheckIn });
            attendance.HasIndex(x => x.CheckIn);

            // One check-in per employee per day
            attendance.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
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

        builder.Entity<FaceProfil>()
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FaceEmbedding>(faceEmbedding =>
        {
            faceEmbedding
                .HasOne(x => x.Employee)
                .WithMany(x => x.FaceEmbeddings)
                .HasForeignKey(x => x.EmployeeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient lookup by EmployeeId and IsActive
            faceEmbedding.HasIndex(x => new { x.EmployeeId, x.IsActive });
            // Index for cache refresh queries
            faceEmbedding.HasIndex(x => x.IsActive);
        });

        builder.Entity<Report>()
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}



