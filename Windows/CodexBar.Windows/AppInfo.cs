using System.Reflection;

namespace CodexBar.Windows;

internal static class AppInfo
{
    public const string DisplayName = "CodexBar-Windows";
    public const string RunKeyName = "CodexBar-Windows";
    public const string CliFileName = "CodexBarCLI.exe";

    public static string Version
    {
        get
        {
            var versionPath = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(versionPath))
            {
                var packagedVersion = File.ReadAllText(versionPath).Trim();
                if (!string.IsNullOrWhiteSpace(packagedVersion))
                {
                    return packagedVersion;
                }
            }

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        }
    }

    public static string AppDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DisplayName);
}
