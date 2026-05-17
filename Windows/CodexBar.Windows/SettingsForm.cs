namespace CodexBar.Windows;

internal sealed class SettingsForm : Form
{
    private readonly TextBox cliPathBox;
    private readonly ComboBox providerBox;
    private readonly ComboBox setupProviderBox;
    private readonly TextBox apiKeyBox;
    private readonly CheckBox setupEnableBox;
    private readonly Button saveApiKeyButton;
    private readonly Button enableProviderButton;
    private readonly Button disableProviderButton;
    private readonly Label providerSetupResultLabel;
    private readonly NumericUpDown refreshIntervalBox;
    private readonly CheckBox launchAtLoginBox;
    private readonly CheckBox startMinimizedBox;
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
        ClientSize = new Size(760, 560);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(tabs, 0, 0);

        refreshIntervalBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 120,
            Value = Settings.RefreshIntervalMinutes,
            Dock = DockStyle.Left,
            Width = 90,
        };
        launchAtLoginBox = new CheckBox
        {
            Text = "Launch at sign-in",
            Checked = Settings.LaunchAtLogin,
            AutoSize = true,
        };
        startMinimizedBox = new CheckBox
        {
            Text = "Start minimized to tray",
            Checked = Settings.StartMinimized,
            AutoSize = true,
        };

        providerBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        providerBox.Items.AddRange(ProviderCatalog.ProviderIds);
        providerBox.Text = Settings.Provider;

        cliPathBox = new TextBox { Dock = DockStyle.Fill, Text = Settings.CliPath ?? "" };
        testResultLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = "",
            MaximumSize = new Size(460, 0),
        };

        setupProviderBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        foreach (var provider in ProviderCatalog.ApiKeyEntries)
        {
            setupProviderBox.Items.Add(new ProviderChoice(provider.Id, provider.DisplayName));
        }

        if (setupProviderBox.Items.Count > 0)
        {
            setupProviderBox.SelectedIndex = 0;
        }

        apiKeyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
        };
        setupEnableBox = new CheckBox
        {
            Text = "Enable provider after saving",
            Checked = true,
            AutoSize = true,
        };
        saveApiKeyButton = new Button { Text = "Save API Key", AutoSize = true };
        saveApiKeyButton.Click += async (_, _) => await SaveProviderApiKeyAsync();
        enableProviderButton = new Button { Text = "Enable", AutoSize = true };
        enableProviderButton.Click += async (_, _) => await ChangeSelectedProviderEnabledAsync(enabled: true);
        disableProviderButton = new Button { Text = "Disable", AutoSize = true };
        disableProviderButton.Click += async (_, _) => await ChangeSelectedProviderEnabledAsync(enabled: false);
        providerSetupResultLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = "Save an API key here to make the provider available in the tray popover.",
            MaximumSize = new Size(560, 0),
            ForeColor = Color.FromArgb(88, 92, 104),
        };

        tabs.TabPages.Add(BuildGeneralPage());
        tabs.TabPages.Add(BuildDisplayPage());
        tabs.TabPages.Add(BuildProvidersPage());
        tabs.TabPages.Add(BuildAdvancedPage());
        tabs.TabPages.Add(BuildAboutPage());

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0),
        };
        root.Controls.Add(buttons, 0, 1);

        var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        saveButton.Click += (_, _) => CaptureSettings();
        buttons.Controls.Add(saveButton);

        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public AppSettings Settings { get; private set; }

    private TabPage BuildGeneralPage()
    {
        var page = new TabPage("General");
        var root = SettingsGrid();
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "Refresh interval", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        root.Controls.Add(refreshIntervalBox, 1, 0);
        root.Controls.Add(launchAtLoginBox, 1, 1);
        root.Controls.Add(startMinimizedBox, 1, 2);

        return page;
    }

    private TabPage BuildDisplayPage()
    {
        var page = new TabPage("Display");
        var root = SettingsGrid();
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "Provider scope", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        root.Controls.Add(providerBox, 1, 0);
        root.Controls.Add(new Label
        {
            Text = "The tray popover renders the selected provider scope. Use all to mirror enabled providers from config.",
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            ForeColor = Color.FromArgb(88, 92, 104),
        }, 1, 1);

        return page;
    }

    private TabPage BuildProvidersPage()
    {
        var page = new TabPage("Providers");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(root);

        root.Controls.Add(BuildProviderSetupGroup(), 0, 0);

        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = System.Windows.Forms.View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            UseCompatibleStateImageBehavior = false,
        };
        list.Columns.Add("Provider", 150);
        list.Columns.Add("ID", 100);
        list.Columns.Add("Windows", 80);
        list.Columns.Add("Notes", 340);
        foreach (var provider in ProviderCatalog.Entries)
        {
            var item = new ListViewItem(provider.DisplayName);
            item.SubItems.Add(provider.Id);
            item.SubItems.Add(provider.Support);
            item.SubItems.Add(provider.Notes);
            list.Items.Add(item);
        }
        root.Controls.Add(list, 0, 1);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0),
        };
        root.Controls.Add(actions, 0, 2);

        var openConfigButton = new Button { Text = "Open Config File", AutoSize = true };
        openConfigButton.Click += (_, _) => ConfigLocator.OpenConfigFile();
        actions.Controls.Add(openConfigButton);

        var openFolderButton = new Button { Text = "Open Config Folder", AutoSize = true };
        openFolderButton.Click += (_, _) => ConfigLocator.OpenConfigFolder();
        actions.Controls.Add(openFolderButton);

        return page;
    }

    private GroupBox BuildProviderSetupGroup()
    {
        var group = new GroupBox
        {
            Text = "API-key setup",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(12),
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 5; index++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        group.Controls.Add(grid);

        grid.Controls.Add(new Label { Text = "Provider", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(setupProviderBox, 1, 0);
        grid.Controls.Add(new Label { Text = "API key", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(apiKeyBox, 1, 1);
        grid.Controls.Add(setupEnableBox, 1, 2);

        var actionRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0),
        };
        actionRow.Controls.Add(saveApiKeyButton);
        actionRow.Controls.Add(enableProviderButton);
        actionRow.Controls.Add(disableProviderButton);
        grid.Controls.Add(actionRow, 1, 3);

        grid.Controls.Add(providerSetupResultLabel, 1, 4);

        return group;
    }

    private TabPage BuildAdvancedPage()
    {
        var page = new TabPage("Advanced");
        var root = SettingsGrid();
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "CLI path", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(cliPathBox, 0, 0);

        var browseButton = new Button { Text = "Browse", AutoSize = true };
        browseButton.Click += (_, _) => BrowseForCli();
        pathRow.Controls.Add(browseButton, 1, 0);
        root.Controls.Add(pathRow, 1, 0);

        var testButton = new Button { Text = "Test CLI", AutoSize = true };
        testButton.Click += async (_, _) => await TestCliAsync();
        root.Controls.Add(testButton, 1, 1);
        root.Controls.Add(testResultLabel, 1, 2);

        return page;
    }

    private static TabPage BuildAboutPage()
    {
        var page = new TabPage("About");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(18),
        };
        page.Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = $"{AppInfo.DisplayName} {AppInfo.Version}{Environment.NewLine}{Environment.NewLine}" +
                "Windows tray app and CLI package for tracking AI coding-provider limits.",
            AutoSize = true,
            MaximumSize = new Size(580, 0),
        });
        return page;
    }

    private static TableLayoutPanel SettingsGrid()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 8; index++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        return root;
    }

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

    private async Task SaveProviderApiKeyAsync()
    {
        var provider = SelectedProviderChoice();
        if (provider is null)
        {
            SetProviderSetupMessage("Select a provider first.", isError: true);
            return;
        }

        var apiKey = apiKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetProviderSetupMessage("Enter an API key before saving.", isError: true);
            return;
        }

        SetProviderSetupBusy(true, "Saving...");
        try
        {
            CaptureSettings();
            var runner = new CliRunner(Settings);
            var result = await runner.SetApiKeyAsync(
                provider.Id,
                apiKey,
                setupEnableBox.Checked,
                CancellationToken.None);
            if (result.Succeeded)
            {
                apiKeyBox.Clear();
                SetProviderSetupMessage(
                    setupEnableBox.Checked
                        ? $"Saved API key and enabled {provider.DisplayName}."
                        : $"Saved API key for {provider.DisplayName}.",
                    isError: false);
            }
            else
            {
                SetProviderSetupMessage(ShortResultMessage(result), isError: true);
            }
        }
        finally
        {
            SetProviderSetupBusy(false);
        }
    }

    private async Task ChangeSelectedProviderEnabledAsync(bool enabled)
    {
        var provider = SelectedProviderChoice();
        if (provider is null)
        {
            SetProviderSetupMessage("Select a provider first.", isError: true);
            return;
        }

        SetProviderSetupBusy(true, enabled ? "Enabling..." : "Disabling...");
        try
        {
            CaptureSettings();
            var runner = new CliRunner(Settings);
            var result = await runner.SetProviderEnabledAsync(provider.Id, enabled, CancellationToken.None);
            SetProviderSetupMessage(
                result.Succeeded
                    ? $"{provider.DisplayName} is now {(enabled ? "enabled" : "disabled")}."
                    : ShortResultMessage(result),
                isError: !result.Succeeded);
        }
        finally
        {
            SetProviderSetupBusy(false);
        }
    }

    private ProviderChoice? SelectedProviderChoice() => setupProviderBox.SelectedItem as ProviderChoice;

    private void SetProviderSetupBusy(bool busy, string? message = null)
    {
        saveApiKeyButton.Enabled = !busy;
        enableProviderButton.Enabled = !busy;
        disableProviderButton.Enabled = !busy;
        setupProviderBox.Enabled = !busy;
        apiKeyBox.Enabled = !busy;
        setupEnableBox.Enabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            SetProviderSetupMessage(message, isError: false);
        }
    }

    private void SetProviderSetupMessage(string message, bool isError)
    {
        providerSetupResultLabel.ForeColor = isError
            ? Color.FromArgb(176, 42, 55)
            : Color.FromArgb(22, 121, 75);
        providerSetupResultLabel.Text = message;
    }

    private static string ShortResultMessage(CliResult result)
    {
        var text = result.CombinedOutput.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = $"CLI exited with code {result.ExitCode}.";
        }

        return text.Length <= 260 ? text : $"{text[..260]}...";
    }

    private void CaptureSettings()
    {
        Settings.CliPath = string.IsNullOrWhiteSpace(cliPathBox.Text) ? null : cliPathBox.Text.Trim();
        Settings.Provider = string.IsNullOrWhiteSpace(providerBox.Text) ? "all" : providerBox.Text.Trim();
        Settings.RefreshIntervalMinutes = (int)refreshIntervalBox.Value;
        Settings.LaunchAtLogin = launchAtLoginBox.Checked;
        Settings.StartMinimized = startMinimizedBox.Checked;
    }

    private sealed record ProviderChoice(string Id, string DisplayName)
    {
        public override string ToString() => $"{DisplayName} ({Id})";
    }
}
