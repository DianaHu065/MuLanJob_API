using SqlSugar;

namespace MuLanJobAPI.Entity
{
    [SugarTable("groups")]
    public class Groups
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 分组名称
        /// </summary>
        public string group_name { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
