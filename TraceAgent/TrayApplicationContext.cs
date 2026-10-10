using Microsoft.Win32;

namespace TraceAgent;

/// <summary>
/// The running agent: a tray icon, a 1-second sampling timer and a periodic upload.
/// Everything runs on the UI thread; SystemEvents callbacks are marshalled onto it.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AgentSettings settings;
    private readonly Control marshal = new();
    private readonly NotifyIcon trayIcon;
    private readonly System.Windows.Forms.Timer sampleTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer uploadTimer = new() { Interval = 15_000 };
    private readonly System.Windows.Forms.Timer configTimer = new() { Interval = 60_000 };

    private AgentApi api;
    private StatusForm? statusForm;
    private bool configLoaded;
    private bool shutDown;
    // Why the agent is stopping (sent to the server on exit); null when it is unpaired
    private AgentEventKind? exitReason;

    public ActivityTracker Tracker { get; } = new();
    public Uploader Uploader { get; }
    public string? EmployeeName => settings.EmployeeName;
    public string? ServerUrl => settings.ServerUrl;

    public TrayApplicationContext(AgentSettings settings)
    {
        this.settings = settings;
        marshal.CreateControl();
        Uploader = new Uploader(Tracker);
        Uploader.LoadQueue();
        Tracker.RecordEvent(AgentEventKind.Started);
        api = new AgentApi(settings.ServerUrl!, settings.GetToken());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Status…", null, (_, _) => ShowStatus());
        menu.Items.Add("Pair again…", null, (_, _) => PairAgain());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ConfirmExit());

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "TraceAgent – activity monitoring is on",
            ContextMenuStrip = menu,
            Visible = true,
        };
        trayIcon.DoubleClick += (_, _) => ShowStatus();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        sampleTimer.Tick += (_, _) => Tracker.Sample();
        uploadTimer.Tick += async (_, _) => await UploadAsync();
        configTimer.Tick += async (_, _) => await LoadConfigAsync();

        Tracker.Sample();
        sampleTimer.Start();
        uploadTimer.Start();
        configTimer.Start();
        _ = LoadConfigAsync();

        trayIcon.ShowBalloonTip(5000, "TraceAgent",
            $"Activity monitoring is on for {settings.EmployeeName}. Double-click the shield icon to see what is recorded.",
            ToolTipIcon.Info);
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            var config = await api.GetConfigAsync();
            Tracker.IdleThreshold = TimeSpan.FromSeconds(config.IdleThresholdSeconds);
            Tracker.CaptureWindowTitles = config.CaptureWindowTitles;
            uploadTimer.Interval = config.ReportIntervalSeconds * 1000;
            settings.EmployeeName = config.EmployeeName;

            // Once loaded, re-check every 10 minutes for setting changes; until then, every minute
            if (!configLoaded)
            {
                configLoaded = true;
                configTimer.Interval = 10 * 60_000;
                settings.Save();
            }
        }
        catch (UnpairedException)
        {
            HandleUnpaired();
        }
        catch (Exception)
        {
            // Server unreachable: keep the defaults and try again on the next tick
        }
    }

    private async Task UploadAsync()
    {
        try
        {
            await Uploader.UploadAsync(api);
            trayIcon.Text = Uploader.IsDisabled ? "TraceAgent – paused by the administrator"
                : Uploader.LastError is null ? "TraceAgent – activity monitoring is on"
                : "TraceAgent – server unreachable, saving locally";
        }
        catch (UnpairedException)
        {
            HandleUnpaired();
        }
    }

    /// <summary>The administrator removed or re-paired this device: stop and ask for a new code.</summary>
    private void HandleUnpaired()
    {
        // An open "Pair again" window already asks for a code; the next report retries after it
        if (shutDown || !sampleTimer.Enabled || pairAgainOpen)
            return;

        sampleTimer.Stop();
        uploadTimer.Stop();
        configTimer.Stop();
        Tracker.Suspend();
        // The server no longer accepts them for this device
        Tracker.Pending.Clear();
        Tracker.PendingEvents.Clear();
        Uploader.SaveQueue();

        settings.SetToken(null);
        settings.Save();

        using var pairForm = new PairForm(settings);
        if (pairForm.ShowDialog() != DialogResult.OK)
        {
            ExitThread();
            return;
        }

        Reconnect();
        Tracker.RecordEvent(AgentEventKind.Started);
        Tracker.Sample();
        sampleTimer.Start();
        uploadTimer.Start();
        configTimer.Start();
    }

    /// <summary>
    /// Enter a new server address or pairing code by hand. Needed when the server moved (another
    /// network or IP address): the agent then never reaches it, so it is never told to pair again.
    /// Cancelling keeps the current pairing; what is queued is sent to the new server.
    /// </summary>
    private bool pairAgainOpen;

    private void PairAgain()
    {
        if (pairAgainOpen)
            return;
        pairAgainOpen = true;
        try
        {
            using var pairForm = new PairForm(settings);
            if (pairForm.ShowDialog() != DialogResult.OK)
                return;
        }
        finally
        {
            pairAgainOpen = false;
        }

        Reconnect();
        _ = UploadAsync();
    }

    /// <summary>Talks to the server and token now in the settings.</summary>
    private void Reconnect()
    {
        api.Dispose();
        api = new AgentApi(settings.ServerUrl!, settings.GetToken());
        configLoaded = false;
        configTimer.Interval = 60_000;
        _ = LoadConfigAsync();
    }

    /// <summary>Closing stops the monitoring, so say plainly that the administrator will see it.</summary>
    private void ConfirmExit()
    {
        var answer = MessageBox.Show(
            "Stop activity monitoring on this computer?\n\n" +
            "Your administrator will see that TraceAgent was closed, and nothing is recorded until it is started again " +
            "(it also starts automatically the next time you sign in to Windows).",
            "TraceAgent", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;

        exitReason = AgentEventKind.UserExit;
        ExitThread();
    }

    /// <summary>
    /// Sends what is queued right now, waiting at most <paramref name="timeout"/>. Used when the
    /// process is about to stop or sleep, so it blocks instead of using the async timer path.
    /// </summary>
    private void FlushNow(TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            Task.Run(() => Uploader.UploadAsync(api, cts.Token)).Wait(timeout + TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
            // Unpaired or offline: whatever is left stays queued on disk
        }
    }

    private void ShowStatus()
    {
        if (statusForm is { IsDisposed: false })
        {
            statusForm.Activate();
            return;
        }
        statusForm = new StatusForm(this);
        statusForm.Show();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        bool? locked = e.Reason switch
        {
            SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => true,
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => false,
            _ => null,
        };
        if (locked is { } value)
            OnUiThread(() =>
            {
                Tracker.SetLocked(value);
                _ = UploadAsync(); // tell the dashboard right away
            });
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
            OnUiThread(() =>
            {
                Tracker.Suspend();
                Tracker.RecordEvent(AgentEventKind.Sleep);
                // Windows gives a couple of seconds before sleeping; if this fails it is sent after wake-up
                FlushNow(TimeSpan.FromSeconds(2));
                Uploader.SaveQueue();
            });
        else if (e.Mode == PowerModes.Resume)
            OnUiThread(() =>
            {
                Tracker.RecordEvent(AgentEventKind.Resumed);
                Tracker.Sample();
                _ = UploadAsync();
            });
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => OnUiThread(() =>
    {
        exitReason = AgentEventKind.Shutdown;
        ExitThread();
    });

    private void OnUiThread(Action action)
    {
        if (marshal.InvokeRequired)
            marshal.BeginInvoke(action);
        else
            action();
    }

    protected override void ExitThreadCore()
    {
        if (!shutDown)
        {
            shutDown = true;
            sampleTimer.Stop();
            uploadTimer.Stop();
            configTimer.Stop();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;

            // Close the open session, say why we stop, and make a short last attempt to send it all.
            // If the server is unreachable, the exit is delivered from disk on the next start.
            Tracker.Suspend();
            if (exitReason is { } reason)
                Tracker.RecordEvent(reason);
            FlushNow(TimeSpan.FromSeconds(3));
            Uploader.SaveQueue();

            trayIcon.Visible = false;
            trayIcon.Dispose();
            statusForm?.Close();
            api.Dispose();
            marshal.Dispose();
        }
        base.ExitThreadCore();
    }
}
