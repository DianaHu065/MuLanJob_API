using SqlSugar;
using System.Text.Json.Serialization;

namespace MuLanJobAPI.Entity
{
    /// <summary>
    /// 订单表
    /// </summary>
    [SugarTable("orders")]
    public class Orders
    {
        /// <summary>
        /// 订单自增ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 订单编号：员工前缀+年月日时分秒
        /// </summary>
        public string? order_no { get; set; }

        /// <summary>
        /// 订单标题
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// 订单详细内容
        /// </summary>
        public string content { get; set; }
        
        /// <summary>
        /// 阿姨要求
        /// </summary>
        public string requirement { get; set; }
        
        /// <summary>
        /// 薪资
        /// </summary>
        public string salary { get; set; }           

        /// <summary>
        /// 工作天数
        /// </summary>
        public int workDays { get; set; }

        /// <summary>
        /// 省
        /// </summary>
        public string city { get; set; }

        /// <summary>
        /// 服务内容
        /// </summary>
        public string services { get; set; }

        /// <summary>
        /// 住家类型
        /// </summary>
        public string? liveType { get; set; }

        /// <summary>
        /// 客户位置
        /// </summary>
        public string? location { get; set; }          

        /// <summary>
        /// 上户时间类型
        /// </summary>
        public string? startTimeType { get; set; }   

        /// <summary>
        /// 指定上户日期
        /// </summary>
        public string? appointDate { get; set; }
   
        /// <summary>
        /// 订单类型
        /// </summary>
        public string orderType { get; set; }

        /// <summary>
        /// 归属业务老师ID
        /// </summary>
        public int business_user_id { get; set; }

        /// <summary>
        /// 归属业务老师姓名
        /// </summary>
        //public string business_username { get; set; }

        /// <summary>
        /// 订单发布人ID
        /// </summary>
        public int publisher_id { get; set; }

        /// <summary>
        /// 分组
        /// </summary>
        public int? group_id { get; set; }

        /// <summary>
        /// 1上架 0下架
        /// </summary>
        public byte? is_publish { get; set; } = 1;

        /// <summary>
        /// 1软删除 0正常
        /// </summary>
        public byte? is_delete { get; set; } = 0;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime update_time { get; set; } = DateTime.Now;

        #region 导航属性
        //[Navigate(NavigateType.OneToOne, nameof(business_user_id))]
        //[SugarColumn(IsIgnore = true)]
        //public User business_user { get; set; }

        //[Navigate(NavigateType.OneToOne, nameof(publisher_id))]
        //[SugarColumn(IsIgnore = true)]
        //public User publisher { get; set; }
        #endregion
    }
}