namespace TraceAgent;

/// <summary>
/// TraceAgent: the desktop monitoring agent of TraceProject. Runs in the system tray of an
/// employee's Windows computer and reports active app, working, idle and locked time to the API.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // One agent per Windows user session
        using var mutex = new Mutex(true, @"Local\TraceAgent.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();

        var settings = AgentSettings.Load();
        if (!settings.IsPaired || settings.GetToken() is null)
        {
            using var pairForm = new PairForm(settings);
            if (pairForm.ShowDialog() != DialogResult.OK)
                return;
        }
        else
        {
            // Keep the Run key pointing at this exe if it was moved or updated
            AgentSettings.EnableAutoStart();
        }

        Application.Run(new TrayApplicationContext(settings));
    }
}
