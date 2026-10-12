using BookingManagementService.Data;
using BookingManagementService.DTOs;
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
            return await _context.Resources.AsNoTracking().ToListAsync();
        }

        public async Task<Resource?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return await _context.Resources
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id.Trim());
        }

        public async Task<Resource> CreateAsync(CreateResourceRequest request)
        {
            var resource = new Resource
            {
                Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id.Trim(),
                Name = request.Name.Trim(),
                Type = request.Type.Trim()
            };

            _context.Resources.Add(resource);

            await _context.SaveChangesAsync();

            return resource;
        }
    }
}