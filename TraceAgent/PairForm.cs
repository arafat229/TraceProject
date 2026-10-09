namespace TraceAgent;

/// <summary>
/// First-run window: the server address and the pairing code an administrator created
/// for this computer on the dashboard (or with POST /api/Devices).
/// </summary>
internal sealed class PairForm : Form
{
    private readonly AgentSettings settings;
    private readonly TextBox serverBox = new() { Width = 300 };
    private readonly TextBox codeBox = new() { Width = 300, CharacterCasing = CharacterCasing.Upper, MaxLength = 12 };
    private readonly Button pairButton = new() { Text = "Pair this computer", AutoSize = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(320, 0), ForeColor = Color.Firebrick };

    public PairForm(AgentSettings settings)
    {
        this.settings = settings;

        Text = "TraceAgent – pair this computer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Icon = SystemIcons.Shield;

        serverBox.Text = settings.ServerUrl ?? "http://";
        serverBox.PlaceholderText = "http://192.168.1.10:5297";

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
        };
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Text = "This computer's activity (active app, working time and idle time) will be shared with your " +
                   "organization's TraceProject server. Keystrokes, screenshots and file contents are never recorded.",
            Margin = new Padding(0, 0, 0, 12),
        });
        layout.Controls.Add(new Label { Text = "Server address", AutoSize = true });
        layout.Controls.Add(serverBox);
        layout.Controls.Add(new Label { Text = "Pairing code (from the administrator)", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        layout.Controls.Add(codeBox);
        layout.Controls.Add(pairButton);
        layout.Controls.Add(statusLabel);
        Controls.Add(layout);

        AcceptButton = pairButton;
        pairButton.Click += async (_, _) => await PairAsync();
    }

    private async Task PairAsync()
    {
        if (string.IsNullOrWhiteSpace(serverBox.Text) || string.IsNullOrWhiteSpace(codeBox.Text))
        {
            statusLabel.Text = "Enter the server address and the pairing code.";
            return;
        }

        pairButton.Enabled = false;
        statusLabel.ForeColor = SystemColors.ControlText;
        statusLabel.Text = "Connecting…";
        try
        {
            var serverUrl = AgentApi.NormalizeUrl(serverBox.Text);
            using var api = new AgentApi(serverUrl);
            var result = await api.PairAsync(codeBox.Text.Trim());

            settings.ServerUrl = serverUrl;
            settings.SetToken(result.DeviceToken);
            settings.EmployeeName = result.EmployeeName;
            settings.Save();
            AgentSettings.EnableAutoStart();

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = ex is HttpRequestException or TaskCanceledException
                ? $"Cannot reach the server. Check the address, that the server is running and that the firewall allows port 5297.\n({ex.Message})"
                : ex.Message;
            pairButton.Enabled = true;
        }
    }
}
