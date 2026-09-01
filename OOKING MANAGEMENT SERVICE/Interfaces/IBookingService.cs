using BookingManagementService.DTOs;

namespace OOKING_MANAGEMENT_SERVICE.Interface;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request);

    Task<IEnumerable<BookingResponse>> GetBookingsAsync(int resourceId, DateTime from, DateTime to);
    Task<bool> CancelBookingAsync(int id);
}