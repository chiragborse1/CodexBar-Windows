using System.Reflection;

namespace CodexBar.Windows;

internal static class AppInfo
{
    public const string DisplayName = "CodexBar-Windows";
    public const string RunKeyName = "CodexBar-Windows";
    public const string CliFileName = "CodexBarCLI.exe";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    public static string AppDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DisplayName);
}
