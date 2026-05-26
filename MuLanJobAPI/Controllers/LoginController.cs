using Microsoft.AspNetCore.Mvc;
using MuLanJobAPI.Entity;
using MuLanJobAPI.Service;
using SqlSugar;
using StackExchange.Redis;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace MuLanJobAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LoginController : ControllerBase
    {

        private readonly LoginService _loginService;
        private readonly IWebHostEnvironment _env;
        public LoginController(LoginService loginService, IWebHostEnvironment env)
        {
            _loginService = loginService;
            _env = env;
        }

        /// <summary>
        /// 小程序微信登录（openid + 手机号）
        /// </summary>
        [HttpPost("WxLogin")]
        public async Task<IActionResult> WxLogin([FromBody] WxLoginDto dto)
        {
            var result = await _loginService.WxLoginAsync(dto.code, dto.phoneCode, dto.nickName,dto.avatar);
            return Ok(result);
        }

        //查询所有组
        [HttpGet("GetGroupList")]
        public async Task<IActionResult> GetGroupList()
        {
            var list = await _loginService.GetGroupListAsync();
            return Ok(list);
        }

        //新增员工
        [HttpPost("AddUser")]
        public async Task<IActionResult> AddUser([FromBody] User user)
        {
            var id = await _loginService.AddUserAsync(user);
            return Ok(id);
        }

        //删除员工
        [HttpPost("DeleteUser")]
        public async Task<IActionResult> DeleteUser([FromBody] int id)
        {
            var row = await _loginService.DeleteUserAsync(id);
            if (row > 0)
            {
                return Ok(ApiResult<int>.Success("删除成功", row));
            }
            else
            {
                return Ok(ApiResult<int>.Fail("删除失败，用户不存在"));
            }
        }

        // 禁用/启用员工
        [HttpPost("UpdateUserStatus")]
        public async Task<IActionResult> UpdateUserStatus([FromBody] UpdateUserStatusDto dto)
        {
            if (dto.Id <= 0)
                return Ok(ApiResult<int>.Fail("参数错误"));

            var row = await _loginService.UpdateUserStatusAsync(dto.Id, dto.Status);

            if (row > 0)
                return Ok(ApiResult<int>.Success("员工状态修改成功", row));
            else
                return Ok(ApiResult<int>.Fail("修改失败"));
        }


        // 修改员工
        [HttpPost("UpdateUser")]
        public async Task<IActionResult> UpdateUser([FromBody] User user)
        {
            var row = await _loginService.UpdateUserAsync(user);
            return Ok(row > 0 ? ApiResult<int>.Success("修改成功", row) : ApiResult<int>.Fail("修改失败"));
        }

        // 获取用户详情
        [HttpGet("GetUserById")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _loginService.GetUserByIdAsync(id);
            if (user == null)
                return Ok(ApiResult<User>.Fail("用户不存在"));

            return Ok(ApiResult<User>.Success("获取成功", user));
        }
        // 查询人员列表
        [HttpGet("GetUserList")]
        public async Task<IActionResult> GetUserList(int groupId = 0)
        {
            var list = await _loginService.GetUserListAsync(groupId);
            return Ok(list);
        }

        //修改员工手机号
        [HttpPost("UpdatePhone")]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdateUserPhoneDto model)
        {
            var row = await _loginService.UpdatePhoneAsync(model);
            return Ok(row > 0 ? ApiResult<int>.Success("修改手机号成功", row) : ApiResult<int>.Fail("修改手机号失败"));
        }

        //修改员工更新微信二维码
        [HttpPost("UpdateUserWechat")]
        public async Task<IActionResult> UpdateUserWechat([FromBody] WechatDto dto)
        {
            var row = await _loginService.UpdateUserWechatAsync(dto);
            return Ok(row > 0 ? ApiResult<int>.Success("修改二维码成功", row) : ApiResult<int>.Fail("修改二维码失败"));
        }

        //修改员工更新头像
        [HttpPost("UpdateAvatar")]
        public async Task<IActionResult> UpdateAvatar([FromBody] AvatarDto dto)
        {
            var row = await _loginService.UpdateAvatarAsync(dto);
            return Ok(row > 0 ? ApiResult<int>.Success("修改头像成功", row) : ApiResult<int>.Fail("修改头像失败"));
        }

        //查询名片
        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var list = await _loginService.GetAllUsersAsync();
            return Ok(ApiResult<object>.Success(list));
        }

        /// <summary>
        /// 根据老师userId查询所有订单
        /// </summary>
        [HttpGet("GetTeacherOrderList")]
        public async Task<IActionResult> GetTeacherOrderList(int userId)
        {
            var list = await _loginService.GetTeacherOrderListAsync(userId);
            return Ok(ApiResult<List<OrderDetailDto>>.Success(list));
        }

        /// <summary>
        /// 收藏老师
        /// </summary>
        [HttpPost("CollectBusiness")]
        public async Task<IActionResult> CollectBusiness([FromBody] CollectBusinessDto dto)
        {
            var result = await _loginService.CollectBusinessAsync(dto.UserId, dto.BusinessId);
            return Ok(result);
        }

        /// <summary>
        /// 取消收藏
        /// </summary>
        [HttpPost("CancelCollectBusiness")]
        public async Task<IActionResult> CancelCollectBusiness([FromBody] CollectBusinessDto dto)
        {
            var result = await _loginService.CancelCollectBusinessAsync(dto.UserId, dto.BusinessId);
            return Ok(result);
        }

        /// <summary>
        /// 获取我的收藏ID
        /// </summary>
        [HttpGet("GetMyCollectBusinessIds")]
        public async Task<IActionResult> GetMyCollectBusinessIds(int userId)
        {
            var result = await _loginService.GetMyCollectBusinessIdsAsync(userId);
            return Ok(result);
        }

        [HttpPost("UploadImage")]
        public async Task<ApiResult<object>> UploadImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return ApiResult<object>.Fail("请选择上传图片");

                // 格式校验
                string[] allowTypes = { ".png", ".jpg", ".jpeg", ".bmp" };
                string ext = Path.GetExtension(file.FileName).ToLower();

                if (!allowTypes.Contains(ext))
                    return ApiResult<object>.Fail("只能上传 PNG、JPG、BMP 图片");

                // 上传目录 wwwroot/uploads
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{new Random().Next(100, 999)}{ext}";

                // 保存
                var filePath = Path.Combine(uploadDir, fileName);
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }

                // 只返回相对路径，不包含域名
                string fullUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
                return ApiResult<object>.Success(new { url = fullUrl });
            }
            catch (Exception ex)
            {
                return ApiResult<object>.Fail("上传失败：" + ex.Message);
            }
        }


    }
}

