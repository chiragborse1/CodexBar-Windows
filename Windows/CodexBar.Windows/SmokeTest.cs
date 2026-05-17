namespace CodexBar.Windows;

internal static class SmokeTest
{
    public static int Run()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.AppDataDirectory);
            _ = AppSettings.Load();
            using var icon = IconFactory.CreateTrayIcon();
            return icon.Handle == IntPtr.Zero ? 1 : 0;
        }
        catch
        {
            return 1;
        }
    }
}
