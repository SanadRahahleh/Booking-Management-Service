using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateResourceRequest
{
    public string? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;
}
