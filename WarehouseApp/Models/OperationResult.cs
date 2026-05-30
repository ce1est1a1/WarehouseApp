namespace WarehouseApp.Models;

public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    internal static OperationResult Ok(string message = "") => new() { Success = true, Message = message };
    internal static OperationResult Fail(string message) => new() { Success = false, Message = message };
}

public class OperationResult<T> : OperationResult
{
    public T? Data { get; set; }

    internal static OperationResult<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    internal new static OperationResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
