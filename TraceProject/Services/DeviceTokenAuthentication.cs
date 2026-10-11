using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraceProject.Data;

namespace TraceProject.Services;

/// <summary>
/// Authenticates the desktop agent by its device token ("Authorization: Device &lt;token&gt;").
/// Kept separate from the dashboard's JWT scheme: a device token can only reach AgentController.
/// </summary>
public class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApplicationDbContext dbContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";
    public const string DeviceIdClaim = "device_id";
    public const string EmployeeIdClaim = "employee_id";
    public const string DeviceActiveClaim = "device_active";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        const string prefix = "Device ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var hash = DeviceTokens.Hash(header[prefix.Length..].Trim());
        DeviceInfo? device;
        try
        {
            device = await dbContext.Devices.AsNoTracking()
                .Where(d => d.TokenHash == hash)
                .Select(d => new DeviceInfo(d.Id, d.EmployeeId, d.IsActive))
                .FirstOrDefaultAsync(Context.RequestAborted);
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            // The agent gave up on the request (its timeout, exit or sleep): nobody is waiting for an answer
            return AuthenticateResult.NoResult();
        }

        if (device is null)
            return AuthenticateResult.Fail("Unknown device token");

        // A disabled device still authenticates, so AgentController can answer 403 instead of 401:
        // the agent keeps its token (401 means "pair again") and resumes once re-enabled.
        var identity = new ClaimsIdentity(
        [
            new Claim(DeviceIdClaim, device.Id.ToString()),
            new Claim(EmployeeIdClaim, device.EmployeeId.ToString()),
            new Claim(DeviceActiveClaim, device.IsActive ? "true" : "false"),
        ], SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    private sealed record DeviceInfo(long Id, long EmployeeId, bool IsActive);
}

public static class DeviceTokens
{
    // No 0/O, 1/I/L: the code is read off a screen and typed by hand
    private const string PairingAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NewToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string NewPairingCode() => RandomNumberGenerator.GetString(PairingAlphabet, 8);

    public static string NormalizePairingCode(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
