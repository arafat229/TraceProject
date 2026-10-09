using HlkHelpers;

namespace TraceProject.Models;

/// <summary>
/// A computer running the desktop monitoring agent (TraceAgent), assigned to one employee.
/// An admin creates the device and gets a short-lived pairing code; the agent exchanges
/// that code for a long-lived device token. Only the token's SHA-256 hash is stored.
/// </summary>
public class Device : Auditable
{
    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>Admin-chosen label, e.g. "Alice's laptop".</summary>
    public string Name { get; set; } = null!;

    /// <summary>Windows machine name reported by the agent when it pairs.</summary>
    public string? MachineName { get; set; }
    public string? AgentVersion { get; set; }

    public string? TokenHash { get; set; }
    public string? PairingCode { get; set; }
    public DateTime? PairingCodeExpiresAt { get; set; }

    public DateTime? PairedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;

    public List<ActivitySession> Sessions { get; set; } = [];
}
