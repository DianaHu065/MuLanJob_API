using System;
using System.Threading.Tasks;

namespace MuLanJobAPI.Service
{
    public interface IAppCacheService
    {
        Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan expire,
            Func<T, bool>? shouldCache = null);

        Task RemoveAsync(string key);

        Task RemoveByPatternAsync(string pattern);
    }
}
