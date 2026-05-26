
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MuLanJobAPI.Entity;
using SqlSugar;
using StackExchange.Redis;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuLanJobAPI.Service
{
    public class LoginService
    {
        private readonly ISqlSugarClient _db;
        private readonly IDatabase _redis;
        private readonly WxConfig _wxConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<LoginService> _logger;
        private readonly IAppCacheService _cache; 

        // Redis 缓存 Key
        private const string WX_ACCESS_TOKEN_KEY = "wechat:access_token";
        private const string WX_ACCESS_TOKEN_LOCK_KEY = "wechat:access_token_lock";
        public LoginService(ISqlSugarClient db,IConnectionMultiplexer redis,IOptions<WxConfig> wxConfig,ILogger<LoginService> logger, IAppCacheService cache)
        {
            _db = db;
            _redis = redis.GetDatabase();
            _wxConfig = wxConfig.Value;
            _httpClient = new HttpClient();
            _logger = logger;
            _cache = cache;
        }

        private Task ClearEmployeeCacheAsync() => OrderCacheKeys.ClearEmployeeAsync(_cache);

        /// <summary>用户资料变更时清理订单列表/详情中的展示字段缓存</summary>
        private Task ClearOrderDisplayCacheAsync() => OrderCacheKeys.ClearDisplayAsync(_cache);

        private async Task ClearUserRelatedOrderCacheAsync()
        {
            await OrderCacheKeys.ClearAllAsync(_cache);
        }



        /// <summary>
        /// 微信小程序登录（获取 openid + 手机号）
        /// </summary>
        public async Task<dynamic> WxLoginAsync(string code, string phoneCode, string nickName,string avatar)
        {
            try
            {
                // 1. 获取 openid（不缓存）
                string openid = await GetOpenIdAsync(code);
                if (string.IsNullOrEmpty(openid))
                {
                    return new { code = 0, msg = "获取openid失败" };
                }

                // 2. 获取 access_token（Redis 缓存）
                string accessToken = await GetAccessTokenWithRedisAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new { code = 0, msg = "获取AccessToken失败" };
                }

                // 3. 获取手机号（不缓存）
                string phone = await GetPhoneNumberAsync(accessToken, phoneCode);
                if (string.IsNullOrEmpty(phone))
                {
                    return new { code = 0, msg = "获取手机号失败" };
                }


                // 4. 登录逻辑：自动判断 员工 / 游客
                return await HandleUserLoginAsync(openid, phone, nickName, avatar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信登录失败");
                return new { code = 0, msg = "登录失败：" + ex.Message };
            }
        }

        /// <summary>
        /// 获取 openid
        /// </summary>
        private async Task<string> GetOpenIdAsync(string code)
        {
            var sessionUrl = $"https://api.weixin.qq.com/sns/jscode2session" +
                $"?appid={_wxConfig.AppId}" +
                $"&secret={_wxConfig.AppSecret}" +
                $"&js_code={code}" +
                $"&grant_type=authorization_code";

            string sessionJson = await _httpClient.GetStringAsync(sessionUrl);
            var sessionRes = JsonDocument.Parse(sessionJson).RootElement;

            if (sessionRes.TryGetProperty("errcode", out var errCode))
            {
                _logger.LogError("获取 openid 失败: {Error}", sessionJson);
                return null;
            }

            return sessionRes.GetProperty("openid").GetString();
        }

        /// <summary>
        /// 获取 access_token（Redis 缓存，支持分布式锁）
        /// </summary>
        private async Task<string> GetAccessTokenWithRedisAsync()
        {
            try
            {
                // 1. 先从 Redis 获取
                string cachedToken = await _redis.StringGetAsync(WX_ACCESS_TOKEN_KEY);
                if (!string.IsNullOrEmpty(cachedToken))
                {
                    _logger.LogDebug("从 Redis 获取 access_token 成功");
                    return cachedToken;
                }

                // 2. 使用分布式锁，防止多个请求同时刷新
                string lockValue = Guid.NewGuid().ToString();
                bool lockAcquired = await _redis.StringSetAsync(
                    WX_ACCESS_TOKEN_LOCK_KEY,
                    lockValue,
                    TimeSpan.FromSeconds(10),
                    When.NotExists);

                if (lockAcquired)
                {
                    try
                    {
                        _logger.LogInformation("获取锁成功，刷新 access_token");

                        // 请求新的 access_token
                        string newToken = await FetchAccessTokenFromWeChatAsync();
                        if (!string.IsNullOrEmpty(newToken))
                        {
                            // 存入 Redis，有效期 7000 秒（微信是7200秒，提前200秒过期）
                            await _redis.StringSetAsync(WX_ACCESS_TOKEN_KEY, newToken, TimeSpan.FromSeconds(7000));
                            return newToken;
                        }

                        return null;
                    }
                    finally
                    {
                        // 释放锁（使用 Lua 脚本保证原子性）
                        var script = @"
                            if redis.call('get', KEYS[1]) == ARGV[1] then
                                return redis.call('del', KEYS[1])
                            else
                                return 0
                            end";

                        await _redis.ScriptEvaluateAsync(script,
                            new RedisKey[] { WX_ACCESS_TOKEN_LOCK_KEY },
                            new RedisValue[] { lockValue });
                    }
                }
                else
                {
                    // 没获取到锁，等待其他实例刷新完成
                    _logger.LogInformation("等待其他实例刷新 token");

                    for (int i = 0; i < 30; i++) // 等待最多 3 秒
                    {
                        await Task.Delay(100);
                        cachedToken = await _redis.StringGetAsync(WX_ACCESS_TOKEN_KEY);
                        if (!string.IsNullOrEmpty(cachedToken))
                        {
                            return cachedToken;
                        }
                    }

                    // 超时后降级：直接请求
                    _logger.LogWarning("等待超时，直接请求微信 API");
                    return await FetchAccessTokenFromWeChatAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 access_token 异常");
                // 降级：直接请求微信 API
                return await FetchAccessTokenFromWeChatAsync();
            }
        }

        /// <summary>
        /// 从微信 API 获取 access_token
        /// </summary>
        private async Task<string> FetchAccessTokenFromWeChatAsync()
        {
            var tokenUrl = $"https://api.weixin.qq.com/cgi-bin/token" +
                $"?grant_type=client_credential" +
                $"&appid={_wxConfig.AppId}" +
                $"&secret={_wxConfig.AppSecret}";

            string tokenJson = await _httpClient.GetStringAsync(tokenUrl);
            var tokenRes = JsonDocument.Parse(tokenJson).RootElement;

            if (tokenRes.TryGetProperty("errcode", out var errCode))
            {
                _logger.LogError("获取 AccessToken 失败: {Error}", tokenJson);
                return null;
            }

            string accessToken = tokenRes.GetProperty("access_token").GetString();
            int expiresIn = tokenRes.GetProperty("expires_in").GetInt32();

            _logger.LogInformation("成功获取新的 access_token，有效期 {ExpiresIn} 秒", expiresIn);

            return accessToken;
        }

        /// <summary>
        /// 获取手机号
        /// </summary>
        private async Task<string> GetPhoneNumberAsync(string accessToken, string phoneCode)
        {
            var phoneUrl = $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={accessToken}";

            var phoneContent = new { code = phoneCode };
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(phoneContent),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage phoneResponse = await _httpClient.PostAsync(phoneUrl, jsonContent);
            string phoneJson = await phoneResponse.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(phoneJson))
            {
                _logger.LogError("获取手机号失败：接口返回空");
                return null;
            }

            JsonDocument phoneDoc;
            try
            {
                phoneDoc = JsonDocument.Parse(phoneJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取手机号失败：返回非JSON格式 {PhoneJson}", phoneJson);
                return null;
            }

            var phoneRes = phoneDoc.RootElement;

            int errcode = phoneRes.GetProperty("errcode").GetInt32();
            if (errcode != 0)
            {
                _logger.LogError("微信错误：{Errcode} {Errmsg}", errcode, phoneRes.GetProperty("errmsg").GetString());
                return null;
            }

            return phoneRes
                .GetProperty("phone_info")
                .GetProperty("phoneNumber")
                .GetString();
        }

        /// <summary>
        /// 处理用户登录逻辑（员工/游客）
        /// </summary>
        private async Task<dynamic> HandleUserLoginAsync(string openid, string phone, string nickName, string avatar)
        {
            // 用手机号查询是否是员工（管理员预先添加）
            var existUser = await _db.Queryable<User>()
                .LeftJoin<Roles>((u, r) => u.role_id == r.id)
                .Where(u => u.phone == phone)
                .Select((u, r) => new
                {
                    User = u,
                    Roles = r
                })
                .FirstAsync();

            if (existUser != null)
            {
                // 员工：绑定 openid + 更新昵称，直接登录
                var u = existUser.User;
                u.openid = openid;
                u.nick_name = nickName; 
                u.avatar = avatar;

                await _db.Updateable(u)
                    .UpdateColumns(x => new { x.openid, x.nick_name, x.avatar })
                    .ExecuteCommandAsync();

                var role = existUser.Roles;
                return new
                {
                    code = 200,
                    msg = "登录成功",
                    data = new
                    {
                        userId = u.id,
                        openid = u.openid,
                        phone = u.phone,
                        nickName = u.nick_name,
                        realName = u.real_name,
                        prefix = u.prefix,
                        groupId = u.group_id,
                        roleId = u.role_id,
                        isStaff = u.is_staff,
                        status = u.status,
                        wechat = u.wechat,
                        avatar=u.avatar,
                        canOperate = role.can_operate == 1,
                        viewAll = role.view_all == 1,
                        viewGroup = role.view_group == 1,
                        viewOwn = role.view_own == 1
                    }
                };
            }
            else
            {
                // 游客：自动创建
                var newUser = new User
                {
                    openid = openid,
                    phone = phone,
                    nick_name = nickName,
                    avatar = avatar,
                    role_id = 4,
                    is_staff = 0,
                    status = 1,
                    create_time = DateTime.Now
                };
                long newId = await _db.Insertable(newUser).ExecuteReturnIdentityAsync();
                newUser.id = (int)newId;

                return new
                {
                    code = 200,
                    msg = "游客登录成功",
                    data = new
                    {
                        userId = newUser.id,
                        openid = newUser.openid,
                        phone = newUser.phone,
                        nickName = newUser.nick_name,
                        avatar = newUser.avatar,
                        roleId = 4,
                        isStaff = false,
                        canOperate = false,
                        groupId = 0,
                        prefix = ""
                    }
                };
            }
        }

        /// <summary>
        /// 手动刷新 access_token（管理后台调用）
        /// </summary>
        public async Task<bool> RefreshAccessTokenManuallyAsync()
        {
            try
            {
                // 删除缓存
                await _redis.KeyDeleteAsync(WX_ACCESS_TOKEN_KEY);

                // 重新获取
                string newToken = await FetchAccessTokenFromWeChatAsync();
                if (!string.IsNullOrEmpty(newToken))
                {
                    await _redis.StringSetAsync(WX_ACCESS_TOKEN_KEY, newToken, TimeSpan.FromSeconds(7000));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手动刷新 token 失败");
                return false;
            }
        }

        /// <summary>
        /// 获取 token 状态（监控用）
        /// </summary>
        public async Task<object> GetTokenStatusAsync()
        {
            var cachedToken = await _redis.StringGetAsync(WX_ACCESS_TOKEN_KEY);

            return new
            {
                isCached = !string.IsNullOrEmpty(cachedToken),
                cacheKey = WX_ACCESS_TOKEN_KEY,
                tokenLength = cachedToken.ToString()?.Length ?? 0,
                cachedTime = DateTime.Now
            };
        }


        #region 废弃方法
        ///// <summary>
        ///// 微信小程序登录（获取 openid + 手机号）
        ///// </summary>
        //public async Task<dynamic> WxLoginAsync(string code, string phoneCode, string nickName)
        //{
        //    try
        //    {
        //        // 1. 获取 openid

        //        var sessionUrl = $"https://api.weixin.qq.com/sns/jscode2session" +
        //            $"?appid={_wxConfig.AppId}" +
        //            $"&secret={_wxConfig.AppSecret}" +
        //            $"&js_code={code}" +
        //            $"&grant_type=authorization_code";

        //        string sessionJson = await _httpClient.GetStringAsync(sessionUrl);
        //        var sessionRes = JsonDocument.Parse(sessionJson).RootElement;

        //        // 先判断有没有错误
        //        if (sessionRes.TryGetProperty("errcode", out var errCode))
        //        {
        //            return new
        //            {
        //                code = 0,
        //                msg = "微信登录失败：" + sessionRes.GetProperty("errmsg").GetString()
        //            };
        //        }


        //        string openid = sessionRes.GetProperty("openid").GetString()!;

        //        // 2. 获取 access_token

        //        var tokenUrl = $"https://api.weixin.qq.com/cgi-bin/token" +
        //            $"?grant_type=client_credential" +
        //            $"&appid={_wxConfig.AppId}" +
        //            $"&secret={_wxConfig.AppSecret}";

        //        string tokenJson = await _httpClient.GetStringAsync(tokenUrl);
        //        var tokenRes = JsonDocument.Parse(tokenJson).RootElement;

        //        if (tokenRes.TryGetProperty("errcode", out _))
        //        {
        //            return new { code = 0, msg = "获取AccessToken失败" };
        //        }

        //        string accessToken = tokenRes.GetProperty("access_token").GetString()!;

        //        // 3. 获取手机号
        //        var phoneUrl = $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={accessToken}";

        //        var phoneContent = new { code = phoneCode };
        //        var jsonContent = new StringContent(
        //            JsonSerializer.Serialize(phoneContent),
        //            Encoding.UTF8,
        //            "application/json");

        //        HttpResponseMessage phoneResponse = await _httpClient.PostAsync(phoneUrl, jsonContent);
        //        string phoneJson = await phoneResponse.Content.ReadAsStringAsync();

        //        if (string.IsNullOrWhiteSpace(phoneJson))
        //        {
        //            return new { code = 0, msg = "获取手机号失败：接口返回空" };
        //        }

        //        JsonDocument phoneDoc;
        //        try
        //        {
        //            phoneDoc = JsonDocument.Parse(phoneJson);
        //        }
        //        catch
        //        {
        //            return new { code = 0, msg = "获取手机号失败：返回非JSON格式" };
        //        }

        //        var phoneRes = phoneDoc.RootElement;

        //        int errcode = phoneRes.GetProperty("errcode").GetInt32();
        //        if (errcode != 0)
        //        {
        //            return new { code = 0, msg = $"微信错误：{errcode} {phoneRes.GetProperty("errmsg")}" };
        //        }

        //        string phone = phoneRes
        //            .GetProperty("phone_info")
        //            .GetProperty("phoneNumber")
        //            .GetString()!;

        //        // 4. 登录逻辑：自动判断 员工 / 游客


        //        // 4.1. 用手机号查询是否是 员工（管理员预先添加）
        //        var existUser = await _db.Queryable<User>()
        //            .LeftJoin<Roles>((u, r) => u.role_id == r.id)
        //            .Where(u => u.phone == phone)
        //            .Select((u, r) => new
        //            {
        //                User = u,
        //                Roles = r
        //            })
        //            .FirstAsync();

        //        if (existUser != null)
        //        {
        //            // 员工：绑定 openid + 更新昵称 ，直接登录
        //            var u = existUser.User;
        //            u.openid = openid; // 第一次登录绑定微信
        //            u.nick_name = nickName;
        //            await _db.Updateable(u).UpdateColumns(x => new { x.openid, x.nick_name }).ExecuteCommandAsync();


        //            var role = existUser.Roles;
        //            return new
        //            {
        //                code = 200,
        //                msg = "登录成功",
        //                data = new
        //                {
        //                    userId = u.id,
        //                    openid = u.openid,
        //                    phone = u.phone,
        //                    nickName = u.nick_name,
        //                    //avatar = u.avatar,
        //                    realName = u.real_name,
        //                    prefix = u.prefix,         // 员工前缀（生成订单号用）
        //                    groupId = u.group_id,       // 所属分组
        //                    roleId = u.role_id,
        //                    isStaff = u.is_staff,  // 是否内部员工（true=有权限）
        //                    status = u.status,
        //                    wechat = u.wechat,
        //                    // 权限字段
        //                    canOperate = role.can_operate == 1,    // 操作订单权限
        //                    viewAll = role.view_all == 1,          // 查看全部
        //                    viewGroup = role.view_group == 1,      // 查看本组
        //                    viewOwn = role.view_own == 1           // 查看自己
        //                }
        //            };
        //        }
        //        else
        //        {
        //            // 游客：自动创建
        //            var newUser = new User
        //            {
        //                openid = openid,
        //                phone = phone,
        //                nick_name = nickName,
        //                avatar = "",
        //                role_id = 4,
        //                is_staff = 0,
        //                status = 1,
        //                create_time = DateTime.Now
        //            };
        //            await _db.Insertable(newUser).ExecuteReturnIdentityAsync();

        //            return new
        //            {
        //                code = 200,
        //                msg = "游客登录成功",
        //                data = new
        //                {
        //                    userId = newUser.id,
        //                    openid = newUser.openid,
        //                    phone = newUser.phone,
        //                    nickName = newUser.nick_name,
        //                    roleId = 4,
        //                    isStaff = false,       // 无权限
        //                    canOperate = false,
        //                    groupId = 0,
        //                    prefix = ""
        //                }
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return new { code = 0, msg = "登录失败：" + ex.Message };
        //    }
        //}
        #endregion


        //查询分组
        public async Task<List<Groups>> GetGroupListAsync()
        {
            try
            {
                return await _db.Queryable<Groups>().ToListAsync();
            }
            catch
            {
                return new List<Groups>();
            }
        }

        // 新增员工
        public async Task<int> AddUserAsync(User user)
        {
            user.create_time = DateTime.Now;
            user.status = 1;

            var result = await _db.Insertable(user).ExecuteReturnIdentityAsync();

            await ClearUserRelatedOrderCacheAsync();

            return result;
        }

        //删除员工
        public async Task<int> DeleteUserAsync(int id)
        {
            var result = await _db.Deleteable<User>().Where(u => u.id == id).ExecuteCommandAsync();

            if (result > 0)
                await ClearUserRelatedOrderCacheAsync();

            return result;
        }

        // 禁用 启用员工
        public async Task<int> UpdateUserStatusAsync(int userId, int status)
        {
            var result = await _db.Updateable<User>()
                .SetColumns(u => u.status == status)
                .Where(u => u.id == userId)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearUserRelatedOrderCacheAsync();

            return result;
        }

        //修改员工
        public async Task<int> UpdateUserAsync(User user)
        {
            var result = await _db.Updateable(user)
                .IgnoreColumns(u => new { u.openid, u.create_time })
                .Where(u => u.id == user.id)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearUserRelatedOrderCacheAsync();

            return result;
        }

        // 查询人员列表 管理员 groupId=0 → 查全部   团助 groupId=查自己组
        public async Task<List<object>> GetUserListAsync(int groupId = 0)
        {
            // 角色：2团助 | 3员工 | 4阿姨
            var query = _db.Queryable<User, Groups>((u, g) => u.group_id == g.id)
                .Where(u => u.role_id == 2 || u.role_id == 3 || u.role_id == 4);

            // 按分组筛选（管理员=0查全部）
            if (groupId > 0)
            {
                query = query.Where(u => u.group_id == groupId);
            }

            var list = await query
                .Select((u, g) => new
                {
                    id = u.id,
                    real_name = u.real_name,
                    phone = u.phone,
                    avatar = u.avatar,
                    wechat = u.wechat,
                    role_id = u.role_id,
                    status = u.status,
                    group_name = g.group_name ?? "无分组"
                })
                .ToListAsync();

            return list.Cast<object>().ToList();
        }

        // 获取用户详情
        public async Task<User> GetUserByIdAsync(int id)
        {
            return await _db.Queryable<User>().In(id).FirstAsync();
        }

        //修改手机号
        public async Task<int> UpdatePhoneAsync(UpdateUserPhoneDto dto)
        {
            var result = await _db.Updateable<User>()
                .SetColumns(u => u.phone == dto.phone)
                .Where(u => u.id == dto.Id)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearOrderDisplayCacheAsync();

            return result;
        }


        //修改更新微信二维码
        public async Task<int> UpdateUserWechatAsync(WechatDto dto)
        {

            var result = await _db.Updateable<User>()
                .SetColumns(u => u.wechat == dto.wechat)
                .Where(u => u.id == dto.id)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearOrderDisplayCacheAsync();

            return result;
        }
        //修改更新头像
        public async Task<int> UpdateAvatarAsync(AvatarDto dto)
        {

            var result = await _db.Updateable<User>()
                .SetColumns(u => u.avatar == dto.avatar)
                .Where(u => u.id == dto.id)
                .ExecuteCommandAsync();

            if (result > 0)
                await ClearOrderDisplayCacheAsync();

            return result;
        }

        // 卡片：查询人员列表 查询user表的所有数据

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _db.Queryable<User>()
                      .Where(u => u.status == 1 && u.role_id !=4) // 只查正常状态
                      .ToListAsync();
        }

        // 查询老师所有订单
        public async Task<List<OrderDetailDto>> GetTeacherOrderListAsync(int userId)
        {
            if (userId <= 0)
                throw new Exception("用户ID无效");

            var orderList = await _db.Queryable<Orders, User>((o, u) => o.business_user_id == u.id)
                    .Where(o => o.business_user_id == userId && o.is_publish == 1)
                    .Select((o, u) => new OrderDetailDto
                    {
                        id = o.id,
                        order_no = o.order_no,
                        title = o.title,
                        content = o.content,
                        create_time = o.create_time,
                        requirement = o.requirement,
                        salary = o.salary,
                        workDays = o.workDays,
                        city = o.city,
                        services = o.services,
                        liveType = o.liveType,
                        location = o.location,
                        startTimeType = o.startTimeType,
                        appointDate = o.appointDate,
                        orderType = o.orderType,
                        is_publish = o.is_publish,

                        // 从 User 表取
                        real_name = u.real_name,
                        phone = u.phone,
                        wechat = u.wechat,
                        avatar = u.avatar
                    })
                    .ToListAsync();

            return orderList;
        }

        // 收藏老师
        public async Task<ApiResult<int>> CollectBusinessAsync(int userId, int businessId)
        {
            if (userId <= 0 || businessId <= 0)
                return ApiResult<int>.Fail("参数错误");

            // 判断是否已收藏
            var isExist = await _db.Queryable<BusinessCollect>()
                                   .Where(x => x.UserId == userId && x.BusinessId == businessId)
                                   .AnyAsync();

            if (isExist)
                return ApiResult<int>.Success("已收藏", 0);

            // 新增收藏
            var model = new BusinessCollect
            {
                UserId = userId,
                BusinessId = businessId,
                CreateTime = DateTime.Now
            };

            var id = await _db.Insertable(model).ExecuteReturnIdentityAsync();
            return ApiResult<int>.Success("收藏成功", id);
        }

        // 取消收藏
        public async Task<ApiResult<string>> CancelCollectBusinessAsync(int userId, int businessId)
        {
            if (userId <= 0 || businessId <= 0)
                return ApiResult<string>.Fail("参数错误");

            await _db.Deleteable<BusinessCollect>()
                     .Where(x => x.UserId == userId && x.BusinessId == businessId)
                     .ExecuteCommandAsync();

            return ApiResult<string>.Success("取消成功");
        }

        // 获取我的收藏ID列表
        public async Task<ApiResult<List<int>>> GetMyCollectBusinessIdsAsync(int userId)
        {
            if (userId <= 0)
                return ApiResult<List<int>>.Fail("参数错误");

            var ids = await _db.Queryable<BusinessCollect>()
                               .Where(x => x.UserId == userId)
                               .Select(x => x.BusinessId)
                               .ToListAsync();

            return ApiResult<List<int>>.Success(ids);
        }
    }
}