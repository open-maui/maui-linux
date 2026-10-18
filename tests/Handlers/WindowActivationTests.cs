// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using FluentAssertions;
using Microsoft.Maui;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Moq;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

/// <summary>
/// Application.ActivateWindow, headless: the ApplicationHandler command routing to the window's
/// native window, the X11 requests (_NET_ACTIVE_WINDOW, raise/focus without EWMH) and startup
/// notification messages, and the Wayland xdg-activation token decisions.
/// </summary>
[Collection("LinuxApplication.Current")]
public class WindowActivationTests : IDisposable
{
    private readonly LinuxApplication _app = new();

    public void Dispose() => _app.Dispose();

    private sealed class ActivatableWindow : IDisplayWindow, IActivatableWindow
    {
        public int Activations;
        public int Width => 800;
        public int Height => 600;
        public bool IsRunning => true;

        public event EventHandler<KeyEventArgs>? KeyDown { add { } remove { } }
        public event EventHandler<KeyEventArgs>? KeyUp { add { } remove { } }
        public event EventHandler<TextInputEventArgs>? TextInput { add { } remove { } }
        public event EventHandler<PointerEventArgs>? PointerMoved { add { } remove { } }
        public event EventHandler<PointerEventArgs>? PointerPressed { add { } remove { } }
        public event EventHandler<PointerEventArgs>? PointerReleased { add { } remove { } }
        public event EventHandler<ScrollEventArgs>? Scroll { add { } remove { } }
        public event EventHandler? Exposed { add { } remove { } }
        public event EventHandler<(int Width, int Height)>? Resized { add { } remove { } }
        public event EventHandler? CloseRequested { add { } remove { } }
        public event EventHandler? FocusGained { add { } remove { } }
        public event EventHandler? FocusLost { add { } remove { } }

        public bool Activate() { Activations++; return true; }
        public void Show() { }
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { }
        public void SetCursor(CursorType cursorType) { }
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() { }
        public int GetFileDescriptor() => -1;
        public void Present(IntPtr pixels, int width, int height, int stride) { }
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() { }
    }

    #region ApplicationHandler / LinuxApplication

    [Fact]
    public void ActivateWindow_is_in_the_command_mapper()
    {
        ApplicationHandler.CommandMapper.GetCommand(nameof(IApplication.ActivateWindow)).Should().NotBeNull();
    }

    [Fact]
    public void ActivateWindow_activates_the_windows_native_window()
    {
        var first = new ActivatableWindow();
        var second = new ActivatableWindow();
        var ctx1 = _app.AttachWindowContext(first, null, raisesMauiLifecycle: false);
        var ctx2 = _app.AttachWindowContext(second, null, raisesMauiLifecycle: false);
        var window1 = new Mock<IWindow>().Object;
        var window2 = new Mock<IWindow>().Object;
        ctx1.MauiWindow = window1;
        ctx2.MauiWindow = window2;

        ApplicationHandler.MapActivateWindow(null!, new Mock<IApplication>().Object, window2);

        second.Activations.Should().Be(1);
        first.Activations.Should().Be(0);
    }

    [Fact]
    public void Activating_an_unknown_window_does_nothing()
    {
        var native = new ActivatableWindow();
        var ctx = _app.AttachWindowContext(native, null, raisesMauiLifecycle: false);
        ctx.MauiWindow = new Mock<IWindow>().Object;

        _app.ActivateMauiWindow(new Mock<IWindow>().Object).Should().BeFalse();
        ApplicationHandler.MapActivateWindow(null!, new Mock<IApplication>().Object, "not a window");

        native.Activations.Should().Be(0);
    }

    #endregion

    #region X11

    [Fact]
    public void An_EWMH_window_manager_gets_NET_ACTIVE_WINDOW()
    {
        X11Activation.Plan(wmSupportsNetActiveWindow: true, isMapped: true)
            .Should().Equal(X11ActivationStep.SendNetActiveWindow);
    }

    [Fact]
    public void A_minimized_window_is_mapped_and_raised_first()
    {
        X11Activation.Plan(wmSupportsNetActiveWindow: true, isMapped: false)
            .Should().Equal(X11ActivationStep.MapRaised, X11ActivationStep.SendNetActiveWindow);
    }

    [Fact]
    public void Without_EWMH_the_window_raises_and_focuses_itself()
    {
        X11Activation.Plan(wmSupportsNetActiveWindow: false, isMapped: true)
            .Should().Equal(X11ActivationStep.RaiseWindow, X11ActivationStep.SetInputFocus);
        X11Activation.Plan(wmSupportsNetActiveWindow: false, isMapped: false)
            .Should().Equal(new[] { X11ActivationStep.MapRaised, X11ActivationStep.FocusOnMap },
                "focusing a window that is not viewable yet is BadMatch");
    }

