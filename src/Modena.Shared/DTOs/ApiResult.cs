namespace Modena.Shared.DTOs;

public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ApiResult<T> Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
