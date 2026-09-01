using System.ComponentModel.DataAnnotations;

namespace BookingManagementService.DTOs;

public class CreateBookingRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ResourceId { get; set; }
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
    
    public DateTime StartDateTime { get; set; }
    
    public DateTime EndDateTime { get; set; }
}