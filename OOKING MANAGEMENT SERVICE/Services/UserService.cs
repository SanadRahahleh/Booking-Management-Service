using BookingManagementService.Data;
using BookingManagementService.DTOs;
using BookingManagementService.Models;
using Microsoft.EntityFrameworkCore;
using OOKING_MANAGEMENT_SERVICE.Interface;

namespace BookingManagementService.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.AsNoTracking().ToListAsync();
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id.Trim());
        }

        public async Task<User> CreateAsync(CreateUserRequest request)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name.Trim(),
                Email = request.Email.Trim().ToLowerInvariant()
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return user;
        }
    }
}