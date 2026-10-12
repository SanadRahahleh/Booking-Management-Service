using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Exceptions;
using BookingManagementService.Models;
using BookingManagementService.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingManagementService.Tests;

public class BookingConcurrencyTests
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=BookingManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

    private AppDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentOverlappingRequests_OnlyOneSucceedsAndOneFails()
    {
        // 1. Arrange & Seed initial test entities using a setup context
        using (var setupContext = CreateSqlServerDbContext())
        {
            await setupContext.Database.MigrateAsync();

            var testResourceId = "room-concurrency-" + Guid.NewGuid().ToString("N")[..8];
            var testUserId = "user-concurrency-" + Guid.NewGuid().ToString("N")[..8];

            setupContext.Resources.Add(new Resource
            {
                Id = testResourceId,
                Name = "Concurrency Test Room",
                Type = "Meeting Room"
            });

            setupContext.Users.Add(new User
            {
                Id = testUserId,
                Name = "Concurrency User",
                Email = "concurrency@example.com"
            });

            await setupContext.SaveChangesAsync();

            var slotStart = DateTime.UtcNow.AddDays(10);
            var slotEnd = slotStart.AddHours(2);

            var request1 = new CreateBookingRequest
            {
                ResourceId = testResourceId,
                UserId = testUserId,
                StartDateTime = slotStart,
                EndDateTime = slotEnd
            };

            var request2 = new CreateBookingRequest
            {
                ResourceId = testResourceId,
                UserId = testUserId,
                StartDateTime = slotStart.AddMinutes(30), // Overlaps with request1
                EndDateTime = slotEnd.AddMinutes(30)
            };

            // 2. Prepare two independent DbContext and Service instances simulating concurrent HTTP requests
            using var context1 = CreateSqlServerDbContext();
            using var context2 = CreateSqlServerDbContext();

            var service1 = new BookingService(context1);
            var service2 = new BookingService(context2);

            BookingResponse? result1 = null;
            BookingResponse? result2 = null;
            Exception? error1 = null;
            Exception? error2 = null;

            // 3. Act: Fire both requests concurrently using Task.WhenAll
            var task1 = Task.Run(async () =>
            {
                try
                {
                    result1 = await service1.CreateBookingAsync(request1);
                }
                catch (Exception ex)
                {
                    error1 = ex;
                }
            });

            var task2 = Task.Run(async () =>
            {
                try
                {
                    result2 = await service2.CreateBookingAsync(request2);
                }
                catch (Exception ex)
                {
                    error2 = ex;
                }
            });

            await Task.WhenAll(task1, task2);

            // 4. Assert: Exactly one must succeed and one must fail with BookingConflictException
            var successCount = (result1 != null ? 1 : 0) + (result2 != null ? 1 : 0);
            var failureCount = (error1 != null ? 1 : 0) + (error2 != null ? 1 : 0);

            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);

            var failureException = error1 ?? error2;
            Assert.NotNull(failureException);
            Assert.IsType<BookingConflictException>(failureException);

            // 5. Verify database integrity directly: Only 1 active booking exists for the slot
            using var verifyContext = CreateSqlServerDbContext();
            var activeBookings = await verifyContext.Bookings
                .Where(b => b.ResourceId == testResourceId && b.Status == BookingStatus.Active)
                .ToListAsync();

            Assert.Single(activeBookings);
        }
    }
}
