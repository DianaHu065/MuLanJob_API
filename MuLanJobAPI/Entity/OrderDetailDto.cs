namespace MuLanJobAPI.Entity
{
    public class OrderDetailDto
    {
        // 订单字段
        public int id { get; set; }
        public string order_no { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public DateTime create_time { get; set; }
        public string requirement { get; set; }
        public string salary { get; set; }
        public int workDays { get; set; }
        public string city { get; set; }
        public string services { get; set; }
        public string liveType { get; set; }
        public string location { get; set; }
        public string? startTimeType { get; set; }
        public string? appointDate { get; set; }

        public string real_name { get; set; }
        public string phone { get; set; }
        public string wechat { get; set; }

        public byte? is_publish { get; set; } = 1;
        public string orderType { get; set; }

        public string? avatar { get; set; }
        
    }
}
