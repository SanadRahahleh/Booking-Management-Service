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
    public async Task<ActionResult<BookingResponse>> CreateBooking(CreateBookingRequest request)
    {
        var booking = await _bookingService.CreateBookingAsync(request);

        return CreatedAtAction(
            nameof(GetBookings),
            new { id = booking.Id },
            booking);
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
        if (from >= to)
        {
            throw new ArgumentException("From date must be before To date.");
        }

        var bookings = await _bookingService.GetBookingsAsync(
            resourceId,
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
            return NotFound(new { message = "Booking not found." });
        }

        return NoContent();
    }
}