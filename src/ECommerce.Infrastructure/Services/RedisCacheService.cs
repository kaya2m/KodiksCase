using ECommerce.Core.Interfaces.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ECommerce.Infrastructure.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IConnectionMultiplexer? _connectionMultiplexer;
        private readonly IDatabase? _database;
        private readonly bool _isRedisAvailable;

        public RedisCacheService(
            IDistributedCache cache,
            ILogger<RedisCacheService> logger,
            IConnectionMultiplexer? connectionMultiplexer = null)
        {
            _cache = cache;
            _logger = logger;
            _connectionMultiplexer = connectionMultiplexer;

            try
            {
                _database = _connectionMultiplexer?.GetDatabase();
                _isRedisAvailable = _connectionMultiplexer?.IsConnected ?? false;

                if (!_isRedisAvailable)
                {
                    _logger.LogWarning("Redis connection is not available. Cache operations will be skipped.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Redis database connection");
                _isRedisAvailable = false;
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (!_isRedisAvailable)
            {
                _logger.LogDebug("Redis not available, skipping cache get for key {Key}", key);
                return null;
            }

            try
            {
                var cachedValue = await _cache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                    return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Deserialize<T>(cachedValue, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached value for key {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            if (!_isRedisAvailable)
            {
                _logger.LogDebug("Redis not available, skipping cache set for key {Key}", key);
                return;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var serializedValue = JsonSerializer.Serialize(value, options);
                var cacheOptions = new DistributedCacheEntryOptions();

                if (expiry.HasValue)
                    cacheOptions.SetAbsoluteExpiration(expiry.Value);
                else
                    cacheOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(5)); // Default 5 minutes

                await _cache.SetStringAsync(key, serializedValue, cacheOptions);
                _logger.LogDebug("Successfully cached value for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching value for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogDebug("Redis not available, skipping cache remove for key {Key}", key);
                return;
            }

            try
            {
                await _cache.RemoveAsync(key);
                _logger.LogDebug("Successfully removed cache for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            if (!_isRedisAvailable || _database == null || _connectionMultiplexer == null)
            {
                _logger.LogDebug("Redis not available, skipping pattern remove for pattern {Pattern}", pattern);
                return;
            }

            try
            {
                var endPoints = _connectionMultiplexer.GetEndPoints();
                if (endPoints.Length == 0)
                {
                    _logger.LogWarning("No Redis endpoints available for pattern removal");
                    return;
                }

                var server = _connectionMultiplexer.GetServer(endPoints.First());
                var keys = server.Keys(pattern: pattern);

                var keyArray = keys.ToArray();
                if (keyArray.Length > 0)
                {
                    await _database.KeyDeleteAsync(keyArray);
                    _logger.LogDebug("Successfully removed {Count} keys matching pattern {Pattern}", keyArray.Length, pattern);
                }
                else
                {
                    _logger.LogDebug("No keys found matching pattern {Pattern}", pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache keys by pattern {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (!_isRedisAvailable || _database == null)
            {
                return false;
            }

            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists {Key}", key);
                return false;
            }
        }

        public void Dispose()
        {
        
        }
    }
}