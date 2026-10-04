// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace OpenMaui.Conformance.Tests;

/// <summary>
/// Compiled only when the dotnet/maui test sources are absent (see the csproj):
/// the conformance suite compiles MAUI's own handler tests in place, so there is
/// nothing to run without them.
/// </summary>
public class MauiSourcesMissing
{
    [Fact(Skip = "dotnet/maui test sources not found. Run tests/Conformance/get-maui-sources.sh " +
                 "(clones tag 10.0.110 into ~/.cache/openmaui-build/maui-src) or build with -p:MauiSrc=<maui checkout>.")]
    public void MauiConformanceSuiteNeedsTheMauiSources()
    {
    }
}
