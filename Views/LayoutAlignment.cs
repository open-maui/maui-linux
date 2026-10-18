// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

[Obsolete("Use Microsoft.Maui.Primitives.LayoutAlignment; this type is removed in OpenMaui 11.0.")]
public enum LayoutAlignment
{
    Fill,
    Start,
    Center,
    End
}

/// <summary>
/// Maps MAUI Controls LayoutAlignment (Start=0, Center=1, End=2, Fill=3)
/// to Microsoft.Maui.Primitives.LayoutAlignment (Fill=0, Start=1, Center=2, End=3).
/// These enums share the same names but different ordinal values.
/// </summary>
internal static class LayoutAlignmentHelper
{
    internal static Primitives.LayoutAlignment MapFromMaui(Microsoft.Maui.Controls.LayoutOptions options)
    {
        return options.Alignment switch
        {
            Microsoft.Maui.Controls.LayoutAlignment.Start => Primitives.LayoutAlignment.Start,
            Microsoft.Maui.Controls.LayoutAlignment.Center => Primitives.LayoutAlignment.Center,
            Microsoft.Maui.Controls.LayoutAlignment.End => Primitives.LayoutAlignment.End,
            _ => Primitives.LayoutAlignment.Fill
        };
    }
}
