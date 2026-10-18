// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// xdg_activation_v1: window activation (MAUI's Application.ActivateWindow) and the launcher's
/// startup token. A Wayland client cannot raise itself; it asks for an activation token, ideally
/// from the window that has keyboard focus with its latest input serial, and hands the token to
/// the compositor with the surface to activate. The compositor decides (focus-stealing
/// prevention may only mark the window as wanting attention). The token arrives as an event on
/// the normal dispatch path; nothing here dispatches or round-trips.
/// </summary>
public partial class WaylandWindow : IActivatableWindow
{
    private static IntPtr _xdg_activation_v1_interface;
    private static IntPtr _xdg_activation_token_v1_interface;

    // xdg_activation_v1 requests
    private const uint XDG_ACTIVATION_V1_DESTROY = 0;
    private const uint XDG_ACTIVATION_V1_GET_ACTIVATION_TOKEN = 1;
    private const uint XDG_ACTIVATION_V1_ACTIVATE = 2;

    // xdg_activation_token_v1 requests
    private const uint XDG_ACTIVATION_TOKEN_V1_SET_SERIAL = 0;
    private const uint XDG_ACTIVATION_TOKEN_V1_SET_SURFACE = 2;
    private const uint XDG_ACTIVATION_TOKEN_V1_COMMIT = 3;
    private const uint XDG_ACTIVATION_TOKEN_V1_DESTROY = 4;

