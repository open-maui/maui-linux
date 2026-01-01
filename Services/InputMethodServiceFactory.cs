// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Factory for creating the appropriate Input Method service.
/// Automatically selects IBus or XIM based on availability.
/// </summary>
public static class InputMethodServiceFactory
{
    private static IInputMethodService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton input method service instance.
    /// </summary>
    public static IInputMethodService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= CreateService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Creates the most appropriate input method service for the current environment.
    /// </summary>
    public static IInputMethodService CreateService()
    {
        // Check environment variable for user preference
        var imePreference = Environment.GetEnvironmentVariable("MAUI_INPUT_METHOD");

        if (!string.IsNullOrEmpty(imePreference))
        {
            return imePreference.ToLowerInvariant() switch
            {
                "ibus" => CreateIBusService(),
                "xim" => CreateXIMService(),
                "none" => new NullInputMethodService(),
                _ => CreateAutoService()
            };
        }

        return CreateAutoService();
    }

    private static IInputMethodService CreateAutoService()
    {
        // Try IBus first (most common on modern Linux)
        if (IsIBusAvailable())
        {
            Console.WriteLine("InputMethodServiceFactory: Using IBus");
            return CreateIBusService();
        }

        // Fall back to XIM
        if (IsXIMAvailable())
        {
            Console.WriteLine("InputMethodServiceFactory: Using XIM");
            return CreateXIMService();
        }

        // No IME available
        Console.WriteLine("InputMethodServiceFactory: No IME available, using null service");
        return new NullInputMethodService();
    }

    private static IInputMethodService CreateIBusService()
    {
        try
        {
            return new IBusInputMethodService();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"InputMethodServiceFactory: Failed to create IBus service - {ex.Message}");
            return new NullInputMethodService();
        }
    }

    private static IInputMethodService CreateXIMService()
    {
        try
        {
            return new X11InputMethodService();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"InputMethodServiceFactory: Failed to create XIM service - {ex.Message}");
            return new NullInputMethodService();
        }
    }

    private static bool IsIBusAvailable()
    {
        // Check if IBus daemon is running
        var ibusAddress = Environment.GetEnvironmentVariable("IBUS_ADDRESS");
        if (!string.IsNullOrEmpty(ibusAddress))
        {
            return true;
        }

        // Try to load IBus library
        try
        {
            var handle = NativeLibrary.Load("libibus-1.0.so.5");
            NativeLibrary.Free(handle);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsXIMAvailable()
    {
        // Check XMODIFIERS environment variable
        var xmodifiers = Environment.GetEnvironmentVariable("XMODIFIERS");
        if (!string.IsNullOrEmpty(xmodifiers) && xmodifiers.Contains("@im="))
        {
            return true;
        }

        // Check if running under X11
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        return !string.IsNullOrEmpty(display);
    }

    /// <summary>
    /// Resets the singleton instance (useful for testing).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _instance?.Shutdown();
            _instance = null;
        }
    }
}

/// <summary>
/// Null implementation of IInputMethodService for when no IME is available.
/// </summary>
public class NullInputMethodService : IInputMethodService
{
    public bool IsActive => false;
    public string PreEditText => string.Empty;
    public int PreEditCursorPosition => 0;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    public void Initialize(nint windowHandle) { }
    public void SetFocus(IInputContext? context) { }
    public void SetCursorLocation(int x, int y, int width, int height) { }
    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown) => false;
    public void Reset() { }
    public void Shutdown() { }
}
