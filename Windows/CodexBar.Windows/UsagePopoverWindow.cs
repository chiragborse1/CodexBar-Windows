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
    private const double PopoverWidth = 392;
    private const double PopoverHeight = 640;
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
        MinWidth = 360;
        MinHeight = 520;
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
            Background = Brush("#F7F7FA"),
            BorderBrush = Brush("#DADAE0"),
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
            Margin = new Wpf.Thickness(10, 8, 10, 8),
        };
        contentScroll = new WpfControls.ScrollViewer
        {
            Content = cardsStack,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled,
            Background = Brush("#F7F7FA"),
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
            Background = Brush("#FBFBFD"),
            Margin = new Wpf.Thickness(12, 9, 12, 8),
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
            FontSize = 14.5,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });
        statusText = new WpfControls.TextBlock
        {
            Text = "Ready",
            FontSize = 11,
            Foreground = Brush("#6E6E73"),
            Margin = new Wpf.Thickness(0, 1, 0, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        titleStack.Children.Add(statusText);

        var actionRow = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            VerticalAlignment = Wpf.VerticalAlignment.Top,
        };
        WpfControls.Grid.SetColumn(actionRow, 1);
        titleRow.Children.Add(actionRow);

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
            Background = Brush("#FBFBFD"),
            BorderBrush = Brush("#E1E1E6"),
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
            Background = Brush("#FBFBFD"),
            BorderBrush = Brush("#E1E1E6"),
            BorderThickness = new Wpf.Thickness(0, 1, 0, 0),
            Padding = new Wpf.Thickness(10, 0, 10, 0),
            Height = 30,
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
            cardsStack.Children.Add(InfoCard(
                "No usage loaded yet",
                "Refresh to load provider usage from the bundled CLI.",
                Brush("#6E6E73")));
            cardsStack.Children.Add(MenuDivider());
            cardsStack.Children.Add(BuildUsageActionSection());
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

    private Wpf.FrameworkElement BuildProviderSetupSection()
    {
        var setupProviderBox = new WpfControls.ComboBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            ItemsSource = ProviderCatalog.ApiKeyEntries
                .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
                .ToArray(),
            SelectedIndex = 0,
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
        var providerBox = new WpfControls.ComboBox
        {
            MinHeight = 30,
            FontSize = 11.5,
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#D8D8DD"),
            ItemsSource = ProviderCatalog.CookieEntries
                .Select(entry => new ProviderChoice(entry.Id, entry.DisplayName))
                .ToArray(),
            SelectedIndex = 0,
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
        var resultText = Caption("Manual Cookie headers are saved in the local config file.");
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
                SetResult(resultText, "Paste a Cookie header before saving.", isError: true);
                return;
            }

            SetButtonsEnabled(false, importButtons.Concat(new[] { saveButton, clearButton }).ToArray());
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
                SetButtonsEnabled(true, importButtons.Concat(new[] { saveButton, clearButton }).ToArray());
            }
        };
        clearButton.Click += (_, _) =>
        {
            cookieBox.Clear();
            resultText.Text = "Manual Cookie headers are saved in the local config file.";
            resultText.Foreground = Brush("#6E6E73");
        };

        var content = new WpfControls.StackPanel();
        content.Children.Add(FieldLabel("Web provider"));
        content.Children.Add(providerBox);
        content.Children.Add(FieldLabel("Import browser session"));
        content.Children.Add(ActionWrap(importButtons.Cast<Wpf.FrameworkElement>().ToArray()));
        content.Children.Add(FieldLabel("Cookie header"));
        content.Children.Add(cookieBox);
        content.Children.Add(enableBox);
        content.Children.Add(ActionRow(saveButton, clearButton));
        content.Children.Add(resultText);

        return SectionCard(
            "Manual Web Session",
            "Import from Windows browsers or paste a Cookie header when auto import cannot read a session.",
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

        foreach (var row in rows.OrderBy(RowSortRank).ThenBy(row => row.DisplayName))
        {
            cardsStack.Children.Add(CreateProviderCard(row));
        }

        cardsStack.Children.Add(MenuDivider());
        cardsStack.Children.Add(BuildUsageActionSection());
    }

    private Wpf.FrameworkElement BuildProviderSwitcherSection(IReadOnlyList<UsagePayloadRow> rows)
    {
        var wrap = new WpfControls.WrapPanel
        {
            Margin = new Wpf.Thickness(0, 0, 0, 0),
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
            .Select(id => new ProviderChoice(id, ProviderCatalog.DisplayNameFor(id)))
            .ToArray();
    }

    private WpfControls.Button ProviderSwitchButton(string provider, string title)
    {
        var selected = string.Equals(settings.Provider, provider, StringComparison.OrdinalIgnoreCase);
        var content = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
        };
        content.Children.Add(ProviderIcon(title, ProviderAccent(provider), 16, 8.5));
        content.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 11,
            Foreground = selected ? Brush("#1D1D1F") : Brush("#6E6E73"),
            Margin = new Wpf.Thickness(4, 0, 0, 0),
            VerticalAlignment = Wpf.VerticalAlignment.Center,
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });

        var button = new WpfControls.Button
        {
            Content = content,
            Background = selected ? Brush("#E9E9EC") : WpfMedia.Brushes.White,
            BorderBrush = selected ? Brush("#CFCFD5") : Brush("#E5E5EA"),
            BorderThickness = new Wpf.Thickness(1),
            Padding = new Wpf.Thickness(6, 4, 7, 5),
            Margin = new Wpf.Thickness(0, 0, 5, 5),
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6),
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

        stack.Children.Add(MenuActionRow("Refresh", "Reload usage from the bundled CLI.", (Func<Task>)(async () =>
        {
            ShowUsageView();
            await RefreshUsageAsync();
        })));
        if (DashboardProviderForActions() is { } dashboardProvider &&
            ProviderCatalog.DashboardUrlFor(dashboardProvider) is { } dashboardUrl)
        {
            stack.Children.Add(MenuActionRow(
                $"Open {ProviderCatalog.DisplayNameFor(dashboardProvider)} Dashboard",
                "Open the provider account or usage page in your browser.",
                () => OpenUrl(dashboardUrl)));
        }
        stack.Children.Add(MenuActionRow("Settings", "Provider scope, API keys, web sessions.", () => ShowSettingsView()));
        stack.Children.Add(MenuActionRow("More", "Diagnostics, updates, raw CLI output.", () => ShowDiagnosticsView()));
        stack.Children.Add(MenuActionRow("Open Config File", "Edit the local provider config.", () => ConfigLocator.OpenConfigFile()));
        stack.Children.Add(MenuActionRow("Open Config Folder", "Open the CodexBar-Windows config folder.", () => ConfigLocator.OpenConfigFolder()));
        return stack;
    }

    private string? DashboardProviderForActions()
    {
        if (!IsBroadProviderScope(settings.Provider) &&
            ProviderCatalog.DashboardUrlFor(settings.Provider) is not null)
        {
            return settings.Provider;
        }

        return lastRows
            .OrderBy(RowSortRank)
            .ThenBy(row => row.DisplayName)
            .Select(row => row.Provider)
            .FirstOrDefault(provider => ProviderCatalog.DashboardUrlFor(provider) is not null);
    }

    private Wpf.FrameworkElement MenuActionRow(string title, string subtitle, Action action) =>
        MenuActionRow(title, subtitle, () =>
        {
            action();
            return Task.CompletedTask;
        });

    private Wpf.FrameworkElement MenuActionRow(string title, string subtitle, Func<Task> action)
    {
        var grid = new WpfControls.Grid
        {
            Margin = new Wpf.Thickness(0),
        };
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

        var text = new WpfControls.StackPanel();
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = title,
            FontSize = 12.5,
            Foreground = Brush("#1D1D1F"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new WpfControls.TextBlock
        {
            Text = subtitle,
            FontSize = 10.8,
            Foreground = Brush("#6E6E73"),
            Margin = new Wpf.Thickness(0, 1, 12, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        });
        WpfControls.Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var chevron = new WpfControls.TextBlock
        {
            Text = ">",
            FontSize = 13,
            Foreground = Brush("#8E8E93"),
            VerticalAlignment = Wpf.VerticalAlignment.Center,
        };
        WpfControls.Grid.SetColumn(chevron, 1);
        grid.Children.Add(chevron);

        var button = new WpfControls.Button
        {
            Content = grid,
            Background = WpfMedia.Brushes.White,
            BorderBrush = WpfMedia.Brushes.Transparent,
            BorderThickness = new Wpf.Thickness(0),
            Padding = new Wpf.Thickness(6, 6, 6, 7),
            HorizontalContentAlignment = Wpf.HorizontalAlignment.Stretch,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6),
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
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#E6E6EA"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(10, 9, 10, 10),
            Margin = new Wpf.Thickness(0, 0, 0, 8),
        };

        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0),
        };
        card.Child = stack;

        var header = new WpfControls.Grid();
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        stack.Children.Add(header);

        var icon = ProviderIcon(row.DisplayName, accent, 30, 12);
        icon.Margin = new Wpf.Thickness(0, 1, 9, 0);
        WpfControls.Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titleStack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 0, 8, 0),
        };
        WpfControls.Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        titleStack.Children.Add(new WpfControls.TextBlock
        {
            Text = row.DisplayName,
            FontSize = 13.5,
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

        var percent = row.Metrics.FirstOrDefault()?.RemainingPercent;
        Wpf.FrameworkElement trailing = percent.HasValue
            ? new WpfControls.TextBlock
            {
                Text = $"{percent.Value:0}% left",
                FontSize = 11.5,
                Foreground = accent,
                VerticalAlignment = Wpf.VerticalAlignment.Top,
                TextAlignment = Wpf.TextAlignment.Right,
                TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            }
            : StatusPill(StatusLabel(row), StatusForeground(row), StatusBackground(row));
        WpfControls.Grid.SetColumn(trailing, 2);
        header.Children.Add(trailing);

        if (row.Metrics.Count > 0)
        {
            stack.Children.Add(MenuDivider(new Wpf.Thickness(0, 7, 0, 0)));
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
        }

        return card;
    }

    private Wpf.FrameworkElement CreateSetupHintCard()
    {
        var card = new WpfControls.Border
        {
            Background = Brush("#F5F7FA"),
            BorderBrush = Brush("#E5E5EA"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(10, 9, 10, 10),
            Margin = new Wpf.Thickness(0, 0, 0, 8),
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

        return card;
    }

    private Wpf.FrameworkElement CreateMetric(UsagePayloadMetric metric, WpfMedia.Brush accent)
    {
        var stack = new WpfControls.StackPanel
        {
            Margin = new Wpf.Thickness(0, 9, 0, 0),
        };

        var header = new WpfControls.Grid();
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
        stack.Children.Add(header);

        var title = new WpfControls.TextBlock
        {
            Text = metric.Title,
            FontSize = 12,
            Foreground = Brush("#3C3C43"),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        WpfControls.Grid.SetColumn(title, 0);
        header.Children.Add(title);

        var remaining = new WpfControls.TextBlock
        {
            Text = $"{metric.RemainingPercent:0}% left",
            FontSize = 11.5,
            Foreground = Brush("#6E6E73"),
        };
        WpfControls.Grid.SetColumn(remaining, 1);
        header.Children.Add(remaining);

        stack.Children.Add(ProgressBar(metric.UsedPercent, accent));

        if (!string.IsNullOrWhiteSpace(metric.Reset))
        {
            stack.Children.Add(new WpfControls.TextBlock
            {
                Text = metric.Reset,
                FontSize = 11,
                Foreground = Brush("#6E6E73"),
                TextWrapping = Wpf.TextWrapping.Wrap,
                Margin = new Wpf.Thickness(0, 3, 0, 0),
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
            Background = Brush("#F5F5F7"),
            BorderBrush = Brush("#E5E5EA"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(10),
            Child = MessageLine(
                text,
                result.Succeeded ? Brush("#6E6E73") : Brush("#B42318"),
                new Wpf.Thickness(0)),
        };
    }

    private static WpfControls.StackPanel ViewStack() =>
        new()
        {
            Margin = new Wpf.Thickness(10, 8, 10, 8),
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
        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#E6E6EA"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(10, 9, 10, 11),
            Margin = new Wpf.Thickness(0, 0, 0, 8),
            Child = stack,
        };
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
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#E6E6EA"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(10, 9, 10, 10),
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
            FontSize = 11.5,
            Foreground = Brush("#1D1D1F"),
            Background = Brush("#F7F7FA"),
            BorderBrush = Brush("#DCDCE3"),
            BorderThickness = new Wpf.Thickness(1),
            Padding = new Wpf.Thickness(9, 4, 9, 5),
            Margin = new Wpf.Thickness(5, 0, 0, 0),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
            Template = RoundedButtonTemplate(6),
        };
        return button;
    }

    private static WpfControls.Button HeaderButton(string text)
    {
        var button = PopoverButton(text);
        button.FontSize = 11;
        button.Padding = new Wpf.Thickness(8, 3, 8, 4);
        button.Background = Brush("#FBFBFD");
        button.BorderBrush = Brush("#DCDCE3");
        button.Template = RoundedButtonTemplate(5);
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
        button.Background = selected ? Brush("#E9E9EE") : Brush("#FBFBFD");
        button.Foreground = selected ? Brush("#1D1D1F") : Brush("#3C3C43");
        button.BorderBrush = selected ? Brush("#CFCFD5") : Brush("#DCDCE3");
    }

    private static WpfControls.ControlTemplate RoundedButtonTemplate(double radius)
    {
        var template = new WpfControls.ControlTemplate(typeof(WpfControls.Button));
        var border = new Wpf.FrameworkElementFactory(typeof(WpfControls.Border));
        border.SetValue(Wpf.FrameworkElement.NameProperty, "Chrome");
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
            WpfControls.Border.BackgroundProperty,
            Brush("#F3F3F6"),
            "Chrome"));
        template.Triggers.Add(hover);

        var pressed = new Wpf.Trigger { Property = WpfControls.Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Wpf.Setter(
            WpfControls.Border.BackgroundProperty,
            Brush("#E9E9EE"),
            "Chrome"));
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
