namespace MuLanJobAPI.Entity
{
    public class ApiResult<T>
    {
        public bool success { get; set; }
        public string message { get; set; }
        public T data { get; set; }
        public ApiResult()
        {
        }

        public static ApiResult<T> Success(T data) => new ApiResult<T> { success = true, data = data };
        public static ApiResult<T> Success(string message, T data) => new ApiResult<T> { success = true, message = message, data = data };
        public static ApiResult<T> Fail(string message) => new ApiResult<T> { success = false, message = message };
    }
}
