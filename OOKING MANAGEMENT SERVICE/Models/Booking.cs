using System.Data;
namespace BookingManagementService.Models;

public class Booking
{
    public int Id { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public Resource Resource { get; set; } = null!;

    public User User { get; set; } = null!;
}

public enum BookingStatus
{
    Active,Cancelled
}