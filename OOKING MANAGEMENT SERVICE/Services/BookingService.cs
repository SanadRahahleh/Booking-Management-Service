using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Models;
using Microsoft.EntityFrameworkCore;
using OOKING_MANAGEMENT_SERVICE.Interface;
using System.Data;

namespace BookingManagementService.Services;

public class BookingService : IBookingService
{
    private readonly AppDbContext _context;

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BookingResponse> CreateBookingAsync(
        CreateBookingRequest request)
    {
        // 1. Validate time range
        if (request.StartDateTime >= request.EndDateTime)
        {
            throw new ArgumentException(
                "Start time must be before end time.");
        }

        // 2. Check that Resource exists
        var resourceExists = await _context.Resources
            .AnyAsync(r => r.Id == request.ResourceId);

        if (!resourceExists)
        {
            throw new KeyNotFoundException(
                "Resource not found.");
        }

        // 3. Check that User exists
        var userExists = await _context.Users
            .AnyAsync(u => u.Id == request.UserId);

        if (!userExists)
        {
            throw new KeyNotFoundException(
                "User not found.");
        }

        // Start transaction
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // 4. Check for overlapping active bookings
        var hasOverlap = await _context.Bookings
            .AnyAsync(b =>
                b.ResourceId == request.ResourceId &&
                b.Status == BookingStatus.Active &&
                request.StartDateTime < b.EndDateTime &&
                request.EndDateTime > b.StartDateTime);

        if (hasOverlap)
        {
            throw new InvalidOperationException(
                "The resource is already booked during this time.");
        }

        // 5. Create booking
        var booking = new Booking
        {
            ResourceId = request.ResourceId,
            UserId = request.UserId,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 6. Save
        _context.Bookings.Add(booking);

        await _context.SaveChangesAsync();

        // Commit transaction
        await transaction.CommitAsync();

        // 7. Return response
        return new BookingResponse
        {
            Id = booking.Id,
            ResourceId = booking.ResourceId,
            UserId = booking.UserId,
            StartDateTime = booking.StartDateTime,
            EndDateTime = booking.EndDateTime,
            Status = booking.Status.ToString(),
            CreatedAt = booking.CreatedAt
        };
    }

    public async Task<IEnumerable<BookingResponse>> GetBookingsAsync(
        int resourceId,
        DateTime from,
        DateTime to)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.ResourceId == resourceId &&
                b.StartDateTime < to &&
                b.EndDateTime > from)
            .Select(b => new BookingResponse
            {
                Id = b.Id,
                ResourceId = b.ResourceId,
                UserId = b.UserId,
                StartDateTime = b.StartDateTime,
                EndDateTime = b.EndDateTime,
                Status = b.Status.ToString(),
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<bool> CancelBookingAsync(int id)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return false;
        }

        booking.Status = BookingStatus.Cancelled;

        await _context.SaveChangesAsync();

        return true;
    }
}