using System.Text.Json;

namespace CodexBar.Windows;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string? CliPath { get; set; }
    public string Provider { get; set; } = "enabled";
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool LaunchAtLogin { get; set; }
    public bool StartMinimized { get; set; }

    public static string SettingsPath =>
        Path.Combine(AppInfo.AppDataDirectory, "windows-app-settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsPath),
                    JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    loaded.LaunchAtLogin = StartupManager.IsEnabled();
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults when a local settings file is malformed.
        }

        var settings = new AppSettings
        {
            LaunchAtLogin = StartupManager.IsEnabled(),
        };
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(AppInfo.AppDataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        StartupManager.SetEnabled(LaunchAtLogin);
    }

    private void Normalize()
    {
        Provider = string.IsNullOrWhiteSpace(Provider) ? "enabled" : Provider.Trim().ToLowerInvariant();
        RefreshIntervalMinutes = Math.Clamp(RefreshIntervalMinutes, 1, 120);
        if (string.IsNullOrWhiteSpace(CliPath))
        {
            CliPath = null;
        }
        else
        {
            CliPath = CliPath.Trim();
        }
    }
}
