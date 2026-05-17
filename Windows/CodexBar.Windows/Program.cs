namespace CodexBar.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{AppInfo.DisplayName} {AppInfo.Version}");
            return 0;
        }

        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            return SmokeTest.Run();
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var context = new TrayApplicationContext();
        Application.Run(context);
        return 0;
    }
}
