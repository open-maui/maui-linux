// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The keyring through libsecret's <c>secret-tool</c>, one process per call (serialized in this
/// process). The fallback when the Secret Service cannot be used over D-Bus directly.
/// </summary>
internal sealed class SecretToolKeyring : ISecretKeyring
{
    public string Name => "secret-tool";

    /// <summary>Whether <c>secret-tool</c> is on PATH.</summary>
    public static bool IsInstalled()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "secret-tool",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The messages secret-tool prints when no Secret Service can be reached at all.</summary>
    internal static bool IsUnreachable(string error) =>
        error.Contains("Could not connect", StringComparison.OrdinalIgnoreCase)
        || error.Contains("not provided by any .service files", StringComparison.OrdinalIgnoreCase)
        || error.Contains("Cannot autolaunch", StringComparison.OrdinalIgnoreCase)
        || error.Contains("Unable to autolaunch", StringComparison.OrdinalIgnoreCase);

    public Task<string?> LookupAsync(string ns, string key) => Task.Run(() => Lookup(ns, key));

    public async Task StoreAsync(string ns, string key, string value)
    {
        var (exit, error) = await Task.Run(() =>
        {
            // A keyring daemon can drop the session a store opened ("Can't find session")
            // when clients come and go quickly; a store is idempotent, so try again.
            (int Code, string Error) attempt = default;
            for (var i = 0; i < 3; i++)
            {
                var (code, _, err) = RunCapture(value, "store", "--label=" + key, "service", ns, "key", key);
                attempt = (code, err);
                if (code is 0 or -1 || IsUnreachable(err))
                    break;
                Thread.Sleep(50 * (i + 1));
            }
            return attempt;
        }).ConfigureAwait(false);

        if (exit == -1 || IsUnreachable(error))
            throw new SecretServiceUnavailableException(exit == -1 ? $"secret-tool could not be started ({error.Trim()})" : error.Trim());
        if (exit != 0)
            throw new InvalidOperationException($"Failed to store secret: {error}");
        // A keyring daemon (KDE's ksecretd) can acknowledge a store before a lookup sees it:
        // return once it reads back, so a value set is a value read, as on the other platforms.
        await Task.Run(() => WaitUntilVisible(ns, key, value)).ConfigureAwait(false);
    }

    public Task<bool> ClearAsync(string ns, string key) => Task.Run(() =>
    {
        var existed = Lookup(ns, key) != null;
        if (existed)
        {
            Run(null, "clear", "service", ns, "key", key);
            WaitUntilVisible(ns, key, null);
        }
        return existed;
    });

    public Task ClearAllAsync(string ns) => Task.Run(() =>
    {
        var (exit, _, error) = RunCapture(null, "clear", "service", ns);
        if (exit == -1 || IsUnreachable(error))
            throw new SecretServiceUnavailableException(exit == -1 ? $"secret-tool could not be started ({error.Trim()})" : error.Trim());
        // As for a single key: return once the keyring no longer lists the app's items.
        for (var attempt = 0; attempt < 20 && HasItems(ns); attempt++)
            Thread.Sleep(50);
    });

    /// <summary>True while the keyring lists any item under <paramref name="ns"/> (attributes go to stderr).</summary>
    private static bool HasItems(string ns)
    {
        var (exit, _, error) = RunCapture(null, "search", "--all", "service", ns);
        return exit == 0 && error.Contains("attribute.key = ", StringComparison.Ordinal);
    }

    private static string? Lookup(string ns, string key)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (exit, output, error) = RunCapture(null, "lookup", "service", ns, "key", key);
            // secret-tool prints the secret without a trailing newline when stdout is not a
            // terminal; older versions add one.
            if (exit == 0)
                return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\n');
            if (exit == -1 || IsUnreachable(error))
                throw new SecretServiceUnavailableException(exit == -1 ? $"secret-tool could not be started ({error.Trim()})" : error.Trim());
            // Not found is a silent exit 1. An error message means the daemon failed the call
            // (a dropped session under load): ask again rather than report "no value".
            if (string.IsNullOrWhiteSpace(error) || attempt == 2)
                return null;
            Thread.Sleep(50 * (attempt + 1));
        }
    }

    /// <summary>Waits (about a second at most) until a lookup of <paramref name="key"/> gives <paramref name="expected"/> (null: gone).</summary>
    private static void WaitUntilVisible(string ns, string key, string? expected)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (Lookup(ns, key) == expected)
                return;
            Thread.Sleep(50);
        }
        DiagnosticLog.Warn("SecureStorageService", $"The keyring did not reflect the change to '{key}' within a second");
    }

    private static int Run(string? input, params string[] args) => RunCapture(input, args).Exit;

    // One secret-tool at a time per process: the keyring daemon drops sessions under many
    // concurrent clients ("Can't find session"), and a lookup racing a clear of the same item
    // could see the value after Remove returned.
    private static readonly object s_secretToolLock = new();

    /// <summary>Runs secret-tool with <paramref name="args"/> (no shell); exit -1 when it could not start.</summary>
    private static (int Exit, string Output, string Error) RunCapture(string? input, params string[] args)
    {
        lock (s_secretToolLock)
            return RunCaptureLocked(input, args);
    }

    private static (int Exit, string Output, string Error) RunCaptureLocked(string? input, string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "secret-tool",
                UseShellExecute = false,
                RedirectStandardInput = input != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (-1, "", "");
            if (input != null)
            {
                process.StandardInput.Write(input);
                process.StandardInput.Close();
            }
            var errorTask = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, errorTask.GetAwaiter().GetResult());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (-1, "", ex.Message);
        }
    }
}
