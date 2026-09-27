// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Creates an application's startup window the way MAUI's own platform hosts
/// do: <see cref="IApplication.CreateWindow"/> receives an
/// <see cref="ActivationState"/> carrying the platform <see cref="IMauiContext"/>.
/// </summary>
/// <remarks>
/// <see cref="Microsoft.Maui.Controls.Application"/>'s default CreateWindow
/// resolves an <c>IWindowCreator</c> from <c>activationState.Context.Services</c>.
/// Frameworks that own window creation (Prism's <c>UsePrism(...).CreateWindow(...)</c>)
/// register one and leave CreateWindow un-overridden; with a null activation
/// state their window was never created and the app started without a page.
/// </remarks>
internal static class StartupWindowFactory
{
    public static IWindow Create(IApplication application, IMauiContext mauiContext)
        => application.CreateWindow(new ActivationState(mauiContext));
}
