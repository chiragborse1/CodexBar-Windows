using System.Diagnostics;
using Forms = System.Windows.Forms;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfData = System.Windows.Data;
using WpfEffects = System.Windows.Media.Effects;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;
using WpfShapes = System.Windows.Shapes;

namespace CodexBar.Windows;

internal sealed class UsagePopoverWindow : Wpf.Window
{
    private const double PopoverWidth = 360;
    private const double PopoverHeight = 604;
    private WpfControls.TextBlock statusText = null!;
    private WpfControls.TextBlock footerText = null!;
    private WpfControls.Button refreshButton = null!;
    private WpfControls.Button usageButton = null!;
    private WpfControls.Button settingsButton = null!;
    private WpfControls.Button diagnosticsButton = null!;
    private WpfControls.ScrollViewer contentScroll = null!;
    private WpfControls.StackPanel cardsStack = null!;
    private IReadOnlyList<UsagePayloadRow> lastRows = [];
    private CliResult? lastResult;
    private CancellationTokenSource? refreshCancellation;
    private PopoverView currentView = PopoverView.Usage;
    private AppSettings settings;
    private CliRunner cliRunner;
    private readonly bool demoMode;

    public UsagePopoverWindow(
        AppSettings settings,
        CliRunner cliRunner,
        bool demoMode = false,
        bool showInTaskbar = false,
        bool hideOnDeactivate = true)
    {
        this.settings = settings;
        this.cliRunner = cliRunner;
        this.demoMode = demoMode;

        Title = AppInfo.DisplayName;
        Width = PopoverWidth;
        Height = PopoverHeight;
        MinWidth = 340;
        MinHeight = 520;
        ResizeMode = Wpf.ResizeMode.NoResize;
        WindowStyle = Wpf.WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfMedia.Brushes.Transparent;
        ShowInTaskbar = showInTaskbar;
        Topmost = true;
        FontFamily = new WpfMedia.FontFamily("Segoe UI");
        if (hideOnDeactivate)
        {
            Deactivated += (_, _) => Hide();
        }

        var root = new WpfControls.Border
        {
            Background = Brush("#FAFAFA"),
            BorderBrush = Brush("#D4D4D8"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(12),
            SnapsToDevicePixels = true,
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 8,
                Opacity = 0.24,
                Color = WpfMedia.Color.FromRgb(15, 23, 42),
            },
        };
        Content = root;

        var shell = new WpfControls.DockPanel
        {
            LastChildFill = true,
        };
        root.Child = shell;

        var header = BuildHeader();
        WpfControls.DockPanel.SetDock(header, WpfControls.Dock.Top);
        shell.Children.Add(header);

        var footer = BuildFooter();
        WpfControls.DockPanel.SetDock(footer, WpfControls.Dock.Bottom);
        shell.Children.Add(footer);

        cardsStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 6, 0, 6),
        };
        contentScroll = new WpfControls.ScrollViewer
        {
            Content = cardsStack,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled,
            Background = Brush("#FAFAFA"),
        };
        shell.Children.Add(contentScroll);

        WpfControls.Panel.SetZIndex(header, 2);
        WpfControls.Panel.SetZIndex(footer, 2);
    }

    public event EventHandler<AppSettingsChangedEventArgs>? SettingsChanged;

    public bool IsClosed { get; private set; }

    public void ApplySettings(AppSettings newSettings, CliRunner newCliRunner)
    {
        settings = newSettings;
        cliRunner = newCliRunner;
        _ = RefreshUsageAsync();
    }

    public void NavigateToUsage() => ShowUsageView();

    public void NavigateToSettings() => ShowSettingsView();

    public void NavigateToDiagnostics() => ShowDiagnosticsView();

    public void ShowNearCursor()
    {
        var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var x = Math.Min(Forms.Cursor.Position.X - Width + 24, area.Right - Width - 10);
        var y = Math.Min(Forms.Cursor.Position.Y + 12, area.Bottom - Height - 10);
        Left = Math.Max(area.Left + 10, x);
        Top = Math.Max(area.Top + 10, y);
        Show();
        Activate();
        _ = RefreshUsageAsync();
    }

    public void ShowCentered()
    {
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Left = area.Left + Math.Max(10, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(10, (area.Height - Height) / 2);
        Show();
        Activate();
        _ = RefreshUsageAsync();
    }

    public async Task RefreshUsageAsync()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        var token = refreshCancellation.Token;

        refreshButton.IsEnabled = false;
        statusText.Text = demoMode ? "Refreshing demo data..." : $"Refreshing {settings.Provider}...";
        footerText.Text = "";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await cliRunner.UsageJsonAsync(token);
            stopwatch.Stop();
            if (token.IsCancellationRequested)
            {
                return;
            }

            var rows = TryParseRows(result.StandardOutput);
            lastRows = rows;
            lastResult = result;
            if (currentView == PopoverView.Usage)
            {
                RenderRows(rows, result);
            }
            else if (currentView == PopoverView.Diagnostics)
            {
                ShowDiagnosticsView();
            }

            if (currentView == PopoverView.Usage)
            {
                statusText.Text = result.Succeeded
                    ? demoMode
                        ? $"Demo data updated {DateTime.Now:t}"
                        : $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s"
                    : $"Updated with provider errors, code {result.ExitCode}";
                footerText.Text = rows.Count == 0
                    ? "No provider usage returned."
                    : $"{rows.Count} provider{(rows.Count == 1 ? "" : "s")} shown";
            }
            else if (currentView == PopoverView.Diagnostics)
            {
                statusText.Text = result.Succeeded ? "Diagnostics updated" : $"Diagnostics updated, code {result.ExitCode}";
                footerText.Text = "Raw output and provider compatibility.";
            }
            else
            {
                statusText.Text = "Settings";
                footerText.Text = "Settings are saved to your Windows profile.";
            }
        }
        finally
        {
            refreshButton.IsEnabled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        base.OnClosed(e);
    }

    private WpfControls.Border BuildHeader()
    {
        var header = new WpfControls.Grid
        {
            Background = Brush("#FAFAFA"),
            Margin = new Wpf.Thickness(12, 7, 12, 7),
        };
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var titleStack = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Vertical,
        };
        WpfControls.Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);

        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = AppInfo.DisplayName,
            FontSize = 13,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });
        statusText = new WpfControls.TextBlock
        {
            Text = demoMode ? "Demo data" : "Ready",
            FontSize = 10.8,
            Foreground = Brush("#6E6E73"),
            Margin = new Wpf.Thickness(0, 0, 0, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        titleStack.Children.Add(statusText);

        var actionRow = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            VerticalAlignment = Wpf.VerticalAlignment.Top,
        };
        WpfControls.Grid.SetColumn(actionRow, 1);
        header.Children.Add(actionRow);

        usageButton = HeaderButton("Usage");
        usageButton.Click += (_, _) => ShowUsageView();
        actionRow.Children.Add(usageButton);

        refreshButton = HeaderButton("Refresh");
        refreshButton.Click += async (_, _) =>
        {
            ShowUsageView();
            await RefreshUsageAsync();
        };
        actionRow.Children.Add(refreshButton);

        settingsButton = HeaderButton("Settings");
        settingsButton.Click += (_, _) => ShowSettingsView();
        actionRow.Children.Add(settingsButton);

        diagnosticsButton = HeaderButton("More");
        diagnosticsButton.Click += (_, _) => ShowDiagnosticsView();
        actionRow.Children.Add(diagnosticsButton);

        UpdateNavigationChrome();

        return new WpfControls.Border
        {
            Background = Brush("#FAFAFA"),
            BorderBrush = Brush("#E5E5EA"),
            BorderThickness = new Wpf.Thickness(0, 0, 0, 1),
            CornerRadius = new Wpf.CornerRadius(12, 12, 0, 0),
            Child = header,
        };
    }

    private WpfControls.Border BuildFooter()
    {
        footerText = new WpfControls.TextBlock
        {
            FontSize = 11,
            Foreground = Brush("#6E6E73"),
            VerticalAlignment = Wpf.VerticalAlignment.Center,
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };

        return new WpfControls.Border
        {
            Background = Brush("#FAFAFA"),
            BorderBrush = Brush("#E5E5EA"),
            BorderThickness = new Wpf.Thickness(0, 1, 0, 0),
            Padding = new Wpf.Thickness(12, 0, 12, 0),
            Height = 24,
            Child = footerText,
        };
    }

    private void ShowUsageView()
    {
        currentView = PopoverView.Usage;
        contentScroll.Content = cardsStack;
        UpdateNavigationChrome();
        refreshButton.Visibility = Wpf.Visibility.Visible;
        if (lastResult is not null)
        {
            RenderRows(lastRows, lastResult);
            footerText.Text = lastRows.Count == 0
                ? "No provider usage returned."
                : $"{lastRows.Count} provider{(lastRows.Count == 1 ? "" : "s")} shown";
        }
        else
        {
            cardsStack.Children.Clear();
            cardsStack.Children.Add(BuildProviderSwitcherSection(lastRows));
            cardsStack.Children.Add(MenuDivider(new Wpf.Thickness(0, 3, 0, 7)));
            if (demoMode)
            {
                cardsStack.Children.Add(CreateDemoModeCard());
            }
            cardsStack.Children.Add(InfoCard(
                "No usage loaded yet",
                demoMode
                    ? "Refresh to render built-in sample provider usage."
                    : "Refresh to load provider usage from the bundled CLI.",
                Brush("#6E6E73")));
            cardsStack.Children.Add(MenuDivider());
            cardsStack.Children.Add(BuildUsageActionSection());
            footerText.Text = demoMode ? "Waiting for demo refresh." : "Waiting for first refresh.";
        }
    }

    private void ShowSettingsView()
    {
        currentView = PopoverView.Settings;
        UpdateNavigationChrome();
        refreshButton.Visibility = Wpf.Visibility.Collapsed;
        statusText.Text = "Settings";
        footerText.Text = "Settings are saved to your Windows profile.";
        contentScroll.Content = BuildSettingsView();
    }

    private void ShowDiagnosticsView()
    {
        currentView = PopoverView.Diagnostics;
        UpdateNavigationChrome();
        refreshButton.Visibility = Wpf.Visibility.Visible;
        statusText.Text = "Diagnostics";
        footerText.Text = "Raw output and provider compatibility.";
        contentScroll.Content = BuildDiagnosticsView();
    }

    private Wpf.FrameworkElement BuildSettingsView()
    {
        var root = ViewStack();
        root.Children.Add(BuildSetupStatusSection());
        root.Children.Add(BuildCliPathSection());

        var scopeBox = new WpfControls.ComboBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            ItemsSource = ProviderCatalog.ProviderIds
                .Select(id => new ProviderChoice(id, ProviderCatalog.DisplayNameFor(id)))
                .ToArray(),
            SelectedValuePath = nameof(ProviderChoice.Id),
            SelectedValue = settings.Provider,
        };

        var intervalLabel = Caption($"{settings.RefreshIntervalMinutes} min");
        var intervalSlider = new WpfControls.Slider
        {
            Minimum = 1,
            Maximum = 120,
            Value = settings.RefreshIntervalMinutes,
            TickFrequency = 5,
            IsSnapToTickEnabled = false,
        };
        intervalSlider.ValueChanged += (_, _) =>
        {
            intervalLabel.Text = $"{Math.Max(1, (int)Math.Round(intervalSlider.Value))} min";
        };

        var launchBox = new WpfControls.CheckBox
        {
            Content = "Launch at sign-in",
            IsChecked = settings.LaunchAtLogin,
            Margin = new Wpf.Thickness(0, 8, 0, 0),
        };
        var minimizedBox = new WpfControls.CheckBox
        {
            Content = "Start minimized to tray",
            IsChecked = settings.StartMinimized,
            Margin = new Wpf.Thickness(0, 6, 0, 0),
        };
        var saveStatus = Caption("");
        var saveButton = PrimaryButton("Save Settings");
        saveButton.Click += (_, _) =>
        {
            if (scopeBox.SelectedItem is ProviderChoice scope)
            {
                settings.Provider = scope.Id;
            }

            settings.RefreshIntervalMinutes = Math.Max(1, (int)Math.Round(intervalSlider.Value));
            settings.LaunchAtLogin = launchBox.IsChecked == true;
            settings.StartMinimized = minimizedBox.IsChecked == true;
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            saveStatus.Text = "Saved.";
            saveStatus.Foreground = Brush("#15803D");
        };

        var general = new WpfControls.StackPanel();
        general.Children.Add(FieldLabel("Provider scope"));
        general.Children.Add(scopeBox);
        general.Children.Add(Row(FieldLabel("Refresh interval"), intervalLabel));
        general.Children.Add(intervalSlider);
        general.Children.Add(launchBox);
        general.Children.Add(minimizedBox);
        general.Children.Add(ActionRow(saveButton, saveStatus));
        root.Children.Add(SectionCard(
            "General",
            "Control what the tray popover refreshes and how the app starts.",
            general));

        root.Children.Add(BuildProviderSetupSection());
        root.Children.Add(BuildCookieSetupSection());
        return root;
    }

    private Wpf.FrameworkElement BuildSetupStatusSection()
    {
        var provider = settings.Provider;
        var cliPath = cliRunner.ResolveExecutable();
        var content = new WpfControls.StackPanel();
        content.Children.Add(StatusRow(
            "CLI backend",
            string.IsNullOrWhiteSpace(cliPath) ? "Missing" : "Found",
            string.IsNullOrWhiteSpace(cliPath)
                ? $"Set the CLI path or keep {AppInfo.CliFileName} beside the app."
                : cliPath,
            string.IsNullOrWhiteSpace(cliPath) ? "Error" : "Ready"));

        content.Children.Add(StatusRow(
            "Provider scope",
            IsBroadProviderScope(provider) ? ProviderCatalog.DisplayNameFor(provider) : "Selected",
            IsBroadProviderScope(provider)
                ? "Queries configured providers."
                : $"{ProviderCatalog.DisplayNameFor(provider)} - {ProviderCatalog.NotesFor(provider)}",
            IsBroadProviderScope(provider) ? "Info" : ProviderCatalog.SupportFor(provider)));

        if (!IsBroadProviderScope(provider))
        {
            if (ProviderCatalog.SupportsConfigApiKey(provider))
            {
                var hasKey = WindowsCredentialStore.HasApiKey(provider);
                content.Children.Add(StatusRow(
                    "API key",
                    hasKey ? "Stored" : "Needed",
                    hasKey
                        ? "Stored in Windows Credential Manager."
                        : "Save a key below to enable API-backed usage.",
                    hasKey ? "Ready" : "Setup"));
            }

            if (ProviderCatalog.SupportsBrowserSession(provider))
            {
                content.Children.Add(StatusRow(
                    "Browser session",
                    "Available",
                    "Use Manual Web Session below if this provider needs a signed-in browser session.",
                    "Info"));
            }
        }

        var more = SecondaryButton("More");
        more.Click += (_, _) => ShowDiagnosticsView();
        var config = SecondaryButton("Open Config");
        config.Click += (_, _) => ConfigLocator.OpenConfigFile();
        content.Children.Add(ActionRow(more, config));

        return SectionCard(
            demoMode ? "Demo Status" : "Setup Status",
            demoMode
                ? "The app is running with local demo data; real provider checks are skipped."
                : "Current Windows readiness for the selected provider scope.",
            content);
    }

    private Wpf.FrameworkElement BuildCliPathSection()
    {
        var pathBox = new WpfControls.TextBox
        {
            Text = settings.CliPath ?? "",
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            Foreground = Brush("#3C3C43"),
            Padding = new Wpf.Thickness(8, 5, 8, 5),
            TextWrapping = Wpf.TextWrapping.NoWrap,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
        };
        pathBox.ToolTip = $"Optional path to {AppInfo.CliFileName}. Leave empty to use the bundled CLI beside the app.";

        var resultText = Caption(CurrentCliPathMessage());
        var saveButton = PrimaryButton("Save CLI Path");
        var browseButton = SecondaryButton("Browse");
        var clearButton = SecondaryButton("Use Bundled");

        saveButton.Click += (_, _) =>
        {
            var path = pathBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                SetResult(resultText, "That file does not exist. Choose CodexBarCLI.exe or leave the field empty.", isError: true);
                return;
            }

            settings.CliPath = string.IsNullOrWhiteSpace(path) ? null : path;
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            SetResult(
                resultText,
                settings.CliPath is null
                    ? "Using the bundled CLI or PATH lookup."
                    : $"Using {settings.CliPath}.",
                isError: false);
        };

        browseButton.Click += (_, _) =>
        {
            using var dialog = new Forms.OpenFileDialog
            {
                Title = $"Select {AppInfo.CliFileName}",
                Filter = $"{AppInfo.CliFileName}|{AppInfo.CliFileName}|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FileName = AppInfo.CliFileName,
                CheckFileExists = true,
            };

            var currentPath = pathBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var currentDirectory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
                {
                    dialog.InitialDirectory = currentDirectory;
                }
            }
            else if (Directory.Exists(AppContext.BaseDirectory))
            {
                dialog.InitialDirectory = AppContext.BaseDirectory;
            }

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                pathBox.Text = dialog.FileName;
                SetResult(resultText, "Selected CLI path. Save to apply it.", isError: false);
            }
        };

        clearButton.Click += (_, _) =>
        {
            pathBox.Clear();
            settings.CliPath = null;
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            SetResult(resultText, "Using the bundled CLI or PATH lookup.", isError: false);
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(FieldLabel($"{AppInfo.CliFileName} path"));
        content.Children.Add(pathBox);
        content.Children.Add(ActionRow(saveButton, browseButton, clearButton));
        content.Children.Add(resultText);

        return SectionCard(
            "CLI Backend",
            "Point the tray UI at the backend executable when running from a dev folder or custom install.",
            content);
    }

    private string CurrentCliPathMessage()
    {
        var resolved = cliRunner.ResolveExecutable();
        return string.IsNullOrWhiteSpace(resolved)
            ? $"{AppInfo.CliFileName} was not found. Select it here or keep it beside the app."
            : $"Resolved CLI: {resolved}";
    }

    private Wpf.FrameworkElement BuildProviderSetupSection()
    {
        var apiKeyProviders = ProviderCatalog.ApiKeyEntries
            .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
            .ToArray();
        var setupProviderBox = new WpfControls.ComboBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            ItemsSource = apiKeyProviders,
            SelectedItem = PreferredProviderChoice(apiKeyProviders),
        };
        var apiKeyBox = new WpfControls.PasswordBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            Padding = new Wpf.Thickness(7, 4, 7, 4),
        };
        var enableBox = new WpfControls.CheckBox
        {
            Content = "Enable provider after saving",
            IsChecked = true,
            Margin = new Wpf.Thickness(0, 8, 0, 0),
        };
        var resultText = Caption("");
        var saveKeyButton = PrimaryButton("Save API Key");
        var enableButton = SecondaryButton("Enable");
        var disableButton = SecondaryButton("Disable");
        var forgetButton = SecondaryButton("Forget Key");

        void UpdateCredentialStatus()
        {
            if (setupProviderBox.SelectedItem is not ProviderChoice provider)
            {
                resultText.Text = "Choose a provider first.";
                resultText.Foreground = Brush("#6E6E73");
                return;
            }

            var environmentKey = ProviderSecretEnvironment.PrimaryEnvironmentKeyFor(provider.Id);
            var stored = WindowsCredentialStore.HasApiKey(provider.Id);
            resultText.Text = stored
                ? $"Stored in Windows Credential Manager. CLI runs receive {environmentKey}."
                : $"Not stored yet. Saving here uses Windows Credential Manager, not plain JSON.";
            resultText.Foreground = Brush("#6E6E73");
        }

        setupProviderBox.SelectionChanged += (_, _) => UpdateCredentialStatus();

        saveKeyButton.Click += async (_, _) =>
        {
            if (setupProviderBox.SelectedItem is not ProviderChoice provider)
            {
                SetResult(resultText, "Choose a provider first.", isError: true);
                return;
            }

            var apiKey = apiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                SetResult(resultText, "Enter an API key before saving.", isError: true);
                return;
            }

            SetButtonsEnabled(false, saveKeyButton, enableButton, disableButton, forgetButton);
            SetResult(resultText, "Saving to Windows Credential Manager...", isError: false);
            try
            {
                WindowsCredentialStore.WriteApiKey(provider.Id, apiKey);

                if (enableBox.IsChecked == true)
                {
                    var result = await cliRunner.SetProviderEnabledAsync(provider.Id, enabled: true, CancellationToken.None);
                    if (!result.Succeeded)
                    {
                        SetResult(resultText, $"Saved key, but enabling failed: {ShortResultMessage(result)}", isError: true);
                        return;
                    }
                }

                apiKeyBox.Clear();
                settings.Provider = provider.Id;
                SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
                SetResult(resultText, $"Saved securely and focused {provider.DisplayName}.", isError: false);
            }
            catch (Exception ex)
            {
                SetResult(resultText, ex.Message, isError: true);
            }
            finally
            {
                SetButtonsEnabled(true, saveKeyButton, enableButton, disableButton, forgetButton);
            }
        };

        enableButton.Click += async (_, _) =>
            await ChangeProviderEnabledAsync(setupProviderBox, resultText, enabled: true, saveKeyButton, enableButton, disableButton, forgetButton);
        disableButton.Click += async (_, _) =>
            await ChangeProviderEnabledAsync(setupProviderBox, resultText, enabled: false, saveKeyButton, enableButton, disableButton, forgetButton);
        forgetButton.Click += (_, _) =>
        {
            if (setupProviderBox.SelectedItem is not ProviderChoice provider)
            {
                SetResult(resultText, "Choose a provider first.", isError: true);
                return;
            }

            try
            {
                WindowsCredentialStore.DeleteApiKey(provider.Id);
                SetResult(resultText, $"Removed stored key for {provider.DisplayName}.", isError: false);
            }
            catch (Exception ex)
            {
                SetResult(resultText, ex.Message, isError: true);
            }
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(FieldLabel("Provider"));
        content.Children.Add(setupProviderBox);
        content.Children.Add(FieldLabel("API key"));
        content.Children.Add(apiKeyBox);
        content.Children.Add(enableBox);
        content.Children.Add(ActionRow(saveKeyButton, enableButton, disableButton));
        content.Children.Add(ActionRow(forgetButton));
        content.Children.Add(resultText);
        UpdateCredentialStatus();

        return SectionCard(
            "Provider Setup",
            "Add a token without editing JSON, opening another window, or storing secrets in plain text.",
            content);
    }

    private async Task ChangeProviderEnabledAsync(
        WpfControls.ComboBox setupProviderBox,
        WpfControls.TextBlock resultText,
        bool enabled,
        params WpfControls.Button[] buttons)
    {
        if (setupProviderBox.SelectedItem is not ProviderChoice provider)
        {
            SetResult(resultText, "Choose a provider first.", isError: true);
            return;
        }

        SetButtonsEnabled(false, buttons);
        SetResult(resultText, enabled ? "Enabling..." : "Disabling...", isError: false);
        try
        {
            var result = await cliRunner.SetProviderEnabledAsync(provider.Id, enabled, CancellationToken.None);
            if (result.Succeeded)
            {
                SetResult(resultText, $"{provider.DisplayName} is now {(enabled ? "enabled" : "disabled")}.", isError: false);
                settings.Provider = enabled ? provider.Id : settings.Provider;
                SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            }
            else
            {
                SetResult(resultText, ShortResultMessage(result), isError: true);
            }
        }
        finally
        {
            SetButtonsEnabled(true, buttons);
        }
    }

    private Wpf.FrameworkElement BuildCookieSetupSection()
    {
        var cookieProviders = ProviderCatalog.CookieEntries
            .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
            .ToArray();
        var providerBox = new WpfControls.ComboBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            ItemsSource = cookieProviders,
            SelectedItem = PreferredProviderChoice(cookieProviders),
        };
        var cookieBox = new WpfControls.TextBox
        {
            MinHeight = 72,
            MaxHeight = 116,
            AcceptsReturn = true,
            TextWrapping = Wpf.TextWrapping.Wrap,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            FontFamily = new WpfMedia.FontFamily("Consolas"),
            FontSize = 11,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            Padding = new Wpf.Thickness(8),
        };
        var enableBox = new WpfControls.CheckBox
        {
            Content = "Enable provider after saving",
            IsChecked = true,
            Margin = new Wpf.Thickness(0, 8, 0, 0),
        };
        var resultText = Caption("Manual Cookie headers and session payloads are saved in the local config file.");
        var saveButton = PrimaryButton("Save Cookie");
        var clearButton = SecondaryButton("Clear Text");
        var importButtons = new List<WpfControls.Button>();
        foreach (var browser in BrowserCookieImporter.Browsers)
        {
            var importButton = SecondaryButton($"Import {browser.DisplayName}");
            importButton.ToolTip = $"Import {browser.DisplayName} cookies for the selected provider.";
            importButton.Click += async (_, _) =>
            {
                var buttons = importButtons.Concat(new[] { saveButton, clearButton }).ToArray();
                await ImportCookieFromBrowserAsync(browser, providerBox, cookieBox, enableBox, resultText, buttons);
            };
            importButtons.Add(importButton);
        }

        saveButton.Click += async (_, _) =>
        {
            if (providerBox.SelectedItem is not ProviderChoice provider)
            {
                SetResult(resultText, "Choose a provider first.", isError: true);
                return;
            }

            var cookieHeader = cookieBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cookieHeader))
            {
                SetResult(resultText, "Paste a Cookie header or session payload before saving.", isError: true);
                return;
            }

            SetButtonsEnabled(false, importButtons.Concat(new[] { saveButton, clearButton }).ToArray());
            SetResult(resultText, "Saving manual web session...", isError: false);
            try
            {
                var result = await cliRunner.SetCookieHeaderAsync(
                    provider.Id,
                    cookieHeader,
                    enableBox.IsChecked == true,
                    CancellationToken.None);
                if (result.Succeeded)
                {
                    cookieBox.Clear();
                    settings.Provider = provider.Id;
                    SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
                    SetResult(resultText, $"Saved manual web session for {provider.DisplayName}.", isError: false);
                }
                else
                {
                    SetResult(resultText, ShortResultMessage(result), isError: true);
                }
            }
            finally
            {
                SetButtonsEnabled(true, importButtons.Concat(new[] { saveButton, clearButton }).ToArray());
            }
        };
        clearButton.Click += (_, _) =>
        {
            cookieBox.Clear();
            resultText.Text = "Manual Cookie headers and session payloads are saved in the local config file.";
            resultText.Foreground = Brush("#6E6E73");
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(FieldLabel("Web provider"));
        content.Children.Add(providerBox);
        content.Children.Add(FieldLabel("Import browser session"));
        content.Children.Add(ActionWrap(importButtons.Cast<Wpf.FrameworkElement>().ToArray()));
        content.Children.Add(FieldLabel("Cookie header or session payload"));
        content.Children.Add(cookieBox);
        content.Children.Add(enableBox);
        content.Children.Add(ActionRow(saveButton, clearButton));
        content.Children.Add(resultText);

        return SectionCard(
            "Manual Web Session",
            "Import from Windows browsers or paste a Cookie header/session payload when auto import cannot read a session.",
            content);
    }

    private async Task ImportCookieFromBrowserAsync(
        BrowserCookieBrowser browser,
        WpfControls.ComboBox providerBox,
        WpfControls.TextBox cookieBox,
        WpfControls.CheckBox enableBox,
        WpfControls.TextBlock resultText,
        params WpfControls.Button[] buttons)
    {
        if (providerBox.SelectedItem is not ProviderChoice provider)
        {
            SetResult(resultText, "Choose a provider first.", isError: true);
            return;
        }

        SetButtonsEnabled(false, buttons);
        SetResult(resultText, $"Reading {browser.DisplayName} browser cookies...", isError: false);
        try
        {
            if (string.Equals(provider.Id, "windsurf", StringComparison.OrdinalIgnoreCase))
            {
                await ImportWindsurfSessionFromBrowserAsync(browser, provider, cookieBox, enableBox, resultText);
                return;
            }

            var importResult = await BrowserCookieImporter.ImportAsync(
                provider.Id,
                browser.Id,
                CancellationToken.None);
            if (!importResult.Succeeded || string.IsNullOrWhiteSpace(importResult.CookieHeader))
            {
                SetResult(resultText, importResult.Message, isError: true);
                return;
            }

            cookieBox.Text = importResult.CookieHeader;
            SetResult(resultText, $"Saving imported session from {importResult.SourceLabel}...", isError: false);

            var saveResult = await cliRunner.SetCookieHeaderAsync(
                provider.Id,
                importResult.CookieHeader,
                enableBox.IsChecked == true,
                CancellationToken.None);
            if (saveResult.Succeeded)
            {
                cookieBox.Clear();
                settings.Provider = provider.Id;
                SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
                SetResult(
                    resultText,
                    $"Saved {provider.DisplayName} session from {importResult.SourceLabel}.",
                    isError: false);
            }
            else
            {
                SetResult(resultText, ShortResultMessage(saveResult), isError: true);
            }
        }
        catch (OperationCanceledException)
        {
            SetResult(resultText, "Browser cookie import was cancelled.", isError: true);
        }
        catch (Exception ex)
        {
            SetResult(resultText, ex.Message, isError: true);
        }
        finally
        {
            SetButtonsEnabled(true, buttons);
        }
    }

    private async Task ImportWindsurfSessionFromBrowserAsync(
        BrowserCookieBrowser browser,
        ProviderChoice provider,
        WpfControls.TextBox cookieBox,
        WpfControls.CheckBox enableBox,
        WpfControls.TextBlock resultText)
    {
        SetResult(resultText, $"Reading {browser.DisplayName} Windsurf localStorage...", isError: false);
        var importResult = await BrowserLocalStorageImporter.ImportWindsurfAsync(
            browser.Id,
            CancellationToken.None);
        if (!importResult.Succeeded || string.IsNullOrWhiteSpace(importResult.SessionPayload))
        {
            SetResult(resultText, importResult.Message, isError: true);
            return;
        }

        cookieBox.Text = importResult.SessionPayload;
        SetResult(resultText, $"Saving Windsurf session from {importResult.SourceLabel}...", isError: false);

        var saveResult = await cliRunner.SetCookieHeaderAsync(
            provider.Id,
            importResult.SessionPayload,
            enableBox.IsChecked == true,
            CancellationToken.None);
        if (saveResult.Succeeded)
        {
            cookieBox.Clear();
            settings.Provider = provider.Id;
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            SetResult(
                resultText,
                $"Saved Windsurf session from {importResult.SourceLabel}.",
                isError: false);
        }
        else
        {
            SetResult(resultText, ShortResultMessage(saveResult), isError: true);
        }
    }

    private Wpf.FrameworkElement BuildDiagnosticsView()
    {
        var root = ViewStack();
        var actionsStatus = Caption("");

        var actions = new WpfControls.StackPanel();
        actions.Children.Add(ActionWrap(
            PrimaryButton("Refresh"),
            SecondaryButton("Copy Raw"),
            SecondaryButton("Validate")));

        if (actions.Children[0] is WpfControls.Panel row)
        {
            if (row.Children[0] is WpfControls.Button refresh)
            {
                refresh.Click += async (_, _) =>
                {
                    ShowUsageView();
                    await RefreshUsageAsync();
                    ShowDiagnosticsView();
                };
            }

            if (row.Children[1] is WpfControls.Button copy)
            {
                copy.Click += (_, _) =>
                {
                    var text = lastResult?.StandardOutput.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        SetResult(actionsStatus, "No raw output to copy.", isError: true);
                        return;
                    }

                    Wpf.Clipboard.SetText(text);
                    SetResult(actionsStatus, "Raw output copied.", isError: false);
                };
            }

            if (row.Children[2] is WpfControls.Button validate)
            {
                validate.Click += async (_, _) =>
                {
                    SetResult(actionsStatus, "Validating config...", isError: false);
                    var result = await cliRunner.ValidateConfigAsync(CancellationToken.None);
                    SetResult(actionsStatus, result.Succeeded ? "Config is valid." : ShortResultMessage(result), !result.Succeeded);
                };
            }
        }

        actions.Children.Add(ActionWrap(
            SecondaryButton("Open Config"),
            SecondaryButton("Open Folder")));

        if (actions.Children[1] is WpfControls.Panel fileRow)
        {
            if (fileRow.Children[0] is WpfControls.Button openConfig)
            {
                openConfig.Click += (_, _) => SafeDiagnosticAction(ConfigLocator.OpenConfigFile, actionsStatus);
            }

            if (fileRow.Children[1] is WpfControls.Button openFolder)
            {
                openFolder.Click += (_, _) => SafeDiagnosticAction(ConfigLocator.OpenConfigFolder, actionsStatus);
            }
        }

        actions.Children.Add(actionsStatus);
        root.Children.Add(SectionCard("Diagnostics", "Inspect CLI output without leaving the popover.", actions));
        root.Children.Add(BuildUpdateSection());
        root.Children.Add(BuildAboutSection());

        var rawText = lastResult?.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            rawText = lastResult?.CombinedOutput.Trim();
        }

        var rawBox = new WpfControls.TextBox
        {
            Text = string.IsNullOrWhiteSpace(rawText) ? "No CLI output captured yet." : rawText,
            IsReadOnly = true,
            TextWrapping = Wpf.TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            MaxHeight = 170,
            FontFamily = new WpfMedia.FontFamily("Consolas"),
            FontSize = 11,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            Foreground = Brush("#3C3C43"),
            Padding = new Wpf.Thickness(8),
        };
        root.Children.Add(SectionCard("Raw Output", null, rawBox));

        var compatibility = new WpfControls.StackPanel();
        foreach (var provider in ProviderCatalog.Entries)
        {
            compatibility.Children.Add(ProviderCompatibilityRow(provider));
        }

        root.Children.Add(SectionCard("Provider Compatibility", null, compatibility));
        return root;
    }

    private Wpf.FrameworkElement BuildAboutSection()
    {
        var content = new WpfControls.StackPanel();
        content.Children.Add(MessageLine(
            $"{AppInfo.DisplayName} {AppInfo.Version}",
            Brush("#1D1D1F"),
            new Wpf.Thickness(0)));
        content.Children.Add(MessageLine(
            "Windows tray app with the bundled CLI backend.",
            Brush("#6E6E73"),
            new Wpf.Thickness(0, 4, 0, 0)));
        return SectionCard("About", null, content);
    }

    private Wpf.FrameworkElement BuildUpdateSection()
    {
        var status = Caption($"Installed version: {AppInfo.Version}");
        var checkButton = PrimaryButton("Check Updates");
        var openButton = SecondaryButton("Open Release");
        openButton.IsEnabled = false;
        string? releaseUrl = null;

        checkButton.Click += async (_, _) =>
        {
            SetButtonsEnabled(false, checkButton, openButton);
            SetResult(status, "Checking GitHub releases...", isError: false);
            try
            {
                var update = await UpdateChecker.CheckAsync(CancellationToken.None);
                if (update is null)
                {
                    releaseUrl = null;
                    openButton.IsEnabled = false;
                    status.Text = "No GitHub releases found.";
                    status.Foreground = Brush("#6E6E73");
                    return;
                }

                releaseUrl = update.Url;
                openButton.IsEnabled = true;
                if (update.IsNewer)
                {
                    SetResult(
                        status,
                        $"Update available: {update.TagName}{(update.Prerelease ? " prerelease" : "")}.",
                        isError: false);
                }
                else
                {
                    status.Text = $"You are on the latest checked release: {update.TagName}.";
                    status.Foreground = Brush("#6E6E73");
                }
            }
            catch (Exception ex)
            {
                releaseUrl = null;
                openButton.IsEnabled = false;
                SetResult(status, ex.Message, isError: true);
            }
            finally
            {
                checkButton.IsEnabled = true;
            }
        };

        openButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(releaseUrl))
            {
                OpenUrl(releaseUrl);
            }
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(ActionRow(checkButton, openButton));
        content.Children.Add(status);
        return SectionCard("Updates", "Check the GitHub release channel without leaving the tray.", content);
    }

    private void RenderRows(IReadOnlyList<UsagePayloadRow> rows, CliResult result)
    {
        cardsStack.Children.Clear();
        cardsStack.Children.Add(BuildProviderSwitcherSection(rows));
        cardsStack.Children.Add(MenuDivider(new Wpf.Thickness(0, 3, 0, 7)));
        if (demoMode)
        {
            cardsStack.Children.Add(CreateDemoModeCard());
        }

        if (rows.Count == 0)
        {
            cardsStack.Children.Add(CreateEmptyState(result));
            cardsStack.Children.Add(MenuDivider());
            cardsStack.Children.Add(BuildUsageActionSection());
            return;
        }

        if (ShouldShowSetupHint(rows))
        {
            cardsStack.Children.Add(CreateSetupHintCard());
        }

        var orderedRows = rows.OrderBy(RowSortRank).ThenBy(row => row.DisplayName).ToArray();
        for (var index = 0; index < orderedRows.Length; index++)
        {
            cardsStack.Children.Add(CreateProviderCard(orderedRows[index]));
            if (index < orderedRows.Length - 1)
            {
                cardsStack.Children.Add(MenuDivider(new Wpf.Thickness(12, 4, 12, 8)));
            }
        }

        cardsStack.Children.Add(MenuDivider());
        cardsStack.Children.Add(BuildUsageActionSection());
    }

    private Wpf.FrameworkElement BuildProviderSwitcherSection(IReadOnlyList<UsagePayloadRow> rows)
    {
        var wrap = new WpfControls.WrapPanel
        {
            Margin = new Wpf.Thickness(8, 0, 8, 0),
        };

        foreach (var choice in SwitcherProviderChoices(rows))
        {
            wrap.Children.Add(ProviderSwitchButton(choice.Id, choice.DisplayName));
        }

        return wrap;
    }

    private IReadOnlyList<ProviderChoice> SwitcherProviderChoices(IReadOnlyList<UsagePayloadRow> rows)
    {
        var selected = settings.Provider;
        var ordered = rows
            .OrderBy(RowSortRank)
            .ThenBy(row => row.DisplayName)
            .Select(row => row.Provider)
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        foreach (var id in new[] { selected, "enabled", "all" })
        {
            if (!string.IsNullOrWhiteSpace(id) &&
                !ordered.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Insert(0, id);
            }
        }

        return ordered
            .Select(id => new ProviderChoice(id, SwitcherTitleFor(id)))
            .ToArray();
    }

    private WpfControls.Button ProviderSwitchButton(string provider, string title)
    {
        var selected = string.Equals(settings.Provider, provider, StringComparison.OrdinalIgnoreCase);
        var content = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
        };
        var foreground = selected ? WpfMedia.Brushes.White : Brush("#6E6E73");
        content.Children.Add(ProviderBrandIcon(provider, title, foreground, 15));
        content.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 10.8,
            Foreground = foreground,
            Margin = new Wpf.Thickness(5, 0, 0, 0),
            VerticalAlignment = Wpf.VerticalAlignment.Center,
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        var button = new WpfControls.Button
        {
            Content = content,
            Background = selected ? Brush("#0A84FF") : WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(7, 4, 7, 5),
            Margin = new Wpf.Thickness(0, 0, 3, 5),
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(
                6,
                selected ? Brush("#0A84FF") : Brush("#ECECF0"),
                selected ? Brush("#006AD4") : Brush("#E1E1E6")),
        };
        button.Click += async (_, _) =>
        {
            settings.Provider = provider;
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            ShowUsageView();
            await RefreshUsageAsync();
        };
        return button;
    }

    private Wpf.FrameworkElement BuildUsageActionSection()
    {
        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 2, 0, 0),
        };

        var contextualRows = 0;
        var actionProvider = ProviderForMenuActions();
        if (!string.IsNullOrWhiteSpace(actionProvider) && ProviderSupportsSetupAction(actionProvider))
        {
            var label = HasAccountInfo(actionProvider) ? "Switch Account..." : "Add Account...";
            stack.Children.Add(MenuActionRow(
                label,
                $"Open setup for {ProviderCatalog.DisplayNameFor(actionProvider)}.",
                () => ShowProviderSetup(actionProvider)));
            contextualRows++;
        }

        if (!string.IsNullOrWhiteSpace(actionProvider) &&
            ProviderCatalog.DashboardUrlFor(actionProvider) is { } dashboardUrl)
        {
            stack.Children.Add(MenuActionRow(
                "Usage Dashboard",
                $"Open {ProviderCatalog.DisplayNameFor(actionProvider)} usage in your browser.",
                () => OpenUrl(dashboardUrl)));
            contextualRows++;
        }

        if (!string.IsNullOrWhiteSpace(actionProvider) &&
            ProviderCatalog.StatusUrlFor(actionProvider) is { } statusUrl)
        {
            stack.Children.Add(MenuActionRow(
                "Status Page",
                $"Open {ProviderCatalog.DisplayNameFor(actionProvider)} service status.",
                () => OpenUrl(statusUrl)));
            contextualRows++;
        }

        if (!string.IsNullOrWhiteSpace(actionProvider) &&
            ProviderCatalog.ChangelogUrlFor(actionProvider) is { } changelogUrl)
        {
            stack.Children.Add(MenuActionRow(
                "Changelog",
                $"Open {ProviderCatalog.DisplayNameFor(actionProvider)} changelog.",
                () => OpenUrl(changelogUrl)));
            contextualRows++;
        }

        if (StatusLineForMenuActions(actionProvider) is { } statusLine)
        {
            stack.Children.Add(MenuTextRow(statusLine));
            contextualRows++;
        }

        if (contextualRows > 0)
        {
            stack.Children.Add(MenuDivider(new Wpf.Thickness(0, 4, 0, 4)));
        }

        stack.Children.Add(MenuActionRow("Refresh", "Reload usage from the bundled CLI.", (Func<Task>)(async () =>
        {
            ShowUsageView();
            await RefreshUsageAsync();
        })));
        stack.Children.Add(MenuActionRow("Settings...", "Provider scope, API keys, web sessions.", () => ShowSettingsView()));
        stack.Children.Add(MenuActionRow("About CodexBar", "Version, update, and project details.", () => ShowDiagnosticsView()));
        stack.Children.Add(MenuActionRow("Quit", "Exit CodexBar-Windows.", Forms.Application.Exit));
        return stack;
    }

    private string? ProviderForMenuActions()
    {
        if (!IsBroadProviderScope(settings.Provider))
        {
            return settings.Provider;
        }

        return lastRows
            .OrderBy(RowSortRank)
            .ThenBy(row => row.DisplayName)
            .Select(row => row.Provider)
            .FirstOrDefault(provider => !string.IsNullOrWhiteSpace(provider) && !IsBroadProviderScope(provider));
    }

    private void ShowProviderSetup(string provider)
    {
        settings.Provider = provider;
        SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
        ShowSettingsView();
    }

    private bool HasAccountInfo(string provider) =>
        lastRows.Any(row =>
            string.Equals(row.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(row.Account));

    private static bool ProviderSupportsSetupAction(string provider) =>
        ProviderCatalog.SupportsConfigApiKey(provider) ||
        ProviderCatalog.SupportsBrowserSession(provider);

    private string? StatusLineForMenuActions(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var row = lastRows.FirstOrDefault(item =>
            string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (row is null || string.IsNullOrWhiteSpace(row.Status))
        {
            return null;
        }

        return row.Status;
    }

    private ProviderChoice? PreferredProviderChoice(IReadOnlyList<ProviderChoice> choices)
    {
        var selected = choices.FirstOrDefault(choice =>
            string.Equals(choice.Id, settings.Provider, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            return selected;
        }

        var visible = lastRows
            .OrderBy(RowSortRank)
            .Select(row => row.Provider)
            .FirstOrDefault(provider => choices.Any(choice =>
                string.Equals(choice.Id, provider, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(visible))
        {
            return choices.First(choice =>
                string.Equals(choice.Id, visible, StringComparison.OrdinalIgnoreCase));
        }

        return choices.FirstOrDefault();
    }

    private Wpf.FrameworkElement MenuActionRow(string title, string subtitle, Action action) =>
        MenuActionRow(title, subtitle, () =>
        {
            action();
            return Task.CompletedTask;
        });

    private static Wpf.FrameworkElement MenuTextRow(string text) =>
        new WpfControls.TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = Brush("#6E6E73"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(12, 4, 12, 2),
        };

    private Wpf.FrameworkElement MenuActionRow(string title, string subtitle, Func<Task> action)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var icon = ActionIcon(title, Brush("#3C3C43"), 16);
        icon.Margin = new Wpf.Thickness(0, 0, 8, 0);
        WpfControls.Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 12.2,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };
        text.ToolTip = subtitle;
        WpfControls.Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var chevron = ChevronIcon(Brush("#8E8E93"));
        WpfControls.Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        var button = new WpfControls.Button
        {
            Content = grid,
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(12, 5, 10, 6),
            MinHeight = 28,
            HorizontalContentAlignment = Wpf.HorizontalAlignment.Stretch,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6, Brush("#E9F2FF"), Brush("#DCEBFF")),
        };
        button.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                statusText.Text = ex.Message;
            }
        };
        return button;
    }

    private Wpf.FrameworkElement CreateProviderCard(UsagePayloadRow row)
    {
        var accent = ProviderAccent(row.Provider);
        var card = new WpfControls.Border
        {
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(16, 2, 16, 6),
            Margin = new Wpf.Thickness(0),
        };

        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0),
        };
        card.Child = stack;

        var header = new WpfControls.Grid();
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        stack.Children.Add(header);

        var titleStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 0, 8, 0),
        };
        WpfControls.Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);

        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = row.DisplayName,
            FontSize = 13.2,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        var subtitle = SubtitleText(row);
        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = string.IsNullOrWhiteSpace(subtitle) ? StatusLabel(row) : subtitle,
            FontSize = 11,
            Foreground = string.IsNullOrWhiteSpace(row.Error) ? Brush("#6E6E73") : ErrorBrush(row),
            Margin = new Wpf.Thickness(0, 1, 0, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        var trailingText = string.IsNullOrWhiteSpace(row.Account) ? "" : row.Account;
        Wpf.FrameworkElement trailing = !string.IsNullOrWhiteSpace(trailingText)
            ? new WpfControls.TextBlock
            {
                Text = trailingText,
                FontSize = 11.2,
                Foreground = Brush("#6E6E73"),
                VerticalAlignment = Wpf.VerticalAlignment.Top,
                TextAlignment = Wpf.TextAlignment.Right,
                TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            }
            : StatusPill(StatusLabel(row), StatusForeground(row), StatusBackground(row));
        WpfControls.Grid.SetColumn(trailing, 1);
        header.Children.Add(trailing);

        if (row.Metrics.Count > 0)
        {
            stack.Children.Add(MenuDivider(new Wpf.Thickness(0, 7, 0, 2)));
            foreach (var metric in row.Metrics.Take(4))
            {
                stack.Children.Add(CreateMetric(metric, accent));
            }
        }
        else
        {
            stack.Children.Add(MessageLine(
                string.IsNullOrWhiteSpace(row.Error) ? "Limits not available for this provider yet." : FriendlyError(row),
                string.IsNullOrWhiteSpace(row.Error) ? Brush("#6E6E73") : ErrorBrush(row),
                new Wpf.Thickness(0, 7, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Credits) || !string.IsNullOrWhiteSpace(row.Cost))
        {
            stack.Children.Add(MenuDivider(new Wpf.Thickness(0, 8, 0, 0)));
            stack.Children.Add(MessageLine(
                SmallLine("Credits", row.Credits, "Cost", row.Cost),
                Brush("#3C3C43"),
                new Wpf.Thickness(0, 7, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Status))
        {
            stack.Children.Add(MessageLine(row.Status, Brush("#6E6E73"), new Wpf.Thickness(0, 6, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Error) && row.Metrics.Count > 0)
        {
            stack.Children.Add(MessageLine(FriendlyError(row), ErrorBrush(row), new Wpf.Thickness(0, 6, 0, 0)));
            stack.Children.Add(ActionRow(CopyErrorButton(row)));
        }

        if (!string.IsNullOrWhiteSpace(row.Error) && row.Metrics.Count == 0)
        {
            stack.Children.Add(BuildProviderRecoveryActions(row));
        }

        return card;
    }

    private Wpf.FrameworkElement BuildProviderRecoveryActions(UsagePayloadRow row)
    {
        var actions = new List<Wpf.FrameworkElement>();
        var settingsAction = SecondaryButton(IsMissingCliError(row.Error) ? "CLI Path" : "Set Up");
        settingsAction.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(row.Provider))
            {
                settings.Provider = row.Provider;
                SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(settings));
            }

            ShowSettingsView();
        };
        actions.Add(settingsAction);

        if (ProviderCatalog.DashboardUrlFor(row.Provider) is { } url)
        {
            var dashboard = SecondaryButton("Dashboard");
            dashboard.Click += (_, _) => OpenUrl(url);
            actions.Add(dashboard);
        }

        if (ProviderCatalog.StatusUrlFor(row.Provider) is { } statusUrl)
        {
            var status = SecondaryButton("Status");
            status.Click += (_, _) => OpenUrl(statusUrl);
            actions.Add(status);
        }

        actions.Add(CopyErrorButton(row));
        return ActionRow(actions.ToArray());
    }

    private WpfControls.Button CopyErrorButton(UsagePayloadRow row)
    {
        var copyError = SecondaryButton("Copy Error");
        copyError.Click += (_, _) =>
        {
            var text = string.IsNullOrWhiteSpace(row.Error) ? FriendlyError(row) : row.Error;
            if (string.IsNullOrWhiteSpace(text))
            {
                statusText.Text = "No provider error to copy.";
                return;
            }

            Wpf.Clipboard.SetText(text);
            statusText.Text = "Provider error copied.";
        };
        return copyError;
    }

    private Wpf.FrameworkElement CreateSetupHintCard()
    {
        var grid = new WpfControls.Grid();
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var textStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 0, 12, 0),
        };
        WpfControls.Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);
        textStack.Children.Add(new WpfControls.TextBlock
        {
            Text = "Set up a Windows provider",
            FontSize = 12.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
        });
        textStack.Children.Add(new WpfControls.TextBlock
        {
            Text = "Save an API key in Settings to replace these setup and compatibility messages with live usage.",
            FontSize = 11,
            Foreground = Brush("#6E6E73"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 3, 0, 0),
        });

        var button = HeaderButton("Settings");
        button.Margin = new Wpf.Thickness(0);
        button.VerticalAlignment = Wpf.VerticalAlignment.Center;
        button.Click += (_, _) => ShowSettingsView();
        WpfControls.Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return new WpfControls.Border
        {
            Background = Brush("#F5F7FA"),
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(16, 8, 16, 9),
            Margin = new Wpf.Thickness(0, 0, 0, 5),
            Child = grid,
        };
    }

    private Wpf.FrameworkElement CreateDemoModeCard()
    {
        var stack = new WpfControls.StackPanel();
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = "Demo data",
            FontSize = 12.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
        });
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = "These rows are generated locally for UI testing. Start without --demo to refresh real provider usage.",
            FontSize = 11,
            Foreground = Brush("#4B5563"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 3, 0, 0),
        });
        return new WpfControls.Border
        {
            Background = Brush("#F5F7FA"),
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(16, 8, 16, 9),
            Margin = new Wpf.Thickness(0, 0, 0, 5),
            Child = stack,
        };
    }

    private Wpf.FrameworkElement CreateMetric(UsagePayloadMetric metric, WpfMedia.Brush accent)
    {
        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 10, 0, 0),
        };

        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = metric.Title,
            FontSize = 12.2,
            FontWeight = Wpf.FontWeights.Medium,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        stack.Children.Add(ProgressBar(metric.UsedPercent, accent));

        var labels = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 4, 0, 0),
        };
        labels.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        labels.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        labels.Children.Add(new WpfControls.TextBlock
        {
            Text = $"{metric.RemainingPercent:0}% left",
            FontSize = 11,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        if (!string.IsNullOrWhiteSpace(metric.Reset))
        {
            var reset = new WpfControls.TextBlock
            {
                Text = metric.Reset,
                FontSize = 11,
                Foreground = Brush("#6E6E73"),
                TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
                TextAlignment = Wpf.TextAlignment.Right,
            };
            WpfControls.Grid.SetColumn(reset, 1);
            labels.Children.Add(reset);
        }
        stack.Children.Add(labels);

        return stack;
    }

    private Wpf.FrameworkElement CreateEmptyState(CliResult result)
    {
        var text = result.Succeeded
            ? "No provider usage returned."
            : string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"CLI exited with code {result.ExitCode}."
                : result.CombinedOutput.Trim();

        var stack = new WpfControls.StackPanel();
        stack.Children.Add(MessageLine(
            text,
            result.Succeeded ? Brush("#6E6E73") : Brush("#B42318"),
            new Wpf.Thickness(0)));

        var settings = PrimaryButton("Settings");
        settings.Click += (_, _) => ShowSettingsView();
        var more = SecondaryButton("More");
        more.Click += (_, _) => ShowDiagnosticsView();
        stack.Children.Add(ActionRow(settings, more));

        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(16, 7, 16, 8),
            Child = stack,
        };
    }

    private static WpfControls.StackPanel ViewStack() =>
        new()
        {
            Margin = new Wpf.Thickness(0, 6, 0, 6),
        };

    private static Wpf.FrameworkElement SectionCard(string title, string? subtitle, Wpf.FrameworkElement content)
    {
        var stack = new WpfControls.StackPanel();
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new WpfControls.TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                Foreground = Brush("#6E6E73"),
                TextWrapping = Wpf.TextWrapping.Wrap,
                Margin = new Wpf.Thickness(0, 2, 0, 8),
            });
        }
        else
        {
            stack.Children.Add(new WpfControls.Border { Height = 7 });
        }

        stack.Children.Add(content);
        var section = new WpfControls.Border
        {
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(14, 7, 14, 9),
            Margin = new Wpf.Thickness(0),
            Child = stack,
        };

        var root = new WpfControls.StackPanel();
        root.Children.Add(section);
        root.Children.Add(MenuDivider(new Wpf.Thickness(12, 0, 12, 5)));
        return root;
    }

    private static Wpf.FrameworkElement InfoCard(string title, string body, WpfMedia.Brush foreground)
    {
        var stack = new WpfControls.StackPanel();
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
        });
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = body,
            FontSize = 11,
            Foreground = foreground,
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 3, 0, 0),
        });

        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(16, 7, 16, 8),
            Child = stack,
        };
    }

    private static Wpf.FrameworkElement MenuDivider() =>
        MenuDivider(new Wpf.Thickness(0, 2, 0, 6));

    private static Wpf.FrameworkElement MenuDivider(Wpf.Thickness margin) =>
        new WpfControls.Border
        {
            Height = 1,
            Background = Brush("#ECECEC"),
            Margin = margin,
        };

    private static Wpf.FrameworkElement ProviderCompatibilityRow(ProviderCatalogEntry provider)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 0, 0, 7),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var text = new WpfControls.StackPanel();
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = provider.DisplayName,
            FontSize = 12,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
        });
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = $"{provider.Id} - {provider.Notes}",
            FontSize = 10.8,
            Foreground = Brush("#6E6E73"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 2, 8, 0),
        });
        WpfControls.Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var color = provider.Support switch
        {
            "Ready" => Brush("#15803D"),
            "Partial" => Brush("#8A5A00"),
            _ => Brush("#6E6E73"),
        };
        var background = provider.Support switch
        {
            "Ready" => Brush("#DCFCE7"),
            "Partial" => Brush("#FEF3C7"),
            _ => Brush("#E2E8F0"),
        };
        var pill = StatusPill(provider.Support, color, background);
        WpfControls.Grid.SetColumn(pill, 1);
        grid.Children.Add(pill);
        return grid;
    }

    private static string SwitcherTitleFor(string provider) =>
        IsBroadProviderScope(provider) ? "Overview" : ProviderCatalog.DisplayNameFor(provider);

    private static WpfControls.TextBlock FieldLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#3C3C43"),
            Margin = new Wpf.Thickness(0, 9, 0, 4),
        };

    private static WpfControls.TextBlock Caption(string text) =>
        new()
        {
            Text = text,
            FontSize = 11,
            Foreground = Brush("#6E6E73"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };

    private static Wpf.FrameworkElement Row(Wpf.FrameworkElement left, Wpf.FrameworkElement right)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 8, 0, 0),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        WpfControls.Grid.SetColumn(left, 0);
        WpfControls.Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static Wpf.FrameworkElement StatusRow(string label, string value, string detail, string status)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 7, 0, 0),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var text = new WpfControls.StackPanel();
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = label,
            FontSize = 11.8,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = detail,
            FontSize = 10.7,
            Foreground = Brush("#6E6E73"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 1, 12, 0),
        });
        WpfControls.Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var foreground = status switch
        {
            "Ready" => Brush("#15803D"),
            "Setup" => Brush("#8A5A00"),
            "Error" => Brush("#B42318"),
            "Partial" => Brush("#8A5A00"),
            _ => Brush("#4B5563"),
        };
        var background = status switch
        {
            "Ready" => Brush("#DCFCE7"),
            "Setup" => Brush("#FEF3C7"),
            "Error" => Brush("#FEE4E2"),
            "Partial" => Brush("#FEF3C7"),
            _ => Brush("#E2E8F0"),
        };
        var pill = StatusPill(value, foreground, background);
        WpfControls.Grid.SetColumn(pill, 1);
        grid.Children.Add(pill);
        return grid;
    }

    private static WpfControls.StackPanel ActionRow(params Wpf.FrameworkElement[] controls)
    {
        var row = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            Margin = new Wpf.Thickness(0, 9, 0, 0),
        };
        foreach (var control in controls)
        {
            control.Margin = new Wpf.Thickness(0, 0, 6, 0);
            row.Children.Add(control);
        }

        return row;
    }

    private static WpfControls.WrapPanel ActionWrap(params Wpf.FrameworkElement[] controls)
    {
        var row = new WpfControls.WrapPanel
        {
            Margin = new Wpf.Thickness(0, 9, 0, 0),
        };
        foreach (var control in controls)
        {
            control.Margin = new Wpf.Thickness(0, 0, 6, 6);
            row.Children.Add(control);
        }

        return row;
    }

    private static WpfControls.Button PopoverButton(string text)
    {
        var button = new WpfControls.Button
        {
            Content = text,
            FontSize = 11.2,
            Foreground = Brush("#1D1D1F"),
            Background = WpfMedia.Brushes.Transparent,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(8, 3, 8, 4),
            Margin = new Wpf.Thickness(4, 0, 0, 0),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6, Brush("#ECECF0"), Brush("#E1E1E6")),
        };
        return button;
    }

    private static WpfControls.Button HeaderButton(string text)
    {
        var button = PopoverButton(text);
        button.FontSize = 11;
        button.Padding = new Wpf.Thickness(8, 3, 8, 4);
        button.Background = WpfMedia.Brushes.Transparent;
        button.BorderBrush = WpfMedia.Brushes.Transparent;
        button.Template = RoundedButtonTemplate(5, Brush("#ECECF0"), Brush("#E1E1E6"));
        return button;
    }

    private static WpfControls.Button PrimaryButton(string text) =>
        StyledButton(text, Brush("#1D1D1F"), WpfMedia.Brushes.White, Brush("#1D1D1F"));

    private static WpfControls.Button SecondaryButton(string text) =>
        StyledButton(text, WpfMedia.Brushes.White, Brush("#1D1D1F"), Brush("#D8D8DD"));

    private static WpfControls.Button NavButton(string text)
    {
        var button = StyledButton(text, WpfMedia.Brushes.White, Brush("#6E6E73"), WpfMedia.Brushes.White);
        button.Margin = new Wpf.Thickness(0);
        button.Padding = new Wpf.Thickness(8, 5, 8, 5);
        return button;
    }

    private static WpfControls.Button StyledButton(
        string text,
        WpfMedia.Brush background,
        WpfMedia.Brush foreground,
        WpfMedia.Brush border)
    {
        return new WpfControls.Button
        {
            Content = text,
            FontSize = 11.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = foreground,
            Background = background,
            BorderBrush = border,
            BorderThickness = new Wpf.Thickness(1),
            Padding = new Wpf.Thickness(9, 5, 9, 6),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6),
        };
    }

    private void UpdateNavigationChrome()
    {
        UpdateNavButton(usageButton, currentView == PopoverView.Usage);
        UpdateNavButton(settingsButton, currentView == PopoverView.Settings);
        UpdateNavButton(diagnosticsButton, currentView == PopoverView.Diagnostics);
    }

    private static void UpdateNavButton(WpfControls.Button button, bool selected)
    {
        button.Visibility = selected && button.Content?.ToString() == "Usage"
            ? Wpf.Visibility.Collapsed
            : Wpf.Visibility.Visible;
        button.Background = selected ? Brush("#E9E9EE") : WpfMedia.Brushes.Transparent;
        button.Foreground = selected ? Brush("#1D1D1F") : Brush("#3C3C43");
        button.BorderBrush = WpfMedia.Brushes.Transparent;
    }

    private static WpfControls.ControlTemplate RoundedButtonTemplate(
        double radius,
        WpfMedia.Brush? hoverBackground = null,
        WpfMedia.Brush? pressedBackground = null)
    {
        var template = new WpfControls.ControlTemplate(typeof(WpfControls.Button));
        var border = new Wpf.FrameworkElementFactory(typeof(WpfControls.Border));
        border.SetValue(WpfControls.Border.CornerRadiusProperty, new Wpf.CornerRadius(radius));
        border.SetBinding(
            WpfControls.Border.BackgroundProperty,
            new WpfData.Binding("Background") { RelativeSource = WpfData.RelativeSource.TemplatedParent });
        border.SetBinding(
            WpfControls.Border.BorderBrushProperty,
            new WpfData.Binding("BorderBrush") { RelativeSource = WpfData.RelativeSource.TemplatedParent });
        border.SetBinding(
            WpfControls.Border.BorderThicknessProperty,
            new WpfData.Binding("BorderThickness") { RelativeSource = WpfData.RelativeSource.TemplatedParent });

        var presenter = new Wpf.FrameworkElementFactory(typeof(WpfControls.ContentPresenter));
        presenter.SetValue(WpfControls.ContentPresenter.HorizontalAlignmentProperty, Wpf.HorizontalAlignment.Center);
        presenter.SetValue(WpfControls.ContentPresenter.VerticalAlignmentProperty, Wpf.VerticalAlignment.Center);
        presenter.SetBinding(
            WpfControls.ContentPresenter.MarginProperty,
            new WpfData.Binding("Padding") { RelativeSource = WpfData.RelativeSource.TemplatedParent });
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Wpf.Trigger { Property = Wpf.UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Wpf.Setter(
            WpfControls.Control.BackgroundProperty,
            hoverBackground ?? Brush("#F3F3F6")));
        template.Triggers.Add(hover);

        var pressed = new Wpf.Trigger { Property = WpfControls.Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Wpf.Setter(
            WpfControls.Control.BackgroundProperty,
            pressedBackground ?? Brush("#E9E9EE")));
        template.Triggers.Add(pressed);

        var disabled = new Wpf.Trigger { Property = WpfControls.Control.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Wpf.Setter(Wpf.UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(disabled);
        return template;
    }

    private static void SetResult(WpfControls.TextBlock textBlock, string text, bool isError)
    {
        textBlock.Text = text;
        textBlock.Foreground = isError ? Brush("#B42318") : Brush("#15803D");
    }

    private static void SetButtonsEnabled(bool enabled, params WpfControls.Button[] buttons)
    {
        foreach (var button in buttons)
        {
            button.IsEnabled = enabled;
        }
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

    private static void SafeDiagnosticAction(Action action, WpfControls.TextBlock status)
    {
        try
        {
            action();
            SetResult(status, "Opened.", isError: false);
        }
        catch (Exception ex)
        {
            SetResult(status, ex.Message, isError: true);
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private static WpfControls.Border ProviderIcon(
        string displayName,
        WpfMedia.Brush accent,
        double size = 34,
        double fontSize = 13.5)
    {
        var letter = string.IsNullOrWhiteSpace(displayName)
            ? "?"
            : displayName.Trim()[0].ToString().ToUpperInvariant();

        return new WpfControls.Border
        {
            Width = size,
            Height = size,
            CornerRadius = new Wpf.CornerRadius(size / 2),
            Background = accent,
            Child = new WpfControls.TextBlock
            {
                Text = letter,
                FontSize = fontSize,
                FontWeight = Wpf.FontWeights.SemiBold,
                Foreground = WpfMedia.Brushes.White,
                HorizontalAlignment = Wpf.HorizontalAlignment.Center,
                VerticalAlignment = Wpf.VerticalAlignment.Center,
            },
        };
    }

    private static Wpf.FrameworkElement ProviderBrandIcon(
        string provider,
        string displayName,
        WpfMedia.Brush foreground,
        double size)
    {
        if (IsBroadProviderScope(provider))
        {
            return ActionIcon("Overview", foreground, size);
        }

        var pathData = ProviderSvgPath(provider);
        if (!string.IsNullOrWhiteSpace(pathData))
        {
            return new WpfControls.Viewbox
            {
                Width = size,
                Height = size,
                Child = new WpfShapes.Path
                {
                    Data = WpfMedia.Geometry.Parse(pathData),
                    Fill = foreground,
                    Stretch = WpfMedia.Stretch.Uniform,
                },
            };
        }

        return new WpfControls.TextBlock
        {
            Text = string.IsNullOrWhiteSpace(displayName) ? "?" : displayName.Trim()[0].ToString().ToUpperInvariant(),
            Width = size,
            Height = size,
            FontSize = Math.Max(8, size - 6),
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = foreground,
            TextAlignment = Wpf.TextAlignment.Center,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };
    }

    private static Wpf.FrameworkElement ActionIcon(string title, WpfMedia.Brush stroke, double size)
    {
        if (string.Equals(title, "About CodexBar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title, "About", StringComparison.OrdinalIgnoreCase))
        {
            return InfoIcon(stroke, size);
        }

        var pathData = ActionIconPath(title);
        if (!string.IsNullOrWhiteSpace(pathData))
        {
            return new WpfControls.Viewbox
            {
                Width = size,
                Height = size,
                Child = new WpfShapes.Path
                {
                    Data = WpfMedia.Geometry.Parse(pathData),
                    Stroke = stroke,
                    StrokeThickness = 1.65,
                    StrokeStartLineCap = WpfMedia.PenLineCap.Round,
                    StrokeEndLineCap = WpfMedia.PenLineCap.Round,
                    StrokeLineJoin = WpfMedia.PenLineJoin.Round,
                    Fill = WpfMedia.Brushes.Transparent,
                    Stretch = WpfMedia.Stretch.Uniform,
                },
            };
        }

        return new WpfControls.TextBlock
        {
            Text = "?",
            Width = size,
            Height = size,
            FontSize = Math.Max(8, size - 6),
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = stroke,
            TextAlignment = Wpf.TextAlignment.Center,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };
    }

    private static string? ActionIconPath(string title)
    {
        var normalized = title.ToLowerInvariant();
        if (normalized.Contains("refresh", StringComparison.Ordinal))
        {
            return "M12 4 L12 1 L15 4 M12 1 C8 1 5 4 5 8 C5 11 7 13 10 14 M4 12 L4 15 L1 12 M4 15 C8 15 11 12 11 8 C11 5 9 3 6 2";
        }

        if (normalized.Contains("dashboard", StringComparison.Ordinal))
        {
            return "M2 14 L14 14 M4 14 L4 8 M8 14 L8 4 M12 14 L12 6";
        }

        if (normalized.Contains("status", StringComparison.Ordinal))
        {
            return "M2 9 L5 9 L7 4 L10 13 L12 9 L15 9";
        }

        if (normalized.Contains("changelog", StringComparison.Ordinal) ||
            normalized.Contains("more", StringComparison.Ordinal))
        {
            return "M4 4 L13 4 M4 8 L13 8 M4 12 L13 12";
        }

        if (normalized.Contains("settings", StringComparison.Ordinal) ||
            normalized.Contains("setup", StringComparison.Ordinal) ||
            normalized.Contains("path", StringComparison.Ordinal))
        {
            return "M3 5 L13 5 M3 11 L13 11 M6 3 L6 7 M10 9 L10 13";
        }

        if (normalized.Contains("config file", StringComparison.Ordinal))
        {
            return "M4 2 L10 2 L14 6 L14 14 L4 14 Z M10 2 L10 6 L14 6";
        }

        if (normalized.Contains("config folder", StringComparison.Ordinal) ||
            normalized.Contains("folder", StringComparison.Ordinal))
        {
            return "M2 5 L7 5 L9 7 L14 7 L14 14 L2 14 Z";
        }

        if (normalized.Contains("account", StringComparison.Ordinal))
        {
            return "M8 8 C10 8 11 7 11 5 C11 3 10 2 8 2 C6 2 5 3 5 5 C5 7 6 8 8 8 Z M3 14 C4 11 5 10 8 10 C11 10 12 11 13 14";
        }

        if (normalized.Contains("terminal", StringComparison.Ordinal))
        {
            return "M2 3 L14 3 L14 13 L2 13 Z M4 6 L7 8 L4 10 M8 10 L12 10";
        }

        if (normalized.Contains("quit", StringComparison.Ordinal) ||
            normalized.Contains("exit", StringComparison.Ordinal))
        {
            return "M4 4 L12 12 M12 4 L4 12";
        }

        if (normalized.Contains("overview", StringComparison.Ordinal) ||
            normalized.Contains("usage", StringComparison.Ordinal))
        {
            return "M2 2 L7 2 L7 7 L2 7 Z M9 2 L14 2 L14 7 L9 7 Z M2 9 L7 9 L7 14 L2 14 Z M9 9 L14 9 L14 14 L9 14 Z";
        }

        return null;
    }

    private static Wpf.FrameworkElement InfoIcon(WpfMedia.Brush stroke, double size)
    {
        var grid = new WpfControls.Grid
        {
            Width = size,
            Height = size,
        };
        grid.Children.Add(new WpfShapes.Ellipse
        {
            Stroke = stroke,
            StrokeThickness = 1.55,
            Width = size,
            Height = size,
        });
        grid.Children.Add(new WpfControls.TextBlock
        {
            Text = "i",
            FontSize = Math.Max(9, size - 5),
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = stroke,
            HorizontalAlignment = Wpf.HorizontalAlignment.Center,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
            Margin = new Wpf.Thickness(0, -1, 0, 0),
        });
        return grid;
    }

    private static string? ProviderSvgPath(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "codex" or "openai" =>
                "M83.7733 42.8087C84.6678 40.1149 84.9771 37.2613 84.6807 34.4385C84.3843 31.6156 83.489 28.8885 82.0544 26.4394C77.6908 18.8436 68.9203 14.9365 60.3548 16.7725C57.9831 14.1344 54.9591 12.1668 51.5864 11.0673C48.2137 9.96772 44.611 9.77498 41.1402 10.5084C37.6694 11.2418 34.4527 12.8755 31.8132 15.2455C29.1736 17.6155 27.204 20.6383 26.1024 24.0103C23.3212 24.5806 20.6938 25.738 18.3958 27.405C16.0977 29.0721 14.1819 31.2104 12.7765 33.6772C8.36538 41.2609 9.3669 50.8267 15.2527 57.3327C14.3549 60.0251 14.0424 62.8782 14.3361 65.7012C14.6298 68.5241 15.523 71.2518 16.9558 73.7017C21.325 81.3002 30.1011 85.207 38.6712 83.3686C40.5554 85.4904 42.8707 87.1858 45.4623 88.3416C48.0539 89.4975 50.8622 90.0871 53.6999 90.0713C62.4793 90.079 70.2575 84.4114 72.9393 76.0515C75.7201 75.4802 78.347 74.3225 80.6449 72.6555C82.9427 70.9886 84.8587 68.8507 86.2649 66.3846C90.6227 58.8145 89.6172 49.3005 83.7733 42.8087ZM53.6999 84.8356C50.1955 84.8411 46.801 83.6129 44.1116 81.3661L44.5848 81.098L60.5123 71.9043C60.9087 71.6718 61.2379 71.3402 61.4674 70.942C61.6969 70.5439 61.8189 70.0929 61.8215 69.6333V47.1769L68.5553 51.072C68.6225 51.1063 68.6694 51.1707 68.6814 51.2456V69.854C68.6641 78.1208 61.9667 84.8183 53.6999 84.8356ZM21.4977 71.0843C19.7402 68.0497 19.1092 64.4925 19.7156 61.0386L20.1885 61.3225L36.1321 70.5165C36.5266 70.748 36.9757 70.87 37.4331 70.87C37.8905 70.87 38.3396 70.748 38.7341 70.5165L58.21 59.2883V67.0628C58.2081 67.1031 58.1973 67.1424 58.1782 67.1779C58.1591 67.2134 58.1322 67.2441 58.0996 67.2678L41.9671 76.5722C34.798 80.7022 25.6388 78.2463 21.4977 71.0843ZM17.3026 36.3898C19.0723 33.3357 21.8655 31.0062 25.1878 29.8138V48.7376C25.1818 49.1949 25.2986 49.6453 25.5261 50.042C25.7535 50.4387 26.0833 50.7671 26.4809 50.9928L45.8622 62.1739L39.1283 66.069C39.0919 66.0883 39.0513 66.0984 39.0101 66.0984C38.9689 66.0984 38.9283 66.0883 38.8919 66.069L22.7908 56.7809C15.6359 52.6337 13.1822 43.4816 17.3026 36.3112V36.3898ZM72.624 49.2426L53.1792 37.9512L59.8976 34.0718C59.9341 34.0524 59.9747 34.0423 60.016 34.0423C60.0573 34.0423 60.0979 34.0524 60.1344 34.0718L76.2355 43.3761C78.6973 44.7966 80.7043 46.8882 82.0221 49.4065C83.3398 51.9249 83.914 54.7661 83.6775 57.5985C83.4411 60.431 82.4038 63.1377 80.6867 65.4027C78.9696 67.6677 76.6436 69.3975 73.9803 70.3901V51.466C73.9663 51.0096 73.834 50.5647 73.5962 50.1749C73.3584 49.7851 73.0234 49.4638 72.624 49.2426ZM79.3261 39.1657L78.8529 38.8815L62.9411 29.6089C62.5442 29.376 62.0924 29.2532 61.6322 29.2532C61.172 29.2532 60.7202 29.376 60.3233 29.6089L40.8629 40.8374V33.0628C40.8587 33.0233 40.8654 32.9834 40.882 32.9473C40.8987 32.9113 40.9248 32.8803 40.9575 32.8579L57.0586 23.5692C59.5263 22.1476 62.3478 21.458 65.193 21.5811C68.0382 21.7042 70.7896 22.6348 73.1253 24.2642C75.461 25.8936 77.2845 28.1543 78.3825 30.782C79.4806 33.4097 79.8077 36.2957 79.3257 39.1025V39.1657H79.3261ZM37.1888 52.9484L30.455 49.069C30.4213 49.0487 30.3925 49.0212 30.3707 48.9884C30.3488 48.9557 30.3345 48.9186 30.3286 48.8797V30.3188C30.3323 27.4714 31.1466 24.6839 32.6761 22.2822C34.2057 19.8805 36.3874 17.9639 38.9661 16.7564C41.5448 15.549 44.4139 15.1005 47.2381 15.4636C50.0622 15.8267 52.7247 16.9862 54.9141 18.8067L54.4409 19.0748L38.5134 28.2686C38.117 28.5011 37.7879 28.8327 37.5584 29.2308C37.329 29.629 37.207 30.0799 37.2045 30.5395L37.1888 52.9487V52.9484ZM40.8472 45.0632L49.5209 40.0643L58.21 45.0635V55.0615L49.5523 60.0608L40.8632 55.0615L40.8472 45.0632Z",
            "claude" =>
                "M25.7146 63.2153L41.4393 54.3917L41.7025 53.6226L41.4393 53.1976H40.6705L38.0394 53.0359L29.054 52.7929L21.2624 52.4691L13.7134 52.0644L11.8111 51.6594L10.0303 49.3118L10.2123 48.138L11.8111 47.0657L14.0981 47.2681L19.1574 47.6119L26.7467 48.138L32.2516 48.4618L40.4073 49.3118H41.7025L41.8846 48.7857L41.4393 48.4618L41.0955 48.138L33.243 42.8155L24.7432 37.1894L20.2909 33.9513L17.8824 32.3119L16.6684 30.774L16.1422 27.4147L18.328 25.0062L21.2624 25.2088L22.0112 25.4112L24.9861 27.6979L31.3407 32.616L39.6381 38.7273L40.8525 39.7391L41.3381 39.395L41.399 39.1523L40.8525 38.2415L36.3394 30.0858L31.5227 21.7883L29.3775 18.3478L28.811 16.2837C28.6087 15.4334 28.4669 14.7252 28.4669 13.8549L30.9563 10.4753L32.3321 10.0303L35.6515 10.4756L37.0479 11.6897L39.112 16.4052L42.4513 23.8327L47.6321 33.9313L49.15 36.9265L49.9594 39.6991L50.2632 40.5491H50.7894V40.0632L51.2141 34.3766L52.0035 27.3944L52.7726 18.4087L53.0358 15.8793L54.2905 12.8435L56.7795 11.2041L58.7224 12.135L60.3212 14.422L60.0986 15.899L59.1474 22.0718L57.2857 31.7458L56.0713 38.2218H56.7795L57.5892 37.4121L60.8677 33.061L66.3723 26.18L68.801 23.448L71.6342 20.4325L73.4556 18.9957H76.8962L79.4255 22.7601L78.2926 26.6456L74.7509 31.1384L71.8163 34.943L67.607 40.6097L64.9758 45.1431L65.2188 45.5072L65.8464 45.4466L75.358 43.4228L80.4984 42.4917L86.6304 41.4393L89.4033 42.7346L89.7065 44.0502L88.6135 46.7419L82.0566 48.3607L74.3662 49.8989L62.9118 52.6109L62.77 52.7121L62.9321 52.9144L68.0925 53.4L70.2987 53.5214H75.7021L85.7601 54.2702L88.3912 56.0108L89.9697 58.1358L89.7065 59.7545L85.6589 61.8189L80.1949 60.5236L67.4452 57.4881L63.0735 56.3952H62.4665V56.7596L66.1093 60.3213L72.7877 66.3523L81.1461 74.1236L81.5707 76.0462L80.4984 77.5638L79.3649 77.4021L72.0186 71.8772L69.1854 69.3879L62.77 63.9844H62.3453V64.5509L63.8223 66.7164L71.6342 78.4544L72.0389 82.0567L71.4725 83.2308L69.4487 83.939L67.2222 83.534L62.6485 77.1189L57.9333 69.8937L54.1284 63.4177L53.6631 63.6809L51.4167 87.8651L50.3644 89.0995L47.9356 90.0303L45.9121 88.4924L44.8392 86.0031L45.9118 81.0852L47.2071 74.6701L48.2594 69.5699L49.2106 63.2356L49.7773 61.131L49.7367 60.9892L49.2715 61.0498L44.4954 67.607L37.23 77.4224L31.4825 83.5746L30.1063 84.1211L27.7181 82.8864L27.9408 80.6805L29.2763 78.7177L37.2297 68.5988L42.026 62.3248L45.1227 58.7025L45.1024 58.176H44.9204L23.7917 71.8975L20.0274 72.3831L18.4083 70.8655L18.6106 68.3761L19.3798 67.5664L25.7343 63.195L25.7146 63.2153Z",
            _ => null,
        };

    private static Wpf.FrameworkElement ChevronIcon(WpfMedia.Brush stroke) =>
        new WpfShapes.Path
        {
            Data = WpfMedia.Geometry.Parse("M 1 1 L 5 5 L 1 9"),
            Stroke = stroke,
            StrokeThickness = 1.6,
            StrokeStartLineCap = WpfMedia.PenLineCap.Round,
            StrokeEndLineCap = WpfMedia.PenLineCap.Round,
            StrokeLineJoin = WpfMedia.PenLineJoin.Round,
            Width = 6,
            Height = 10,
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };

    private static WpfControls.Border StatusPill(string text, WpfMedia.Brush foreground, WpfMedia.Brush background) =>
        new()
        {
            Background = background,
            CornerRadius = new Wpf.CornerRadius(999),
            Padding = new Wpf.Thickness(7, 2, 7, 3),
            VerticalAlignment = Wpf.VerticalAlignment.Top,
            Child = new WpfControls.TextBlock
            {
                Text = text,
                FontSize = 10.8,
                FontWeight = Wpf.FontWeights.SemiBold,
                Foreground = foreground,
            },
        };

    private static Wpf.FrameworkElement ProgressBar(double usedPercent, WpfMedia.Brush accent)
    {
        var clamped = Math.Clamp(usedPercent, 0, 100);
        var grid = new WpfControls.Grid
        {
            Height = 6,
            Margin = new Wpf.Thickness(0, 5, 0, 0),
            ClipToBounds = true,
        };
        var track = new WpfControls.Border
        {
            Background = Brush("#E8E8EE"),
            CornerRadius = new Wpf.CornerRadius(3),
        };
        var fill = new WpfControls.Border
        {
            Background = accent,
            CornerRadius = new Wpf.CornerRadius(3),
            HorizontalAlignment = Wpf.HorizontalAlignment.Left,
        };
        grid.Children.Add(track);
        grid.Children.Add(fill);
        grid.SizeChanged += (_, args) =>
        {
            fill.Width = Math.Max(0, args.NewSize.Width * (clamped / 100D));
        };
        return grid;
    }

    private static WpfControls.TextBlock MessageLine(string text, WpfMedia.Brush brush, Wpf.Thickness margin) =>
        new()
        {
            Text = text,
            FontSize = 11,
            Foreground = brush,
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = margin,
        };

    private static string SubtitleText(UsagePayloadRow row)
    {
        var parts = new[] { row.Account, row.Source, row.Updated }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join("  |  ", parts);
    }

    private static string SmallLine(string leftTitle, string leftValue, string rightTitle, string rightValue)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(leftValue))
        {
            parts.Add($"{leftTitle}: {leftValue}");
        }

        if (!string.IsNullOrWhiteSpace(rightValue))
        {
            parts.Add($"{rightTitle}: {rightValue}");
        }

        return string.Join("  |  ", parts);
    }

    private static string StatusLabel(UsagePayloadRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Error))
        {
            if (NeedsApiKeySetup(row))
            {
                return "Setup";
            }

            if (IsWindowsPendingError(row.Error))
            {
                return "Pending";
            }

            return "Error";
        }

        return string.IsNullOrWhiteSpace(row.Status) ? "Ready" : row.Status.Split(':')[0];
    }

    private static WpfMedia.Brush StatusForeground(UsagePayloadRow row)
    {
        if (NeedsApiKeySetup(row))
        {
            return Brush("#8A5A00");
        }

        if (!string.IsNullOrWhiteSpace(row.Error) && IsWindowsPendingError(row.Error))
        {
            return Brush("#4B5563");
        }

        return string.IsNullOrWhiteSpace(row.Error) ? Brush("#15803D") : Brush("#B42318");
    }

    private static WpfMedia.Brush StatusBackground(UsagePayloadRow row)
    {
        if (NeedsApiKeySetup(row))
        {
            return Brush("#FEF3C7");
        }

        if (!string.IsNullOrWhiteSpace(row.Error) && IsWindowsPendingError(row.Error))
        {
            return Brush("#F3F4F6");
        }

        return string.IsNullOrWhiteSpace(row.Error) ? Brush("#DCFCE7") : Brush("#FEE4E2");
    }

    private static WpfMedia.Brush ErrorBrush(UsagePayloadRow row) =>
        NeedsApiKeySetup(row) || IsWindowsPendingError(row.Error) ? Brush("#6E6E73") : Brush("#B42318");

    private static string FriendlyError(UsagePayloadRow row)
    {
        if (NeedsApiKeySetup(row))
        {
            return "Add this provider's API key in Settings to enable Windows usage checks.";
        }

        if (IsMissingCliError(row.Error))
        {
            return $"Could not find {AppInfo.CliFileName}. Put it beside {AppInfo.DisplayName}.exe or set the CLI path in Settings.";
        }

        if (IsNoFetchStrategyError(row.Error))
        {
            if (ProviderCatalog.SupportsConfigApiKey(row.Provider) &&
                ProviderCatalog.SupportsBrowserSession(row.Provider))
            {
                return "Save an API key or import a browser session in Settings, then refresh this provider.";
            }

            if (ProviderCatalog.SupportsConfigApiKey(row.Provider))
            {
                return "Save this provider's API key in Settings, then refresh usage.";
            }

            if (ProviderCatalog.SupportsBrowserSession(row.Provider))
            {
                return "Import a signed-in browser session in Settings, then refresh usage.";
            }
        }

        if (IsWindowsPendingError(row.Error))
        {
            var notes = ProviderCatalog.NotesFor(row.Provider);
            return string.IsNullOrWhiteSpace(notes)
                ? "This source is not available on Windows yet."
                : $"This source needs a Windows-specific setup path. {notes}";
        }

        return row.Error;
    }

    private bool ShouldShowSetupHint(IReadOnlyList<UsagePayloadRow> rows) =>
        IsBroadProviderScope(settings.Provider) &&
        rows.Count > 0 &&
        rows.All(row => row.Metrics.Count == 0) &&
        rows.Any(row => !string.IsNullOrWhiteSpace(row.Error));

    private static bool IsBroadProviderScope(string provider) =>
        string.Equals(provider, "enabled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "all", StringComparison.OrdinalIgnoreCase);

    private static bool IsMissingCliError(string error) =>
        error.Contains($"Could not find {AppInfo.CliFileName}", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoFetchStrategyError(string error) =>
        error.Contains("No available fetch strategy", StringComparison.OrdinalIgnoreCase);

    private static int RowSortRank(UsagePayloadRow row)
    {
        if (row.Metrics.Count > 0)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(row.Error))
        {
            return 1;
        }

        if (NeedsApiKeySetup(row))
        {
            return 2;
        }

        return IsWindowsPendingError(row.Error) ? 3 : 4;
    }

    private static bool NeedsApiKeySetup(UsagePayloadRow row) =>
        ProviderCatalog.SupportsConfigApiKey(row.Provider) &&
        !string.IsNullOrWhiteSpace(row.Error) &&
        (ContainsIgnoreCase(row.Error, "No available fetch strategy") ||
            ContainsIgnoreCase(row.Error, "api key") ||
            ContainsIgnoreCase(row.Error, "api token") ||
            ContainsIgnoreCase(row.Error, "credential") ||
            ContainsIgnoreCase(row.Error, "selected source requires web support"));

    private static bool IsWindowsPendingError(string error) =>
        ContainsIgnoreCase(error, "only supported on macOS") ||
        ContainsIgnoreCase(error, "requires web support") ||
        ContainsIgnoreCase(error, "browser cookie import");

    private static bool ContainsIgnoreCase(string text, string value) =>
        text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    private static IReadOnlyList<UsagePayloadRow> TryParseRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return UsagePayloadParser.Parse(json);
        }
        catch
        {
            return [];
        }
    }

    private static WpfMedia.Brush ProviderAccent(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "claude" => Brush("#C76732"),
            "codex" or "openai" => Brush("#14925C"),
            "gemini" or "vertexai" => Brush("#2A70D6"),
            "copilot" => Brush("#4B5FCC"),
            "cursor" => Brush("#20242B"),
            "openrouter" => Brush("#6C55CD"),
            "zai" => Brush("#28919C"),
            "kilo" or "kiro" => Brush("#C67222"),
            "bedrock" => Brush("#E08524"),
            _ => Brush("#308094"),
        };

    private static WpfMedia.Brush Tint(WpfMedia.Brush brush, double amount)
    {
        if (brush is not WpfMedia.SolidColorBrush solid)
        {
            return Brush("#F3F4F6");
        }

        var color = solid.Color;
        byte mix(byte value) => (byte)Math.Round(value + ((255 - value) * amount));
        return new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(mix(color.R), mix(color.G), mix(color.B)));
    }

    private static WpfMedia.Brush Brush(string hex)
    {
        var converter = WpfMedia.ColorConverter.ConvertFromString(hex);
        if (converter is WpfMedia.Color color)
        {
            var brush = new WpfMedia.SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        return WpfMedia.Brushes.Transparent;
    }

    private enum PopoverView
    {
        Usage,
        Settings,
        Diagnostics,
    }

    private sealed record ProviderChoice(string Id, string DisplayName)
    {
        public override string ToString() => $"{DisplayName} ({Id})";
    }
}

internal sealed class AppSettingsChangedEventArgs : EventArgs
{
    public AppSettingsChangedEventArgs(AppSettings settings)
    {
        Settings = settings;
    }

    public AppSettings Settings { get; }
}
