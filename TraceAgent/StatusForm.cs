namespace TraceAgent;

/// <summary>Shows the employee exactly what is being recorded and whether it reaches the server.</summary>
internal sealed class StatusForm : Form
{
    private readonly TrayApplicationContext agent;
    private readonly Label details = new() { AutoSize = true, Font = new Font("Consolas", 10f) };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 1000 };

    public StatusForm(TrayApplicationContext agent)
    {
        this.agent = agent;

        Text = "TraceAgent – status";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Icon = SystemIcons.Shield;
        ShowInTaskbar = true;

        Controls.Add(details);
        refresh.Tick += (_, _) => UpdateText();
        Shown += (_, _) => { UpdateText(); refresh.Start(); };
        FormClosed += (_, _) => refresh.Dispose();
    }

    private void UpdateText()
    {
        var tracker = agent.Tracker;
        var uploader = agent.Uploader;

        details.Text = string.Join(Environment.NewLine,
            $"Employee:        {agent.EmployeeName}",
            $"Server:          {agent.ServerUrl}",
            $"Connection:      {(uploader.IsDisabled ? "Paused – disabled by the administrator" : uploader.LastError is null ? "OK" : "Not connected – " + uploader.LastError)}",
            $"Last sent:       {uploader.LastSuccessAt?.ToString("T") ?? "never"}",
            $"Waiting to send: {tracker.Pending.Count} session(s)",
            "",
            $"Now:             {tracker.CurrentState}",
            $"App:             {tracker.CurrentApp ?? "—"}",
            $"Window title:    {(tracker.CaptureWindowTitles ? Shorten(tracker.CurrentTitle) : "not recorded")}",
            $"Idle after:      {tracker.IdleThreshold.TotalMinutes:0.#} min without input",
            "",
            "Today on this computer",
            $"  Active:        {Format(tracker.TodaySeconds(ActivityState.Active))}",
            $"  Idle:          {Format(tracker.TodaySeconds(ActivityState.Idle))}",
            $"  Locked:        {Format(tracker.TodaySeconds(ActivityState.Locked))}",
            "",
            "Recorded: app name, window title, active/idle/locked time.",
            "Never recorded: keystrokes, screenshots, file contents.");
    }

    private static string Format(double seconds) =>
        TimeSpan.FromSeconds(seconds) is var t ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : "";

    private static string Shorten(string? text) =>
        string.IsNullOrEmpty(text) ? "—" : text.Length > 60 ? text[..57] + "…" : text;
}
