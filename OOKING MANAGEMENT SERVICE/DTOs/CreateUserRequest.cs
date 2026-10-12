using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateUserRequest
{
    [Required(ErrorMessage = "User name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "User name must be between 1 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}