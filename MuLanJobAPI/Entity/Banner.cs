using SqlSugar;

namespace MuLanJobAPI.Entity
{
    [SugarTable("Banner")]
    public class Banner
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        /// <summary>
        /// 主键ID
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// 图片地址
        /// </summary>
        public string image_url { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int sort { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? create_time { get; set; }

        public bool? is_enable { get; set; }
        
    }
}
