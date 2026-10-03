namespace ApiForge.Core;

public sealed record ApiResponse<T>(
    bool Success,
    string? Message,
    T? Data,
    string? Error
)
{
    public static ApiResponse<T> Ok(T? data, string? message = null) =>
        new(Success: true, Message: message, Data: data, Error: null);

    public static ApiResponse<T> Fail(string error) =>
        new(Success: false, Message: null, Data: default, Error: error);
}
