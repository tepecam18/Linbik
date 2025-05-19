namespace Linbik.Core.Responses;

public class LBaseResponse<T> where T : class
{
    public bool isSuccess { get; set; }
    public LFriendlyMessageResponse? friendlyMessage { get; set; }
    public long serverTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public T? data { get; set; }

    public LBaseResponse(T _data, bool _isSuccess = true)
    {
        isSuccess = _isSuccess;
        data = _data;
    }

    public LBaseResponse(string title, string message, bool _isSuccess = false)
    {
        friendlyMessage = new()
        {
            title = title,
            message = message
        };
        isSuccess = _isSuccess;
    }


    public LBaseResponse(string title, string message, T _data, bool _isSuccess = true)
    {
        friendlyMessage = new()
        {
            title = title,
            message = message
        };
        data = data;
        isSuccess = _isSuccess;
    }

    public LBaseResponse(string message, bool _isSuccess = false)
    {
        friendlyMessage = new()
        {
            title = "throw",
            message = message
        };
        isSuccess = _isSuccess;
    }

    public LBaseResponse(bool _isSuccess = true)
    {
        isSuccess = _isSuccess;
    }
}
