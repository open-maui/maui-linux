// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

/// <summary>
/// The shape <see cref="SkiaIndicatorView"/> draws its indicators with. MAUI's
/// <c>IndicatorShape</c> has Circle and Square; RoundedSquare and Diamond are OpenMaui's.
/// </summary>
public enum SkiaIndicatorShape
{
    Circle,
    Square,
    RoundedSquare,
    Diamond
}
