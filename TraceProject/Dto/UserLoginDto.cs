using System.ComponentModel.DataAnnotations;

namespace TraceProject.Dto;
    public record UserLoginDto([Required,EmailAddress]string Email, [Required]string Password);

