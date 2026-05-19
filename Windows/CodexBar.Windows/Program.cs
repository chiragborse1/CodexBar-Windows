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

        if (args.Contains("--pty-bridge", StringComparer.OrdinalIgnoreCase))
        {
            return PtyBridge.RunFromStandardInput();
        }

        if (args.Contains("--pty-bridge-smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            return PtyBridge.SmokeTest();
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var forceWindow = args.Contains("--window", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--foreground", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--show-window", StringComparer.OrdinalIgnoreCase);
        var demoMode = args.Contains("--demo", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--ui-demo", StringComparer.OrdinalIgnoreCase);
        using var context = new TrayApplicationContext(forceWindow, demoMode);
        Application.Run(context);
        return 0;
    }
}
