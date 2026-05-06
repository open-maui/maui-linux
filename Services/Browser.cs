// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Static Browser class providing MAUI-compatible API for opening URLs.
/// </summary>
public static class Browser
{
    private static IBrowser? _browser;

    /// <summary>
    /// Gets or sets the browser implementation. Set during app initialization.
    /// </summary>
    public static IBrowser Default
    {
        get => _browser ??= new BrowserService();
        set => _browser = value;
    }

    /// <summary>
    /// Opens the specified URI in the default browser.
    /// </summary>
    public static Task<bool> OpenAsync(string uri)
    {
        return Default.OpenAsync(uri);
    }

    /// <summary>
    /// Opens the specified URI in the default browser with the specified launch mode.
    /// </summary>
    public static Task<bool> OpenAsync(string uri, BrowserLaunchMode launchMode)
    {
        return Default.OpenAsync(uri, launchMode);
    }

    /// <summary>
    /// Opens the specified URI in the default browser.
    /// </summary>
    public static Task<bool> OpenAsync(Uri uri)
    {
        return Default.OpenAsync(uri);
    }

    /// <summary>
    /// Opens the specified URI in the default browser with the specified launch mode.
    /// </summary>
    public static Task<bool> OpenAsync(Uri uri, BrowserLaunchMode launchMode)
    {
        return Default.OpenAsync(uri, launchMode);
    }

    /// <summary>
    /// Opens the specified URI in the default browser with the specified options.
    /// </summary>
    public static Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options)
    {
        return Default.OpenAsync(uri, options);
    }
}
