namespace BookingManagementService.DTOs;

public class BookingResponse
{
    public int Id { get; set; }

    public int ResourceId { get; set; }

    public int UserId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}