using System.Threading.Tasks;

namespace MuLanJobAPI.Service
{
    /// <summary>
    /// 订单相关 Redis 缓存键统一管理
    /// </summary>
    public static class OrderCacheKeys
    {
        public const string Prefix = "order:";
        public const string IndexPrefix = Prefix + "index:";
        public const string DetailPrefix = Prefix + "detail:";
        public const string MyDetailPrefix = Prefix + "mydetail:";
        public const string EmployeePrefix = Prefix + "employee:";

        public static string IndexKey(string? keyword, string? city, int page, int pageSize) =>
            $"{IndexPrefix}{keyword ?? ""}:{city ?? ""}:{page}:{pageSize}";

        public static string DetailKey(int id) => $"{DetailPrefix}{id}";
        public static string MyDetailKey(int id) => $"{MyDetailPrefix}{id}";
        public static string EmployeeKey(int loginUserId) => $"{EmployeePrefix}{loginUserId}";

        public static Task ClearEmployeeAsync(IAppCacheService cache) =>
            cache.RemoveByPatternAsync($"{EmployeePrefix}*");

        /// <summary>清理含用户展示字段的订单缓存（列表、详情）</summary>
        public static Task ClearDisplayAsync(IAppCacheService cache) =>
            ClearPatternsAsync(cache, $"{IndexPrefix}*", $"{DetailPrefix}*", $"{MyDetailPrefix}*");

        public static Task ClearAllAsync(IAppCacheService cache) =>
            ClearPatternsAsync(cache, $"{IndexPrefix}*", $"{DetailPrefix}*", $"{MyDetailPrefix}*", $"{EmployeePrefix}*");

        private static async Task ClearPatternsAsync(IAppCacheService cache, params string[] patterns)
        {
            foreach (var pattern in patterns)
                await cache.RemoveByPatternAsync(pattern);
        }
    }
}
