using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Exceptions;
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

    public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request)
    {
        var resourceId = request.ResourceId?.Trim() ?? string.Empty;
        var userId = request.UserId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("ResourceId and UserId are required.");
        }

        // 1. Validate time range
        if (request.StartDateTime >= request.EndDateTime)
        {
            throw new ArgumentException("Start time must be before end time.");
        }

        // 2. Check that Resource exists
        var resourceExists = await _context.Resources
            .AnyAsync(r => r.Id == resourceId);

        if (!resourceExists)
        {
            throw new KeyNotFoundException("Resource not found.");
        }

        // 3. Check that User exists
        var userExists = await _context.Users
            .AnyAsync(u => u.Id == userId);

        if (!userExists)
        {
            throw new KeyNotFoundException("User not found.");
        }

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            try
            {
                // 4. Check for overlapping active bookings
                var hasOverlap = await _context.Bookings
                    .AnyAsync(b =>
                        b.ResourceId == resourceId &&
                        b.Status == BookingStatus.Active &&
                        request.StartDateTime < b.EndDateTime &&
                        request.EndDateTime > b.StartDateTime);

                if (hasOverlap)
                {
                    throw new BookingConflictException("The resource is already booked during this time.");
                }

                // 5. Create booking
                var booking = new Booking
                {
                    ResourceId = resourceId,
                    UserId = userId,
                    StartDateTime = request.StartDateTime,
                    EndDateTime = request.EndDateTime,
                    Status = BookingStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };

                // 6. Save
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

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
            catch (BookingConflictException)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            catch (Exception ex) when (IsDeadlockException(ex))
            {
                if (transaction != null)
                {
                    try { await transaction.RollbackAsync(); } catch { /* ignore rollback on deadlocked connection */ }
                }

                _context.ChangeTracker.Clear();

                if (attempt < maxRetries)
                {
                    await Task.Delay(50 * attempt + Random.Shared.Next(10, 40));
                    continue;
                }

                throw new ConcurrencyException("The operation encountered transient database contention and could not complete.", ex);
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        throw new ConcurrencyException("Unexpected failure during booking creation.");
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(int id)
    {
        var b = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (b == null)
        {
            return null;
        }

        return new BookingResponse
        {
            Id = b.Id,
            ResourceId = b.ResourceId,
            UserId = b.UserId,
            StartDateTime = b.StartDateTime,
            EndDateTime = b.EndDateTime,
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt
        };
    }

    public async Task<IEnumerable<BookingResponse>> GetBookingsAsync(
        string resourceId,
        DateTime from,
        DateTime to,
        int page = 1,
        int pageSize = 10,
        string? sortBy = "StartDateTime",
        string? sortOrder = "asc")
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var cleanResourceId = resourceId?.Trim() ?? string.Empty;

        var query = _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.ResourceId == cleanResourceId &&
                b.StartDateTime < to &&
                b.EndDateTime > from);

        var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        query = (sortBy?.ToLower()) switch
        {
            "enddatetime" => isDescending ? query.OrderByDescending(b => b.EndDateTime) : query.OrderBy(b => b.EndDateTime),
            "createdat" => isDescending ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt),
            "id" => isDescending ? query.OrderByDescending(b => b.Id) : query.OrderBy(b => b.Id),
            _ => isDescending ? query.OrderByDescending(b => b.StartDateTime) : query.OrderBy(b => b.StartDateTime)
        };

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

    private static bool IsDeadlockException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 1205)
            {
                return true;
            }
            if (ex.Message.Contains("1205", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            ex = ex.InnerException;
        }
        return false;
    }
}