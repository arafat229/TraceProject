using System.Diagnostics;

namespace TraceAgent;

/// <summary>
/// Samples the foreground window and input idle time, and cuts the timeline into sessions:
/// a new session starts whenever the state (Active / Idle / Locked), the app or the window title changes.
/// Runs on the UI thread only (WinForms timer and SystemEvents), so it needs no locking.
/// </summary>
internal sealed class ActivityTracker
{
    // A gap between samples longer than this means the computer slept or hibernated
    private static readonly TimeSpan SleepGap = TimeSpan.FromSeconds(30);
    private const int MaxPending = 20_000;

    private readonly Dictionary<uint, string> appNames = new();
    private readonly Dictionary<(DateOnly Day, ActivityState State), double> closedSeconds = new();

    private OpenSession? current;
    private DateTime? lastSampleAt;
    // Start of the current uninterrupted run of samples (agent start, wake from sleep)
    private DateTime samplingSince = DateTime.UtcNow;
    private bool locked;

    public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public bool CaptureWindowTitles { get; set; } = true;

    /// <summary>Finished sessions not yet accepted by the server, oldest first.</summary>
    public List<AgentSessionDto> Pending { get; } = new();

    public ActivityState CurrentState => current?.State ?? (locked ? ActivityState.Locked : ActivityState.Active);
    public string? CurrentApp => current?.AppName;
    public string? CurrentTitle => current?.WindowTitle;

    public void Sample()
    {
        var now = DateTime.UtcNow;
        if (lastSampleAt is not { } last || now - last > SleepGap)
        {
            // Asleep in between: end the session where sampling stopped, not now
            if (current is not null && lastSampleAt is { } stoppedAt)
                Close(stoppedAt);
            samplingSince = now;
        }
        lastSampleAt = now;

        var (state, app, title, startedAt) = Observe(now);
        // Windows' last-input clock keeps running through sleep and before the agent started;
        // never date idle time back into a period that was not being recorded
        if (startedAt < samplingSince)
            startedAt = samplingSince;

        if (current is null)
        {
            Start(state, app, title, startedAt);
        }
        else if (current.State != state || current.AppName != app || current.WindowTitle != title)
        {
            // Going idle is noticed only after the threshold; date it back to the last input
            var boundary = state == ActivityState.Idle && startedAt > current.StartedAt ? startedAt : now;
            Close(boundary);
            Start(state, app, title, boundary);
        }
        else
        {
            current.LastSeenAt = now;
        }
    }

    public void SetLocked(bool isLocked)
    {
        locked = isLocked;
        Sample();
    }

    /// <summary>Sleep, shutdown or exit: end the open session now.</summary>
    public void Suspend()
    {
        if (current is not null)
            Close(DateTime.UtcNow);
        lastSampleAt = null;
    }

    /// <summary>The open session as it stands, sent with every report so the dashboard stays live.</summary>
    public AgentSessionDto? OpenSnapshot() => current is null
        ? null
        : new AgentSessionDto(current.ClientId, current.State, current.AppName, current.WindowTitle, current.StartedAt, current.LastSeenAt);

    /// <summary>Today's local totals in seconds (finished sessions plus the open one).</summary>
    public double TodaySeconds(ActivityState state)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var total = closedSeconds.GetValueOrDefault((today, state));
        if (current is not null && current.State == state && DateOnly.FromDateTime(current.StartedAt.ToLocalTime()) == today)
            total += (current.LastSeenAt - current.StartedAt).TotalSeconds;
        return total;
    }

    private (ActivityState State, string? App, string? Title, DateTime StartedAt) Observe(DateTime now)
    {
        if (locked)
            return (ActivityState.Locked, null, null, now);

        var idle = NativeMethods.GetIdleTime();
        if (idle >= IdleThreshold)
            // Window details are left out while idle, so a changing title (a video, a clock) does not split idle time
            return (ActivityState.Idle, null, null, now - idle);

        var hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return (ActivityState.Active, null, null, now);

        var title = NativeMethods.GetWindowTitle(hWnd);
        NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
        var app = AppName(pid);

        // Store apps run inside ApplicationFrameHost; their window title is the app's name
        if (app == "ApplicationFrameHost" && title.Length > 0)
            app = title;

        return (ActivityState.Active, app, CaptureWindowTitles && title.Length > 0 ? title : null, now);
    }

    private string? AppName(uint pid)
    {
        if (pid == 0)
            return null;
        if (appNames.TryGetValue(pid, out var cached))
            return cached;

        string? name = null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            try
            {
                // "Google Chrome" reads better than "chrome"
                name = process.MainModule?.FileVersionInfo.FileDescription;
            }
            catch (Exception)
            {
                // Elevated or protected processes do not allow reading their module
            }
            if (string.IsNullOrWhiteSpace(name))
                name = process.ProcessName;
        }
        catch (Exception)
        {
            return null; // exited
        }

        if (appNames.Count > 500)
            appNames.Clear();
        appNames[pid] = name;
        return name;
    }

    private void Start(ActivityState state, string? app, string? title, DateTime startedAt) =>
        current = new OpenSession(Guid.NewGuid(), state, app, title, startedAt) { LastSeenAt = DateTime.UtcNow };

    private void Close(DateTime endedAt)
    {
        var session = current!;
        current = null;
        if (endedAt < session.StartedAt)
            endedAt = session.StartedAt;

        var key = (DateOnly.FromDateTime(session.StartedAt.ToLocalTime()), session.State);
        closedSeconds[key] = closedSeconds.GetValueOrDefault(key) + (endedAt - session.StartedAt).TotalSeconds;

        Pending.Add(new AgentSessionDto(session.ClientId, session.State, session.AppName, session.WindowTitle, session.StartedAt, endedAt));
        if (Pending.Count > MaxPending)
            Pending.RemoveRange(0, Pending.Count - MaxPending); // offline for days: drop the oldest
    }

    private sealed record OpenSession(Guid ClientId, ActivityState State, string? AppName, string? WindowTitle, DateTime StartedAt)
    {
        public DateTime LastSeenAt { get; set; }
    }
}
