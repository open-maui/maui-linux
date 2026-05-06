// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Hosting;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class HandlerMappingExtensions
{
    public static IMauiHandlersCollection AddHandler<TView, THandler>(this IMauiHandlersCollection handlers)
        where TView : class
        where THandler : class
    {
        handlers.AddHandler(typeof(TView), typeof(THandler));
        return handlers;
    }
}
