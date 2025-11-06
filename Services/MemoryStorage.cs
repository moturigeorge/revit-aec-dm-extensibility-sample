using System.Collections.Concurrent;

namespace revit_aec_dm_extensibility_sample.Services
{
    public interface IMemoryStorage
    {
        void Store<T>(string key, T value, TimeSpan? expiration = null);
        T Get<T>(string key);
        bool TryGet<T>(string key, out T value);
        void Remove(string key);
        void Clear();
    }

    public class MemoryStorage : IMemoryStorage
    {
        private readonly ConcurrentDictionary<string, (object value, DateTime? expiration)> _cache =
            new ConcurrentDictionary<string, (object, DateTime?)>();

        public void Store<T>(string key, T value, TimeSpan? expiration = null)
        {
            var expirationTime = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null;
            _cache[key] = (value, expirationTime);
        }

        public T Get<T>(string key)
        {
            if (TryGet<T>(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key {key} not found in cache");
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                if (cached.expiration == null || cached.expiration > DateTime.UtcNow)
                {
                    value = (T)cached.value;
                    return true;
                }
                _cache.TryRemove(key, out _);
            }
            value = default;
            return false;
        }

        public void Remove(string key) => _cache.TryRemove(key, out _);
        public void Clear() => _cache.Clear();
    }
}