using Dm;
using Microsoft.AspNetCore.Mvc;
using MuLanJobAPI.Entity;
using MuLanJobAPI.Service;
using SqlSugar;

namespace MuLanJobAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BannerController : ControllerBase
    {
        private readonly ISqlSugarClient _db;
        private readonly IWebHostEnvironment _env;
        public BannerController(ISqlSugarClient db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }
        #region 1. 获取首页轮播图（前端首页调用）
        /// <summary>
        /// 获取轮播图列表（排序后、未删除）
        /// </summary>
        [HttpGet("GetBannerList")]
        public async Task<ApiResult<List<Banner>>> GetBannerList()
        {
            var list = await _db.Queryable<Banner>()
                .Where(b=>b.is_enable==true)
                .OrderByDescending(b=>b.sort)
                .ToListAsync();
            return ApiResult<List<Banner>>.Success(list);
        }
        #endregion

        [HttpGet("GetAllBannerList")]
        public async Task<ApiResult<List<Banner>>> GetAllBannerList()
        {
            var list = await _db.Queryable<Banner>()
                .OrderByDescending(b => b.create_time)
                .ToListAsync();
            return ApiResult<List<Banner>>.Success(list);
        }

        #region 2. 上传轮播图（后台管理调用）
        /// <summary>
        /// 上传轮播图
        /// </summary>
        [HttpPost("Upload")]
        public async Task<ApiResult<object>> Upload(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return ApiResult<object>.Fail("请选择上传图片");

                string[] allowTypes = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                string ext = Path.GetExtension(file.FileName).ToLower();

                if (!allowTypes.Contains(ext))
                    return ApiResult<object>.Fail("只能上传 PNG、JPG、BMP、GIF 图片");

                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "banner");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{new Random().Next(100, 999)}{ext}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }

                // 生成完整URL
                string fullUrl = $"{Request.Scheme}://{Request.Host}/uploads/banner/{fileName}";


                // 先查表内是否有数据，没有就默认0，有就取最大sort
                int maxSort = 0;
                var hasData = await _db.Queryable<Banner>().AnyAsync();
                if (hasData)
                {
                    maxSort = await _db.Queryable<Banner>().MaxAsync(x => x.sort);
                }
                int newSort = maxSort + 1;

                var banner = new Banner
                {
                    image_url = fullUrl, 
                    sort = newSort,      
                    is_enable = true,
                    create_time = DateTime.Now,
                };

                await _db.Insertable(banner).ExecuteCommandAsync();

          
                return ApiResult<object>.Success(new { url = fullUrl });
            }
            catch (Exception ex)
            {
                return ApiResult<object>.Fail("上传失败：" + ex.Message);
            }
        }
        #endregion


        #region 启用 / 禁用
        [HttpPost("UpdateEnable")]
        public async Task<ApiResult<string>> UpdateEnable([FromBody] UpdateEnableDto dto)
        {
            await _db.Updateable<Banner>()
         .SetColumns(b => b.is_enable == dto.is_enable)
         .Where(b => b.id == dto.id)
         .ExecuteCommandAsync();

            return ApiResult<string>.Success("修改成功");
        }
        #endregion

        #region 修改排序
        [HttpPost("UpdateSort")]
        public async Task<ApiResult<string>> UpdateSort([FromBody] UpdateSortDto dto)
        {
            await _db.Updateable<Banner>()
                .SetColumns(b => b.sort, dto.sort)
                .Where(b => b.id == dto.id)
                .ExecuteCommandAsync();
            return ApiResult<string>.Success("成功");
        }
        #endregion

        #region 删除
        [HttpPost("Delete")]
        public async Task<ApiResult<string>> Delete(int id)
        {
            await _db.Deleteable<Banner>()
                .Where(b => b.id == id)
                .ExecuteCommandAsync();
            return ApiResult<string>.Success("删除成功");
        }
        #endregion

        #region 7. 修改图片（新增）
        [HttpPost("UpdateImage")]
        public async Task<ApiResult<string>> UpdateImage(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ApiResult<string>.Fail("请选择图片");

            string[] allowTypes = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            string ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowTypes.Contains(ext))
                return ApiResult<string>.Fail("格式错误");

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "banner");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{new Random().Next(100, 999)}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            string fullUrl = $"{Request.Scheme}://{Request.Host}/uploads/banner/{fileName}";

            await _db.Updateable<Banner>()
                .SetColumns(b => b.image_url == fullUrl)
                .Where(b => b.id == id)
                .ExecuteCommandAsync();

            return ApiResult<string>.Success("修改成功");
        }
        #endregion
    }
}
