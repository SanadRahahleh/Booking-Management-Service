using BookingManagementService.DTOs;
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResourceById(string id)
        {
            var resource = await _resourceService.GetByIdAsync(id);
            if (resource == null)
            {
                return NotFound(new { message = $"Resource with ID '{id}' was not found." });
            }

            return Ok(resource);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateResourceRequest request)
        {
            var createdResource = await _resourceService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetResourceById),
                new { id = createdResource.Id },
                createdResource);
        }
    }
}