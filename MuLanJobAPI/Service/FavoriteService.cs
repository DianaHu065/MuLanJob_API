using MuLanJobAPI.Entity;
using SqlSugar;

namespace MuLanJobAPI.Service
{
    public class FavoriteService
    {
        private readonly ISqlSugarClient _db;

        public FavoriteService(ISqlSugarClient db)
        {
            _db = db;
        }

        // 查询是否收藏
        public async Task<bool> IsFavorite(int userId, int orderId)
        {
            return await _db.Queryable<Favorite>()
                .AnyAsync(f => f.user_id == userId && f.order_id == orderId);
        }

        // 切换收藏（收藏/取消）
        public async Task<ApiResult<dynamic>> ToggleFavoriteAsync(int userId, int orderId)
        {
            if (userId <= 0 || orderId <= 0)
                return ApiResult<dynamic>.Fail("参数错误");

            // 检查是否已经收藏
            var exists = await _db.Queryable<Favorite>()
                .AnyAsync(f => f.user_id == userId && f.order_id == orderId);

            if (exists)
            {
                // 取消收藏
                await _db.Deleteable<Favorite>()
                    .Where(f => f.user_id == userId && f.order_id == orderId)
                    .ExecuteCommandAsync();

                return ApiResult<dynamic>.Success("取消收藏成功");
            }
            else
            {
                // 添加收藏
                var fav = new Favorite
                {
                    user_id = userId,
                    order_id = orderId,
                    create_time = DateTime.Now
                };

                await _db.Insertable(fav).ExecuteCommandAsync();
                return ApiResult<dynamic>.Success("收藏成功");
            }
        }

        // 获取我的收藏列表
        public async Task<object> GetMyFavoritesAsync(int userId, int page = 1, int pageSize = 20)
        {
            var q = _db.Queryable<Favorite, Orders, User>((f, o, u) =>
                f.order_id == o.id && o.business_user_id == u.id) 
            .Where((f, o, u) =>
                f.user_id == userId &&
                o.is_publish == 1 &&
                o.is_delete == 0)
            .OrderByDescending(f => f.create_time);

            var total = await q.CountAsync();

            var list = await q
                .Select((f, o,u) => new
                {
                    o.id,
                    o.order_no,
                    o.title,
                    o.content,
                    o.create_time,
                    o.requirement,
                    o.salary,
                    o.workDays,
                    o.city,
                    o.services,
                    o.liveType,
                    o.location,
                    o.startTimeType,
                    o.appointDate,
                    o.orderType,
                    u.avatar,       // 头像
                    u.real_name,    // 真实姓名
                    u.nick_name     // 昵称（可选）

                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new { list, total, page, pageSize };
        }
    }
}