using System.Diagnostics;

namespace CodexBar.Windows;

internal sealed class DashboardForm : Form
{
    private readonly Label statusLabel;
    private readonly DataGridView summaryGrid;
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

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(tabs, 0, 2);

        var summaryPage = new TabPage("Dashboard");
        tabs.TabPages.Add(summaryPage);

        summaryGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };
        AddTextColumn("Provider", nameof(UsagePayloadRow.Provider), 95);
        AddTextColumn("Account", nameof(UsagePayloadRow.Account), 150);
        AddTextColumn("Source", nameof(UsagePayloadRow.Source), 110);
        AddTextColumn("Primary", nameof(UsagePayloadRow.Primary), 190);
        AddTextColumn("Secondary", nameof(UsagePayloadRow.Secondary), 190);
        AddTextColumn("Credits", nameof(UsagePayloadRow.Credits), 90);
        AddTextColumn("Cost", nameof(UsagePayloadRow.Cost), 110);
        AddTextColumn("Status", nameof(UsagePayloadRow.Status), 140);
        AddTextColumn("Updated", nameof(UsagePayloadRow.Updated), 165);
        AddTextColumn("Error", nameof(UsagePayloadRow.Error), 260);
        summaryPage.Controls.Add(summaryGrid);

        var rawPage = new TabPage("Raw Output");
        tabs.TabPages.Add(rawPage);

        outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Cascadia Mono", 9F),
            ReadOnly = true,
            BackColor = Color.FromArgb(252, 252, 252),
            WordWrap = false,
        };
        rawPage.Controls.Add(outputBox);

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
            var result = await cliRunner.UsageJsonAsync(token);
            stopwatch.Stop();

            if (token.IsCancellationRequested)
            {
                return;
            }

            var output = string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.CombinedOutput
                : result.StandardOutput.Trim();
            outputBox.Text = string.IsNullOrWhiteSpace(output) ? "No usage output returned." : output;

            var parsedRows = TryParseRows(result.StandardOutput);
            if (parsedRows.Count > 0)
            {
                summaryGrid.DataSource = parsedRows;
                statusLabel.Text = result.Succeeded
                    ? $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s"
                    : $"Updated with provider errors, code {result.ExitCode}";
            }
            else if (result.Succeeded)
            {
                summaryGrid.DataSource = Array.Empty<UsagePayloadRow>();
                statusLabel.Text = $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s";
            }
            else
            {
                summaryGrid.DataSource = Array.Empty<UsagePayloadRow>();
                statusLabel.Text = $"CLI exited with code {result.ExitCode}";
            }
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }

    private void AddTextColumn(string header, string propertyName, int width)
    {
        summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic,
        });
    }

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
