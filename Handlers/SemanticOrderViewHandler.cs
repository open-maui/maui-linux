// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for CommunityToolkit.Maui's <c>SemanticOrderView</c>: a ContentView whose
/// <c>ViewOrder</c> sets the order assistive technology reads the views inside it in. The
/// toolkit's platform-neutral handler ignores ViewOrder (its Windows handler gives the listed views
/// TabIndex 1..n); here it orders the AT-SPI children of the Skia tree below the view
/// (<see cref="SkiaView.SetAccessibleOrder"/>): listed views first, in ViewOrder, the others
/// after. Registered automatically when the toolkit is part of the app; OpenMaui takes no
/// dependency on it.
/// </summary>
public class SemanticOrderViewHandler : ContentViewHandler
{
    private const string SemanticOrderViewTypeName = "CommunityToolkit.Maui.Views.SemanticOrderView, CommunityToolkit.Maui";
    private const string ContractName = "CommunityToolkit.Maui.Core.ISemanticOrderView";

    public static new IPropertyMapper<IContentView, SemanticOrderViewHandler> Mapper =
        new PropertyMapper<IContentView, SemanticOrderViewHandler>(ContentViewHandler.Mapper)
        {
            ["ViewOrder"] = MapViewOrder,
        };

    public SemanticOrderViewHandler() : base(Mapper, CommandMapper)
    {
    }

    /// <summary>
    /// The toolkit's SemanticOrderView type when CommunityToolkit.Maui is loadable in this app,
    /// otherwise null (the handler is then never registered).
    /// </summary>
    internal static Type? ToolkitSemanticOrderViewType { get; } = ResolveToolkitType();

    private static Type? ResolveToolkitType()
    {
        try
        {
            return Type.GetType(SemanticOrderViewTypeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }

    protected override void ConnectHandler(SkiaContentView platformView)
    {
        base.ConnectHandler(platformView);
        MapViewOrder(this, VirtualView);
    }

    protected override void DisconnectHandler(SkiaContentView platformView)
    {
        platformView.SetAccessibleOrder(null);
        base.DisconnectHandler(platformView);
    }

    public static void MapViewOrder(SemanticOrderViewHandler handler, IContentView view)
    {
        if (handler.PlatformView is not { } platformView || view == null)
            return;
        platformView.SetAccessibleOrder(() => ViewOrderOf(view));
    }

    /// <summary>The Skia views of the view's <c>ISemanticOrderView.ViewOrder</c>, in order.</summary>
    internal static IReadOnlyList<SkiaView> ViewOrderOf(IContentView view)
    {
        var order = view.GetType().GetInterface(ContractName)?.GetProperty("ViewOrder")?.GetValue(view) as IEnumerable;
        if (order == null)
            return Array.Empty<SkiaView>();
        var views = new List<SkiaView>();
        foreach (var item in order)
        {
            if (item is IView { Handler.PlatformView: SkiaView skia })
                views.Add(skia);
        }
        return views;
    }
}
