using BookingManagementService.DTOs;
using BookingManagementService.Models;

namespace OOKING_MANAGEMENT_SERVICE.Interface
{
    public interface IResourceService
    {
        Task<List<Resource>> GetAllAsync();

        Task<Resource> CreateAsync(CreateResourceRequest request);
    }
}