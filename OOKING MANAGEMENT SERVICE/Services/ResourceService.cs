using BookingManagementService.Data;
using BookingManagementService.Models;
using Microsoft.EntityFrameworkCore;
using OOKING_MANAGEMENT_SERVICE.Interface;

namespace BookingManagementService.Services
{
    public class ResourceService : IResourceService
    {
        private readonly AppDbContext _context;

        public ResourceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Resource>> GetAllAsync()
        {
            return await _context.Resources.ToListAsync();
        }

        public async Task<Resource> CreateAsync(Resource resource)
        {
            _context.Resources.Add(resource);

            await _context.SaveChangesAsync();

            return resource;
        }
    }
}