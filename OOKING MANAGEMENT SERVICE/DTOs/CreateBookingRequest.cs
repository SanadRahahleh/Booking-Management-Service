using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateBookingRequest
{
    [Required(ErrorMessage = "ResourceId is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "ResourceId must be between 1 and 50 characters.")]
    public string ResourceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "UserId is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "UserId must be between 1 and 50 characters.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "StartDateTime is required.")]
    public DateTime StartDateTime { get; set; }

    [Required(ErrorMessage = "EndDateTime is required.")]
    public DateTime EndDateTime { get; set; }
}