namespace CodexBar.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly bool forceWindow;
    private readonly bool demoMode;
    private AppSettings settings;
    private CliRunner cliRunner;
    private UsagePopoverWindow? popover;

    public TrayApplicationContext(bool forceWindow = false, bool demoMode = false)
    {
        this.forceWindow = forceWindow;
        this.demoMode = demoMode;
        settings = AppSettings.Load();
        cliRunner = new CliRunner(settings, demoMode);

        notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Text = AppInfo.DisplayName,
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                ShowPopover();
            }
        };
        notifyIcon.DoubleClick += (_, _) => ShowDiagnosticsInPopover();

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Tick += async (_, _) => await RefreshUsageAsync();
        ApplyTimerInterval();

        if (forceWindow ||
            (!Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase) &&
             !settings.StartMinimized))
        {
            ShowInitialPopoverAfterMessageLoopStarts();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            popover?.Close();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open CodexBar", null, (_, _) => ShowPopover());
        menu.Items.Add("Refresh", null, async (_, _) => await RefreshUsageAsync(forceShow: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettingsInPopover());
        menu.Items.Add("More", null, (_, _) => ShowDiagnosticsInPopover());
        menu.Items.Add("Open Config File", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFile));
        menu.Items.Add("Open Config Folder", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About CodexBar", null, (_, _) => ShowAbout());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowInitialPopoverAfterMessageLoopStarts()
    {
        var timer = new System.Windows.Forms.Timer
        {
            Interval = 150,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            ShowPopover(forceWindow);
        };
        timer.Start();
    }

    private void ShowPopover(bool asWindow = false)
    {
        if (popover is null || popover.IsClosed || (asWindow && !popover.ShowInTaskbar))
        {
            if (popover is not null && !popover.IsClosed)
            {
                popover.Close();
            }

            popover = new UsagePopoverWindow(
                settings,
                cliRunner,
                demoMode: demoMode,
                showInTaskbar: asWindow,
                hideOnDeactivate: !asWindow);
            popover.SettingsChanged += (_, args) => ApplySettings(args.Settings, showBalloon: false);
            popover.Closed += (_, _) => popover = null;
        }

        if (asWindow)
        {
            popover.ShowCentered();
        }
        else
        {
            popover.ShowNearCursor();
        }
    }

    private void ShowSettingsInPopover()
    {
        ShowPopover();
        popover?.NavigateToSettings();
    }

    private void ShowDiagnosticsInPopover()
    {
        ShowPopover();
        popover?.NavigateToDiagnostics();
    }

    private async Task RefreshUsageAsync(bool forceShow = false)
    {
        if (forceShow)
        {
            ShowPopover();
        }

        if (popover is not null && !popover.IsClosed && popover.IsVisible)
        {
            await popover.RefreshUsageAsync();
        }
    }

    private void ApplySettings(AppSettings newSettings, bool showBalloon)
    {
        settings = newSettings;
        settings.Save();
        cliRunner = new CliRunner(settings, demoMode);
        ApplyTimerInterval();

        if (popover is not null && !popover.IsClosed)
        {
            popover.ApplySettings(settings, cliRunner);
        }

        if (showBalloon)
        {
            notifyIcon.ShowBalloonTip(
                2000,
                AppInfo.DisplayName,
                "Settings saved.",
                ToolTipIcon.Info);
        }
    }

    private void ApplyTimerInterval()
    {
        refreshTimer.Interval = Math.Max(1, settings.RefreshIntervalMinutes) * 60 * 1000;
        refreshTimer.Start();
    }

    private static void SafeOpen(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            $"{AppInfo.DisplayName} {AppInfo.Version}{Environment.NewLine}{Environment.NewLine}" +
            "Windows tray shell for the CodexBar-Windows CLI backend.",
            AppInfo.DisplayName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
