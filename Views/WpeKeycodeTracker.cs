// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary>
/// Pairs the hardware keycode of a printable key press with the text it
/// produces. The platform delivers a printable key twice: a key-down (with the
/// hardware keycode) and then a text-input event (with the composed text, no
/// keycode). <see cref="WpeWebView"/> forwards only the text-input one to
/// WebKit so IME composition and keyboard layouts apply; this tracker lets
/// that event carry the keycode of the key that produced it, so the page sees
/// DOM <c>KeyboardEvent.code</c> (e.g. "KeyA") as well as <c>key</c>.
/// </summary>
/// <remarks>
/// Text that does not come straight from one key press (an IME commit, a
/// paste, several characters at once) gets keycode 0, which WebKit reports as
/// an empty <c>code</c>, matching other browsers for synthesized input.
/// </remarks>
internal sealed class WpeKeycodeTracker
{
    private uint _pending;

    /// <summary>A printable key went down; its text-input event is expected next.</summary>
    public void PrintableKeyDown(uint hardwareKeycode) => _pending = hardwareKeycode;

    /// <summary>A key was released: a keycode not yet claimed by text is dropped.</summary>
    public void KeyUp(uint hardwareKeycode)
    {
        if (hardwareKeycode == 0 || hardwareKeycode == _pending)
            _pending = 0;
    }

    /// <summary>
    /// The keycode to send with <paramref name="text"/>: the pending key's
    /// keycode when the text is a single character (one key, one character),
    /// else 0. Either way the pending keycode is consumed.
    /// </summary>
    public uint TakeForText(string text)
    {
        uint keycode = _pending;
        _pending = 0;
        if (keycode == 0 || string.IsNullOrEmpty(text))
            return 0;

        var runes = text.EnumerateRunes();
        int count = 0;
        foreach (var _ in runes)
        {
            if (++count > 1)
                return 0;
        }
        return count == 1 ? keycode : 0;
    }
}
