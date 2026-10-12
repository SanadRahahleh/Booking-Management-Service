using BookingManagementService.DTOs;
using BookingManagementService.Models;

namespace OOKING_MANAGEMENT_SERVICE.Interface
{
    public interface IUserService
    {
        Task<List<User>> GetAllAsync();

        Task<User?> GetByIdAsync(string id);

        Task<User> CreateAsync(CreateUserRequest request);
    }
}