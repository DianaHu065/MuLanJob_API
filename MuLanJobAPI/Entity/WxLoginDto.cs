namespace MuLanJobAPI.Entity
{
    public class WxLoginDto
    {
        public string code { get; set; } = string.Empty;
        public string phoneCode { get; set; } = string.Empty;

        public string nickName { get; set; } = "";  // 昵称
        public string avatar { get; set; } = "";  // 头像
    }
}
