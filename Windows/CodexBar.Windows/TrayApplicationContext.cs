namespace CodexBar.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private AppSettings settings;
    private CliRunner cliRunner;
    private UsagePopoverForm? popover;
    private DashboardForm? diagnostics;

    public TrayApplicationContext()
    {
        settings = AppSettings.Load();
        cliRunner = new CliRunner(settings);

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
        notifyIcon.DoubleClick += (_, _) => ShowDiagnostics();

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Tick += async (_, _) => await RefreshUsageAsync();
        ApplyTimerInterval();

        if (!Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase) &&
            !settings.StartMinimized)
        {
            ShowPopover();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            popover?.Dispose();
            diagnostics?.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open CodexBar", null, (_, _) => ShowPopover());
        menu.Items.Add("Refresh Now", null, async (_, _) => await RefreshUsageAsync(forceShow: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Diagnostics", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Open Config File", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFile));
        menu.Items.Add("Open Config Folder", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowPopover()
    {
        if (popover is null || popover.IsDisposed)
        {
            popover = new UsagePopoverForm(settings, cliRunner);
            popover.SettingsRequested += (_, _) => ShowSettings();
            popover.DiagnosticsRequested += (_, _) => ShowDiagnostics();
            popover.FormClosed += (_, _) => popover = null;
        }

        popover.ShowNearCursor();
    }

    private void ShowDiagnostics()
    {
        if (diagnostics is null || diagnostics.IsDisposed)
        {
            diagnostics = new DashboardForm(settings, cliRunner);
            diagnostics.SettingsRequested += (_, _) => ShowSettings();
            diagnostics.FormClosed += (_, _) => diagnostics = null;
        }

        diagnostics.Show();
        diagnostics.WindowState = FormWindowState.Normal;
        diagnostics.Activate();
    }

    private async Task RefreshUsageAsync(bool forceShow = false)
    {
        if (forceShow)
        {
            ShowPopover();
        }

        if (popover is not null && !popover.IsDisposed && popover.Visible)
        {
            await popover.RefreshUsageAsync();
        }

        if (diagnostics is not null && !diagnostics.IsDisposed)
        {
            await diagnostics.RefreshUsageAsync();
        }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(settings, cliRunner);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = form.Settings;
            settings.Save();
            cliRunner = new CliRunner(settings);
            ApplyTimerInterval();

            if (popover is not null && !popover.IsDisposed)
            {
                popover.ApplySettings(settings, cliRunner);
            }

            if (diagnostics is not null && !diagnostics.IsDisposed)
            {
                diagnostics.ApplySettings(settings, cliRunner);
            }

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
