using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace CodexBar.Windows;

[SupportedOSPlatform("windows10.0.17763.0")]
internal static class PtyBridge
{
    private const int ExtendedStartupInfoPresent = 0x00080000;
    private const int CreateUnicodeEnvironment = 0x00000400;
    private const int ProcThreadAttributePseudoconsole = 0x00020016;
    private const int WaitObject0 = 0x00000000;
    private const int WaitTimeout = 0x00000102;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static int RunFromStandardInput()
    {
        try
        {
            var json = Console.In.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
            {
                WriteResponse(PtyBridgeResponse.Failed("Missing PTY bridge request."));
                return 2;
            }

            var request = JsonSerializer.Deserialize<PtyBridgeRequest>(json, JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.Binary))
            {
                WriteResponse(PtyBridgeResponse.Failed("Invalid PTY bridge request."));
                return 2;
            }
            Normalize(request);

            var result = Run(request);
            WriteResponse(PtyBridgeResponse.Succeeded(result));
            return 0;
        }
        catch (Exception ex)
        {
            WriteResponse(PtyBridgeResponse.Failed(ex.Message));
            return 1;
        }
    }

    public static int SmokeTest()
    {
        try
        {
            var request = new PtyBridgeRequest
            {
                Binary = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Script = "echo CodexBarPTYBridgeSmoke\r\nexit",
                Options = new PtyBridgeOptions
                {
                    Timeout = 8,
                    IdleTimeout = 1,
                    StopOnSubstrings = ["CodexBarPTYBridgeSmoke"],
                    SettleAfterStop = 0.2,
                },
            };
            request.Environment = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(
                    entry => entry.Key.ToString() ?? "",
                    entry => entry.Value?.ToString() ?? "",
                    StringComparer.OrdinalIgnoreCase);

            var text = Run(request);
            if (!text.Contains("CodexBarPTYBridgeSmoke", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("PTY bridge smoke marker was not captured.");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string Run(PtyBridgeRequest request)
    {
        var inputRead = IntPtr.Zero;
        var inputWrite = IntPtr.Zero;
        var outputRead = IntPtr.Zero;
        var outputWrite = IntPtr.Zero;
        var pseudoConsole = IntPtr.Zero;
        var attributeList = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
        var processInfo = new ProcessInformation();

        FileStream? input = null;
        FileStream? output = null;

        try
        {
            CreatePipeChecked(out inputRead, out inputWrite);
            CreatePipeChecked(out outputRead, out outputWrite);

            var size = new Coord
            {
                X = (short)Math.Clamp(request.Options.Cols <= 0 ? 160 : request.Options.Cols, 20, 500),
                Y = (short)Math.Clamp(request.Options.Rows <= 0 ? 50 : request.Options.Rows, 10, 200),
            };
            CheckHResult(CreatePseudoConsole(size, inputRead, outputWrite, 0, out pseudoConsole));
            CloseHandleIfOpen(ref inputRead);
            CloseHandleIfOpen(ref outputWrite);

            var startupInfo = CreateStartupInfo(pseudoConsole, out attributeList);
            environmentBlock = CreateEnvironmentBlock(request.Environment);

            var shell = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(shell) || !File.Exists(shell))
            {
                shell = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            }

            var command = new StringBuilder(BuildCommandLine(shell, request.Binary, request.Arguments));
            var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Environment.CurrentDirectory
                : request.WorkingDirectory;

            if (!CreateProcess(
                    shell,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ExtendedStartupInfoPresent | CreateUnicodeEnvironment,
                    environmentBlock,
                    workingDirectory,
                    ref startupInfo,
                    out processInfo))
            {
                throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            }

            input = new FileStream(new SafeFileHandle(inputWrite, ownsHandle: true), FileAccess.Write, 4096, false);
            inputWrite = IntPtr.Zero;
            output = new FileStream(new SafeFileHandle(outputRead, ownsHandle: true), FileAccess.Read, 8192, false);
            outputRead = IntPtr.Zero;

            var automation = new PtyAutomation(request, input, output, processInfo.HProcess);
            return automation.Capture();
        }
        finally
        {
            try
            {
                input?.Dispose();
                output?.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }

            if (processInfo.HProcess != IntPtr.Zero)
            {
                if (WaitForSingleObject(processInfo.HProcess, 0) == WaitTimeout)
                {
                    TerminateProcess(processInfo.HProcess, 1);
                    WaitForSingleObject(processInfo.HProcess, 2000);
                }
            }

            CloseHandleIfOpen(ref processInfo.HThread);
            CloseHandleIfOpen(ref processInfo.HProcess);
            CloseHandleIfOpen(ref inputRead);
            CloseHandleIfOpen(ref inputWrite);
            CloseHandleIfOpen(ref outputRead);
            CloseHandleIfOpen(ref outputWrite);
            if (pseudoConsole != IntPtr.Zero)
            {
                ClosePseudoConsole(pseudoConsole);
            }

            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (environmentBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }
        }
    }

    private static void Normalize(PtyBridgeRequest request)
    {
        request.Arguments ??= [];
        request.Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        request.Options ??= new PtyBridgeOptions();
        request.Options.SendOnSubstrings ??= new Dictionary<string, string>(StringComparer.Ordinal);
        request.Options.StopOnSubstrings ??= [];
    }

    private static StartupInfoEx CreateStartupInfo(IntPtr pseudoConsole, out IntPtr attributeList)
    {
        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        if (size == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not size ConPTY attribute list.");
        }

        attributeList = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
        {
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");
        }

        if (!UpdateProcThreadAttribute(
                attributeList,
                0,
                (IntPtr)ProcThreadAttributePseudoconsole,
                pseudoConsole,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
        {
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
        }

        var startupInfo = new StartupInfoEx();
        startupInfo.StartupInfo.Cb = Marshal.SizeOf<StartupInfoEx>();
        startupInfo.AttributeList = attributeList;
        return startupInfo;
    }

    private static void CreatePipeChecked(out IntPtr readPipe, out IntPtr writePipe)
    {
        if (!CreatePipe(out readPipe, out writePipe, IntPtr.Zero, 0))
        {
            throw new InvalidOperationException($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private static IntPtr CreateEnvironmentBlock(IReadOnlyDictionary<string, string>? environment)
    {
        if (environment is null || environment.Count == 0)
        {
            return IntPtr.Zero;
        }

        var builder = new StringBuilder();
        foreach (var item in environment.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Contains('='))
            {
                continue;
            }

            builder.Append(item.Key).Append('=').Append(item.Value).Append('\0');
        }
        builder.Append('\0');

        var bytes = Encoding.Unicode.GetBytes(builder.ToString());
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return pointer;
    }

    private static string BuildCommandLine(string shell, string binary, IReadOnlyList<string>? arguments)
    {
        var command = new StringBuilder();
        command.Append(Quote(binary));
        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                command.Append(' ').Append(Quote(argument));
            }
        }

        return $"{Quote(shell)} /d /c {Quote(command.ToString())}";
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var builder = new StringBuilder();
        builder.Append('"');
        var backslashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            if (backslashes > 0)
            {
                builder.Append('\\', backslashes);
                backslashes = 0;
            }
            builder.Append(ch);
        }

        if (backslashes > 0)
        {
            builder.Append('\\', backslashes * 2);
        }
        builder.Append('"');
        return builder.ToString();
    }

    private static void CheckHResult(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static void CloseHandleIfOpen(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            handle = IntPtr.Zero;
            return;
        }

        CloseHandle(handle);
        handle = IntPtr.Zero;
    }

    private static void WriteResponse(PtyBridgeResponse response)
    {
        Console.Out.Write(JsonSerializer.Serialize(response, JsonOptions));
        Console.Out.Flush();
    }

    private sealed class PtyAutomation
    {
        private static readonly byte[] CursorQuery = [0x1B, 0x5B, 0x36, 0x6E];
        private readonly PtyBridgeRequest request;
        private readonly FileStream input;
        private readonly FileStream output;
        private readonly IntPtr processHandle;
        private readonly ConcurrentQueue<byte[]> chunks = new();
        private readonly AutoResetEvent outputReady = new(false);
        private readonly MemoryStream captured = new();
        private readonly HashSet<string> triggeredSends = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource readerStop = new();
        private string recentText = "";
        private DateTime lastOutputAt = DateTime.UtcNow;

        public PtyAutomation(PtyBridgeRequest request, FileStream input, FileStream output, IntPtr processHandle)
        {
            this.request = request;
            this.input = input;
            this.output = output;
            this.processHandle = processHandle;
        }

        public string Capture()
        {
            var reader = Task.Run(ReadOutput);
            Thread.Sleep(TimeSpan.FromSeconds(Math.Clamp(request.Options.InitialDelay, 0, 10)));

            try
            {
                if (IsCodexStatusRequest())
                {
                    CaptureCodexStatus();
                }
                else
                {
                    CaptureGeneric();
                }
            }
            finally
            {
                TrySend("/exit\r");
                WaitForExitOrKill(750);
                readerStop.Cancel();
                outputReady.Set();
                try
                {
                    reader.Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // Best-effort reader cleanup.
                }
            }

            return Encoding.UTF8.GetString(captured.ToArray());
        }

        private void CaptureGeneric()
        {
            var options = request.Options;
            var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(options.Timeout, 1, 300));
            var trimmed = request.Script.Trim();
            if (trimmed.Length > 0)
            {
                Send(trimmed);
                Send("\r");
            }

            var urlSeen = false;
            var lastEnter = DateTime.UtcNow;

            while (DateTime.UtcNow < deadline)
            {
                var hadData = DrainChunks(chunk =>
                {
                    ProcessCursorQuery(chunk);
                    ProcessConfiguredSends();
                    if (!urlSeen && ContainsAnyUrl())
                    {
                        urlSeen = true;
                    }
                });

                if (ContainsStopNeedle(options.StopOnSubstrings) ||
                    (options.StopOnUrl && urlSeen))
                {
                    DrainFor(TimeSpan.FromSeconds(Math.Clamp(options.SettleAfterStop, 0, 5)));
                    return;
                }

                if (options.IdleTimeout is > 0 &&
                    captured.Length > 0 &&
                    DateTime.UtcNow - lastOutputAt >= TimeSpan.FromSeconds(options.IdleTimeout.Value))
                {
                    return;
                }

                if (!urlSeen && options.SendEnterEvery is > 0 &&
                    DateTime.UtcNow - lastEnter >= TimeSpan.FromSeconds(options.SendEnterEvery.Value))
                {
                    TrySend("\r");
                    lastEnter = DateTime.UtcNow;
                }

                if (IsProcessExited() && !hadData && chunks.IsEmpty)
                {
                    DrainFor(TimeSpan.FromMilliseconds(200));
                    return;
                }

                outputReady.WaitOne(TimeSpan.FromMilliseconds(50));
            }

            if (captured.Length == 0)
            {
                throw new TimeoutException("PTY command timed out.");
            }
        }

        private void CaptureCodexStatus()
        {
            var options = request.Options;
            var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(options.Timeout, 1, 300));
            var script = string.IsNullOrWhiteSpace(request.Script) ? "/status" : request.Script.Trim();
            var statusMarkers = new[] { "Credits:", "5h limit", "5-hour limit", "Weekly limit" };
            var updateMarkers = new[] { "update available!", "run bun install -g @openai/codex", "0.60.1 ->" };
            var sentScript = false;
            var skippedUpdate = false;
            var sawUpdate = false;
            var sawStatus = false;
            var enterRetries = 0;
            var resendRetries = 0;
            var lastEnter = DateTime.MinValue;
            DateTime? scriptSentAt = null;

            while (DateTime.UtcNow < deadline)
            {
                DrainChunks(ProcessCursorQuery);
                var lower = recentText.ToLowerInvariant();
                sawStatus = sawStatus || statusMarkers.Any(marker => recentText.Contains(marker, StringComparison.Ordinal));
                sawUpdate = sawUpdate || updateMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));

                if (!skippedUpdate && sawUpdate)
                {
                    TrySend("\u001b[B");
                    Thread.Sleep(120);
                    TrySend("\r");
                    Thread.Sleep(150);
                    TrySend("\r");
                    TrySend(script);
                    TrySend("\r");
                    skippedUpdate = true;
                    sentScript = false;
                    scriptSentAt = null;
                    captured.SetLength(0);
                    recentText = "";
                    sawStatus = false;
                    Thread.Sleep(300);
                    continue;
                }

                if (!sentScript && (!sawUpdate || skippedUpdate))
                {
                    TrySend(script);
                    TrySend("\r");
                    sentScript = true;
                    scriptSentAt = DateTime.UtcNow;
                    lastEnter = DateTime.UtcNow;
                    Thread.Sleep(200);
                    continue;
                }

                if (sentScript && !sawStatus)
                {
                    if (DateTime.UtcNow - lastEnter >= TimeSpan.FromSeconds(1.2) && enterRetries < 6)
                    {
                        TrySend("\r");
                        enterRetries++;
                        lastEnter = DateTime.UtcNow;
                        Thread.Sleep(120);
                        continue;
                    }

                    if (scriptSentAt is { } sentAt &&
                        DateTime.UtcNow - sentAt >= TimeSpan.FromSeconds(3) &&
                        resendRetries < 2)
                    {
                        TrySend(script);
                        TrySend("\r");
                        resendRetries++;
                        captured.SetLength(0);
                        recentText = "";
                        scriptSentAt = DateTime.UtcNow;
                        lastEnter = DateTime.UtcNow;
                        Thread.Sleep(220);
                        continue;
                    }
                }

                if (sawStatus)
                {
                    DrainFor(TimeSpan.FromSeconds(2));
                    return;
                }

                if (IsProcessExited() && chunks.IsEmpty)
                {
                    DrainFor(TimeSpan.FromMilliseconds(200));
                    break;
                }

                outputReady.WaitOne(TimeSpan.FromMilliseconds(120));
            }

            if (captured.Length == 0)
            {
                throw new TimeoutException("PTY command timed out.");
            }
        }

        private bool DrainChunks(Action<byte[]>? afterAppend = null)
        {
            var hadData = false;
            while (chunks.TryDequeue(out var chunk))
            {
                hadData = true;
                captured.Write(chunk, 0, chunk.Length);
                lastOutputAt = DateTime.UtcNow;
                var text = Encoding.UTF8.GetString(chunk);
                recentText += text;
                if (recentText.Length > 16384)
                {
                    recentText = recentText[^16384..];
                }
                afterAppend?.Invoke(chunk);
            }

            return hadData;
        }

        private void DrainFor(TimeSpan duration)
        {
            var until = DateTime.UtcNow.Add(duration);
            while (DateTime.UtcNow < until)
            {
                DrainChunks(ProcessCursorQuery);
                outputReady.WaitOne(TimeSpan.FromMilliseconds(40));
            }
        }

        private void ProcessCursorQuery(byte[] chunk)
        {
            if (IndexOf(chunk, CursorQuery) >= 0 || recentText.Contains("\u001b[6n", StringComparison.Ordinal))
            {
                TrySend("\u001b[1;1R");
            }
        }

        private void ProcessConfiguredSends()
        {
            foreach (var item in request.Options.SendOnSubstrings)
            {
                if (triggeredSends.Contains(item.Key))
                {
                    continue;
                }

                if (recentText.Contains(item.Key, StringComparison.Ordinal))
                {
                    TrySend(item.Value);
                    triggeredSends.Add(item.Key);
                }
            }
        }

        private bool ContainsStopNeedle(IEnumerable<string> needles) =>
            needles.Any(needle => !string.IsNullOrEmpty(needle) &&
                recentText.Contains(needle, StringComparison.Ordinal));

        private bool ContainsAnyUrl() =>
            recentText.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            recentText.Contains("http://", StringComparison.OrdinalIgnoreCase);

        private bool IsCodexStatusRequest()
        {
            var name = Path.GetFileNameWithoutExtension(request.Binary);
            return request.Options.ForceCodexStatusMode ||
                (string.Equals(name, "codex", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(request.Script.Trim(), "/status", StringComparison.OrdinalIgnoreCase));
        }

        private void Send(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            input.Write(bytes, 0, bytes.Length);
            input.Flush();
        }

        private void TrySend(string text)
        {
            try
            {
                Send(text);
            }
            catch
            {
                // The child may already have exited.
            }
        }

        private void ReadOutput()
        {
            var buffer = new byte[8192];
            while (!readerStop.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = output.Read(buffer, 0, buffer.Length);
                }
                catch
                {
                    return;
                }

                if (read <= 0)
                {
                    return;
                }

                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                chunks.Enqueue(chunk);
                outputReady.Set();
            }
        }

        private bool IsProcessExited() => WaitForSingleObject(processHandle, 0) == WaitObject0;

        private void WaitForExitOrKill(uint milliseconds)
        {
            var result = WaitForSingleObject(processHandle, milliseconds);
            if (result == WaitTimeout)
            {
                TerminateProcess(processHandle, 1);
                WaitForSingleObject(processHandle, 2000);
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var matched = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref StartupInfoEx lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(
        Coord size,
        IntPtr hInput,
        IntPtr hOutput,
        uint dwFlags,
        out IntPtr phPC);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr HProcess;
        public IntPtr HThread;
        public int ProcessId;
        public int ThreadId;
    }
}

internal sealed class PtyBridgeRequest
{
    public string Binary { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public string Script { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public PtyBridgeOptions Options { get; set; } = new();
}

internal sealed class PtyBridgeOptions
{
    public int Rows { get; set; } = 50;
    public int Cols { get; set; } = 160;
    public double Timeout { get; set; } = 20;
    public double? IdleTimeout { get; set; }
    public double InitialDelay { get; set; } = 0.4;
    public double? SendEnterEvery { get; set; }
    public Dictionary<string, string> SendOnSubstrings { get; set; } = new(StringComparer.Ordinal);
    public bool StopOnUrl { get; set; }
    public List<string> StopOnSubstrings { get; set; } = [];
    public double SettleAfterStop { get; set; } = 0.25;
    public bool ForceCodexStatusMode { get; set; }
}

internal sealed class PtyBridgeResponse
{
    public bool Ok { get; init; }
    public string Text { get; init; } = "";
    public string Error { get; init; } = "";

    public static PtyBridgeResponse Succeeded(string text) => new()
    {
        Ok = true,
        Text = text,
    };

    public static PtyBridgeResponse Failed(string error) => new()
    {
        Ok = false,
        Error = error,
    };
}
