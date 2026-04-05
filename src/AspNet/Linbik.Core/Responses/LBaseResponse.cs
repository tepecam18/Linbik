namespace Linbik.Core.Responses;

public sealed class LBaseResponse<T> where T : class
{
    public bool IsSuccess { get; set; }
    public LFriendlyMessageResponse? FriendlyMessage { get; set; }
    public long ServerTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public T? Data { get; set; }

    public LBaseResponse(T data, bool isSuccess = true)
    {
        IsSuccess = isSuccess;
        Data = data;
    }

    public LBaseResponse(string title, string message, bool isSuccess = false)
    {
        FriendlyMessage = new()
        {
            Title = title,
            Message = message
        };
        IsSuccess = isSuccess;
    }

    public LBaseResponse(string title, string message, T data, bool isSuccess = true)
    {
        FriendlyMessage = new()
        {
            Title = title,
            Message = message
        };
        Data = data; // Bug düzeltildi: data = data yerine Data = data
        IsSuccess = isSuccess;
    }

    public LBaseResponse(string message, bool isSuccess = false)
    {
        FriendlyMessage = new()
        {
            Title = "throw",
            Message = message
        };
        IsSuccess = isSuccess;
    }

    public LBaseResponse(bool isSuccess)
    {
        IsSuccess = isSuccess;
    }

    public LBaseResponse()
    {
        IsSuccess = true;
    }
}
