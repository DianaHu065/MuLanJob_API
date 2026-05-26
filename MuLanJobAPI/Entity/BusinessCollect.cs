using SqlSugar;

namespace MuLanJobAPI.Entity
{
    [SugarTable("BusinessCollect")]
    /// <summary>
    /// 商家/老师收藏表
    /// </summary>
    public class BusinessCollect
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        /// <summary>
        /// 主键Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 商家/老师ID
        /// </summary>
        public int BusinessId { get; set; }

        /// <summary>
        /// 收藏时间
        /// </summary>
        public DateTime CreateTime { get; set; }
    }
}
