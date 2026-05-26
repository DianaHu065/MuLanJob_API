using Microsoft.AspNetCore.Mvc;
using MuLanJobAPI.Entity;
using MuLanJobAPI.Service;

namespace MuLanJobAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FavoriteController : ControllerBase
    {
        private readonly FavoriteService _favoriteService;

        public FavoriteController(FavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }
        // 查询是否收藏
        [HttpGet("IsFavorite")]
        public async Task<ApiResult<object>> IsFavorite(int userId, int orderId)
        {
            var isFav = await _favoriteService.IsFavorite(userId, orderId);
            return ApiResult<object>.Success("查询成功", new { isFav });
        }

        // 收藏 / 取消收藏
        [HttpPost("ToggleFavorite")]
        public async Task<ApiResult<dynamic>> ToggleFavorite([FromBody] FavoriteToggleRequest request)
        {
            return await _favoriteService.ToggleFavoriteAsync(request.userId, request.orderId);
        }

        // 我的收藏列表
        [HttpGet("GetMyFavorites")]
        public async Task<ApiResult<object>> GetMyFavorites(int userId, int page = 1, int pageSize = 20)
        {
            var data = await _favoriteService.GetMyFavoritesAsync(userId, page, pageSize);
            return ApiResult<object>.Success("获取成功", data);
        }
    }
}
