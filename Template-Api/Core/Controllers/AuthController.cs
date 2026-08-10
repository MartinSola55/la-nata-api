using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.Auth;
using Template_Api.Core.Services;

namespace Template_Api.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AuthController(AuthService authService) : ControllerBase
    {
        private readonly AuthService _authService = authService;

        [HttpPost]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<BaseResponse<LoginResponse>> Login([FromBody] LoginRequest rq)
        {
            return await _authService.Login(rq);
        }

        [HttpPost]
        [Authorize]
        public BaseResponse Logout()
        {
            return _authService.Logout();
        }
    }
}
