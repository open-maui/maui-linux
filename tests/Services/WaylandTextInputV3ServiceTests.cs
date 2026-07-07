// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class WaylandTextInputV3ServiceTests
{
    // The zwp_text_input_v3 protocol counts bytes in UTF-8 but our IInputContext
    // implementations index Text in UTF-16 code units. These tests pin the
    // conversion so a Japanese / emoji-laden preedit retraction doesn't tear a
    // multi-byte character in half.

    [Theory]
    [InlineData("hello", 5, 3, 3)]                  // ASCII: 1 byte/char
    [InlineData("hello", 5, 99, 5)]                 // overflow → clamp to length
    [InlineData("hello", 5, 0, 0)]                  // zero
    [InlineData("hello", 0, 3, 0)]                  // caret at start, nothing to delete
    [InlineData("héllo", 2, 2, 1)]                  // 'é' = 2 UTF-8 bytes; 1 char
    [InlineData("héllo", 2, 1, 0)]                  // landing mid-char clamps to 0
    [InlineData("中文", 2, 3, 1)]                   // each CJK char = 3 UTF-8 bytes
    [InlineData("中文", 2, 6, 2)]                   // both chars
    [InlineData("中文", 2, 4, 1)]                   // 4 bytes lands mid-second-char → clamp to first
    public void Utf8BytesToCharsBeforeCaret_HandlesMultiByteBoundaries(string text, int caret, int byteCount, int expected)
    {
        WaylandTextInputV3Service.Utf8BytesToCharsBeforeCaret(text, caret, byteCount)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("hello", 0, 3, 3)]                  // ASCII forward
    [InlineData("hello", 5, 1, 0)]                  // caret at end
    [InlineData("héllo", 0, 2, 1)]                  // 'é' is 2 bytes
    [InlineData("中文", 0, 3, 1)]                   // one CJK char
    [InlineData("中文", 0, 5, 1)]                   // 5 bytes spans 1 full + half → clamp to 1
    public void Utf8BytesToCharsAfterCaret_HandlesMultiByteBoundaries(string text, int caret, int byteCount, int expected)
    {
        WaylandTextInputV3Service.Utf8BytesToCharsAfterCaret(text, caret, byteCount)
            .Should().Be(expected);
    }

    [Fact]
    public void Utf8BytesToCharsBeforeCaret_HandlesSurrogatePair()
    {
        // 😀 (U+1F600) is one UTF-32 code point, two UTF-16 code units (surrogate
        // pair), four UTF-8 bytes. Deleting "one emoji's worth" of bytes must
        // remove BOTH halves of the surrogate pair, not just one.
        var text = "a😀b";   // [a][hi-surr][lo-surr][b]
        WaylandTextInputV3Service.Utf8BytesToCharsBeforeCaret(text, caret: 3, byteCount: 4)
            .Should().Be(2);    // drops both surrogate halves
    }

    [Fact]
    public void Utf8BytesToCharsAfterCaret_HandlesSurrogatePair()
    {
        var text = "a😀b";
        WaylandTextInputV3Service.Utf8BytesToCharsAfterCaret(text, caret: 1, byteCount: 4)
            .Should().Be(2);
    }

    // set_surrounding_text sends UTF-8 text with BYTE offsets for cursor/anchor,
    // windowed to stay under the 4096-byte Wayland message cap. These tests pin
    // the window math: passthrough for short text, caret-centered windowing with
    // budget redistribution near the buffer ends, code-point-boundary cuts for
    // multi-byte text, and edge-clamping for anchors that fall outside the window.

    [Fact]
    public void BuildSurroundingWindow_ShortText_PassesThroughWithByteOffsets()
    {
        // "héllo": h=1 byte, é=2 bytes → caret after 'é' (char index 2) = byte 3.
        var (text, cursor, anchor) = WaylandTextInputV3Service.BuildSurroundingWindow("héllo", 2, 5);
        text.Should().Be("héllo");
        cursor.Should().Be(3);
        anchor.Should().Be(6);   // whole string is 6 UTF-8 bytes
    }

    [Fact]
    public void BuildSurroundingWindow_LongText_CentersWindowOnCaret()
    {
        var (window, cursor, anchor) = WaylandTextInputV3Service.BuildSurroundingWindow(new string('a', 6000), 3000, 3000);
        window.Length.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes);
        cursor.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes / 2);
        anchor.Should().Be(cursor);
    }

    [Fact]
    public void BuildSurroundingWindow_CaretAtEnd_ReclaimsUnusedForwardBudget()
    {
        var (window, cursor, _) = WaylandTextInputV3Service.BuildSurroundingWindow(new string('a', 6000), 6000, 6000);
        window.Length.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes);
        cursor.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes);
    }

    [Fact]
    public void BuildSurroundingWindow_CaretAtStart_ReclaimsUnusedBackwardBudget()
    {
        var (window, cursor, _) = WaylandTextInputV3Service.BuildSurroundingWindow(new string('a', 6000), 0, 0);
        window.Length.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes);
        cursor.Should().Be(0);
    }

    [Fact]
    public void BuildSurroundingWindow_MultiByte_CutsOnCodepointBoundaries()
    {
        // 2000 × 中 (3 UTF-8 bytes each) = 6000 bytes, caret mid-buffer.
        // 4000-byte budget → 666 chars behind (1998 B) + 667 ahead (2001 B).
        var (window, cursor, _) = WaylandTextInputV3Service.BuildSurroundingWindow(new string('中', 2000), 1000, 1000);
        System.Text.Encoding.UTF8.GetByteCount(window)
            .Should().BeLessThanOrEqualTo(WaylandTextInputV3Service.MaxSurroundingBytes);
        window.Length.Should().Be(1333);
        cursor.Should().Be(1998);
    }

    [Fact]
    public void BuildSurroundingWindow_AnchorOutsideWindow_ClampsToEdge()
    {
        // Caret near the end, anchor at 0 — anchor can't be expressed inside
        // the window so it clamps to the window start.
        var (window, cursor, anchor) = WaylandTextInputV3Service.BuildSurroundingWindow(new string('a', 6000), 5000, 0);
        window.Length.Should().Be(WaylandTextInputV3Service.MaxSurroundingBytes);
        cursor.Should().Be(3000);   // window covers chars 2000..6000
        anchor.Should().Be(0);
    }
}
