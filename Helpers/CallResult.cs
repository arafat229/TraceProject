namespace TraceProject.Helpers;

// A consistent "envelope" every endpoint returns, so the frontend always
// knows what shape to expect regardless of which controller it called —
// instead of some endpoints returning raw objects and others returning
// error strings. This is what your AccountController's
// `CallResult.Ok(...)` and `CallResult.Error(...)` calls construct.
public class CallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }

    public static CallResult Ok(string message, object? data = null)
        => new() { Success = true, Message = message, Data = data };

    public static CallResult Error(string message, IEnumerable<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };

    // Overload for catch blocks: turns any C# exception into the same
    // consistent shape, so the frontend never has to special-case
    // "unexpected server errors" differently from "expected" ones.
    public static CallResult Error(Exception ex)
        => new() { Success = false, Message = "An unexpected error occurred", Errors = new[] { ex.Message } };
}
