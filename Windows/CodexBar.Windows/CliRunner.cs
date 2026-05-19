using System.Diagnostics;

namespace CodexBar.Windows;

internal sealed class CliRunner
{
    private readonly AppSettings settings;
    private readonly bool demoMode;

    public CliRunner(AppSettings settings, bool demoMode = false)
    {
        this.settings = settings;
        this.demoMode = demoMode;
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
        demoMode
            ? Task.FromResult(DemoResult($"{AppInfo.DisplayName} ui-dev-demo"))
            : RunAsync(["--version"], TimeSpan.FromSeconds(15), cancellationToken);

    public Task<CliResult> ValidateConfigAsync(CancellationToken cancellationToken) =>
        demoMode
            ? Task.FromResult(DemoResult("[]"))
            : RunAsync(["config", "validate", "--format", "json", "--pretty"], TimeSpan.FromSeconds(20), cancellationToken);

    public Task<CliResult> ProvidersAsync(CancellationToken cancellationToken) =>
        demoMode
            ? Task.FromResult(DemoResult("codex: enabled default (Codex)\nopenai: enabled (OpenAI)\nclaude: disabled (Claude)"))
            : RunAsync(["config", "providers"], TimeSpan.FromSeconds(20), cancellationToken);

    public Task<CliResult> SetApiKeyAsync(
        string provider,
        string apiKey,
        bool enableProvider,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "config",
            "set-api-key",
            "--provider",
            provider,
            "--stdin",
            "--format",
            "json",
            "--pretty",
        };
        if (!enableProvider)
        {
            arguments.Add("--no-enable");
        }

        if (demoMode)
        {
            return Task.FromResult(DemoResult("{\"saved\":true,\"demo\":true}"));
        }

        return RunAsync(
            arguments,
            TimeSpan.FromSeconds(20),
            cancellationToken,
            standardInput: apiKey);
    }

    public Task<CliResult> SetProviderEnabledAsync(
        string provider,
        bool enabled,
        CancellationToken cancellationToken) =>
        demoMode
            ? Task.FromResult(DemoResult($"{{\"provider\":\"{provider}\",\"enabled\":{enabled.ToString().ToLowerInvariant()},\"demo\":true}}"))
            : RunAsync(
                ["config", enabled ? "enable" : "disable", "--provider", provider, "--format", "json", "--pretty"],
                TimeSpan.FromSeconds(20),
                cancellationToken);

    public Task<CliResult> SetCookieHeaderAsync(
        string provider,
        string cookieHeader,
        bool enableProvider,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "config",
            "set-cookie",
            "--provider",
            provider,
            "--stdin",
            "--format",
            "json",
            "--pretty",
        };
        if (!enableProvider)
        {
            arguments.Add("--no-enable");
        }

        if (demoMode)
        {
            return Task.FromResult(DemoResult("{\"saved\":true,\"demo\":true}"));
        }

        return RunAsync(
            arguments,
            TimeSpan.FromSeconds(20),
            cancellationToken,
            standardInput: cookieHeader);
    }

    public Task<CliResult> UsageTextAsync(CancellationToken cancellationToken) =>
        demoMode
            ? Task.FromResult(DemoResult("Codex: 66% left\nOpenAI: 81% left\nClaude: setup needed"))
            : RunAsync(
                UsageArguments("text", noColor: true),
                TimeSpan.FromSeconds(90),
                cancellationToken);

    public Task<CliResult> UsageJsonAsync(CancellationToken cancellationToken) =>
        demoMode
            ? Task.FromResult(DemoResult(DemoUsageJson))
            : RunAsync(
                UsageArguments("json", pretty: true),
                TimeSpan.FromSeconds(90),
                cancellationToken);

    public async Task<CliResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? standardInput = null)
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
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
        };
        startInfo.Environment["CODEXBAR_CONFIG"] = ConfigLocator.ConfigPath;
        var ptyBridge = Path.Combine(AppContext.BaseDirectory, $"{AppInfo.DisplayName}.exe");
        if (File.Exists(ptyBridge))
        {
            startInfo.Environment["CODEXBAR_PTY_BRIDGE"] = ptyBridge;
        }
        ProviderSecretEnvironment.ApplyStoredCredentials(startInfo.Environment);

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
            if (standardInput is not null)
            {
                await process.StandardInput.WriteLineAsync(standardInput).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(linked.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

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

    private List<string> UsageArguments(string format, bool noColor = false, bool pretty = false)
    {
        var arguments = new List<string> { "usage" };
        if (!string.Equals(settings.Provider, "enabled", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("--provider");
            arguments.Add(settings.Provider);
        }

        arguments.Add("--format");
        arguments.Add(format);
        if (pretty)
        {
            arguments.Add("--pretty");
        }

        if (noColor)
        {
            arguments.Add("--no-color");
        }

        return arguments;
    }

    private static CliResult DemoResult(string standardOutput) =>
        new(0, standardOutput, "", false);

    private static readonly string DemoUsageJson = """
[
  {
    "provider": "codex",
    "account": "demo@example.com",
    "source": "ui-dev demo",
    "status": {
      "indicator": "Ready",
      "description": "Demo data"
    },
    "usage": {
      "updatedAt": "demo",
      "primary": {
        "usedPercent": 34,
        "resetDescription": "resets in 2h 18m"
      },
      "secondary": {
        "usedPercent": 58,
        "resetDescription": "weekly reset Friday"
      }
    },
    "credits": {
      "remaining": 124.5,
      "updatedAt": "demo"
    }
  },
  {
    "provider": "openai",
    "account": "platform demo",
    "source": "api",
    "status": {
      "indicator": "Ready",
      "description": "API key path"
    },
    "usage": {
      "updatedAt": "demo",
      "primary": {
        "usedPercent": 19,
        "resetDescription": "billing cycle resets June 1"
      }
    },
    "credits": {
      "remaining": 42.75,
      "updatedAt": "demo"
    }
  },
  {
    "provider": "claude",
    "source": "setup",
    "error": {
      "message": "No available fetch strategy for claude."
    }
  },
  {
    "provider": "windsurf",
    "account": "localStorage demo",
    "source": "web",
    "status": {
      "indicator": "Ready",
      "description": "Chromium session import"
    },
    "usage": {
      "updatedAt": "demo",
      "primary": {
        "usedPercent": 72,
        "resetDescription": "resets tomorrow"
      }
    }
  }
]
""";

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
