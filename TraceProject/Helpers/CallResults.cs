using Microsoft.VisualBasic;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;

namespace TraceProject.Helpers;

public class CallResult<T>
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static CallResult<T?> Ok( T? data, string? message ) => new()
    {
        IsSuccess = true,
        Message = message ?? Constants.SuccessMessage,
        Data = data
    };

    public static CallResult<T?> Error(T? data,string? message) => new()
    {
            IsSuccess = false,
            Message = message ?? Constants.FailedMessage,
            Data = data

    };
    public static CallResult<T?> Error(Exception ex, string? message) => new()
    {

        IsSuccess = false,
        Message = ex is null ? Constants.FailedMessage : ex.Message, 

    }; 


}
public class CallResult : CallResult<object?>
{
    public static CallResult<object?> Ok(string? message = null, object? data = null) => new()
    {
        IsSuccess = true,
        Message = message ?? Constants.SuccessMessage,
        Data = data

    };

    public static CallResult<object?> Error(string? message = null, object? data = null) => new()
    {

        IsSuccess = false,
        Message = message ?? Constants.FailedMessage,
        Data = data

    };

    public static CallResult<object?> Error(Exception ex) => new()
    {
        IsSuccess = false,
        Message = ex is null ? Constants.FailedMessage : ex.GetFullErrorMessage(),

    };



}






