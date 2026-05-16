// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Input;

namespace Microsoft.Maui.Platform.Linux.Window;

// Wayland keyboard support via xkbcommon. The compositor delivers a keymap as a
// mmap-able file descriptor; we parse it once with xkb_keymap_new_from_string,
// hold an xkb_state to track modifiers, and use it to translate keycodes into
// keysyms (for the Key enum) and UTF-8 (for TextInput) on every key press.
public partial class WaylandWindow
{
    private const string LibXkbCommon = "libxkbcommon.so.0";

    [LibraryImport(LibXkbCommon)]
    private static partial IntPtr xkb_context_new(uint flags);

    [LibraryImport(LibXkbCommon)]
    private static partial void xkb_context_unref(IntPtr context);

    [LibraryImport(LibXkbCommon)]
    private static partial IntPtr xkb_keymap_new_from_string(
        IntPtr context, IntPtr str, uint format, uint flags);

    [LibraryImport(LibXkbCommon)]
    private static partial void xkb_keymap_unref(IntPtr keymap);

    [LibraryImport(LibXkbCommon)]
    private static partial IntPtr xkb_state_new(IntPtr keymap);

    [LibraryImport(LibXkbCommon)]
    private static partial void xkb_state_unref(IntPtr state);

    [LibraryImport(LibXkbCommon)]
    private static partial uint xkb_state_update_mask(
        IntPtr state,
        uint depressedMods, uint latchedMods, uint lockedMods,
        uint depressedLayout, uint latchedLayout, uint lockedLayout);

    [LibraryImport(LibXkbCommon)]
    private static partial uint xkb_state_key_get_one_sym(IntPtr state, uint keycode);

    [LibraryImport(LibXkbCommon)]
    private static partial int xkb_state_key_get_utf8(
        IntPtr state, uint keycode, IntPtr buffer, nuint size);

