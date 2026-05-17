using Microsoft.Win32;

namespace CodexBar.Windows;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppInfo.RunKeyName) is string value &&
               value.Contains(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                        Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppInfo.RunKeyName, $"\"{Application.ExecutablePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppInfo.RunKeyName, throwOnMissingValue: false);
        }
    }
}
