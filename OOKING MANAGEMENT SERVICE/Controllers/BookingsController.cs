using BookingManagementService.DTOs;
using Microsoft.AspNetCore.Mvc;
using OOKING_MANAGEMENT_SERVICE.Interface;

namespace BookingManagementService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var booking = await _bookingService.CreateBookingAsync(request);

        return CreatedAtAction(
            nameof(GetBookingById),
            new { id = booking.Id },
            booking);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingResponse>> GetBookingById(int id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);

        if (booking == null)
        {
            return NotFound(new { message = $"Booking with ID {id} was not found." });
        }

        return Ok(booking);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetBookings(
        [FromQuery] string resourceId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "StartDateTime",
        [FromQuery] string? sortOrder = "asc")
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return BadRequest(new { message = "ResourceId query parameter is required." });
        }

        if (from >= to)
        {
            return BadRequest(new { message = "From date must be before To date." });
        }

        if (page < 1)
        {
            return BadRequest(new { message = "Page number must be 1 or greater." });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "PageSize must be between 1 and 100." });
        }

        var bookings = await _bookingService.GetBookingsAsync(
            resourceId.Trim(),
            from,
            to,
            page,
            pageSize,
            sortBy,
            sortOrder);

        return Ok(bookings);
    }
   
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var cancelled = await _bookingService.CancelBookingAsync(id);

        if (!cancelled)
        {
            return NotFound(new { message = $"Booking with ID {id} was not found." });
        }

        return NoContent();
    }
}