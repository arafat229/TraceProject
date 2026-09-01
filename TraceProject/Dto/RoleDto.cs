using System.ComponentModel.DataAnnotations;

namespace TraceProject.Dto;

public class RoleDto
{
    public string Name { get; set; } = null!;
    public List<string> Claims { get; set; } = [];
}

    

