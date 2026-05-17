using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexBar.Windows;

internal static class WindowsCredentialStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const string TargetPrefix = "CodexBar-Windows/api-key/";

    public static void WriteApiKey(string provider, string apiKey)
    {
        var cleaned = apiKey.Trim();
        if (cleaned.Length == 0)
        {
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        }

        var secretBytes = Encoding.Unicode.GetBytes(cleaned);
        var blob = Marshal.AllocCoTaskMem(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);
            var credential = new Credential
            {
                Type = CredentialTypeGeneric,
                TargetName = TargetName(provider),
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = Environment.UserName,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw LastWin32Exception("Could not save the API key to Windows Credential Manager.");
            }
        }
        finally
        {
            ZeroAndFree(blob, secretBytes.Length);
        }
    }

    public static string? ReadApiKey(string provider)
    {
        if (!CredRead(TargetName(provider), CredentialTypeGeneric, 0, out var credentialPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
            return Encoding.Unicode.GetString(secretBytes).Trim();
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static bool HasApiKey(string provider) =>
        !string.IsNullOrWhiteSpace(ReadApiKey(provider));

    public static void DeleteApiKey(string provider)
    {
        if (!CredDelete(TargetName(provider), CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "Could not remove the API key from Windows Credential Manager.");
            }
        }
    }

    private static string TargetName(string provider) =>
        $"{TargetPrefix}{provider.Trim().ToLowerInvariant()}";

    private static Win32Exception LastWin32Exception(string message)
    {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, message);
    }

    private static void ZeroAndFree(IntPtr buffer, int byteCount)
    {
        for (var index = 0; index < byteCount; index++)
        {
            Marshal.WriteByte(buffer, index, 0);
        }

        Marshal.FreeCoTaskMem(buffer);
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string targetName,
        uint type,
        uint reservedFlag,
        out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
