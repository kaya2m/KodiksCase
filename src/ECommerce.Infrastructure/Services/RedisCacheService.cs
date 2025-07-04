using ECommerce.Core.Interfaces.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _database;

        public RedisCacheService(
            IDistributedCache cache,
            ILogger<RedisCacheService> logger,
            IConnectionMultiplexer connectionMultiplexer)
        {
            _cache = cache;
            _logger = logger;
            _connectionMultiplexer = connectionMultiplexer;
            _database = _connectionMultiplexer.GetDatabase();
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cachedValue = await _cache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                    return null;

                return JsonSerializer.Deserialize<T>(cachedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached value for key {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions();

                if (expiry.HasValue)
                    options.SetAbsoluteExpiration(expiry.Value);

                await _cache.SetStringAsync(key, serializedValue, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching value for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);

                var keyArray = keys.ToArray();
                if (keyArray.Length > 0)
                {
                    await _database.KeyDeleteAsync(keyArray);
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

        #region Helper Mehods
        public async Task<bool> ExistsAsync(string key)
        {
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

        public async Task<TimeSpan?> GetTtlAsync(string key)
        {
            try
            {
                return await _database.KeyTimeToLiveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTL for key {Key}", key);
                return null;
            }
        }

        public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                var result = await _database.StringSetAsync(key, serializedValue, expiry, When.NotExists);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value if not exists for key {Key}", key);
                return false;
            }
        }

        public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null)
        {
            try
            {
                var result = await _database.StringIncrementAsync(key, value);

                if (expiry.HasValue)
                    await _database.KeyExpireAsync(key, expiry.Value);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing key {Key}", key);
                return 0;
            }
        }

        public async Task<double> IncrementAsync(string key, double value, TimeSpan? expiry = null)
        {
            try
            {
                var result = await _database.StringIncrementAsync(key, value);

                if (expiry.HasValue)
                    await _database.KeyExpireAsync(key, expiry.Value);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing key {Key}", key);
                return 0;
            }
        }

        public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern)
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);
                return keys.Select(k => k.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting keys by pattern {Pattern}", pattern);
                return Enumerable.Empty<string>();
            }
        }

        public void Dispose()
        {
            _connectionMultiplexer?.Dispose();
        }
        #endregion
    }
}