    [Fact]
    public void NET_ACTIVE_WINDOW_carries_application_source_and_the_user_time()
    {
        X11Activation.NetActiveWindowData(987654).Should().Equal(1L, 987654L, 0L, 0L, 0L);
        X11Activation.NetActiveWindowData(0).Should().Equal(1L, 0L, 0L, 0L, 0L);
    }

    [Theory]
    [InlineData("gnome-shell/Files/4321-0-host_TIME3487345", true, 3487345UL)]
    [InlineData("kde-launch-123_TIME42", true, 42UL)]
    [InlineData("plain-id", false, 0UL)]
    [InlineData("bad_TIMEx12", false, 0UL)]
    [InlineData("", false, 0UL)]
    public void The_launch_time_comes_from_the_startup_id(string id, bool parsed, ulong time)
    {
        X11Activation.TryParseStartupTime(id, out var value).Should().Be(parsed);
        value.Should().Be(time);
    }

    [Fact]
    public void The_startup_remove_message_travels_in_20_byte_pieces()
    {
        var chunks = X11Activation.StartupRemoveChunks("app-1_TIME5\"x\\y");

        var all = new byte[chunks.Count * 20];
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Should().HaveCount(20);
            chunks[i].CopyTo(all, i * 20);
        }
        int nul = Array.IndexOf(all, (byte)0);
        Encoding.UTF8.GetString(all, 0, nul).Should().Be("remove: ID=\"app-1_TIME5\\\"x\\\\y\"");
        all.AsSpan(nul).ToArray().Should().OnlyContain(b => b == 0, "the message is NUL-terminated and zero-padded");
        chunks.Count.Should().Be((nul + 1 + 19) / 20);
    }

    [Fact]
    public void A_format_8_piece_packs_into_the_data_longs_little_endian()
    {
        var chunk = new byte[20];
        for (int i = 0; i < 20; i++)
            chunk[i] = (byte)(i + 1);

        var (l0, l1, l2) = X11Activation.PackFormat8(chunk);

        l0.Should().Be(BitConverter.ToInt64(chunk, 0));
        l1.Should().Be(BitConverter.ToInt64(chunk, 8));
        l2.Should().Be(0x14131211L);
        FluentActions.Invoking(() => X11Activation.PackFormat8(new byte[19])).Should().Throw<ArgumentException>();
    }

    #endregion

    #region Wayland

    private sealed record Win(string Name, bool CanRequest);

    [Fact]
    public void The_focused_window_requests_the_token()
    {
        var target = new Win("target", true);
        var focused = new Win("focused", true);

        WaylandActivation.ChooseRequester(target, focused, w => w.CanRequest).Should().BeSameAs(focused);
    }

    [Fact]
    public void Without_a_capable_focused_window_the_target_requests_it()
    {
        var target = new Win("target", true);

        WaylandActivation.ChooseRequester(target, null, w => w.CanRequest).Should().BeSameAs(target);
        WaylandActivation.ChooseRequester(target, new Win("focused", false), w => w.CanRequest).Should().BeSameAs(target);
        WaylandActivation.ChooseRequester(new Win("target", false), null, w => w.CanRequest).Should().BeNull(
            "a compositor without xdg_activation_v1 cannot activate");
    }

    [Fact]
    public void A_token_carries_the_serial_and_surface_it_has()
    {
        WaylandActivation.TokenSteps(lastInputSerial: 41, hasSeat: true, requesterFocused: true).Should().Equal(
            WaylandActivationTokenStep.SetSerial, WaylandActivationTokenStep.SetSurface, WaylandActivationTokenStep.Commit);
        WaylandActivation.TokenSteps(0, hasSeat: true, requesterFocused: false).Should().Equal(
            WaylandActivationTokenStep.Commit);
        WaylandActivation.TokenSteps(41, hasSeat: false, requesterFocused: true).Should().Equal(
            WaylandActivationTokenStep.SetSurface, WaylandActivationTokenStep.Commit);
    }

    [Fact]
    public void The_startup_token_prefers_XDG_ACTIVATION_TOKEN()
    {
        WaylandActivation.StartupToken(name => name switch
        {
            "XDG_ACTIVATION_TOKEN" => "xdg-token",
            "DESKTOP_STARTUP_ID" => "startup-id",
            _ => null,
        }).Should().Be("xdg-token");
        WaylandActivation.StartupToken(name => name == "DESKTOP_STARTUP_ID" ? "startup-id" : "").Should().Be("startup-id");
        WaylandActivation.StartupToken(_ => null).Should().BeNull();
    }

    #endregion
}
