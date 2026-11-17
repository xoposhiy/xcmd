// @implements ExternalLaunch
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Xcmd.Infrastructure;

internal sealed class WindowsLaunchEnvironment(Func<IReadOnlyDictionary<string, string>>? readEnvironment = null)
{
    private static readonly object synchronization = new();
    private readonly Func<IReadOnlyDictionary<string, string>> readCurrent = readEnvironment ?? ReadCurrent;
    private readonly HashSet<string> managedNames = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine).Keys.Cast<string>()
        .Concat(Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Keys.Cast<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Run(Action launch)
    {
        // ShellExecute cannot take an environment block. Keep the temporary override serialized until the child is created.
        lock (synchronization)
        {
            var fresh = readCurrent();
            managedNames.UnionWith(fresh.Keys);
            var saved = managedNames.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var name in managedNames) Environment.SetEnvironmentVariable(name, fresh.GetValueOrDefault(name));
                launch();
            }
            finally
            {
                foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static Dictionary<string, string> ReadCurrent()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Shell launch requires Windows.");
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        if (!CreateEnvironmentBlock(out var block, identity.Token, false))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the current user and system environment.");
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var cursor = block; Marshal.PtrToStringUni(cursor) is { Length: > 0 } entry; cursor += (entry.Length + 1) * sizeof(char))
            {
                var separator = entry.IndexOf('=');
                if (separator > 0) values[entry[..separator]] = entry[(separator + 1)..];
            }
            return values;
        }
        finally { DestroyEnvironmentBlock(block); }
    }

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out nint environment, nint token, [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(nint environment);
}
