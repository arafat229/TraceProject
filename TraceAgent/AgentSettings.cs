using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace TraceAgent;

/// <summary>
/// Local agent state under %LOCALAPPDATA%\TraceAgent. The device token is encrypted with
/// Windows DPAPI, so only this Windows user on this machine can read it.
/// </summary>
internal sealed class AgentSettings
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraceAgent");

    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TraceAgent.DeviceToken");

    public string? ServerUrl { get; set; }
    public string? ProtectedToken { get; set; }
    public string? EmployeeName { get; set; }

    public bool IsPaired => !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(ProtectedToken);

    public string? GetToken()
    {
        if (string.IsNullOrEmpty(ProtectedToken))
            return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(ProtectedToken), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null; // copied from another user or machine
        }
    }

    public void SetToken(string? token) =>
        ProtectedToken = token is null
            ? null
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser));

    public static AgentSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(FilePath)) ?? new AgentSettings();
        }
        catch (Exception)
        {
            // Corrupt file: start unpaired
        }
        return new AgentSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Starts the agent when this user signs in to Windows.</summary>
    public static void EnableAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue("TraceAgent", $"\"{Environment.ProcessPath}\"");
    }
}
