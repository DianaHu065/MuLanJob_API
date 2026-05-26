using SqlSugar;
namespace MuLanJobAPI.Entity
{
    /// <summary>
    /// 用户表
    /// 员工=业务老师 带prefix前缀；团助/阿姨prefix为空
    /// </summary>
    [SugarTable("users")]
    public class User
    {
        /// <summary>
        /// 用户主键ID 自增
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int id { get; set; }

        /// <summary>
        /// 微信唯一OpenID
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? openid { get; set; }

        /// <summary>
        /// 微信昵称
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? nick_name { get; set; }

        /// <summary>
        /// 微信头像地址
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? avatar { get; set; }

        /// <summary>
        /// 真实姓名
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string real_name { get; set; }

        /// <summary>
        /// 对外展示手机号
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string phone { get; set; }

        /// <summary>
        /// 对外展示微信号
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string wechat { get; set; }

        /// <summary>
        /// 员工(业务老师)订单前缀 
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string prefix { get; set; }

        /// <summary>
        /// 关联角色ID
        /// </summary>
        public int role_id { get; set; }

        /// <summary>
        /// 1=内部员工/团助 0=阿姨游客
        /// </summary>
        public byte is_staff { get; set; } = 0;

        /// <summary>
        /// 1正常 0禁用
        /// </summary>
        public byte status { get; set; } = 1;

        /// <summary>
        /// 分组
        /// </summary>
        public int? group_id { get; set; }
        

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;

        #region 导航属性（SqlSugar 联表用）
        //[Navigate(NavigateType.OneToOne, nameof(role_id))]
        //public Roles role { get; set; }
        #endregion
    }
}
