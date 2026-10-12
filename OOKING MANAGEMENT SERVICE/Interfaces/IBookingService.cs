using BookingManagementService.DTOs;

namespace OOKING_MANAGEMENT_SERVICE.Interface;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request);

    Task<IEnumerable<BookingResponse>> GetBookingsAsync(
        string resourceId,
        DateTime from,
        DateTime to,
        int page = 1,
        int pageSize = 10,
        string? sortBy = "StartDateTime",
        string? sortOrder = "asc");

    Task<BookingResponse?> GetBookingByIdAsync(int id);

    Task<bool> CancelBookingAsync(int id);
}