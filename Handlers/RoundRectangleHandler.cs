// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's RoundRectangleHandler on Linux: a <see cref="ShapeViewHandler"/> that redraws
/// the shape when its CornerRadius changes.
/// </summary>
public partial class RoundRectangleHandler : ShapeViewHandler
{
    public static new IPropertyMapper<Microsoft.Maui.Controls.Shapes.RoundRectangle, IShapeViewHandler> Mapper =
        new PropertyMapper<Microsoft.Maui.Controls.Shapes.RoundRectangle, IShapeViewHandler>(ShapeViewHandler.Mapper)
        {
            [nameof(Microsoft.Maui.Controls.Shapes.RoundRectangle.CornerRadius)] = MapCornerRadius,
        };

    public RoundRectangleHandler() : base(Mapper)
    {
    }

    public RoundRectangleHandler(IPropertyMapper? mapper) : base(mapper ?? Mapper)
    {
    }

    public static void MapCornerRadius(IShapeViewHandler handler, Microsoft.Maui.Controls.Shapes.RoundRectangle roundRectangle) =>
        ShapePlatformView(handler)?.InvalidateShape(roundRectangle);
}
