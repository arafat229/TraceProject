using Microsoft.EntityFrameworkCore;

namespace TraceProject.Models;

/// <summary>
/// Live computer-activity status of an employee (one row each), updated by the desktop agent.
/// History lives in <see cref="ActivitySession"/>.
/// </summary>
[Index(nameof(EmployeeId), IsUnique = true)]
    public class Activity
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        /// <summary>True only while State is Active.</summary>
        public  bool IsActive { get; set; }
        public ActivityState State { get; set; } = ActivityState.Offline;
        public string? CurrentApp { get; set; }
        /// <summary>Last time the employee was active.</summary>
        public DateTime ActiveTime { get; set; }
        /// <summary>Last time the employee stopped being active (went idle, locked or offline).</summary>
        public DateTime OffTime { get; set; }
        /// <summary>Last time the session was locked.</summary>
        public DateTime LockTime { get; set; }
        /// <summary>Last report from the agent; null for rows created manually.</summary>
        public DateTime? LastSeenAt { get; set; }
        /// <summary>Why the computer is Offline (closed by the user, shut down, asleep, connection lost); null otherwise.</summary>
        public AgentEventKind? OfflineReason { get; set; }

    }
