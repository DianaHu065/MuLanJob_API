using MuLanJobAPI.Entity;
using SqlSugar;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

namespace MuLanJobAPI.Service
{
    public class OrderService
    {
        private readonly ISqlSugarClient _db;
        private readonly IAppCacheService _cache;

        private static readonly TimeSpan CacheShort = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan CacheMedium = TimeSpan.FromMinutes(5);

        public OrderService(ISqlSugarClient db, IAppCacheService cache)
        {
            _db = db;
            _cache = cache;
        }

        #region 首页订单列表
        public async Task<object> GetIndexOrderListAsync(string keyword,string city, int page, int pageSize)
        {
            var cacheKey = OrderCacheKeys.IndexKey(keyword, city, page, pageSize);

            return await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                /*var query = _db.Queryable<Orders>()
                    .Where(o => o.is_publish == 1 && o.is_delete == 0)
                    .OrderByDescending(o => o.create_time);*/

                var query = _db.Queryable<Orders, User>((o, u) => o.business_user_id == u.id)
                            .Where(o => o.is_publish == 1 && o.is_delete == 0)
                            .OrderByDescending(o => o.update_time);

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(o =>
                        o.order_no.Contains(keyword) ||
                        o.title.Contains(keyword) ||
                        o.city.Contains(keyword)
                    );
                }

                if (!string.IsNullOrWhiteSpace(city))
                {
                    if (city == "其他")
                    {
                        query = query.Where(o =>
                            !o.city.Contains("上海") &&
                            !o.city.Contains("江苏") &&
                            !o.city.Contains("浙江"));
                    }
                    else
                    {
                        query = query.Where(o => o.city.Contains(city));
                    }
                }
                // 先查总数，再查列表
                /*var total = await query.CountAsync();
                var list = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();*/

