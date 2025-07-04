using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ECommerce.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string correlationId)
        {
            try
            {
                _logger.LogInformation("Login attempt for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId, correlationId);

                // Basit validasyon - gerçek uygulamada kullanıcı doğrulaması yapılır
                if (string.IsNullOrWhiteSpace(request.UserId))
                {
                    return ApiResponse<LoginResponse>.ErrorResponse("User ID is required");
                }

                var token = await GenerateTokenAsync(request.UserId);

                var jwtSettings = _configuration.GetSection("JwtSettings");
                var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "120");

                var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

                var response = new LoginResponse
                {
                    Token = token,
                    UserId = request.UserId,
                    ExpiresAt = expiresAt,
                    TokenType = "Bearer"
                };

                _logger.LogInformation("Login successful for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId, correlationId);

                return ApiResponse<LoginResponse>.SuccessResponse(response, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId, correlationId);

                return ApiResponse<LoginResponse>.ErrorResponse("Login failed. Please try again later.");
            }
        }

        public Task<string> GenerateTokenAsync(string userId)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "120");

            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("JWT SecretKey is not configured");
            }

            var key = Encoding.UTF8.GetBytes(secretKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, userId),
                    new Claim("sub", userId),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat,
                        new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                        ClaimValueTypes.Integer64)
                }),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Task.FromResult(tokenHandler.WriteToken(token));
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"];
                var issuer = jwtSettings["Issuer"];
                var audience = jwtSettings["Audience"];
                var clockSkewMinutes = int.Parse(jwtSettings["ClockSkewMinutes"] ?? "5");

                if (string.IsNullOrEmpty(secretKey))
                {
                    return Task.FromResult(false);
                }

                var key = Encoding.UTF8.GetBytes(secretKey);
                var tokenHandler = new JwtSecurityTokenHandler();

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(clockSkewMinutes)
                }, out SecurityToken validatedToken);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return Task.FromResult(false);
            }
        }
    }
}