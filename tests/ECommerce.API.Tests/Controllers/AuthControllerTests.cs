using ECommerce.API.Controllers;
using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using static ECommerce.API.Controllers.AuthController;

namespace ECommerce.API.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_mockAuthService.Object, _mockLogger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items["CorrelationId"] = "test-correlation-id";
    }

    [Fact]
    public async Task Login_WithValidRequest_ReturnsOkResult()
    {
        var request = new LoginRequest
        {
            UserId = "testuser",
            Password = "password"
        };

        var loginResponse = new LoginResponse
        {
            Token = "test-jwt-token",
            UserId = request.UserId,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            TokenType = "Bearer"
        };

        var serviceResponse = ApiResponse<LoginResponse>.SuccessResponse(loginResponse, "Login successful");
        _mockAuthService.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string>()))
            .ReturnsAsync(serviceResponse);

        var result = await _controller.Login(request);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult?.Value as ApiResponse<LoginResponse>;

        apiResponse?.Success.Should().BeTrue();
        apiResponse?.Data?.Token.Should().Be("test-jwt-token");
        apiResponse?.Data?.UserId.Should().Be(request.UserId);
    }

    [Fact]
    public async Task Login_WithEmptyUserId_ReturnsBadRequest()
    {
        var request = new LoginRequest
        {
            UserId = "",
            Password = "password"
        };

        _controller.ModelState.AddModelError("UserId", "User ID is required");

        var result = await _controller.Login(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_ReturnsOkResult()
    {
        var request = new ValidateTokenRequest
        {
            Token = "valid-jwt-token"
        };

        _mockAuthService.Setup(x => x.ValidateTokenAsync(request.Token))
            .ReturnsAsync(true);

        var result = await _controller.ValidateToken(request);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult?.Value as ApiResponse<object>;

        apiResponse?.Success.Should().BeTrue();
    }
}