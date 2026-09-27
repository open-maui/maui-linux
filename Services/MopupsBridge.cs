// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Mopups popups on Linux. When the app uses Mopups, an <c>IPopupPlatform</c>
/// is registered in its services; Mopups' platform-neutral build (from
/// MarketAlly.Mopups 3.0.12) takes it from there. Popups are shown as window
/// layers without modal navigation, as the native Mopups platforms show them:
/// the page beneath gets no Disappearing/Appearing (pages that reload in
/// OnAppearing would otherwise lose unsaved edits every time a dialog opened).
/// No compile-time reference: Mopups is optional.
/// </summary>
internal static class MopupsBridge
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Lazy<Type?> s_popupPageType = new(() => Type.GetType("Mopups.Pages.PopupPage, Mopups"));

    internal static void Register(IServiceCollection services)
    {
        try
        {
            var contract = Type.GetType("Mopups.Interfaces.IPopupPlatform, Mopups");
            if (contract == null)
                return; // Mopups is not part of the app
            var platform = DispatchProxy.Create(contract, typeof(OpenMauiPopupPlatform));
            services.AddSingleton(contract, platform);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Mopups", "Registering the Linux popup platform failed", ex);
        }
    }

    /// <summary>PopupPage.BackgroundInputTransparent: clicks on the backdrop reach the page beneath.</summary>
    internal static bool IsBackgroundInputTransparent(Page page) =>
        s_popupPageType.Value is { } type && type.IsInstanceOfType(page)
        && type.GetProperty("BackgroundInputTransparent", Any)?.GetValue(page) is true;

    /// <summary>PopupPage.SendBackgroundClick: BackgroundClicked, its command, close when set to.</summary>
    internal static void SendBackgroundClick(Page page)
    {
        try
        {
            s_popupPageType.Value?.GetMethod("SendBackgroundClick", Any, Type.EmptyTypes)?.Invoke(page, null);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Mopups", "Popup background click failed", ex);
        }
    }
}

/// <summary>Mopups' IPopupPlatform: AddAsync(page[, window]), RemoveAsync(page).</summary>
internal class OpenMauiPopupPlatform : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        args ??= Array.Empty<object?>();
        if (args.Length == 0 || args[0] is not Page page)
            return Task.CompletedTask;

        try
        {
            switch (method.Name)
            {
                case "AddAsync":
                    Add(page, args.Length > 1 ? args[1] as Microsoft.Maui.Controls.Window : null);
                    break;
                case "RemoveAsync":
                    Remove(page);
                    break;
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private static void Add(Page page, Microsoft.Maui.Controls.Window? window)
    {
        var app = LinuxApplication.Current ?? throw new InvalidOperationException("OpenMaui is not running.");
        var context = window == null ? app.PrimaryContext : app.WindowContexts.FirstOrDefault(c => ReferenceEquals(c.MauiWindow, window)) ?? app.PrimaryContext;
        if (context == null)
            throw new InvalidOperationException("There is no window to show the popup in.");

        // Parent the popup under the window's page, as the Windows platform
        // does, so resources and inherited values resolve.
        if (page.Parent == null && (window ?? context.MauiWindow as Microsoft.Maui.Controls.Window)?.Page is { } owner)
            owner.AddLogicalChild(page);

        context.PushPopupView(page);
    }

    private static void Remove(Page page)
    {
        var app = LinuxApplication.Current;
        if (app != null)
        {
            foreach (var context in app.WindowContexts)
            {
                if (context.PopPopupView(page))
                    break;
            }
        }
        (page.Parent as Element)?.RemoveLogicalChild(page);
    }
}
