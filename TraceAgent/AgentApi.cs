using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TraceAgent;

// Copies of the server contracts in TraceProject/Dtos/AgentDtos.cs; keep them in sync.
// Enums travel as numbers (the API uses System.Text.Json defaults).
public enum ActivityState { Active = 0, Idle = 1, Locked = 2 }

public record AgentPairDto(string PairingCode, string? MachineName, string? AgentVersion);
public record AgentPairResultDto(long DeviceId, string EmployeeName, string DeviceToken);
public record AgentConfigDto(long DeviceId, string DeviceName, string EmployeeName, int IdleThresholdSeconds, int ReportIntervalSeconds, bool CaptureWindowTitles);
public record AgentSessionDto(Guid ClientId, ActivityState State, string? AppName, string? WindowTitle, DateTime StartedAt, DateTime EndedAt);
public record AgentReportDto(List<AgentSessionDto> Sessions, ActivityState CurrentState, string? CurrentApp, string? AgentVersion);

/// <summary>The device token was revoked or the device deleted/disabled: the agent must pair again.</summary>
public class UnpairedException(string message) : Exception(message);

/// <summary>The administrator disabled this device: keep the token, record nothing until it is enabled again.</summary>
public class DeviceDisabledException(string message) : Exception(message);

public class ApiException(string message) : Exception(message);

/// <summary>HTTP client for the TraceProject API's /api/Agent endpoints.</summary>
public sealed class AgentApi : IDisposable
{
    public static readonly string Version = typeof(AgentApi).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;

    public AgentApi(string serverUrl, string? deviceToken = null)
    {
        http = new HttpClient { BaseAddress = new Uri(NormalizeUrl(serverUrl)), Timeout = TimeSpan.FromSeconds(15) };
        if (deviceToken is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", deviceToken);
    }

    /// <summary>Accepts "192.168.1.10:5297" as well as a full URL.</summary>
    public static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url;
        return url + "/";
    }

    public Task<AgentPairResultDto> PairAsync(string pairingCode, CancellationToken cancellation = default) =>
        SendAsync<AgentPairResultDto>(HttpMethod.Post, "api/Agent/pair",
            new AgentPairDto(pairingCode, Environment.MachineName, Version), cancellation);

    public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellation = default) =>
        SendAsync<AgentConfigDto>(HttpMethod.Get, "api/Agent/me", null, cancellation);

    public Task<JsonElement> ReportAsync(AgentReportDto report, CancellationToken cancellation = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, "api/Agent/report", report, cancellation);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        using var response = await http.SendAsync(request, cancellation);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnpairedException("This computer is no longer paired with the server.");

        // The API wraps every answer as { isSuccess, message, data }
        var envelope = await ReadEnvelopeAsync(response, cancellation);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new DeviceDisabledException(envelope?.Message ?? "This computer has been disabled by the administrator.");
        if (!response.IsSuccessStatusCode || envelope?.IsSuccess == false)
            throw new ApiException(envelope?.Message ?? $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}");

        return envelope!.Data.Deserialize<T>(Json)!;
    }

    private static async Task<Envelope?> ReadEnvelopeAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<Envelope>(Json, cancellation);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Envelope(bool IsSuccess, string? Message, JsonElement Data);

    public void Dispose() => http.Dispose();
}
