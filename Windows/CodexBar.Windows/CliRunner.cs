using System.Diagnostics;

namespace CodexBar.Windows;

internal sealed class CliRunner
{
    private readonly AppSettings settings;

    public CliRunner(AppSettings settings)
    {
        this.settings = settings;
    }

    public string? ResolveExecutable()
    {
        if (!string.IsNullOrWhiteSpace(settings.CliPath) && File.Exists(settings.CliPath))
        {
            return settings.CliPath;
        }

        var localPath = Path.Combine(AppContext.BaseDirectory, AppInfo.CliFileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return FindOnPath(AppInfo.CliFileName);
    }

    public Task<CliResult> VersionAsync(CancellationToken cancellationToken) =>
        RunAsync(["--version"], TimeSpan.FromSeconds(15), cancellationToken);

    public Task<CliResult> ValidateConfigAsync(CancellationToken cancellationToken) =>
        RunAsync(["config", "validate", "--format", "json", "--pretty"], TimeSpan.FromSeconds(20), cancellationToken);

    public Task<CliResult> ProvidersAsync(CancellationToken cancellationToken) =>
        RunAsync(["config", "providers"], TimeSpan.FromSeconds(20), cancellationToken);

    public Task<CliResult> UsageTextAsync(CancellationToken cancellationToken) =>
        RunAsync(
            ["usage", "--provider", settings.Provider, "--format", "text", "--no-color"],
            TimeSpan.FromSeconds(90),
            cancellationToken);

    public Task<CliResult> UsageJsonAsync(CancellationToken cancellationToken) =>
        RunAsync(
            ["usage", "--provider", settings.Provider, "--format", "json", "--pretty"],
            TimeSpan.FromSeconds(90),
            cancellationToken);

    public async Task<CliResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new CliResult(
                2,
                "",
                $"Could not find {AppInfo.CliFileName}. Put it beside {AppInfo.DisplayName}.exe or set the CLI path in Settings.",
                false);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
        };
        startInfo.Environment["CODEXBAR_CONFIG"] = ConfigLocator.ConfigPath;

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new CliResult(1, "", "Failed to start the CLI process.", false);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linked.Token);

            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new CliResult(process.ExitCode, stdout, stderr, false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            TryKill(process);
            return new CliResult(124, "", $"CLI command timed out after {timeout.TotalSeconds:0} seconds.", true);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new CliResult(130, "", "CLI command was cancelled.", false);
        }
        catch (Exception ex)
        {
            TryKill(process);
            return new CliResult(1, "", ex.Message, false);
        }
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
