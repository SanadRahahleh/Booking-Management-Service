using BookingManagementService.Models;
using Microsoft.AspNetCore.Mvc;
using OOKING_MANAGEMENT_SERVICE.Interface;

namespace OOKING_MANAGEMENT_SERVICE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResourcesController : ControllerBase
    {
        private readonly IResourceService _resourceService;

        public ResourcesController(IResourceService resourceService)
        {
            _resourceService = resourceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var resources = await _resourceService.GetAllAsync();

            return Ok(resources);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Resource resource)
        {
            var createdResource = await _resourceService.CreateAsync(resource);

            return CreatedAtAction(
                nameof(GetAll),
                new { id = createdResource.Id },
                createdResource);
        }
    }
}