                // 关联查询 + 返回字段
                var total = await query.CountAsync();
                var list = await query
                    .Select((o, u) => new
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
                        o.startTimeType,
                        o.appointDate,
                        o.orderType,

                        // 用户信息
                        avatar = u.avatar,
                        nickname = u.nick_name,
                        real_name = u.real_name
                    })
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new
                {
                    list,
                    total,
                    page,
                    pageSize
                };
            }, CacheShort);
        }
        #endregion

        #region 首页订单详情
        public async Task<dynamic?> GetOrderDetailAsync(int id, int? share_from = null, int? share_roleId = null)
        {
            var cacheKey = OrderCacheKeys.DetailKey(id);

            var order = await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                return await _db.Queryable<Orders, User>((o, u) => o.business_user_id == u.id)
                    .Where(o => o.id == id && o.is_delete == 0 && o.is_publish == 1)
                    .Select((o, u) => new
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
                        avatar = u.avatar,
                        real_name = u.real_name,
                        originalPhone = u.phone,
                        originalWechat = u.wechat
                    })
                    .FirstAsync();
            }, CacheMedium, result => result != null);

            if (order == null) return null;

            string showPhone = order.originalPhone;
            string showWechat = order.originalWechat;

            if (share_from.HasValue && share_roleId == 2)
            {
                var shareUser = await _db.Queryable<User>()
                    .Where(u => u.id == share_from.Value && u.role_id == 2)
                    .Select(u => new { u.phone, u.wechat })
                    .FirstAsync();

                if (shareUser != null)
                {
                    showPhone = shareUser.phone;
                    showWechat = shareUser.wechat;
                }
            }

            return new
            {
                order.id,
                order.order_no,
                order.title,
                order.content,
                order.create_time,
                order.requirement,
                order.salary,
                order.workDays,
                order.city,
                order.services,
                order.liveType,
                order.location,
                order.startTimeType,
                order.appointDate,
                order.orderType,
                real_name = order.real_name,
                avatar = order.avatar,
                showPhone,
                showWechat
            };
        }
        #endregion

        #region 订单管理：我的订单
        public async Task<object> GetMyOrderListAsync(int loginUserId, int loginRoleId, int loginGroupId, string keyword, int? publishStatus, int page, int pageSize)
        {
            // 按登录人角色拼接查询条件
            var query = _db.Queryable<Orders, User>((o, u) => o.business_user_id == u.id)
                .Where(o => o.is_delete == 0)  // 只看未删除
                .OrderByDescending(o => o.update_time);

            // 权限过滤（核心）
            switch (loginRoleId)
            {
                case 1:
                    // 管理员：看所有
                    break;
                case 2:
                    // 团助：看自己组
                    query = query.Where(o => o.group_id == loginGroupId);
                    break;
                default:
                    // 员工/普通账号：只看自己
                    query = query.Where(o => o.business_user_id == loginUserId);
                    break;
            }

            // 搜索
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(o => o.title.Contains(keyword)
                                      || o.order_no.Contains(keyword)
                                      || o.content.Contains(keyword));
            }
            // 上架/下架 筛选 如果是全部，那就不筛选is_publish，如果是上架/下架就查询is_publish
            if (publishStatus.HasValue)
            {
                query = query.Where(o => o.is_publish == publishStatus.Value);
            }

            // 分页
            /* var total = await query.CountAsync();
            var list = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();*/
            var total = await query.CountAsync();
            var list = await query
                .Select((o, u) => new
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
                    o.startTimeType,
                    o.appointDate,
                    o.orderType,
                    o.is_publish,  // 必须加！

                    // 用户信息
                    avatar = u.avatar,
                    nickname = u.nick_name,
                    real_name = u.real_name
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new { list, total, page, pageSize };
        }
        #endregion



        #region 我的订单详情
        public async Task<dynamic?> GetMyOrderDetailAsync(int id)
        {
            var cacheKey = OrderCacheKeys.MyDetailKey(id);

            return await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                return await _db.Queryable<Orders, User>((o, u) => o.business_user_id == u.id)
                    .Where(o => o.id == id && o.is_delete == 0)
                    .Select((o, u) => new
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
                        o.is_publish,
                        avatar = u.avatar,
                        real_name = u.real_name,
                        phone = u.phone,
                        wechat = u.wechat
                    })
                    .FirstAsync();
            }, CacheMedium, result => result != null);
        }
        #endregion



        #region 员工选择列表
        public async Task<object> EmployeeListAsync(int loginUserId)
        {
            var cacheKey = OrderCacheKeys.EmployeeKey(loginUserId);

            return await _cache.GetOrCreateAsync<object>(cacheKey, async () =>
            {
                var loginUser = await _db.Queryable<User>()
                    .Select(u => new { u.id, u.role_id, u.group_id })
                    .FirstAsync(u => u.id == loginUserId);

                if (loginUser == null)
                    return Array.Empty<object>();

                var query = _db.Queryable<User>()
                    .Where(u => u.role_id != 4);

                query = loginUser.role_id switch
                {
                    2 => query.Where(u => u.group_id == loginUser.group_id),
                    3 => query.Where(u => u.id == loginUser.id),
                    _ => query
                };

                return await query
                    .Select(u => new { u.id, u.real_name })
                    .ToListAsync();
            }, CacheShort, result => result is ICollection { Count: > 0 });
        }
        #endregion

        #region 发布订单
        public async Task<ApiResult<dynamic>> AddOrderAsync(Orders dto)
        {
            try
            {
                // 1. 校验登录用户
                var loginUser = await _db.Queryable<User>()
                    .FirstAsync(u => u.id == dto.publisher_id);

                if (loginUser == null)
                    return ApiResult<dynamic>.Fail("用户不存在");

                // 2. 阿姨禁止发布
                if (loginUser.role_id == 4)
                    return ApiResult<dynamic>.Fail("阿姨无法发布订单");

                // 3. 归属人逻辑
                int businessUserId = loginUser.role_id switch
                {
                    1 or 2 => dto.business_user_id <= 0 ? throw new Exception("请选择员工") : dto.business_user_id,
                    3 => loginUser.id,
                    _ => throw new Exception("无发布权限")
                };

                // 4. 校验员工
                var employee = await _db.Queryable<User>()
                    .FirstAsync(u => u.id == businessUserId);

                if (employee == null)
                    return ApiResult<dynamic>.Fail("员工不存在");

                // 5. 生成订单号
                string prefix = string.IsNullOrWhiteSpace(employee.prefix) ? "QT" : employee.prefix.Trim();
                string orderNo = $"{prefix}{DateTime.Now:yyyyMMddHHmmss}";

                // 6. 构建订单
                var order = new Orders
                {
                    order_no = orderNo,
                    title = dto.title,
                    content = dto.content,
                    group_id = employee.group_id == 0 ? 1 : employee.group_id,
                    business_user_id = businessUserId,
                    publisher_id = loginUser.id,
                    requirement = dto.requirement,
                    salary = dto.salary,
                    workDays = dto.workDays,
                    city = dto.city,
                    services = dto.services,
                    liveType = dto.liveType,
                    location = dto.location,
                    startTimeType = dto.startTimeType,
                    appointDate = dto.appointDate,
                    orderType = dto.orderType,
                    is_publish = 1,
                    is_delete = 0,
                    create_time = DateTime.Now,
                    update_time = DateTime.Now
                };

                // 7. 入库
                int orderId = await _db.Insertable(order).ExecuteReturnIdentityAsync();

                // 8. 清理缓存（发布后必须清）
                await ClearAllOrderCacheAsync();

                return ApiResult<dynamic>.Success("发布成功", new { orderId, orderNo });
            }
            catch (Exception ex)
            {
                return ApiResult<dynamic>.Fail("发布失败：" + ex.Message);
            }
        }
        #endregion

        #region 上下架
        public async Task<ApiResult<object>> TogglePublishAsync(int Id, int Status)
        {
            var order = await _db.Queryable<Orders>().FirstAsync(o => o.id == Id);
            if (order == null)
                return ApiResult<object>.Fail("订单不存在");

            order.is_publish = (byte)Status;
            order.update_time = DateTime.Now;

            await _db.Updateable(order)
                .Where(o => o.id == order.id)
                .UpdateColumns(o => new { o.is_publish, o.update_time })
                .ExecuteCommandAsync();

            await ClearAllOrderCacheAsync();
            return ApiResult<object>.Success("操作成功", null);

            
        }
        #endregion

        #region 编辑获取详情
        public async Task<ApiResult<object>> GetOrderEditDetailAsync(int id)
        {
            var order = await _db.Queryable<Orders>().FirstAsync(o => o.id == id);
            if (order == null) return ApiResult<object>.Fail("订单不存在");

            return ApiResult<object>.Success("获取成功", order);
        }
        #endregion

        #region 编辑提交订单
        public async Task<ApiResult<object>> UpdateOrderAsync(Orders model)
        {
            if (model.id <= 0)
                return ApiResult<object>.Fail("订单ID不存在");

            model.update_time = DateTime.Now;

            await _db.Updateable(model)
                .Where(o => o.id == model.id)
                .IgnoreColumns(o => new {o.order_no ,o.create_time, o.is_delete, o.publisher_id, o.business_user_id, o.group_id })
                .ExecuteCommandAsync();

            await ClearAllOrderCacheAsync();
            return ApiResult<object>.Success("保存成功");
        }
        #endregion

        #region 刷新
        public async Task<int> refreshAsync(int orderId)
        {
            var result = await _db.Updateable<Orders>()
                .SetColumns(u => u.update_time == SqlFunc.GetDate())
                .Where(u => u.id == orderId)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearAllOrderCacheAsync();

            return result;
        }

        #endregion

        #region 统一清理缓存（高内聚）
        public Task ClearAllOrderCacheAsync() => OrderCacheKeys.ClearAllAsync(_cache);
        #endregion
    }
}