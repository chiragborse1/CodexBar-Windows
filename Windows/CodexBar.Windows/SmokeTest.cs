namespace CodexBar.Windows;

internal static class SmokeTest
{
    public static int Run()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.AppDataDirectory);
            _ = AppSettings.Load();
            var rows = UsagePayloadParser.Parse(
                """
                [
                  {
                    "provider": "codex",
                    "source": "api",
                    "usage": {
                      "primary": { "usedPercent": 25 },
                      "updatedAt": "2026-01-01T00:00:00Z"
                    },
                    "error": null
                  }
                ]
                """);
            if (rows.Count != 1 || rows[0].Provider != "codex")
            {
                return 1;
            }

            using var icon = IconFactory.CreateTrayIcon();
            return icon.Handle == IntPtr.Zero ? 1 : 0;
        }
        catch
        {
            return 1;
        }
    }
}
