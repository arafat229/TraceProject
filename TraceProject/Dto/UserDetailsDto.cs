using System.ComponentModel.DataAnnotations;

namespace TraceProject.Dto;
    public record UserDetailsDto
    {
        public long Id { get; set; }
        [Required]
        public string FullName { get; set; } = null!;
        [Phone]
        public string PhoneNumber { get; set; } = null!;
        [EmailAddress]
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
        public bool IsActive { get; set; }
        public string Role { get; set; } = null!;
        public List<string> Permission = [];
    }

