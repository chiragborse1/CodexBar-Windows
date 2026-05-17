using System.Diagnostics;
using Forms = System.Windows.Forms;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfData = System.Windows.Data;
using WpfEffects = System.Windows.Media.Effects;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;

namespace CodexBar.Windows;

internal sealed class UsagePopoverWindow : Wpf.Window
{
    private const double PopoverWidth = 470;
    private const double PopoverHeight = 680;
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

    public UsagePopoverWindow(AppSettings settings, CliRunner cliRunner)
    {
        this.settings = settings;
        this.cliRunner = cliRunner;

        Title = AppInfo.DisplayName;
        Width = PopoverWidth;
        Height = PopoverHeight;
        MinWidth = 420;
        MinHeight = 560;
        ResizeMode = Wpf.ResizeMode.NoResize;
        WindowStyle = Wpf.WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfMedia.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        FontFamily = new WpfMedia.FontFamily("Segoe UI");
        Deactivated += (_, _) => Hide();

        var root = new WpfControls.Border
        {
            Background = Brush("#F3F6FB"),
            BorderBrush = Brush("#C8D2DF"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(18),
            SnapsToDevicePixels = true,
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 34,
                ShadowDepth = 12,
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
            Margin = new Wpf.Thickness(16, 14, 16, 14),
        };
        contentScroll = new WpfControls.ScrollViewer
        {
            Content = cardsStack,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled,
            Background = Brush("#F3F6FB"),
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

    public async Task RefreshUsageAsync()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        var token = refreshCancellation.Token;

        refreshButton.IsEnabled = false;
        statusText.Text = $"Refreshing {settings.Provider}...";
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
                    ? $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s"
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
        var header = new WpfControls.StackPanel
        {
            Background = Brush("#FBFCFE"),
            Margin = new Wpf.Thickness(18, 16, 16, 14),
        };

        var titleRow = new WpfControls.Grid();
        titleRow.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        header.Children.Add(titleRow);

        var titleStack = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Vertical,
        };
        WpfControls.Grid.SetColumn(titleStack, 0);
        titleRow.Children.Add(titleStack);

        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = AppInfo.DisplayName,
            FontSize = 19,
            FontWeight = Wpf.FontWeights.Bold,
            Foreground = Brush("#0F172A"),
        });
        statusText = new WpfControls.TextBlock
        {
            Text = "Ready",
            FontSize = 12,
            Foreground = Brush("#64748B"),
            Margin = new Wpf.Thickness(0, 3, 0, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        titleStack.Children.Add(statusText);

        refreshButton = PopoverButton("Refresh");
        refreshButton.Click += async (_, _) =>
        {
            ShowUsageView();
            await RefreshUsageAsync();
        };
        WpfControls.Grid.SetColumn(refreshButton, 1);
        titleRow.Children.Add(refreshButton);

        var nav = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 14, 0, 0),
            Background = Brush("#E8EEF6"),
        };
        nav.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.Children.Add(new WpfControls.Border
        {
            Background = Brush("#E8EEF6"),
            CornerRadius = new Wpf.CornerRadius(12),
            Padding = new Wpf.Thickness(3),
            Child = nav,
        });

        usageButton = NavButton("Usage");
        usageButton.Click += (_, _) => ShowUsageView();
        WpfControls.Grid.SetColumn(usageButton, 0);
        nav.Children.Add(usageButton);

        settingsButton = NavButton("Settings");
        settingsButton.Click += (_, _) => ShowSettingsView();
        WpfControls.Grid.SetColumn(settingsButton, 1);
        nav.Children.Add(settingsButton);

        diagnosticsButton = NavButton("More");
        diagnosticsButton.Click += (_, _) => ShowDiagnosticsView();
        WpfControls.Grid.SetColumn(diagnosticsButton, 2);
        nav.Children.Add(diagnosticsButton);

        UpdateNavigationChrome();

