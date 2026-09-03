
using Microsoft.EntityFrameworkCore;
using TraceProject.Model;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TraceProject.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{

    public DbSet<Camera> Cameras { get; set; } = null!;
    //public DbSet<FaceProfil> FaceProfil { get; set; } = null!;
    //public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<Attendance> Attendances { get; set; } = null!;
    public DbSet<UnknownFace> UnkownFaces { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Employee>().HasMany(x => x.Attendances).WithOne(f => f.Employee).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Employee>().HasMany(x => x.EmployeeLocations).WithOne(f => f.Employee).OnDelete(DeleteBehavior.Cascade);

        //builder.Entity<Attendance>(attendance =>
        //{

        //    attendance
        //    .HasOne(x => x.Employee)
        //    .WithMany(x => x.Attendances)
        //    .HasForeignKey(x => x.EmployeeId)
        //    .IsRequired();

        //    attendance
        //    .HasOne(x => x.Location)
        //    .WithMany()
        //    .HasForeignKey(x => x.LocationId)
        //    .IsRequired();

        // });

        //builder.Entity<EmployeeAccess>(employeeAccess =>
        //{
        //    employeeAccess
        //     .HasOne(x => x.Employee)
        //     .WithMany(x => x.EmployeeLocations)
        //     .HasForeignKey(x => x.EmployeeId)
        //     .IsRequired();
        //});

        //builder.Entity<UnknownFaceLocation>(unknownFaceLocation =>
        //{

        //    unknownFaceLocation
        //    .HasOne(x => x.UnknownFace)
        //    .WithMany(x => x.UnknownFaceLocations)
        //    .HasForeignKey(x => x.UnknownFaceId)
        //    .IsRequired();

        //});
    }
}



