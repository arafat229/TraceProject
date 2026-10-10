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
        ApplicationConfiguration.Initialize();

        if (!isFirstInstance)
        {
            // Quitting silently looked like the new exe "doing nothing" when the old one was still running
            MessageBox.Show(
                "TraceAgent is already running on this computer.\n\n" +
                "Its icon is in the taskbar notification area (click ^ at the bottom right). " +
                "To update TraceAgent, right-click that icon, choose Exit, then start the new version.",
                "TraceAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
