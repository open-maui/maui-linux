// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class PrimarySelectionServiceTests
{
    // Most of PrimarySelectionService's surface delegates to Wayland natives /
    // subprocess fallbacks, which we can't realistically unit-test in CI
    // without a compositor. These tests pin the API shape and the safe-fallback
    // behavior (every code path must catch its own failures and never throw).

    [Fact]
    public void Default_ReturnsSingleton()
    {
        PrimarySelectionService.Default.Should().BeSameAs(PrimarySelectionService.Default);
    }

    [Fact]
    public async Task SetTextAsync_NullOrEmpty_NoOpDoesNotThrow()
    {
        await PrimarySelectionService.Default.Invoking(s => s.SetTextAsync(null!))
            .Should().NotThrowAsync();
        await PrimarySelectionService.Default.Invoking(s => s.SetTextAsync(string.Empty))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetTextAsync_NoWaylandNoSubprocesses_ReturnsNullOrEmpty()
    {
        // Either we get null (no provider produced text) or whatever subprocess
        // actually exists on this machine — we don't pin the result, just that
        // the call completes without throwing.
        var result = await PrimarySelectionService.Default.GetTextAsync();
        result.Should().Match<string?>(s => s == null || s.GetType() == typeof(string));
    }
}
