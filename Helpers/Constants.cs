namespace TraceProject.Helpers;

public static class Constants
{
    // A custom claim type used to store fine-grained permission strings
    // (e.g. "cameras.delete") on a role, separately from the broad
    // ClaimTypes.Role claim. Your AccountController's Login method reads
    // these out via roleManager.GetClaimsAsync(existingRole) and returns
    // them to the frontend as UserDetailsDto.Permission.
    public const string Privilege = "trace_privilege";
}

// "Extension methods" — these let you call .ToTitleCase() and
// .GetFullErrorMessage() directly on a string/Exception as if they were
// built-in methods, even though we defined them ourselves. The `this`
// keyword on the first parameter is what makes a method an extension method.
public static class StringExtensions
{
    // "administrator" -> "Administrator". Used in your AccountController
    // on dto.Role so that role lookups aren't case-sensitive-dependent on
    // whatever casing the frontend happened to send.
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return char.ToUpper(input[0]) + input[1..].ToLower();
    }
}

public static class ExceptionExtensions
{
    // Walks through Exception.InnerException (common with EF Core errors,
    // where the real cause is often buried a level or two down) and
    // concatenates every message into one string for logging.
    public static string GetFullErrorMessage(this Exception ex)
    {
        var messages = new List<string>();
        var current = ex;
        while (current != null)
        {
            messages.Add(current.Message);
            current = current.InnerException;
        }
        return string.Join(" -> ", messages);
    }
}
