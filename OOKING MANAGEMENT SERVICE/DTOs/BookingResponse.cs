namespace BookingManagementService.DTOs;

public class BookingResponse
{
    public int Id { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}