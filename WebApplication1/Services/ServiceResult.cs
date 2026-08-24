namespace WebApplication1.Services
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }

        public static ServiceResult Ok(string message = "Success", object? data = null) =>
            new() { Success = true, Message = message, Data = data };

        public static ServiceResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
