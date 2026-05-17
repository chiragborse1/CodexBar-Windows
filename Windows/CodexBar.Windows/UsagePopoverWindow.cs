using System.Diagnostics;
using Forms = System.Windows.Forms;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfEffects = System.Windows.Media.Effects;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;

namespace CodexBar.Windows;

internal sealed class UsagePopoverWindow : Wpf.Window
{
    private const double PopoverWidth = 430;
    private const double PopoverHeight = 620;
    private WpfControls.TextBlock statusText = null!;
    private WpfControls.TextBlock footerText = null!;
    private WpfControls.Button refreshButton = null!;
    private WpfControls.StackPanel cardsStack = null!;
    private CancellationTokenSource? refreshCancellation;
    private AppSettings settings;
    private CliRunner cliRunner;

    public UsagePopoverWindow(AppSettings settings, CliRunner cliRunner)
    {
        this.settings = settings;
        this.cliRunner = cliRunner;

        Title = AppInfo.DisplayName;
        Width = PopoverWidth;
        Height = PopoverHeight;
        MinWidth = 390;
        MinHeight = 460;
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
            Background = Brush("#F6F7FA"),
            BorderBrush = Brush("#D8DCE5"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(10),
            SnapsToDevicePixels = true,
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.22,
                Color = WpfMedia.Color.FromRgb(20, 24, 32),
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
            Margin = new Wpf.Thickness(14, 12, 14, 12),
        };
        var scroll = new WpfControls.ScrollViewer
        {
            Content = cardsStack,
            VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled,
            Background = Brush("#F6F7FA"),
        };
        shell.Children.Add(scroll);

        WpfControls.Panel.SetZIndex(header, 2);
        WpfControls.Panel.SetZIndex(footer, 2);
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? DiagnosticsRequested;

    public bool IsClosed { get; private set; }

    public void ApplySettings(AppSettings newSettings, CliRunner newCliRunner)
    {
        settings = newSettings;
        cliRunner = newCliRunner;
        _ = RefreshUsageAsync();
    }

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
            RenderRows(rows, result);

            statusText.Text = result.Succeeded
                ? $"Updated {DateTime.Now:t} in {stopwatch.Elapsed.TotalSeconds:0.0}s"
                : $"Updated with provider errors, code {result.ExitCode}";
            footerText.Text = rows.Count == 0
                ? "No provider usage returned."
                : $"{rows.Count} provider{(rows.Count == 1 ? "" : "s")} shown";
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
            Background = WpfMedia.Brushes.White,
            Margin = new Wpf.Thickness(16, 14, 14, 12),
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
            FontSize = 18,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#111827"),
        });
        statusText = new WpfControls.TextBlock
        {
            Text = "Ready",
            FontSize = 12,
            Foreground = Brush("#6B7280"),
            Margin = new Wpf.Thickness(0, 2, 0, 0),
            TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
        };
        titleStack.Children.Add(statusText);

        var actions = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            VerticalAlignment = Wpf.VerticalAlignment.Top,
            Margin = new Wpf.Thickness(12, 0, 0, 0),
        };
        WpfControls.Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        refreshButton = PopoverButton("Refresh");
        refreshButton.Click += async (_, _) => await RefreshUsageAsync();
        actions.Children.Add(refreshButton);

        var settingsButton = PopoverButton("Settings");
        settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        actions.Children.Add(settingsButton);

        var diagnosticsButton = PopoverButton("More");
        diagnosticsButton.Click += (_, _) => DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        actions.Children.Add(diagnosticsButton);

        return new WpfControls.Border
        {
            Background = WpfMedia.Brushes.White,
            CornerRadius = new Wpf.CornerRadius(10, 10, 0, 0),
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
            Background = WpfMedia.Brushes.White,
            BorderBrush = Brush("#E5E7EB"),
            BorderThickness = new Wpf.Thickness(0, 1, 0, 0),
            Padding = new Wpf.Thickness(16, 0, 16, 0),
            Height = 36,
            Child = footerText,
        };
    }

    private void RenderRows(IReadOnlyList<UsagePayloadRow> rows, CliResult result)
    {
        cardsStack.Children.Clear();
        if (rows.Count == 0)
        {
            cardsStack.Children.Add(CreateEmptyState(result));
            return;
        }

        foreach (var row in rows)
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
            BorderBrush = Brush("#E1E5EC"),
            BorderThickness = new Wpf.Thickness(1),
            CornerRadius = new Wpf.CornerRadius(8),
            Padding = new Wpf.Thickness(12),
            Margin = new Wpf.Thickness(0, 0, 0, 10),
            Effect = new WpfEffects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 1,
                Opacity = 0.06,
                Color = WpfMedia.Color.FromRgb(17, 24, 39),
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
            FontSize = 14,
            FontWeight = Wpf.FontWeights.SemiBold,
            Foreground = Brush("#1F2937"),
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
            percent.HasValue ? accent : Brush("#6B7280"),
            percent.HasValue ? Tint(accent, 0.92) : Brush("#F3F4F6"));
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
                string.IsNullOrWhiteSpace(row.Error) ? "Limits not available for this provider yet." : row.Error,
                string.IsNullOrWhiteSpace(row.Error) ? Brush("#6B7280") : Brush("#B42318"),
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
            stack.Children.Add(MessageLine(row.Error, Brush("#B42318"), new Wpf.Thickness(0, 8, 0, 0)));
        }

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
            Padding = new Wpf.Thickness(10, 4, 10, 5),
            Margin = new Wpf.Thickness(6, 0, 0, 0),
            MinWidth = 0,
            Cursor = WpfInput.Cursors.Hand,
        };
        return button;
    }

    private static WpfControls.Border ProviderIcon(UsagePayloadRow row, WpfMedia.Brush accent)
    {
        var letter = string.IsNullOrWhiteSpace(row.DisplayName)
            ? "?"
            : row.DisplayName.Trim()[0].ToString().ToUpperInvariant();

        return new WpfControls.Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new Wpf.CornerRadius(15),
            Background = accent,
            Child = new WpfControls.TextBlock
            {
                Text = letter,
                FontSize = 13,
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
            Height = 8,
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
}
