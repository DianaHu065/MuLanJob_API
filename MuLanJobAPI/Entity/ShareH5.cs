using SqlSugar;

namespace MuLanJobAPI.Entity
{
    /// <summary>
    /// H5永久分享记录表
    /// </summary>
    [SugarTable("share_h5")]
    public class ShareH5
    {
        /// <summary>
        /// 分享记录ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 唯一分享令牌 永久有效
        /// </summary>
        public string share_token { get; set; }

        /// <summary>
        /// 分享人ID(管理员/团助)
        /// </summary>
        public int share_user_id { get; set; }

        /// <summary>
        /// 关联订单ID
        /// </summary>
        public int order_id { get; set; }

        /// <summary>
        /// 分享时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;

        /// <summary>
        /// 1永久有效 0失效
        /// </summary>
        public byte is_valid { get; set; } = 1;

        #region 导航属性
        [Navigate(NavigateType.OneToOne, nameof(share_user_id))]
        public User share_user { get; set; }

        [Navigate(NavigateType.OneToOne, nameof(order_id))]
        public Orders order { get; set; }
        #endregion
    }
}