    // xdg_activation_v1.activate: (string token, object surface)
    [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal")]
    private static extern void wl_proxy_marshal_string_obj(IntPtr proxy, uint opcode,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg1, IntPtr arg2);

    // xdg_activation_token_v1.set_serial: (uint serial, object seat)
    [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal")]
    private static extern void wl_proxy_marshal_uint_obj(IntPtr proxy, uint opcode, uint arg1, IntPtr arg2);

    [StructLayout(LayoutKind.Sequential)]
    private struct XdgActivationTokenListener
    {
        public IntPtr Done;
    }

    private delegate void ActivationTokenDoneDelegate(IntPtr data, IntPtr token, IntPtr tokenString);

    // One listener for every token proxy (the per-request state travels in the user data).
    private static ActivationTokenDoneDelegate? s_activationTokenDone;
    private static IntPtr s_activationTokenListener;

    /// <summary>The window holding keyboard focus (wl_keyboard.enter), if one of ours.</summary>
    private static WaylandWindow? s_focusedWindow;

    // The launcher's token goes to the first window shown.
    private static bool s_startupTokenTaken;
    private string? _pendingStartupToken;

    private IntPtr _activation;
    private uint _keyboardEnterSerial;

    private sealed class ActivationRequest
    {
        public required WaylandWindow Requester { get; init; }
        public required WaylandWindow Target { get; init; }
    }

    private static void LoadActivationInterfaces()
    {
        if (_xdg_activation_v1_interface != IntPtr.Zero)
            return;

        // xdg-activation-v1 (staging) is not in libopenmaui_wl.so; declared here like xdg-shell's fallback tables.
        _xdg_activation_token_v1_interface = BuildInterface("xdg_activation_token_v1", 1,
            methods: new MessageDef[]
            {
                new("set_serial", "uo", new[] { IntPtr.Zero, _wl_seat_interface }),
                new("set_app_id", "s", new[] { IntPtr.Zero }),
                new("set_surface", "o", new[] { _wl_surface_interface }),
                new("commit", "", Array.Empty<IntPtr>()),
                new("destroy", "", Array.Empty<IntPtr>()),
            },
            events: new MessageDef[]
            {
                new("done", "s", new[] { IntPtr.Zero }),
            });

        _xdg_activation_v1_interface = BuildInterface("xdg_activation_v1", 1,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("get_activation_token", "n", new[] { _xdg_activation_token_v1_interface }),
                new("activate", "so", new[] { IntPtr.Zero, _wl_surface_interface }),
            },
            events: Array.Empty<MessageDef>());

        s_activationTokenDone = ActivationTokenDone;
        s_activationTokenListener = Marshal.AllocHGlobal(Marshal.SizeOf<XdgActivationTokenListener>());
        Marshal.StructureToPtr(new XdgActivationTokenListener
        {
            Done = Marshal.GetFunctionPointerForDelegate(s_activationTokenDone),
        }, s_activationTokenListener, fDeleteOld: false);
    }

    /// <summary>Binds xdg_activation_v1 when the compositor announces it.</summary>
    private void BindActivation(IntPtr registry, uint name)
    {
        LoadActivationInterfaces();
        _activation = wl_registry_bind(registry, name, _xdg_activation_v1_interface, 1);
    }

    private bool CanRequestActivation => _activation != IntPtr.Zero && _display != IntPtr.Zero && !_disposed;

    private uint LastInputSerial => Math.Max(Math.Max(_pointerButtonSerial, _keyboardSerial), _keyboardEnterSerial);

    /// <summary>Asks the compositor to activate (raise and focus) this window.</summary>
    public bool Activate()
    {
        if (_display == IntPtr.Zero || _surface == IntPtr.Zero || _disposed)
            return false;

        var requester = WaylandActivation.ChooseRequester(this, s_focusedWindow, w => w.CanRequestActivation);
        if (requester == null)
        {
            // No xdg_activation_v1, and xdg-shell has no urgency hint to fall back to.
            DiagnosticLog.Info("WaylandWindow", "Activate: the compositor offers no xdg_activation_v1; the window cannot be raised");
            return false;
        }

        var token = wl_proxy_marshal_constructor(requester._activation, XDG_ACTIVATION_V1_GET_ACTIVATION_TOKEN,
            _xdg_activation_token_v1_interface, IntPtr.Zero);
        if (token == IntPtr.Zero)
            return false;

        var request = GCHandle.Alloc(new ActivationRequest { Requester = requester, Target = this });
        wl_proxy_add_listener(token, s_activationTokenListener, GCHandle.ToIntPtr(request));

        bool requesterFocused = ReferenceEquals(requester, s_focusedWindow);
        foreach (var step in WaylandActivation.TokenSteps(requester.LastInputSerial, requester._seat != IntPtr.Zero, requesterFocused))
        {
            switch (step)
            {
                case WaylandActivationTokenStep.SetSerial:
                    wl_proxy_marshal_uint_obj(token, XDG_ACTIVATION_TOKEN_V1_SET_SERIAL, requester.LastInputSerial, requester._seat);
                    break;
                case WaylandActivationTokenStep.SetSurface:
                    wl_proxy_marshal(token, XDG_ACTIVATION_TOKEN_V1_SET_SURFACE, requester._surface);
                    break;
                case WaylandActivationTokenStep.Commit:
                    wl_proxy_marshal(token, XDG_ACTIVATION_TOKEN_V1_COMMIT);
                    break;
            }
        }
        wl_display_flush(requester._display);
        DiagnosticLog.Debug("WaylandWindow",
            $"Activate: token requested ({(ReferenceEquals(requester, this) ? "self" : "focused window")}, serial {requester.LastInputSerial}, focused {requesterFocused})");
        return true;
    }

    private static void ActivationTokenDone(IntPtr data, IntPtr token, IntPtr tokenString)
    {
        var handle = GCHandle.FromIntPtr(data);
        var request = handle.Target as ActivationRequest;
        handle.Free();

        wl_proxy_marshal(token, XDG_ACTIVATION_TOKEN_V1_DESTROY);
        wl_proxy_destroy(token);

        var value = Marshal.PtrToStringUTF8(tokenString);
        if (request != null && !string.IsNullOrEmpty(value))
            request.Target.ActivateWithToken(value);
    }

    /// <summary>xdg_activation_v1.activate on this window's surface (and connection).</summary>
    private void ActivateWithToken(string token)
    {
        if (!CanRequestActivation || _surface == IntPtr.Zero)
            return;
        wl_proxy_marshal_string_obj(_activation, XDG_ACTIVATION_V1_ACTIVATE, token, _surface);
        wl_display_flush(_display);
    }

    /// <summary>
    /// Takes the launcher's token (XDG_ACTIVATION_TOKEN, else DESKTOP_STARTUP_ID) for the first
    /// window shown; it is used once that window has drawn its first frame.
    /// </summary>
    private void TakeStartupToken()
    {
        if (s_startupTokenTaken)
            return;
        s_startupTokenTaken = true;
        var token = WaylandActivation.StartupToken(Environment.GetEnvironmentVariable);
        // Not inherited by processes this app starts.
        Environment.SetEnvironmentVariable("XDG_ACTIVATION_TOKEN", null);
        Environment.SetEnvironmentVariable("DESKTOP_STARTUP_ID", null);
        _pendingStartupToken = token;
    }

    /// <summary>Called from the run loop's pump (outside dispatch), after the first frame.</summary>
    private void ApplyPendingStartupToken()
    {
        var token = _pendingStartupToken;
        if (token == null)
            return;
        _pendingStartupToken = null;
        if (_activation == IntPtr.Zero)
        {
            DiagnosticLog.Debug("WaylandWindow", "Startup activation token ignored: no xdg_activation_v1");
            return;
        }
        ActivateWithToken(token);
        DiagnosticLog.Debug("WaylandWindow", "Startup activation token used");
    }

    private void NoteKeyboardEnter(uint serial)
    {
        _keyboardEnterSerial = serial;
        s_focusedWindow = this;
    }

    private void NoteKeyboardLeave()
    {
        if (ReferenceEquals(s_focusedWindow, this))
            s_focusedWindow = null;
    }

    private void DisposeActivation()
    {
        NoteKeyboardLeave();
        if (_activation != IntPtr.Zero)
        {
            wl_proxy_marshal(_activation, XDG_ACTIVATION_V1_DESTROY);
            wl_proxy_destroy(_activation);
            _activation = IntPtr.Zero;
        }
    }
}