    // Portable modifier check by name. xkb modifier bitmask bit positions vary
    // by keymap (xkb's "Mod1"..."Mod5" can be remapped), so consulting by
    // canonical name is the only reliable way to tell which physical modifier
    // is active. Returns 1 if the named modifier is currently active.
    [LibraryImport(LibXkbCommon, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int xkb_state_mod_name_is_active(
        IntPtr state, string name, uint type);

    // xkb_state_component flags for xkb_state_mod_name_is_active's `type`.
    private const uint XKB_STATE_MODS_DEPRESSED = 1;
    private const uint XKB_STATE_MODS_LATCHED = 2;

    // Canonical xkb modifier names.
    private const string XKB_MOD_NAME_SHIFT = "Shift";
    private const string XKB_MOD_NAME_CTRL = "Control";
    private const string XKB_MOD_NAME_ALT = "Mod1";
    private const string XKB_MOD_NAME_LOGO = "Mod4";
    private const string XKB_MOD_NAME_CAPS = "Lock";
    private const string XKB_MOD_NAME_NUM = "Mod2";

    private const uint XKB_KEYMAP_FORMAT_TEXT_V1 = 1;
    private const uint XKB_KEYMAP_COMPILE_NO_FLAGS = 0;
    private const uint XKB_CONTEXT_NO_FLAGS = 0;

    // wl_keyboard.keymap formats (protocol enum)
    private const uint WL_KEYBOARD_KEYMAP_FORMAT_NO_KEYMAP = 0;
    private const uint WL_KEYBOARD_KEYMAP_FORMAT_XKB_V1 = 1;

    // mmap protections / flags. The keymap fd from the compositor is delivered
    // PROT_READ on most compositors but PROT_READ|PROT_WRITE on older ones; we
    // try the strict path first and fall back if that fails.
    private const int PROT_READ_KEYMAP = 0x1;
    private const int MAP_PRIVATE = 0x02;
    private const int MAP_FAILED = -1;

    private IntPtr _xkbContext;
    private IntPtr _xkbKeymap;
    private IntPtr _xkbState;

    private void HandleKeymap(uint format, int fd, uint size)
    {
        // Whatever happens, we own the fd and must close it.
        try
        {
            if (format != WL_KEYBOARD_KEYMAP_FORMAT_XKB_V1 || size == 0)
                return;

            var mapped = mmap(IntPtr.Zero, size, PROT_READ_KEYMAP, MAP_PRIVATE, fd, 0);
            if (mapped == new IntPtr(MAP_FAILED) || mapped == IntPtr.Zero)
                return;

            try
            {
                if (_xkbContext == IntPtr.Zero)
                    _xkbContext = xkb_context_new(XKB_CONTEXT_NO_FLAGS);
                if (_xkbContext == IntPtr.Zero)
                    return;

                var newKeymap = xkb_keymap_new_from_string(
                    _xkbContext, mapped, XKB_KEYMAP_FORMAT_TEXT_V1, XKB_KEYMAP_COMPILE_NO_FLAGS);
                if (newKeymap == IntPtr.Zero)
                    return;

                var newState = xkb_state_new(newKeymap);
                if (newState == IntPtr.Zero)
                {
                    xkb_keymap_unref(newKeymap);
                    return;
                }

                // Swap in atomically so any in-flight key event reads a coherent pair.
                var oldKeymap = _xkbKeymap;
                var oldState = _xkbState;
                _xkbKeymap = newKeymap;
                _xkbState = newState;
                if (oldState != IntPtr.Zero) xkb_state_unref(oldState);
                if (oldKeymap != IntPtr.Zero) xkb_keymap_unref(oldKeymap);
            }
            finally
            {
                munmap(mapped, size);
            }
        }
        finally
        {
            close(fd);
        }
    }

    private void HandleModifiers(uint modsDepressed, uint modsLatched, uint modsLocked, uint group)
    {
        if (_xkbState != IntPtr.Zero)
        {
            xkb_state_update_mask(_xkbState,
                modsDepressed, modsLatched, modsLocked,
                0, 0, group);

            // Map xkb modifier state → MAUI KeyModifiers via canonical names.
            // Casting the raw bitmask directly is wrong: xkb bit positions are
            // keymap-defined (Mod1 = Alt by convention but not guaranteed) and
            // the MAUI KeyModifiers enum has a totally different value layout
            // (Control = 2, Alt = 4) so a direct cast turned Ctrl into Alt.
            uint flags = 0;
            const uint t = XKB_STATE_MODS_DEPRESSED | XKB_STATE_MODS_LATCHED;
            if (xkb_state_mod_name_is_active(_xkbState, XKB_MOD_NAME_SHIFT, t) > 0) flags |= (uint)KeyModifiers.Shift;
            if (xkb_state_mod_name_is_active(_xkbState, XKB_MOD_NAME_CTRL, t) > 0) flags |= (uint)KeyModifiers.Control;
            if (xkb_state_mod_name_is_active(_xkbState, XKB_MOD_NAME_ALT, t) > 0) flags |= (uint)KeyModifiers.Alt;
            if (xkb_state_mod_name_is_active(_xkbState, XKB_MOD_NAME_LOGO, t) > 0) flags |= (uint)KeyModifiers.Super;
            _modifiers = flags;
        }
        else
        {
            // No keymap yet — leave _modifiers at zero rather than the raw bits,
            // which would mis-cast as before.
            _modifiers = 0;
        }
    }

    /// <summary>
    /// Translates a wl_keyboard keycode (Linux input scancode) into a MAUI
    /// <see cref="Key"/> and the UTF-8 string that key produces under the current
    /// modifier/layout state. Returns <c>(Key.Unknown, null)</c> if no keymap is loaded yet.
    /// </summary>
    private (Key key, string? text) TranslateKey(uint keycode)
    {
        // wl_keyboard scancodes match Linux input event codes; xkbcommon and X11
        // both shift them by 8 to fit historical X keycode space.
        uint xkbKeycode = keycode + 8;

        if (_xkbState == IntPtr.Zero)
        {
            // No keymap yet — fall back to the raw mapping. The compositor sends
            // keymap before the first key event in normal flow, so this only
            // fires on broken implementations.
            return (KeyMapping.FromLinuxKeycode(xkbKeycode), null);
        }

        var keysym = xkb_state_key_get_one_sym(_xkbState, xkbKeycode);
        var key = KeyMapping.FromKeysym(keysym);

        // Text query: probe length first, then allocate. Most keys produce 1-4 bytes.
        Span<byte> stack = stackalloc byte[16];
        unsafe
        {
            fixed (byte* p = stack)
            {
                int needed = xkb_state_key_get_utf8(_xkbState, xkbKeycode, (IntPtr)p, (nuint)stack.Length);
                if (needed <= 0)
                    return (key, null);

                if (needed >= stack.Length)
                {
                    // Rare: composed sequences > 15 bytes. Allocate exactly and re-query.
                    var heap = new byte[needed + 1];
                    fixed (byte* hp = heap)
                    {
                        xkb_state_key_get_utf8(_xkbState, xkbKeycode, (IntPtr)hp, (nuint)heap.Length);
                        var heapText = System.Text.Encoding.UTF8.GetString(heap, 0, needed);
                        return (key, FilterControlText(heapText));
                    }
                }

                var text = System.Text.Encoding.UTF8.GetString(p, needed);
                return (key, FilterControlText(text));
            }
        }
    }

    private static string? FilterControlText(string s)
    {
        // xkbcommon returns a UTF-8 string for non-printable keys too (Backspace=0x08,
        // Enter=0x0D, Escape=0x1B, …). Suppress those — TextInput should fire only for
        // characters the user expects to see in a text field.
        if (string.IsNullOrEmpty(s)) return null;
        foreach (var ch in s)
        {
            if (ch < 0x20 || ch == 0x7F) return null;
        }
        return s;
    }

    private void DisposeXkb()
    {
        if (_xkbState != IntPtr.Zero)
        {
            xkb_state_unref(_xkbState);
            _xkbState = IntPtr.Zero;
        }
        if (_xkbKeymap != IntPtr.Zero)
        {
            xkb_keymap_unref(_xkbKeymap);
            _xkbKeymap = IntPtr.Zero;
        }
        if (_xkbContext != IntPtr.Zero)
        {
            xkb_context_unref(_xkbContext);
            _xkbContext = IntPtr.Zero;
        }
    }
}
