using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public interface ICacheService
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) where T : class;
        void Remove(string key);
        void RemoveByPattern(string pattern);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) where T : class
        {
            if (_cache.TryGetValue(key, out T cachedValue))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            var item = await getItem();

            if (item != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions();

                if (slidingExpiration.HasValue)
                    cacheEntryOptions.SlidingExpiration = slidingExpiration;
                else
                    cacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(30);

                if (absoluteExpiration.HasValue)
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = absoluteExpiration;

                cacheEntryOptions.Priority = CacheItemPriority.Normal;

                _cache.Set(key, item, cacheEntryOptions);
            }

            return item;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }

        public void RemoveByPattern(string pattern)
        {
            // Cette implémentation basique supprime tout le cache si pattern match
            // Pour une implémentation plus sophistiqu�e, utiliser IMemoryCache avec tags
            if (_cache is MemoryCache memCache)
            {
                var field = typeof(MemoryCache).GetField("_coherentState", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(memCache) is object coherentState)
                {
                    var entriesCollection = coherentState.GetType()
                        .GetProperty("EntriesCollection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (entriesCollection?.GetValue(coherentState) is IDictionary<object, object> entries)
                    {
                        var keysToRemove = entries.Keys
                            .OfType<string>()
                            .Where(key => key.Contains(pattern))
                            .ToList();

                        foreach (var key in keysToRemove)
                        {
                            _cache.Remove(key);
                        }
                        
                        _logger.LogDebug("Cache cleared for pattern: {Pattern}, {Count} entries removed", pattern, keysToRemove.Count);
                    }
                }
            }
        }
    }
}
