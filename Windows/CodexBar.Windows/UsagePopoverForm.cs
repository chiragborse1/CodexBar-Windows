using System.Diagnostics;

namespace CodexBar.Windows;

internal sealed class UsagePopoverForm : Form
{
    private readonly Label statusLabel;
    private readonly Label footerLabel;
    private readonly Button refreshButton;
    private readonly Button settingsButton;
    private readonly Button diagnosticsButton;
    private readonly FlowLayoutPanel cardsPanel;
    private CancellationTokenSource? refreshCancellation;
    private AppSettings settings;
    private CliRunner cliRunner;

    public UsagePopoverForm(AppSettings settings, CliRunner cliRunner)
    {
        this.settings = settings;
        this.cliRunner = cliRunner;

        Text = AppInfo.DisplayName;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(390, 560);
        MinimumSize = new Size(360, 420);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(247, 248, 251);
        Padding = new Padding(1);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(14, 12, 14, 8),
            BackColor = Color.White,
            AutoSize = true,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);

        var titleLabel = new Label
        {
            Text = AppInfo.DisplayName,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 1),
        };
        header.Controls.Add(titleLabel, 0, 0);

        statusLabel = new Label
        {
            Text = "Ready",
            AutoSize = true,
            ForeColor = Color.FromArgb(88, 92, 104),
            Margin = new Padding(0, 0, 0, 0),
        };
        header.Controls.Add(statusLabel, 0, 1);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        header.SetRowSpan(actions, 2);
        header.Controls.Add(actions, 1, 0);

        refreshButton = CompactButton("Refresh");
        refreshButton.Click += async (_, _) => await RefreshUsageAsync();
        actions.Controls.Add(refreshButton);

        settingsButton = CompactButton("Settings");
        settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(settingsButton);

        diagnosticsButton = CompactButton("More");
        diagnosticsButton.Click += (_, _) => DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(diagnosticsButton);

        cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = BackColor,
        };
        cardsPanel.Resize += (_, _) => ResizeCards();
        root.Controls.Add(cardsPanel, 0, 1);

        footerLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 30,
            Text = "",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(96, 100, 112),
            BackColor = Color.White,
            Padding = new Padding(14, 0, 14, 0),
        };
        root.Controls.Add(footerLabel, 0, 2);

        Deactivate += (_, _) => Hide();
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? DiagnosticsRequested;

    public void ApplySettings(AppSettings newSettings, CliRunner newCliRunner)
    {
        settings = newSettings;
        cliRunner = newCliRunner;
        _ = RefreshUsageAsync();
    }

    public void ShowNearCursor()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var x = Math.Min(Cursor.Position.X - Width + 18, area.Right - Width - 8);
        var y = Math.Min(Cursor.Position.Y + 10, area.Bottom - Height - 8);
        x = Math.Max(area.Left + 8, x);
        y = Math.Max(area.Top + 8, y);
        Location = new Point(x, y);
        Show();
        Activate();
        _ = RefreshUsageAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
        }

        base.Dispose(disposing);
    }

    public async Task RefreshUsageAsync()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        var token = refreshCancellation.Token;

        refreshButton.Enabled = false;
        statusLabel.Text = $"Refreshing {settings.Provider}...";
        footerLabel.Text = "";

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
            RenderRows(rows, result);

            statusLabel.Text = result.Succeeded
                ? $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s"
                : $"Updated with provider errors, code {result.ExitCode}";
            footerLabel.Text = rows.Count == 0
                ? "No provider usage returned."
                : $"{rows.Count} provider{(rows.Count == 1 ? "" : "s")} shown";
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(214, 218, 226));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void RenderRows(IReadOnlyList<UsagePayloadRow> rows, CliResult result)
    {
        cardsPanel.SuspendLayout();
        try
        {
            cardsPanel.Controls.Clear();
            if (rows.Count == 0)
            {
                cardsPanel.Controls.Add(CreateEmptyState(result));
                return;
            }

            foreach (var row in rows)
            {
                cardsPanel.Controls.Add(CreateProviderCard(row));
            }

            ResizeCards();
        }
        finally
        {
            cardsPanel.ResumeLayout();
        }
    }

    private Control CreateProviderCard(UsagePayloadRow row)
    {
        var accent = ProviderAccent(row.Provider);
        var card = new Panel
        {
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12),
            AutoSize = true,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 1,
        };
        card.Controls.Add(layout);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 6),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(header);

        header.Controls.Add(new Label
        {
            Text = row.DisplayName,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 31, 38),
            Margin = new Padding(0, 0, 8, 0),
        }, 0, 0);

        var percent = row.Metrics.FirstOrDefault()?.RemainingPercent;
        header.Controls.Add(new Label
        {
            Text = percent.HasValue ? $"{percent.Value:0}% left" : StatusLabel(row),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            ForeColor = percent.HasValue ? accent : Color.FromArgb(94, 98, 110),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(8, 1, 0, 0),
        }, 1, 0);

        var subtitle = Subtitle(row);
        header.SetColumnSpan(subtitle, 2);
        header.Controls.Add(subtitle, 0, 1);

        if (row.Metrics.Count == 0)
        {
            layout.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(row.Error) ? "Limits not available" : row.Error,
                AutoSize = true,
                ForeColor = string.IsNullOrWhiteSpace(row.Error)
                    ? Color.FromArgb(99, 103, 114)
                    : Color.FromArgb(184, 57, 47),
                MaximumSize = new Size(330, 0),
                Margin = new Padding(0, 4, 0, 0),
            });
        }
        else
        {
            foreach (var metric in row.Metrics.Take(4))
            {
                layout.Controls.Add(CreateMetric(metric, accent));
            }
        }

        if (!string.IsNullOrWhiteSpace(row.Credits) || !string.IsNullOrWhiteSpace(row.Cost))
        {
            layout.Controls.Add(CreateSmallLine("Credits", row.Credits, "Cost", row.Cost));
        }

        if (!string.IsNullOrWhiteSpace(row.Status))
        {
            layout.Controls.Add(CreateFooterLine(row.Status, Color.FromArgb(88, 92, 104)));
        }

        if (!string.IsNullOrWhiteSpace(row.Error))
        {
            layout.Controls.Add(CreateFooterLine(row.Error, Color.FromArgb(184, 57, 47)));
        }

        return card;
    }

    private Control CreateMetric(UsagePayloadMetric metric, Color accent)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 5, 0, 2),
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(header);

        header.Controls.Add(new Label
        {
            Text = metric.Title,
            AutoSize = true,
            ForeColor = Color.FromArgb(42, 46, 55),
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = $"{metric.RemainingPercent:0}% left",
            AutoSize = true,
            ForeColor = Color.FromArgb(88, 92, 104),
        }, 1, 0);

        layout.Controls.Add(new UsageBarControl
        {
            Dock = DockStyle.Top,
            UsedPercent = metric.UsedPercent,
            AccentColor = accent,
        });

        if (!string.IsNullOrWhiteSpace(metric.Reset))
        {
            layout.Controls.Add(new Label
            {
                Text = metric.Reset,
                AutoSize = true,
                ForeColor = Color.FromArgb(98, 102, 114),
                MaximumSize = new Size(330, 0),
                Margin = new Padding(0, 0, 0, 0),
            });
        }

        return layout;
    }

    private Control CreateEmptyState(CliResult result)
    {
        var panel = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 10),
            AutoSize = true,
        };

        var text = result.Succeeded
            ? "No provider usage returned."
            : string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"CLI exited with code {result.ExitCode}."
                : result.CombinedOutput.Trim();

        panel.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(330, 0),
            ForeColor = result.Succeeded ? Color.FromArgb(88, 92, 104) : Color.FromArgb(184, 57, 47),
        });
        return panel;
    }

    private static Label Subtitle(UsagePayloadRow row)
    {
        var parts = new[] { row.Account, row.Source, row.Updated }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return new Label
        {
            Text = string.Join("  |  ", parts),
            AutoSize = true,
            ForeColor = Color.FromArgb(92, 96, 108),
            MaximumSize = new Size(330, 0),
            Margin = new Padding(0, 1, 0, 0),
        };
    }

    private static Control CreateSmallLine(string leftTitle, string leftValue, string rightTitle, string rightValue)
    {
        var label = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(66, 70, 82),
            Margin = new Padding(0, 8, 0, 0),
        };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(leftValue))
        {
            parts.Add($"{leftTitle}: {leftValue}");
        }

        if (!string.IsNullOrWhiteSpace(rightValue))
        {
            parts.Add($"{rightTitle}: {rightValue}");
        }

        label.Text = string.Join("  |  ", parts);
        return label;
    }

    private static Label CreateFooterLine(string text, Color color) =>
        new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = color,
            MaximumSize = new Size(330, 0),
            Margin = new Padding(0, 6, 0, 0),
        };

    private static Button CompactButton(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(4, 0, 0, 0),
        };

    private static string StatusLabel(UsagePayloadRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Error))
        {
            return "Error";
        }

        return string.IsNullOrWhiteSpace(row.Status) ? "Ready" : row.Status.Split(':')[0];
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

    private void ResizeCards()
    {
        var width = cardsPanel.ClientSize.Width - cardsPanel.Padding.Horizontal - 4;
        if (cardsPanel.VerticalScroll.Visible)
        {
            width -= SystemInformation.VerticalScrollBarWidth;
        }

        foreach (Control control in cardsPanel.Controls)
        {
            control.Width = Math.Max(300, width);
        }
    }

    private static Color ProviderAccent(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "claude" => Color.FromArgb(204, 100, 47),
            "codex" or "openai" => Color.FromArgb(20, 142, 88),
            "gemini" or "vertexai" => Color.FromArgb(42, 112, 214),
            "copilot" => Color.FromArgb(72, 92, 200),
            "cursor" => Color.FromArgb(35, 38, 44),
            "openrouter" => Color.FromArgb(108, 85, 205),
            "zai" => Color.FromArgb(40, 145, 156),
            "kilo" or "kiro" => Color.FromArgb(198, 114, 34),
            "bedrock" => Color.FromArgb(224, 133, 36),
            _ => Color.FromArgb(48, 128, 148),
        };
}
