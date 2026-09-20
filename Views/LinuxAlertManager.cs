// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Bridges <c>Page.DisplayAlert</c> / <c>DisplayPromptAsync</c> /
/// <c>DisplayActionSheet</c> to the Skia dialogs in
/// <see cref="LinuxDialogService"/>.
/// </summary>
/// <remarks>
/// How MAUI 10 dispatches alerts on the generic (non-platform) TFM: each
/// <see cref="Microsoft.Maui.Controls.Window"/> owns an internal
/// <c>AlertManager</c>. When the window's Page gets a handler, the manager
/// subscribes by resolving, in order, an <c>IAlertManagerSubscription</c>
/// service (internal interface), then keyed delegate services
/// (<see cref="DisplayAlertServiceKey"/> and friends), then a built-in no-op.
/// <c>Page.DisplayAlertAsync</c> builds <see cref="AlertArguments"/> and calls
/// <c>Window.AlertManager.RequestAlert(page, args)</c>; the delegate
/// subscription invokes the keyed <c>Func</c> and forwards its Task to
/// <c>args.Result</c>. The subscription only happens when the Window itself
/// has a handler with a MauiContext, which <see cref="WindowContext"/>
/// guarantees by attaching the Linux WindowHandler when it adopts a window.
/// </remarks>
public static class LinuxAlertManager
{
    /// <summary>Keyed-service key MAUI probes for the DisplayAlert delegate.</summary>
    public const string DisplayAlertServiceKey = "Microsoft.Maui.Controls.DisplayAlert";

    /// <summary>Keyed-service key MAUI probes for the DisplayActionSheet delegate.</summary>
    public const string DisplayActionSheetServiceKey = "Microsoft.Maui.Controls.DisplayActionSheet";

    /// <summary>Keyed-service key MAUI probes for the DisplayPrompt delegate.</summary>
    public const string DisplayPromptServiceKey = "Microsoft.Maui.Controls.DisplayPrompt";

    /// <summary>
    /// Registers the three keyed delegates MAUI's AlertManager looks up. Call
    /// once from the platform's service registration (idempotent).
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        services.TryAddKeyedSingleton<Func<Page, AlertArguments, Task<bool>>>(
            DisplayAlertServiceKey, (_, _) => ShowAlertAsync);
        services.TryAddKeyedSingleton<Func<Page, ActionSheetArguments, Task<string>>>(
            DisplayActionSheetServiceKey, (_, _) => ShowActionSheetAsync);
        services.TryAddKeyedSingleton<Func<Page, PromptArguments, Task<string>>>(
            DisplayPromptServiceKey, (_, _) => ShowPromptAsync);
    }

    /// <summary>Shows the Skia alert for a <c>Page.DisplayAlert</c> request.</summary>
    public static Task<bool> ShowAlertAsync(Page page, AlertArguments args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        DiagnosticLog.Debug("LinuxAlertManager", $"DisplayAlert '{args.Title}' from {page?.GetType().Name}");
        return LinuxDialogService.ShowAlertAsync(
            args.Title ?? string.Empty,
            args.Message ?? string.Empty,
            args.Accept,
            args.Cancel);
    }

    /// <summary>
    /// Shows the Skia prompt for a <c>Page.DisplayPromptAsync</c> request.
    /// <c>MaxLength</c> is enforced by the dialog; <c>Keyboard</c> has no
    /// effect (desktop keyboards are not switchable).
    /// </summary>
    public static Task<string> ShowPromptAsync(Page page, PromptArguments args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        DiagnosticLog.Debug("LinuxAlertManager", $"DisplayPrompt '{args.Title}' from {page?.GetType().Name}");
        // The delegate's Task<string> completes with null on cancel, which is
        // exactly what DisplayPromptAsync documents.
        return LinuxDialogService.ShowPromptAsync(
            args.Title ?? string.Empty,
            args.Message ?? string.Empty,
            args.Accept,
            args.Cancel,
            args.InitialValue ?? string.Empty,
            args.MaxLength,
            args.Placeholder)!;
    }

    /// <summary>Shows the Skia action sheet for a <c>Page.DisplayActionSheet</c> request.</summary>
    public static Task<string> ShowActionSheetAsync(Page page, ActionSheetArguments args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        DiagnosticLog.Debug("LinuxAlertManager", $"DisplayActionSheet '{args.Title}' from {page?.GetType().Name}");
        return LinuxDialogService.ShowActionSheetAsync(
            args.Title,
            args.Cancel,
            args.Destruction,
            args.Buttons)!;
    }
}
