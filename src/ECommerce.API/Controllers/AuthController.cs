using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var modelValidation = ValidateModelState<LoginResponse>();
            if (modelValidation != null)
                return modelValidation;

            LogInformation(_logger, "Login request received for user {UserId}", request.UserId);

            var correlationId = GetCorrelationId();
            var result = await _authService.LoginAsync(request, correlationId);

            return HandleServiceResult(result, "Authentication successful");
        }

        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> ValidateToken([FromBody] ValidateTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return ErrorResponse<object>("Token is required", null, 400);
            }

            var isValid = await _authService.ValidateTokenAsync(request.Token);

            if (isValid)
            {
                return SuccessResponse(new { IsValid = true }, "Token is valid");
            }
            else
            {
                return ErrorResponse<object>("Invalid token", null, 401);
            }
        }
    }
    public class ValidateTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}