// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

public enum LayoutAlignment
{
    Fill,
    Start,
    Center,
    End
}

/// <summary>
/// Maps MAUI Controls LayoutAlignment (Start=0, Center=1, End=2, Fill=3)
/// to OpenMaui LayoutAlignment (Fill=0, Start=1, Center=2, End=3).
/// These enums share the same names but different ordinal values.
/// </summary>
internal static class LayoutAlignmentHelper
{
    internal static LayoutAlignment MapFromMaui(Microsoft.Maui.Controls.LayoutOptions options)
    {
        return options.Alignment.ToString() switch
        {
            "Start" => LayoutAlignment.Start,
            "Center" => LayoutAlignment.Center,
            "End" => LayoutAlignment.End,
            _ => LayoutAlignment.Fill
        };
    }
}
