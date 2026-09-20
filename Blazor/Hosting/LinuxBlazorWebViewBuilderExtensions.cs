// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Blazor.Handlers;

namespace Microsoft.Maui.Platform.Linux.Blazor.Hosting;

/// <summary>
/// Registers the Linux BlazorWebView handler. Call after <c>UseLinux()</c>:
/// <code>
/// builder.UseMauiApp&lt;App&gt;().UseLinux().UseLinuxBlazorWebView();
/// </code>
/// Replaces <c>AddMauiBlazorWebView()</c> on Linux: it adds the Blazor WebView
/// services and maps <see cref="BlazorWebView"/> to the WPE-backed handler.
/// No-op on other operating systems.
/// </summary>
public static class LinuxBlazorWebViewBuilderExtensions
{
    public static MauiAppBuilder UseLinuxBlazorWebView(this MauiAppBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
            return builder;

        builder.Services.AddBlazorWebView();
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(BlazorWebView), typeof(LinuxBlazorWebViewHandler));
        });
        return builder;
    }
}
