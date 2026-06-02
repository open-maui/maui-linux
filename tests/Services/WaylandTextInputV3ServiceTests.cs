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
}