        return new WpfControls.Border
        {
            Background = Brush("#FBFCFE"),
            CornerRadius = new Wpf.CornerRadius(18, 18, 0, 0),
            Child = header,
        };
    }

    private WpfControls.Border BuildFooter()
    {
        footerText = new WpfControls.TextBlock
        {
            FontSize = 12,
            Foreground = Brush("#6B7280"),
            VerticalAlignment = Wpf.VerticalAlignment.Center,
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };

        return new WpfControls.Border
        {
            Background = Brush("#FBFCFE"),
            BorderBrush = Brush("#DDE5EF"),
            BorderThickness = new Wpf.Thickness(0, 1, 0, 0),
            Padding = new Wpf.Thickness(16, 0, 16, 0),
            Height = 36,
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
            cardsStack.Children.Add(InfoCard(
                "No usage loaded yet",
                "Refresh to load provider usage from the bundled CLI.",
                Brush("#475569")));
            footerText.Text = "Waiting for first refresh.";
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

        var scopeBox = new WpfControls.ComboBox
        {
            MinHeight = 34,
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

    private Wpf.FrameworkElement BuildProviderSetupSection()
    {
        var setupProviderBox = new WpfControls.ComboBox
        {
            MinHeight = 34,
            ItemsSource = ProviderCatalog.ApiKeyEntries
                .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
                .ToArray(),
            SelectedIndex = 0,
        };
        var apiKeyBox = new WpfControls.PasswordBox
        {
            MinHeight = 34,
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
                resultText.Foreground = Brush("#64748B");
                return;
            }

            var environmentKey = ProviderSecretEnvironment.PrimaryEnvironmentKeyFor(provider.Id);
            var stored = WindowsCredentialStore.HasApiKey(provider.Id);
            resultText.Text = stored
                ? $"Stored in Windows Credential Manager. CLI runs receive {environmentKey}."
                : $"Not stored yet. Saving here uses Windows Credential Manager, not plain JSON.";
            resultText.Foreground = Brush("#64748B");
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
        var providerBox = new WpfControls.ComboBox
        {
            MinHeight = 34,
            ItemsSource = ProviderCatalog.CookieEntries
                .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
                .ToArray(),
            SelectedIndex = 0,
        };
        var cookieBox = new WpfControls.TextBox
        {
            MinHeight = 78,
            MaxHeight = 120,
            AcceptsReturn = true,
            TextWrapping = Wpf.TextWrapping.Wrap,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            FontFamily = new WpfMedia.FontFamily("Consolas"),
            FontSize = 11.5,
            Padding = new Wpf.Thickness(8),
        };
        var enableBox = new WpfControls.CheckBox
        {
            Content = "Enable provider after saving",
            IsChecked = true,
            Margin = new Wpf.Thickness(0, 8, 0, 0),
        };
        var resultText = Caption("Manual Cookie headers are saved in the local config file.");
        var saveButton = PrimaryButton("Save Cookie");
        var clearButton = SecondaryButton("Clear Text");

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
                SetResult(resultText, "Paste a Cookie header before saving.", isError: true);
                return;
            }

            SetButtonsEnabled(false, saveButton, clearButton);
            SetResult(resultText, "Saving manual Cookie header...", isError: false);
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
                    SetResult(resultText, $"Saved manual Cookie header for {provider.DisplayName}.", isError: false);
                }
                else
                {
                    SetResult(resultText, ShortResultMessage(result), isError: true);
                }
            }
            finally
            {
                SetButtonsEnabled(true, saveButton, clearButton);
            }
        };
        clearButton.Click += (_, _) =>
        {
            cookieBox.Clear();
            resultText.Text = "Manual Cookie headers are saved in the local config file.";
            resultText.Foreground = Brush("#64748B");
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(FieldLabel("Web provider"));
        content.Children.Add(providerBox);
        content.Children.Add(FieldLabel("Cookie header"));
        content.Children.Add(cookieBox);
        content.Children.Add(enableBox);
        content.Children.Add(ActionRow(saveButton, clearButton));
        content.Children.Add(resultText);

        return SectionCard(
            "Manual Web Session",
            "Use this for providers whose Windows browser-cookie auto import is still pending.",
            content);
    }

    private Wpf.FrameworkElement BuildDiagnosticsView()
    {
        var root = ViewStack();
        var actionsStatus = Caption("");

        var actions = new WpfControls.StackPanel();
        actions.Children.Add(ActionRow(
            PrimaryButton("Refresh"),
            SecondaryButton("Copy Raw"),
            SecondaryButton("Validate")));

        if (actions.Children[0] is WpfControls.StackPanel row)
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

        actions.Children.Add(ActionRow(
            SecondaryButton("Open Config"),
            SecondaryButton("Open Folder")));

        if (actions.Children[1] is WpfControls.StackPanel fileRow)
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
            MaxHeight = 180,
            FontFamily = new WpfMedia.FontFamily("Consolas"),
            FontSize = 11,
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#D8E0EA"),
            Foreground = Brush("#334155"),
            Padding = new Wpf.Thickness(10),
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

    private void RenderRows(IReadOnlyList<UsagePayloadRow> rows, CliResult result)
    {
        cardsStack.Children.Clear();
        if (rows.Count == 0)
        {
            cardsStack.Children.Add(CreateEmptyState(result));
            return;
        }

        if (ShouldShowSetupHint(rows))
        {
            cardsStack.Children.Add(CreateSetupHintCard());
        }

        foreach (var row in rows.OrderBy(RowSortRank).ThenBy(row => row.DisplayName))
        {
            cardsStack.Children.Add(CreateProviderCard(row));
        }
    }

    private Wpf.FrameworkElement CreateProviderCard(UsagePayloadRow row)
    {
        var accent = ProviderAccent(row.Provider);
        var card = new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#DCE4EF"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(14),
            Padding = new Wpf.Thickness(14),
            Margin = new Wpf.Thickness(0, 0, 0, 12),
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.07,
                Color = WpfMedia.Color.FromRgb(15, 23, 42),
            },
        };

        var stack = new WpfControls.StackPanel();
        card.Child = stack;

        var header = new WpfControls.Grid();
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        stack.Children.Add(header);

        var icon = ProviderIcon(row, accent);
        WpfControls.Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(10, 0, 10, 0),
        };
        WpfControls.Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = row.DisplayName,
            FontSize = 14.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#0F172A"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        var subtitle = SubtitleText(row);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            titleStack.Children.Add(new WpfControls.TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = Brush("#6B7280"),
                Margin = new Wpf.Thickness(0, 1, 0, 0),
                TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            });
        }

        var percent = row.Metrics.FirstOrDefault()?.RemainingPercent;
        var pill = StatusPill(
            percent.HasValue ? $"{percent.Value:0}% left" : StatusLabel(row),
            percent.HasValue ? accent : StatusForeground(row),
            percent.HasValue ? Tint(accent, 0.92) : StatusBackground(row));
        WpfControls.Grid.SetColumn(pill, 2);
        header.Children.Add(pill);

        if (row.Metrics.Count > 0)
        {
            foreach (var metric in row.Metrics.Take(4))
            {
                stack.Children.Add(CreateMetric(metric, accent));
            }
        }
        else
        {
            stack.Children.Add(MessageLine(
                string.IsNullOrWhiteSpace(row.Error) ? "Limits not available for this provider yet." : FriendlyError(row),
                string.IsNullOrWhiteSpace(row.Error) ? Brush("#6B7280") : ErrorBrush(row),
                new Wpf.Thickness(0, 12, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Credits) || !string.IsNullOrWhiteSpace(row.Cost))
        {
            stack.Children.Add(MessageLine(
                SmallLine("Credits", row.Credits, "Cost", row.Cost),
                Brush("#374151"),
                new Wpf.Thickness(0, 10, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Status))
        {
            stack.Children.Add(MessageLine(row.Status, Brush("#6B7280"), new Wpf.Thickness(0, 8, 0, 0)));
        }

        if (!string.IsNullOrWhiteSpace(row.Error) && row.Metrics.Count > 0)
        {
            stack.Children.Add(MessageLine(FriendlyError(row), ErrorBrush(row), new Wpf.Thickness(0, 8, 0, 0)));
        }

        return card;
    }

    private Wpf.FrameworkElement CreateSetupHintCard()
    {
        var card = new WpfControls.Border
        {
            Background = Brush("#EEF6FF"),
            BorderBrush = Brush("#B7D8FF"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(14),
            Padding = new Wpf.Thickness(14),
            Margin = new Wpf.Thickness(0, 0, 0, 12),
        };

        var grid = new WpfControls.Grid();
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        card.Child = grid;

        var textStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 0, 12, 0),
        };
        WpfControls.Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);
        textStack.Children.Add(new WpfControls.TextBlock
        {
            Text = "Set up a Windows provider",
            FontSize = 13.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#153E75"),
        });
        textStack.Children.Add(new WpfControls.TextBlock
        {
            Text = "Save an API key in Settings to replace these setup and compatibility messages with live usage.",
            FontSize = 11.5,
            Foreground = Brush("#34547A"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 3, 0, 0),
        });

        var button = PopoverButton("Open Settings");
        button.Margin = new Wpf.Thickness(0);
        button.VerticalAlignment = Wpf.VerticalAlignment.Center;
        button.Click += (_, _) => ShowSettingsView();
        WpfControls.Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return card;
    }

    private Wpf.FrameworkElement CreateMetric(UsagePayloadMetric metric, WpfMedia.Brush accent)
    {
        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 12, 0, 0),
        };

        var header = new WpfControls.Grid();
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        stack.Children.Add(header);

        var title = new WpfControls.TextBlock
        {
            Text = metric.Title,
            FontSize = 12.5,
            Foreground = Brush("#374151"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        WpfControls.Grid.SetColumn(title, 0);
        header.Children.Add(title);

        var remaining = new WpfControls.TextBlock
        {
            Text = $"{metric.RemainingPercent:0}% left",
            FontSize = 12,
            Foreground = Brush("#6B7280"),
        };
        WpfControls.Grid.SetColumn(remaining, 1);
        header.Children.Add(remaining);

        stack.Children.Add(ProgressBar(metric.UsedPercent, accent));

        if (!string.IsNullOrWhiteSpace(metric.Reset))
        {
            stack.Children.Add(new WpfControls.TextBlock
            {
                Text = metric.Reset,
                FontSize = 11.5,
                Foreground = Brush("#6B7280"),
                TextWrapping = Wpf.TextWrapping.Wrap,
                Margin = new Wpf.Thickness(0, 4, 0, 0),
            });
        }

        return stack;
    }

    private Wpf.FrameworkElement CreateEmptyState(CliResult result)
    {
        var text = result.Succeeded
            ? "No provider usage returned."
            : string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"CLI exited with code {result.ExitCode}."
                : result.CombinedOutput.Trim();

        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#E1E5EC"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(14),
            Child = MessageLine(
                text,
                result.Succeeded ? Brush("#6B7280") : Brush("#B42318"),
                new Wpf.Thickness(0)),
        };
    }

    private static WpfControls.StackPanel ViewStack() =>
        new()
        {
            Margin = new Wpf.Thickness(16, 14, 16, 14),
        };

    private static Wpf.FrameworkElement SectionCard(string title, string? subtitle, Wpf.FrameworkElement content)
    {
        var stack = new WpfControls.StackPanel();
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Wpf.FontWeights.Bold,
            Foreground = Brush("#0F172A"),
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new WpfControls.TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = Brush("#64748B"),
                TextWrapping = Wpf.TextWrapping.Wrap,
                Margin = new Wpf.Thickness(0, 3, 0, 12),
            });
        }
        else
        {
            stack.Children.Add(new WpfControls.Border { Height = 10 });
        }

        stack.Children.Add(content);
        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#DCE4EF"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(14),
            Padding = new Wpf.Thickness(14),
            Margin = new Wpf.Thickness(0, 0, 0, 12),
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.07,
                Color = WpfMedia.Color.FromRgb(15, 23, 42),
            },
            Child = stack,
        };
    }

    private static Wpf.FrameworkElement InfoCard(string title, string body, WpfMedia.Brush foreground)
    {
        var stack = new WpfControls.StackPanel();
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Wpf.FontWeights.Bold,
            Foreground = Brush("#0F172A"),
        });
        stack.Children.Add(new WpfControls.TextBlock
        {
            Text = body,
            FontSize = 12,
            Foreground = foreground,
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 5, 0, 0),
        });

        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#DCE4EF"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(14),
            Padding = new Wpf.Thickness(14),
            Child = stack,
        };
    }

    private static Wpf.FrameworkElement ProviderCompatibilityRow(ProviderCatalogEntry provider)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0, 0, 0, 9),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var text = new WpfControls.StackPanel();
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = provider.DisplayName,
            FontSize = 12.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1E293B"),
        });
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = $"{provider.Id} - {provider.Notes}",
            FontSize = 11,
            Foreground = Brush("#64748B"),
            TextWrapping = Wpf.TextWrapping.Wrap,
            Margin = new Wpf.Thickness(0, 2, 8, 0),
        });
        WpfControls.Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var color = provider.Support switch
        {
            "Ready" => Brush("#15803D"),
            "Partial" => Brush("#8A5A00"),
            _ => Brush("#475569"),
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

    private static WpfControls.TextBlock FieldLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 12,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#334155"),
            Margin = new Wpf.Thickness(0, 10, 0, 5),
        };

    private static WpfControls.TextBlock Caption(string text) =>
        new()
        {
            Text = text,
            FontSize = 11.5,
            Foreground = Brush("#64748B"),
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

    private static WpfControls.StackPanel ActionRow(params Wpf.FrameworkElement[] controls)
    {
        var row = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            Margin = new Wpf.Thickness(0, 12, 0, 0),
        };
        foreach (var control in controls)
        {
            control.Margin = new Wpf.Thickness(0, 0, 8, 0);
            row.Children.Add(control);
        }

        return row;
    }

    private static WpfControls.Button PopoverButton(string text)
    {
        var button = new WpfControls.Button
        {
            Content = text,
            FontSize = 12,
            Foreground = Brush("#1F2937"),
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#C9D1DC"),
            BorderThickness = new Wpf.Thickness(1),
            Padding = new Wpf.Thickness(11, 6, 11, 7),
            Margin = new Wpf.Thickness(6, 0, 0, 0),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(9),
        };
        return button;
    }

    private static WpfControls.Button PrimaryButton(string text) =>
        StyledButton(text, Brush("#2563EB"), WpfMedia.Brushes.White, Brush("#2563EB"));

    private static WpfControls.Button SecondaryButton(string text) =>
        StyledButton(text, WpfMedia.Brushes.White, Brush("#1E293B"), Brush("#CBD5E1"));

    private static WpfControls.Button NavButton(string text)
    {
        var button = StyledButton(text, Brush("#E8EEF6"), Brush("#475569"), Brush("#E8EEF6"));
        button.Margin = new Wpf.Thickness(0);
        button.Padding = new Wpf.Thickness(8, 7, 8, 8);
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
            FontSize = 12,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = foreground,
            Background = background,
            BorderBrush = border,
            BorderThickness = new Wpf.Thickness(1),
            Padding = new Wpf.Thickness(11, 6, 11, 7),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(9),
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
        button.Background = selected ? WpfMedia.Brushes.White : Brush("#E8EEF6");
        button.Foreground = selected ? Brush("#0F172A") : Brush("#64748B");
        button.BorderBrush = selected ? Brush("#D6DFEA") : Brush("#E8EEF6");
    }

    private static WpfControls.ControlTemplate RoundedButtonTemplate(double radius)
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

    private static WpfControls.Border ProviderIcon(UsagePayloadRow row, WpfMedia.Brush accent)
    {
        var letter = string.IsNullOrWhiteSpace(row.DisplayName)
            ? "?"
            : row.DisplayName.Trim()[0].ToString().ToUpperInvariant();

        return new WpfControls.Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new Wpf.CornerRadius(17),
            Background = accent,
            Child = new WpfControls.TextBlock
            {
                Text = letter,
                FontSize = 13.5,
                FontWeight = Wpf.FontWeights.SemiBold,
                Foreground = WpfMedia.Brushes.White,
                HorizontalAlignment = Wpf.HorizontalAlignment.Center,
                VerticalAlignment = Wpf.VerticalAlignment.Center,
            },
        };
    }

    private static WpfControls.Border StatusPill(string text, WpfMedia.Brush foreground, WpfMedia.Brush background) =>
        new()
        {
            Background = background,
            CornerRadius = new Wpf.CornerRadius(999),
            Padding = new Wpf.Thickness(8, 3, 8, 4),
            VerticalAlignment = Wpf.VerticalAlignment.Top,
            Child = new WpfControls.TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = Wpf.FontWeights.SemiBold,
                Foreground = foreground,
            },
        };

    private static Wpf.FrameworkElement ProgressBar(double usedPercent, WpfMedia.Brush accent)
    {
        var clamped = Math.Clamp(usedPercent, 0, 100);
        var grid = new WpfControls.Grid
        {
            Height = 9,
            Margin = new Wpf.Thickness(0, 6, 0, 0),
            ClipToBounds = true,
        };
        var track = new WpfControls.Border
        {
            Background = Brush("#E5E7EB"),
            CornerRadius = new Wpf.CornerRadius(4),
        };
        var fill = new WpfControls.Border
        {
            Background = accent,
            CornerRadius = new Wpf.CornerRadius(4),
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
            FontSize = 11.5,
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
        NeedsApiKeySetup(row) || IsWindowsPendingError(row.Error) ? Brush("#6B7280") : Brush("#B42318");

    private static string FriendlyError(UsagePayloadRow row)
    {
        if (NeedsApiKeySetup(row))
        {
            return "Add this provider's API key in Settings to enable Windows usage checks.";
        }

        if (IsWindowsPendingError(row.Error))
        {
            return "This source is not available on Windows yet.";
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
