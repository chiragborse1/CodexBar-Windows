using System.Diagnostics;

namespace CodexBar.Windows;

internal static class ConfigLocator
{
    public static string ConfigDirectory =>
        AppInfo.AppDataDirectory;

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static void OpenConfigFile()
    {
        Directory.CreateDirectory(ConfigDirectory);
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(
                ConfigPath,
                """
                {
                  "version": 1,
                  "providers": []
                }
                """);
        }

        OpenWithShell(ConfigPath);
    }

    public static void OpenConfigFolder()
    {
        Directory.CreateDirectory(ConfigDirectory);
        OpenWithShell(ConfigDirectory);
    }

    public static void OpenAppDataFolder()
    {
        Directory.CreateDirectory(AppInfo.AppDataDirectory);
        OpenWithShell(AppInfo.AppDataDirectory);
    }

    private static void OpenWithShell(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
        });
    }
}
