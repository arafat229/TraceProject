using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace TraceProject.Models;

[Index(nameof(EmployeeId), IsUnique = true)]
public class FaceProfil
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }

    [MaxLength(1024)]
    public byte[]? RightProfile { get; set; } = [];

    [MaxLength(1024)]
    public byte[]? FrontProfile { get; set; } = [];

    [MaxLength(1024)]
    public byte[]? LeftProfile { get; set; } = [];

    [MaxLength(1024)]
    public byte[]? SmileProfile { get; set; } = [];

    [MaxLength(1024)]
    public byte[]? GlassesProfile { get; set; } = [];
    public List<byte[]>? Unknown { get; set; } = [];

    

}
