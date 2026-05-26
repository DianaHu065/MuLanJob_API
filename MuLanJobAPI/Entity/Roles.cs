using SqlSugar;

namespace MuLanJobAPI.Entity
{
    /// <summary>
    /// 角色表
    /// 1管理员 2团助 3员工 4阿姨
    /// </summary>
    [SugarTable("roles")]
    public class Roles
    {
        /// <summary>
        /// 角色ID 主键自增
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// 1可看全部订单 0否
        /// </summary>
        public byte view_all { get; set; } = 0;

        /// <summary>
        /// 1只看自己归属订单 0否
        /// </summary>
        public byte view_own { get; set; } = 0;

        /// <summary>
        /// 可发布/编辑/上架/下架/删除订单
        /// </summary>
        public byte can_operate { get; set; } = 0;

        /// <summary>
        /// 1可分享H5卡片 0只能分享小程序卡片
        /// </summary>
        public byte can_share_h5 { get; set; } = 0;

        public byte view_group { get; set; } = 0;
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
