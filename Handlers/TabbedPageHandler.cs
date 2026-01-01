// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for TabbedPage on Linux using Skia rendering.
/// Maps ITabbedView interface to SkiaTabbedPage platform view.
/// </summary>
public partial class TabbedPageHandler : ViewHandler<ITabbedView, SkiaTabbedPage>
{
    public static IPropertyMapper<ITabbedView, TabbedPageHandler> Mapper = new PropertyMapper<ITabbedView, TabbedPageHandler>(ViewHandler.ViewMapper)
    {
    };

    public static CommandMapper<ITabbedView, TabbedPageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public TabbedPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public TabbedPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaTabbedPage CreatePlatformView()
    {
        return new SkiaTabbedPage();
    }

    protected override void ConnectHandler(SkiaTabbedPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SelectedIndexChanged += OnSelectedIndexChanged;
    }

    protected override void DisconnectHandler(SkiaTabbedPage platformView)
    {
        platformView.SelectedIndexChanged -= OnSelectedIndexChanged;
        platformView.ClearTabs();
        base.DisconnectHandler(platformView);
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        // Notify the virtual view of selection change
    }
}
