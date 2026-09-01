using BookingManagementService.DTOs;
using Microsoft.AspNetCore.Mvc;
using OOKING_MANAGEMENT_SERVICE.Interface;

namespace OOKING_MANAGEMENT_SERVICE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();

            return Ok(users);
        }
        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest request)
        {
            var user = await _userService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetAll),
                new { id = user.Id },
                user);
        }
    }
}