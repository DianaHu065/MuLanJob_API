namespace MuLanJobAPI.Entity
{
    public class RegisterRequest
    {
        public string openid { get; set; }
        public string nickname { get; set; }
        public string avatar_url { get; set; }
        public int? gender { get; set; }
        public string city { get; set; }
        public string province { get; set; }
        public string country { get; set; }
    }
}
