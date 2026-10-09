namespace TraceProject.Models;

/// <summary>
/// An employee's combined status today: camera presence (attendance check-in) together with
/// computer activity (desktop agent). Computed on the fly by PresenceService, never stored.
/// </summary>
public enum PresenceStatus
{
    /// <summary>Seen by a camera today and active on their computer: verified at the workstation.</summary>
    Working,
    /// <summary>Seen by a camera today; computer idle or locked.</summary>
    PresentIdle,
    /// <summary>Seen by a camera today; computer offline or not paired.</summary>
    PresentNoComputer,
    /// <summary>Active on their computer but not seen by any camera today.</summary>
    Unverified,
    /// <summary>Not seen by a camera and not active on a computer.</summary>
    Absent,
}

/// <summary>Outcome of one employee's day in the daily presence report.</summary>
public enum PresenceOutcome
{
    /// <summary>Checked in by camera and used their computer afterwards.</summary>
    Verified,
    /// <summary>Checked in by camera; no active computer time.</summary>
    PresentNoComputer,
    /// <summary>Active computer time without a camera check-in that day.</summary>
    UnverifiedComputerUse,
    /// <summary>Neither.</summary>
    Absent,
}
