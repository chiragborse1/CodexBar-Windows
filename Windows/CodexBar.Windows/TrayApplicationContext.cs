namespace CodexBar.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private AppSettings settings;
    private CliRunner cliRunner;
    private DashboardForm? dashboard;

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
        notifyIcon.DoubleClick += (_, _) => ShowDashboard();

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        ApplyTimerInterval();

        if (!Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase) &&
            !settings.StartMinimized)
        {
            ShowDashboard();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            dashboard?.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Dashboard", null, (_, _) => ShowDashboard());
        menu.Items.Add("Refresh Now", null, async (_, _) => await RefreshDashboardAsync(forceShow: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Open Config File", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFile));
        menu.Items.Add("Open Config Folder", null, (_, _) => SafeOpen(ConfigLocator.OpenConfigFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowDashboard()
    {
        if (dashboard is null || dashboard.IsDisposed)
        {
            dashboard = new DashboardForm(settings, cliRunner);
            dashboard.SettingsRequested += (_, _) => ShowSettings();
            dashboard.FormClosed += (_, _) => dashboard = null;
        }

        dashboard.Show();
        dashboard.WindowState = FormWindowState.Normal;
        dashboard.Activate();
    }

    private async Task RefreshDashboardAsync(bool forceShow = false)
    {
        if (forceShow)
        {
            ShowDashboard();
        }

        if (dashboard is not null && !dashboard.IsDisposed)
        {
            await dashboard.RefreshUsageAsync();
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

            if (dashboard is not null && !dashboard.IsDisposed)
            {
                dashboard.ApplySettings(settings, cliRunner);
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
