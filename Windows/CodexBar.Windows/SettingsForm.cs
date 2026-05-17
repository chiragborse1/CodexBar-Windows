namespace CodexBar.Windows;

internal sealed class SettingsForm : Form
{
    private readonly TextBox cliPathBox;
    private readonly ComboBox providerBox;
    private readonly NumericUpDown refreshIntervalBox;
    private readonly CheckBox launchAtLoginBox;
    private readonly Label testResultLabel;

    public SettingsForm(AppSettings settings, CliRunner cliRunner)
    {
        Settings = new AppSettings
        {
            CliPath = settings.CliPath,
            Provider = settings.Provider,
            RefreshIntervalMinutes = settings.RefreshIntervalMinutes,
            LaunchAtLogin = settings.LaunchAtLogin,
            StartMinimized = settings.StartMinimized,
        };
        _ = cliRunner;

        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 280);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "CLI path", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        cliPathBox = new TextBox { Dock = DockStyle.Fill, Text = Settings.CliPath ?? "" };
        root.Controls.Add(cliPathBox, 1, 0);
        var browseButton = new Button { Text = "Browse", AutoSize = true };
        browseButton.Click += (_, _) => BrowseForCli();
        root.Controls.Add(browseButton, 2, 0);

        root.Controls.Add(new Label { Text = "Provider", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        providerBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        providerBox.Items.AddRange(
        [
            "all",
            "codex",
            "claude",
            "openai",
            "copilot",
            "gemini",
            "kilo",
            "synthetic",
            "grok",
            "cursor",
        ]);
        providerBox.Text = Settings.Provider;
        root.Controls.Add(providerBox, 1, 1);

        root.Controls.Add(new Label { Text = "Refresh interval", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        refreshIntervalBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 120,
            Value = Settings.RefreshIntervalMinutes,
            Dock = DockStyle.Left,
            Width = 90,
        };
        root.Controls.Add(refreshIntervalBox, 1, 2);

        launchAtLoginBox = new CheckBox
        {
            Text = "Launch at sign-in",
            Checked = Settings.LaunchAtLogin,
            AutoSize = true,
        };
        root.Controls.Add(launchAtLoginBox, 1, 3);

        var testButton = new Button { Text = "Test CLI", AutoSize = true };
        testButton.Click += async (_, _) => await TestCliAsync();
        root.Controls.Add(testButton, 1, 4);
        testResultLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = "" };
        root.Controls.Add(testResultLabel, 2, 4);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        root.SetColumnSpan(buttons, 3);
        root.Controls.Add(buttons, 0, 5);

        var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        saveButton.Click += (_, _) => CaptureSettings();
        buttons.Controls.Add(saveButton);

        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(cancelButton);

        var openConfigButton = new Button { Text = "Open Config Folder", AutoSize = true };
        openConfigButton.Click += (_, _) => ConfigLocator.OpenConfigFolder();
        buttons.Controls.Add(openConfigButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public AppSettings Settings { get; private set; }

    private void BrowseForCli()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CodexBar CLI|CodexBarCLI.exe|Executables|*.exe|All files|*.*",
            Title = "Select CodexBarCLI.exe",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            cliPathBox.Text = dialog.FileName;
        }
    }

    private async Task TestCliAsync()
    {
        CaptureSettings();
        var runner = new CliRunner(Settings);
        testResultLabel.Text = "Testing...";
        var result = await runner.VersionAsync(CancellationToken.None);
        testResultLabel.Text = result.Succeeded ? result.StandardOutput.Trim() : result.CombinedOutput;
    }

    private void CaptureSettings()
    {
        Settings.CliPath = string.IsNullOrWhiteSpace(cliPathBox.Text) ? null : cliPathBox.Text.Trim();
        Settings.Provider = string.IsNullOrWhiteSpace(providerBox.Text) ? "all" : providerBox.Text.Trim();
        Settings.RefreshIntervalMinutes = (int)refreshIntervalBox.Value;
        Settings.LaunchAtLogin = launchAtLoginBox.Checked;
    }
}
