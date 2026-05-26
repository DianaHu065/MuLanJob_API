using Microsoft.AspNetCore.Mvc;
using MuLanJobAPI.Entity;
using MuLanJobAPI.Service;
using System.Security.Claims;

namespace MuLanJobAPI.Controllers
{
    [Route("api/order")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrderController(OrderService orderService)
        {
            _orderService = orderService;
        }

        // 首页订单列表
        [HttpGet("index")]
        public async Task<ApiResult<object>> GetIndexOrders(string? keyword = "", string? city = "", int page = 1,int pageSize = 20)
        {
            var data = await _orderService.GetIndexOrderListAsync(keyword, city,page, pageSize);
            return ApiResult<object>.Success("获取成功", data);
        }
        // 首页 订单详情
        [HttpGet("GetOrderDetail")]
        public async Task<ApiResult<dynamic?>> GetOrderDetail(int id, int? share_from, int? share_roleId)
        {
            var order = await _orderService.GetOrderDetailAsync(id, share_from, share_roleId);
            if (order == null)
            {
                return ApiResult<dynamic?>.Fail("订单不存在");
            }
            return ApiResult<dynamic?>.Success("获取成功", order);
        }

        //订单管理：我的订单
        [HttpGet("GetMyOrderList")]
        public async Task<ApiResult<object>> GetMyOrderList(int loginUserId, int loginRoleId,int loginGroupId,string keyword = "", int? publishStatus = null, int page = 1,int pageSize = 20)
        {
            var data = await _orderService.GetMyOrderListAsync(loginUserId, loginRoleId, loginGroupId, keyword, publishStatus, page, pageSize);
            return ApiResult<object>.Success("获取成功", data);
        }


        // 订单管理：我的订单详情
        [HttpGet("GetMyOrderDetail")]
        public async Task<ApiResult<dynamic?>> GetMyOrderDetail(int id)
        {
            var order = await _orderService.GetMyOrderDetailAsync(id);
            if (order == null)
            {
                return ApiResult<dynamic?>.Fail("订单不存在");
            }
            return ApiResult<dynamic?>.Success("获取成功", order);
        }

        // 员工选择列表
        [HttpGet("EmployeeList")]
        public async Task<ApiResult<object>> EmployeeList(int loginUserId)
        {
            var data = await _orderService.EmployeeListAsync(loginUserId);
            return ApiResult<object>.Success("获取成功", data);
        }

        // 新增订单
        [HttpPost("AddOrder")]
        public async Task<ApiResult<dynamic>> AddOrder([FromBody] Orders orders)
        {
            return await _orderService.AddOrderAsync(orders);
        }


        // 订单上下架
        [HttpPost("TogglePublish")]
        public async Task<ApiResult<object>> TogglePublish([FromBody] TogglePublishDto dto)
        {
            var result = await _orderService.TogglePublishAsync(dto.Id, dto.Status);
            if (!result.success)
                return ApiResult<object>.Fail(result.message);

            return ApiResult<object>.Success("订单上下架成功",result.message);
        }

        //刷新订单 修改时间
        [HttpPost("refresh")]
        public async Task<IActionResult> refresh([FromBody] int orderId)
        {
            var row = await _orderService.refreshAsync(orderId);
            return Ok(row > 0 ? ApiResult<int>.Success("刷新订单成功", row) : ApiResult<int>.Fail("刷新订单失败"));
        }

        // 编辑：获取详情
        [HttpGet("GetOrderEditDetail")]
        public async Task<ApiResult<object>> GetOrderEditDetail(int id)
        {
            var order = await _orderService.GetOrderEditDetailAsync(id);
            if (order == null)
                return ApiResult<object>.Fail("订单不存在");

            return ApiResult<object>.Success("获取成功", order);
        }

        // 编辑订单
        [HttpPost("UpdateOrder")]
        public async Task<ApiResult<object>> UpdateOrder([FromBody] Orders model)
        {
            var result = await _orderService.UpdateOrderAsync(model);
            if (!result.success)
                return ApiResult<object>.Fail(result.message);

            return ApiResult<object>.Success("编辑订单成功",result.message);
        }
    }
}