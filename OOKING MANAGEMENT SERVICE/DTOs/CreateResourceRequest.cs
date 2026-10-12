using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateResourceRequest
{
    [StringLength(50, ErrorMessage = "ResourceId must not exceed 50 characters.")]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Resource name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Resource name must be between 1 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Resource type is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Resource type must be between 1 and 50 characters.")]
    public string Type { get; set; } = string.Empty;
}
