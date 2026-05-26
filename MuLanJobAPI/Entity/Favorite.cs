using SqlSugar;

namespace MuLanJobAPI.Entity
{
    /// <summary>
    /// 收藏表
    /// </summary>
    [SugarTable("favorites")]
    public class Favorite
    {
        /// <summary>
        /// 收藏ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 收藏所属用户ID
        /// </summary>
        public int user_id { get; set; }

        /// <summary>
        /// 被收藏的订单ID
        /// </summary>
        public int order_id { get; set; }

        /// <summary>
        /// 收藏时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;

        #region 导航属性
        [Navigate(NavigateType.OneToOne, nameof(user_id))]
        public User user { get; set; }

        [Navigate(NavigateType.OneToOne, nameof(order_id))]
        public Orders order { get; set; }
        #endregion
    }
}
