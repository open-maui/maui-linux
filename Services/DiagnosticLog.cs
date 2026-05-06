// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Centralized diagnostic logging for the Linux MAUI platform.
/// Logging is enabled only in DEBUG builds by default, or when
/// explicitly enabled via <see cref="IsEnabled"/>.
/// </summary>
public static class DiagnosticLog
{
    private static bool? _isEnabled;

    /// <summary>
    /// Gets or sets whether diagnostic logging is enabled.
    /// Defaults to true in DEBUG builds, false in RELEASE builds.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            if (_isEnabled.HasValue)
                return _isEnabled.Value;
#if DEBUG
            return true;
#else
            return false;
#endif
        }
        set => _isEnabled = value;
    }

    /// <summary>
    /// Logs an informational diagnostic message.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Debug(string tag, string message)
    {
        if (IsEnabled)
            System.Console.WriteLine($"[{tag}] {message}");
    }

    /// <summary>
    /// Logs a debug diagnostic message with exception details.
    /// Only compiled in DEBUG builds.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Debug(string tag, string message, Exception ex)
    {
        if (IsEnabled)
            System.Console.WriteLine($"[{tag}] {message}: {ex.Message}");
    }

    /// <summary>
    /// Logs an informational diagnostic message (always writes when enabled, not conditional on DEBUG).
    /// Use for important operational messages that should appear in release builds when logging is enabled.
    /// </summary>
    public static void Info(string tag, string message)
    {
        if (IsEnabled)
            System.Console.WriteLine($"[{tag}] {message}");
    }

    /// <summary>
    /// Logs a warning message. Always writes when logging is enabled.
    /// </summary>
    public static void Warn(string tag, string message)
    {
        if (IsEnabled)
            System.Console.Error.WriteLine($"[{tag}] WARNING: {message}");
    }

    /// <summary>
    /// Logs an error message. Always writes regardless of IsEnabled.
    /// </summary>
    public static void Error(string tag, string message)
    {
        System.Console.Error.WriteLine($"[{tag}] ERROR: {message}");
    }

    /// <summary>
    /// Logs an error message with exception details. Always writes regardless of IsEnabled.
    /// </summary>
    public static void Error(string tag, string message, Exception ex)
    {
        System.Console.Error.WriteLine($"[{tag}] ERROR: {message}: {ex.Message}");
    }
}
