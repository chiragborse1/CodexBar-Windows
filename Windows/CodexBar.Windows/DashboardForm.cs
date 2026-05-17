using System.Diagnostics;

namespace CodexBar.Windows;

internal sealed class DashboardForm : Form
{
    private readonly Label statusLabel;
    private readonly RichTextBox outputBox;
    private readonly Button refreshButton;
    private readonly Button settingsButton;
    private readonly Button copyButton;
    private readonly Button configButton;
    private CancellationTokenSource? refreshCancellation;
    private AppSettings settings;
    private CliRunner cliRunner;

    public DashboardForm(AppSettings settings, CliRunner cliRunner)
    {
        this.settings = settings;
        this.cliRunner = cliRunner;

        Text = AppInfo.DisplayName;
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Label
        {
            Text = AppInfo.DisplayName,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        };
        root.Controls.Add(header, 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(toolbar, 0, 1);

        refreshButton = new Button { Text = "Refresh", AutoSize = true };
        refreshButton.Click += async (_, _) => await RefreshUsageAsync();
        toolbar.Controls.Add(refreshButton);

        copyButton = new Button { Text = "Copy Output", AutoSize = true };
        copyButton.Click += (_, _) => CopyOutput();
        toolbar.Controls.Add(copyButton);

        configButton = new Button { Text = "Config", AutoSize = true };
        configButton.Click += (_, _) => OpenConfig();
        toolbar.Controls.Add(configButton);

        settingsButton = new Button { Text = "Settings", AutoSize = true };
        settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        toolbar.Controls.Add(settingsButton);

        statusLabel = new Label
        {
            AutoSize = true,
            Text = "Ready",
            Padding = new Padding(12, 6, 0, 0),
        };
        toolbar.Controls.Add(statusLabel);

        outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Cascadia Mono", 9F),
            ReadOnly = true,
            BackColor = Color.FromArgb(252, 252, 252),
            WordWrap = false,
        };
        root.Controls.Add(outputBox, 0, 2);

        Shown += async (_, _) => await RefreshUsageAsync();
    }

    public event EventHandler? SettingsRequested;

    public void ApplySettings(AppSettings newSettings, CliRunner newCliRunner)
    {
        settings = newSettings;
        cliRunner = newCliRunner;
        _ = RefreshUsageAsync();
    }

    public async Task RefreshUsageAsync()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        var token = refreshCancellation.Token;

        refreshButton.Enabled = false;
        statusLabel.Text = $"Refreshing {settings.Provider}...";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await cliRunner.UsageTextAsync(token);
            stopwatch.Stop();

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Succeeded)
            {
                outputBox.Text = string.IsNullOrWhiteSpace(result.StandardOutput)
                    ? "No usage output returned."
                    : result.StandardOutput.Trim();
                statusLabel.Text = $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s";
            }
            else
            {
                outputBox.Text = result.CombinedOutput;
                statusLabel.Text = $"CLI exited with code {result.ExitCode}";
            }
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }

    private void CopyOutput()
    {
        if (!string.IsNullOrWhiteSpace(outputBox.Text))
        {
            Clipboard.SetText(outputBox.Text);
            statusLabel.Text = "Output copied.";
        }
    }

    private static void OpenConfig()
    {
        try
        {
            ConfigLocator.OpenConfigFile();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
