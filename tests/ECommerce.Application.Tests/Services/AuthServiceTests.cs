using ECommerce.Application.Services;
using ECommerce.Core.DTOs.Request;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly AuthService _authService;
    private readonly Mock<IConfigurationSection> _mockJwtSection;

    public AuthServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AuthService>>();
        _mockJwtSection = new Mock<IConfigurationSection>();

        SetupConfiguration();

        _authService = new AuthService(_mockConfiguration.Object, _mockLogger.Object);
    }

    private void SetupConfiguration()
    {
        _mockConfiguration.Setup(x => x.GetSection("JwtSettings"))
            .Returns(_mockJwtSection.Object);

        _mockJwtSection.Setup(x => x["SecretKey"])
            .Returns("E7F9A8B2C4D6E8F1A3B5C7D9E1F3A5B7C9D1E3F5A7B9C1D3E5F7A9B1C3D5E7F9");
        _mockJwtSection.Setup(x => x["Issuer"])
            .Returns("TestIssuer");
        _mockJwtSection.Setup(x => x["Audience"])
            .Returns("TestAudience");
        _mockJwtSection.Setup(x => x["ExpirationMinutes"])
            .Returns("120");
        _mockJwtSection.Setup(x => x["ClockSkewMinutes"])
            .Returns("5");
    }

    [Fact]
    public async Task LoginAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        var request = new LoginRequest
        {
            UserId = "user123",
            Password = "password"
        };

        var result = await _authService.LoginAsync(request, "test-correlation");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UserId.Should().Be(request.UserId);
        result.Data.Token.Should().NotBeNullOrEmpty();
        result.Data.TokenType.Should().Be("Bearer");
        result.Data.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyUserId_ReturnsErrorResponse()
    {
        var request = new LoginRequest
        {
            UserId = "",
            Password = "password"
        };

        var result = await _authService.LoginAsync(request, "test-correlation");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User ID is required");
    }

    [Fact]
    public async Task GenerateTokenAsync_WithValidUserId_ReturnsValidToken()
    {
        var userId = "user123";

        var token = await _authService.GenerateTokenAsync(userId);

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ReturnsTrue()
    {
        var userId = "user123";
        var token = await _authService.GenerateTokenAsync(userId);

        var isValid = await _authService.ValidateTokenAsync(token);

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ReturnsFalse()
    {
        var invalidToken = "invalid.token.here";

        var isValid = await _authService.ValidateTokenAsync(invalidToken);

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithEmptyToken_ReturnsFalse()
    {
        var emptyToken = "";

        var isValid = await _authService.ValidateTokenAsync(emptyToken);

        isValid.Should().BeFalse();
    }
}