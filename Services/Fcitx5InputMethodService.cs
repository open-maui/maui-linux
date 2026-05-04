// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Fcitx5 Input Method service using D-Bus interface.
/// Provides IME support for systems using Fcitx5 (common on some distros).
/// </summary>
public class Fcitx5InputMethodService : IInputMethodService, IDisposable
{
    private IInputContext? _currentContext;
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private bool _isActive;
    private bool _disposed;
    private Process? _dBusMonitor;
    private string? _inputContextPath;

    public bool IsActive => _isActive;
    public string PreEditText => _preEditText;
    public int PreEditCursorPosition => _preEditCursorPosition;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    public void Initialize(nint windowHandle)
    {
        try
        {
            // Create input context via D-Bus
            var output = RunDBusCommand(
                "call --session " +
                "--dest org.fcitx.Fcitx5 " +
                "--object-path /org/freedesktop/portal/inputmethod " +
                "--method org.fcitx.Fcitx.InputMethod1.CreateInputContext " +
                "\"maui-linux\" \"\"");

            if (!string.IsNullOrEmpty(output) && output.Contains("/"))
            {
                // Parse the object path from output like: (objectpath '/org/fcitx/...',)
                var start = output.IndexOf("'/");
                var end = output.IndexOf("'", start + 1);
                if (start >= 0 && end > start)
                {
                    _inputContextPath = output.Substring(start + 1, end - start - 1);
                    Console.WriteLine($"Fcitx5InputMethodService: Created context at {_inputContextPath}");
                    StartMonitoring();
                }
            }
            else
            {
                Console.WriteLine("Fcitx5InputMethodService: Failed to create input context");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fcitx5InputMethodService: Initialization failed - {ex.Message}");
        }
    }

    private void StartMonitoring()
    {
        if (string.IsNullOrEmpty(_inputContextPath)) return;

        Task.Run(async () =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dbus-monitor",
                    Arguments = $"--session \"path='{_inputContextPath}'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                _dBusMonitor = Process.Start(startInfo);
                if (_dBusMonitor == null) return;

                var reader = _dBusMonitor.StandardOutput;
                while (!_disposed && !_dBusMonitor.HasExited)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    // Parse signals for commit and preedit
                    if (line.Contains("CommitString"))
                    {
                        await ProcessCommitSignal(reader);
                    }
                    else if (line.Contains("UpdatePreedit"))
                    {
                        await ProcessPreeditSignal(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fcitx5InputMethodService: Monitor error - {ex.Message}");
            }
        });
    }

    private async Task ProcessCommitSignal(StreamReader reader)
    {
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.Contains("string"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"string\s+""([^""]*)""");
                    if (match.Success)
                    {
                        var text = match.Groups[1].Value;
                        _preEditText = string.Empty;
                        _preEditCursorPosition = 0;
                        _isActive = false;

                        TextCommitted?.Invoke(this, new TextCommittedEventArgs(text));
                        _currentContext?.OnTextCommitted(text);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    private async Task ProcessPreeditSignal(StreamReader reader)
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.Contains("string"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"string\s+""([^""]*)""");
                    if (match.Success)
                    {
                        _preEditText = match.Groups[1].Value;
                        _isActive = !string.IsNullOrEmpty(_preEditText);

                        PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition, new List<PreEditAttribute>()));
                        _currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    public void SetFocus(IInputContext? context)
    {
        _currentContext = context;

        if (!string.IsNullOrEmpty(_inputContextPath))
        {
            if (context != null)
            {
                RunDBusCommand(
                    $"call --session --dest org.fcitx.Fcitx5 " +
                    $"--object-path {_inputContextPath} " +
                    $"--method org.fcitx.Fcitx.InputContext1.FocusIn");
            }
            else
            {
                RunDBusCommand(
                    $"call --session --dest org.fcitx.Fcitx5 " +
                    $"--object-path {_inputContextPath} " +
                    $"--method org.fcitx.Fcitx.InputContext1.FocusOut");
            }
        }
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
        if (string.IsNullOrEmpty(_inputContextPath)) return;

        RunDBusCommand(
            $"call --session --dest org.fcitx.Fcitx5 " +
            $"--object-path {_inputContextPath} " +
            $"--method org.fcitx.Fcitx.InputContext1.SetCursorRect " +
            $"{x} {y} {width} {height}");
    }

    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
    {
        if (string.IsNullOrEmpty(_inputContextPath)) return false;

        uint state = ConvertModifiers(modifiers);
        if (!isKeyDown) state |= 0x40000000; // Release flag

        var result = RunDBusCommand(
            $"call --session --dest org.fcitx.Fcitx5 " +
            $"--object-path {_inputContextPath} " +
            $"--method org.fcitx.Fcitx.InputContext1.ProcessKeyEvent " +
            $"{keyCode} {keyCode} {state} {(isKeyDown ? "true" : "false")} 0");

        return result?.Contains("true") == true;
    }

    private uint ConvertModifiers(KeyModifiers modifiers)
    {
        uint state = 0;
        if (modifiers.HasFlag(KeyModifiers.Shift)) state |= 1;
        if (modifiers.HasFlag(KeyModifiers.CapsLock)) state |= 2;
        if (modifiers.HasFlag(KeyModifiers.Control)) state |= 4;
        if (modifiers.HasFlag(KeyModifiers.Alt)) state |= 8;
        if (modifiers.HasFlag(KeyModifiers.Super)) state |= 64;
        return state;
    }

    public void Reset()
    {
        if (!string.IsNullOrEmpty(_inputContextPath))
        {
            RunDBusCommand(
                $"call --session --dest org.fcitx.Fcitx5 " +
                $"--object-path {_inputContextPath} " +
                $"--method org.fcitx.Fcitx.InputContext1.Reset");
        }

        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        _isActive = false;

        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
    }

    public void Shutdown()
    {
        Dispose();
    }

    private string? RunDBusCommand(string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            return output;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _dBusMonitor?.Kill();
            _dBusMonitor?.Dispose();
        }
        catch { }

        if (!string.IsNullOrEmpty(_inputContextPath))
        {
            RunDBusCommand(
                $"call --session --dest org.fcitx.Fcitx5 " +
                $"--object-path {_inputContextPath} " +
                $"--method org.fcitx.Fcitx.InputContext1.Destroy");
        }
    }

    /// <summary>
    /// Checks if Fcitx5 is available on the system.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = "introspect --session --dest org.fcitx.Fcitx5 --object-path /org/freedesktop/portal/inputmethod",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
