using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Exceptions;
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

    private async Task SeedUserAndResourceAsync(AppDbContext context, string userId = "user-1", string resourceId = "resource-1")
    {
        context.Users.Add(new User { Id = userId, Name = "Test User", Email = "user@example.com" });
        context.Resources.Add(new Resource { Id = resourceId, Name = "Meeting Room A", Type = "Room" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBookingAsync_ValidRequest_CreatesBookingSuccessfully()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");
        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        var response = await service.CreateBookingAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Id > 0);
        Assert.Equal("resource-1", response.ResourceId);
        Assert.Equal("user-1", response.UserId);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);

        var dbBooking = await context.Bookings.FindAsync(response.Id);
        Assert.NotNull(dbBooking);
        Assert.Equal(BookingStatus.Active, dbBooking.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_StartTimeAfterEndTime_ThrowsArgumentException()
    {
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(2),
            EndDateTime = DateTime.UtcNow.AddHours(1)
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateBookingAsync(request));
        Assert.Contains("Start time must be before end time", ex.Message);
    }

    [Theory]
    [InlineData("", "user-1")]
    [InlineData("   ", "user-1")]
    [InlineData("resource-1", "")]
    [InlineData("resource-1", "   ")]
    public async Task CreateBookingAsync_EmptyOrWhitespaceIdentifiers_ThrowsArgumentException(string resourceId, string userId)
    {
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = resourceId,
            UserId = userId,
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateBookingAsync(request));
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBookingAsync_ResourceDoesNotExist_ThrowsKeyNotFoundException()
    {
        using var context = GetInMemoryDbContext();
        context.Users.Add(new User { Id = "user-1", Name = "User 1", Email = "user1@test.com" });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = "non-existent-resource",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateBookingAsync(request));
        Assert.Contains("Resource not found", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_UserDoesNotExist_ThrowsKeyNotFoundException()
    {
        using var context = GetInMemoryDbContext();
        context.Resources.Add(new Resource { Id = "resource-1", Name = "Room A", Type = "Room" });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var request = new CreateBookingRequest
        {
            ResourceId = "resource-1",
            UserId = "non-existent-user",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2)
        };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateBookingAsync(request));
        Assert.Contains("User not found", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_OverlappingActiveBooking_ThrowsBookingConflictException()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var existingStart = DateTime.UtcNow.AddHours(10);
        var existingEnd = DateTime.UtcNow.AddHours(12);

        context.Bookings.Add(new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
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
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = existingStart.AddHours(1),
            EndDateTime = existingEnd.AddHours(1)
        };

        var ex = await Assert.ThrowsAsync<BookingConflictException>(() => service.CreateBookingAsync(overlappingRequest));
        Assert.Contains("already booked", ex.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_BackToBackBookings_AllowedSuccessfully()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var existingStart = DateTime.UtcNow.AddHours(10);
        var existingEnd = DateTime.UtcNow.AddHours(12);

        context.Bookings.Add(new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = existingStart,
            EndDateTime = existingEnd,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Back-to-back request starting exactly when the existing booking ends (12:00 to 14:00)
        var backToBackRequest = new CreateBookingRequest
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = existingEnd,
            EndDateTime = existingEnd.AddHours(2)
        };

        var response = await service.CreateBookingAsync(backToBackRequest);

        Assert.NotNull(response);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_DifferentResources_AllowedSimultaneously()
    {
        using var context = GetInMemoryDbContext();
        context.Users.Add(new User { Id = "user-1", Name = "User 1", Email = "user1@test.com" });
        context.Resources.Add(new Resource { Id = "resource-1", Name = "Room 1", Type = "Room" });
        context.Resources.Add(new Resource { Id = "resource-2", Name = "Room 2", Type = "Room" });

        var slotStart = DateTime.UtcNow.AddHours(10);
        var slotEnd = DateTime.UtcNow.AddHours(12);

        context.Bookings.Add(new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = slotStart,
            EndDateTime = slotEnd,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Booking the same time slot for a different resource
        var differentResourceRequest = new CreateBookingRequest
        {
            ResourceId = "resource-2",
            UserId = "user-1",
            StartDateTime = slotStart,
            EndDateTime = slotEnd
        };

        var response = await service.CreateBookingAsync(differentResourceRequest);

        Assert.NotNull(response);
        Assert.Equal("resource-2", response.ResourceId);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_OverlappingCancelledBooking_AllowsNewBooking()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var existingStart = DateTime.UtcNow.AddHours(10);
        var existingEnd = DateTime.UtcNow.AddHours(12);

        // Add a CANCELLED booking in the slot
        context.Bookings.Add(new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = existingStart,
            EndDateTime = existingEnd,
            Status = BookingStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var newRequest = new CreateBookingRequest
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = existingStart,
            EndDateTime = existingEnd
        };

        var response = await service.CreateBookingAsync(newRequest);

        Assert.NotNull(response);
        Assert.Equal(BookingStatus.Active.ToString(), response.Status);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsBookingResponse()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var booking = new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var response = await service.GetBookingByIdAsync(booking.Id);

        Assert.NotNull(response);
        Assert.Equal(booking.Id, response.Id);
        Assert.Equal("resource-1", response.ResourceId);
    }

    [Fact]
    public async Task GetBookingByIdAsync_NonExistingBooking_ReturnsNull()
    {
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        var response = await service.GetBookingByIdAsync(9999);

        Assert.Null(response);
    }

    [Fact]
    public async Task GetBookingsAsync_ValidFilters_ReturnsFilteredAndPaginatedBookings()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var baseTime = DateTime.UtcNow.AddDays(1);

        context.Bookings.AddRange(
            new Booking { ResourceId = "resource-1", UserId = "user-1", StartDateTime = baseTime.AddHours(1), EndDateTime = baseTime.AddHours(2), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow },
            new Booking { ResourceId = "resource-1", UserId = "user-1", StartDateTime = baseTime.AddHours(3), EndDateTime = baseTime.AddHours(4), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow },
            new Booking { ResourceId = "resource-1", UserId = "user-1", StartDateTime = baseTime.AddHours(5), EndDateTime = baseTime.AddHours(6), Status = BookingStatus.Active, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var bookings = (await service.GetBookingsAsync(
            resourceId: "resource-1",
            from: baseTime,
            to: baseTime.AddHours(10),
            page: 1,
            pageSize: 2,
            sortBy: "StartDateTime",
            sortOrder: "asc")).ToList();

        Assert.Equal(2, bookings.Count);
        Assert.True(bookings[0].StartDateTime < bookings[1].StartDateTime);
    }

    [Fact]
    public async Task CancelBookingAsync_ExistingBooking_UpdatesStatusToCancelled()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var booking = new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var result = await service.CancelBookingAsync(booking.Id);

        Assert.True(result);
        var updatedBooking = await context.Bookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Cancelled, updatedBooking.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelledBooking_ReturnsTrueIdempotently()
    {
        using var context = GetInMemoryDbContext();
        await SeedUserAndResourceAsync(context, userId: "user-1", resourceId: "resource-1");

        var booking = new Booking
        {
            ResourceId = "resource-1",
            UserId = "user-1",
            StartDateTime = DateTime.UtcNow.AddHours(1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Status = BookingStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        var result = await service.CancelBookingAsync(booking.Id);

        Assert.True(result);
        var updatedBooking = await context.Bookings.FindAsync(booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Cancelled, updatedBooking.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_NonExistingBooking_ReturnsFalse()
    {
        using var context = GetInMemoryDbContext();
        var service = new BookingService(context);

        var result = await service.CancelBookingAsync(999);

        Assert.False(result);
    }
}
