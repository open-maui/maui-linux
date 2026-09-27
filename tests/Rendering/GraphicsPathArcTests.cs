// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Rendering;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Arcs in MAUI Graphics paths join the current point on Skia, as on the other
/// backends: a pie wedge (MoveTo centre, AddArc, Close) covers its centre
/// instead of drawing as an arc closed by its chord.
/// </summary>
public class GraphicsPathArcTests
{
    [Fact]
    public void A_wedge_path_covers_its_centre()
    {
        GraphicsPathPatches.Install();
        var wedge = new PathF();
        wedge.MoveTo(50, 50);
        wedge.AddArc(0, 0, 100, 100, 0, 90, false);
        wedge.Close();

        using var sk = wedge.AsSkiaPath();

        sk.Contains(55, 45).Should().BeTrue("the wedge includes the area next to its centre");
    }
}
