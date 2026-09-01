using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Models;
using BookingManagementService.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingManagementService.Tests;

public class BookingServiceTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private async Task SeedUserAndResourceAsync(AppDbContext context, int userId = 1, int resourceId = 1)
    {
        context.Users.Add(new User { Id = userId, Name = "Test User", Email = "user@example.com" });
        context.Resources.Add(new Resource { Id = resourceId, Name = "Meeting Room A", Type = "Room" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBookingAsync_ValidRequest_CreatesBookingSuccessfully()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: 1, resourceId: 1);
        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        // Act
        var response = await service.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Id > 0);
        Assert.Equal(1, response.ResourceId);
        Assert.Equal(1, response.UserId);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);

        var dbBooking = await context.Bookings.FindAsync(response.Id);
        Assert.NotNull(dbBooking);
        Assert.Equal(BookingStatus.Active, dbBooking.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_StartTimeAfterEndTime_ThrowsArgumentException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = DateTime.UtcNow.AddHours(2),
            EndDateTime = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateBookingAsync(request));
        Assert.Contains("Start time must be before end time", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_ResourceDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        context.Users.Add(new User { Id = 1, Name = "User 1", Email = "user1@test.com" });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = 999, // Non-existent resource
            UserId = 1,
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateBookingAsync(request));
        Assert.Contains("Resource not found", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_UserDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        context.Resources.Add(new Resource { Id = 1, Name = "Room A", Type = "Room" });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = 1,
            UserId = 999, // Non-existent user
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateBookingAsync(request));
        Assert.Contains("User not found", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_OverlappingActiveBooking_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: 1, resourceId: 1);

        var existingStart = DateTime.UtcNow.AddHours(10);
        var existingEnd = DateTime.UtcNow.AddHours(12);

        context.Bookings.Add(new Booking
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = existingStart,
            EndDateTime = existingEnd,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Overlapping request (11:00 to 13:00 overlaps with 10:00 to 12:00)
        var overlappingRequest = new CreateBookingRequest
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = existingStart.AddHours(1),
            EndDateTime = existingEnd.AddHours(1)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingAsync(overlappingRequest));
        Assert.Contains("already booked", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_OverlappingCancelledBooking_AllowsNewBooking()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: 1, resourceId: 1);

        var existingStart = DateTime.UtcNow.AddHours(10);
        var existingEnd = DateTime.UtcNow.AddHours(12);

        // Add a CANCELLED booking in the slot
        context.Bookings.Add(new Booking
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = existingStart,
            EndDateTime = existingEnd,
            Status = BookingStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var newRequest = new CreateBookingRequest
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = existingStart,
            EndDateTime = existingEnd
        };

        // Act
        var response = await service.CreateBookingAsync(newRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);
    }

    [Fact]
    public async Task GetBookingsAsync_ValidFilters_ReturnsFilteredAndPaginatedBookings()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: 1, resourceId: 1);

        var baseTime = DateTime.UtcNow.AddDays(1);

        context.Bookings.AddRange(
            new Booking { ResourceId = 1, UserId = 1, StartDateTime = baseTime.AddHours(1), EndDateTime = baseTime.AddHours(2), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow },
            new Booking { ResourceId = 1, UserId = 1, StartDateTime = baseTime.AddHours(3), EndDateTime = baseTime.AddHours(4), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow },
            new Booking { ResourceId = 1, UserId = 1, StartDateTime = baseTime.AddHours(5), EndDateTime = baseTime.AddHours(6), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act - Retrieve with page 1, pageSize 2, sorted by StartDateTime asc
        var bookings = (await service.GetBookingsAsync(
            resourceId: 1,
            from: baseTime,
            to: baseTime.AddHours(10),
            page: 1,
            pageSize: 2,
            sortBy: "StartDateTime",
            sortOrder: "asc")).ToList();

        // Assert
        Assert.Equal(2, bookings.Count);
        Assert.True(bookings[0].StartDateTime < bookings[1].StartDateTime);
    }

    [Fact]
    public async Task CancelBookingAsync_ExistingBooking_UpdatesStatusToCancelled()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: 1, resourceId: 1);

        var booking = new Booking
        {
            ResourceId = 1,
            UserId = 1,
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act
        var result = await service.CancelBookingAsync(booking.Id);

        // Assert
        Assert.True(result);
        var updatedBooking = await context.Bookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Cancelled, updatedBooking.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_NonExistingBooking_ReturnsFalse()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        // Act
        var result = await service.CancelBookingAsync(999);

        // Assert
        Assert.False(result);
    }
}
