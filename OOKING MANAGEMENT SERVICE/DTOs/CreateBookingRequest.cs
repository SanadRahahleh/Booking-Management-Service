using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateBookingRequest
{
    [Required]
    public string ResourceId { get; set; } = string.Empty;
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public DateTime StartDateTime { get; set; }
    
    public DateTime EndDateTime { get; set; }
}