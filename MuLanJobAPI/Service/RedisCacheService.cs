using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MuLanJobAPI.Service
{
    public class RedisCacheService : IAppCacheService
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisCacheService> _logger;

        private const string LockKeyPrefix = "cache:lock:";
        private const int LockWaitRetries = 40;
        private const int LockWaitDelayMs = 50;
        private const int DeleteBatchSize = 200;

        private static readonly string ReleaseLockScript = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _db = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan expire,
            Func<T, bool>? shouldCache = null)
        {
            var cached = await TryGetAsync<T>(key);
            if (cached.found)
                return cached.value!;

            var lockKey = LockKeyPrefix + key;
            var lockValue = Guid.NewGuid().ToString();

            if (await TryAcquireLockAsync(lockKey, lockValue))
            {
                try
                {
                    cached = await TryGetAsync<T>(key);
                    if (cached.found)
                        return cached.value!;

                    var result = await factory();
                    if (shouldCache == null || shouldCache(result))
                    {
                        await _db.StringSetAsync(key, JsonConvert.SerializeObject(result), expire);
                    }
                    return result;
                }
                finally
                {
                    await ReleaseLockAsync(lockKey, lockValue);
                }
            }

            for (var i = 0; i < LockWaitRetries; i++)
            {
                await Task.Delay(LockWaitDelayMs);
                cached = await TryGetAsync<T>(key);
                if (cached.found)
                    return cached.value!;

                if (await TryAcquireLockAsync(lockKey, lockValue))
                {
                    try
                    {
                        cached = await TryGetAsync<T>(key);
                        if (cached.found)
                            return cached.value!;

                        var result = await factory();
                        if (shouldCache == null || shouldCache(result))
                        {
                            await _db.StringSetAsync(key, JsonConvert.SerializeObject(result), expire);
                        }
                        return result;
                    }
                    finally
                    {
                        await ReleaseLockAsync(lockKey, lockValue);
                    }
                }
            }

            _logger.LogWarning("缓存锁等待超时，直接回源 [{Key}]", key);
            return await factory();
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// 按通配符删除缓存（SCAN + 批量 DEL，跳过从节点）
        /// </summary>
        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                foreach (var endpoint in _redis.GetEndPoints())
                {
                    var server = _redis.GetServer(endpoint);
                    if (!server.IsConnected || server.IsReplica)
                        continue;

                    var batch = new List<RedisKey>(DeleteBatchSize);

                    await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 500))
                    {
                        batch.Add(key);
                        if (batch.Count >= DeleteBatchSize)
                        {
                            await _db.KeyDeleteAsync(batch.ToArray());
                            batch.Clear();
                        }
                    }

                    if (batch.Count > 0)
                        await _db.KeyDeleteAsync(batch.ToArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除缓存失败 [{Pattern}]", pattern);
            }
        }

        private static async Task<bool> TryAcquireLockAsync(IDatabase db, string lockKey, string lockValue) =>
            await db.StringSetAsync(lockKey, lockValue, TimeSpan.FromSeconds(30), When.NotExists);

        private async Task<bool> TryAcquireLockAsync(string lockKey, string lockValue) =>
            await TryAcquireLockAsync(_db, lockKey, lockValue);

        private async Task ReleaseLockAsync(string lockKey, string lockValue)
        {
            await _db.ScriptEvaluateAsync(
                ReleaseLockScript,
                new RedisKey[] { lockKey },
                new RedisValue[] { lockValue });
        }

        private async Task<(bool found, T? value)> TryGetAsync<T>(string key)
        {
            try
            {
                var value = await _db.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                    return (false, default);

                return (true, JsonConvert.DeserializeObject<T>(value!));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "缓存反序列化失败，将回源 [{Key}]", key);
                await _db.KeyDeleteAsync(key);
                return (false, default);
            }
        }
    }
}
