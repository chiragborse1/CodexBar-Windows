namespace CodexBar.Windows;

internal sealed record CliResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    public string CombinedOutput
    {
        get
        {
            if (string.IsNullOrWhiteSpace(StandardError))
            {
                return StandardOutput.Trim();
            }

            if (string.IsNullOrWhiteSpace(StandardOutput))
            {
                return StandardError.Trim();
            }

            return $"{StandardOutput.Trim()}{Environment.NewLine}{Environment.NewLine}{StandardError.Trim()}";
        }
    }
}
