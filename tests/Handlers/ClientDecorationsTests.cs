// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerButton = Microsoft.Maui.Platform.PointerButton;

/// <summary>
/// OpenMaui's own decorations for a window with a TitleBar, as Windows extends content into the
/// title bar: the hit-test that sorts presses into resize, window buttons, TitleBar content and
/// move; the decoration mode the Wayland window asks for and uses per compositor answer; the
/// X11 hints and client messages; and the window context asking for them when a TitleBar is set.
/// </summary>
public class ClientDecorationsTests
{
    private const float Width = 800, Height = 600;

    private static ClientDecorations ActiveDecorations(Func<float, float, bool>? passthrough = null)
    {
        // The renderer's layout for an 800-wide window with a 32-tall titlebar.
        return new ClientDecorations
        {
            Active = true,
            TitlebarHeight = 32,
            CloseButtonBounds = new SKRect(768, 2, 796, 30),
            MaximizeButtonBounds = new SKRect(736, 2, 764, 30),
            MinimizeButtonBounds = new SKRect(704, 2, 732, 30),
            TitleBarPassthrough = passthrough,
        };
    }

    [Fact]
    public void Inactive_decorations_leave_every_press_to_the_page()
    {
        var decorations = ActiveDecorations();
        decorations.Active = false;
        decorations.HitTest(2, 2, Width, Height, false).Region.Should().Be(CsdRegion.Content);
        decorations.HitTest(780, 16, Width, Height, false).Region.Should().Be(CsdRegion.Content);
        decorations.HitTest(400, 16, Width, Height, false).Region.Should().Be(CsdRegion.Content);
    }

    [Theory]
    [InlineData(2f, 2f, (int)CsdResizeEdge.TopLeft)]
    [InlineData(798f, 2f, (int)CsdResizeEdge.TopRight)]
    [InlineData(2f, 598f, (int)CsdResizeEdge.BottomLeft)]
    [InlineData(798f, 598f, (int)CsdResizeEdge.BottomRight)]
    [InlineData(400f, 3f, (int)CsdResizeEdge.Top)]
    [InlineData(400f, 597f, (int)CsdResizeEdge.Bottom)]
    [InlineData(3f, 300f, (int)CsdResizeEdge.Left)]
    [InlineData(797f, 300f, (int)CsdResizeEdge.Right)]
    public void The_window_edges_resize(float x, float y, int edgeValue)
    {
        var edge = (CsdResizeEdge)edgeValue;
        var hit = ActiveDecorations().HitTest(x, y, Width, Height, isMaximized: false);
        hit.Region.Should().Be(CsdRegion.ResizeEdge);
        hit.Edge.Should().Be(edge);
    }

    [Fact]
    public void A_maximized_window_has_no_resize_edges()
    {
        var decorations = ActiveDecorations();
        decorations.HitTest(400, 3, Width, Height, isMaximized: true).Region.Should().Be(CsdRegion.Move);
        decorations.HitTest(3, 300, Width, Height, isMaximized: true).Region.Should().Be(CsdRegion.Content);
        ClientDecorations.EdgeAt(0, 0, Width, Height, isMaximized: true).Should().Be(CsdResizeEdge.None);
    }

    [Fact]
    public void The_window_buttons_take_their_presses()
    {
        var decorations = ActiveDecorations(passthrough: (_, _) => true);
        decorations.HitTest(782, 16, Width, Height, false).Region.Should().Be(CsdRegion.Close);
        decorations.HitTest(750, 16, Width, Height, false).Region.Should().Be(CsdRegion.Maximize);
        decorations.HitTest(718, 16, Width, Height, false).Region.Should().Be(CsdRegion.Minimize);
    }

    [Fact]
    public void TitleBar_content_takes_its_presses_and_the_rest_of_the_bar_moves_the_window()
    {
        // The TitleBar's interactive content covers x 10..110 of the bar.
        var decorations = ActiveDecorations(passthrough: (x, y) => x >= 10 && x <= 110 && y <= 32);
        decorations.HitTest(60, 16, Width, Height, false).Region.Should().Be(CsdRegion.TitleBarContent);
        decorations.HitTest(400, 16, Width, Height, false).Region.Should().Be(CsdRegion.Move);
        decorations.HitTest(400, 32, Width, Height, false).Region.Should().Be(CsdRegion.Move, "the titlebar's last row");
        decorations.HitTest(400, 33, Width, Height, false).Region.Should().Be(CsdRegion.Content, "below the titlebar is the page");
    }

    [Fact]
    public void A_taller_TitleBar_grows_the_move_area()
    {
        var decorations = ActiveDecorations();
        decorations.TitlebarHeight = 48;
        decorations.HitTest(400, 40, Width, Height, false).Region.Should().Be(CsdRegion.Move);
        decorations.HitTest(400, 49, Width, Height, false).Region.Should().Be(CsdRegion.Content);
    }

    [Fact]
    public void Two_quick_presses_in_place_are_a_double_click()
    {
        var decorations = ActiveDecorations();
        decorations.RegisterMoveClick(1000, 400, 16).Should().BeFalse();
        decorations.RegisterMoveClick(1000 + ClientDecorations.DoubleClickMilliseconds, 402, 17).Should().BeTrue();
        decorations.RegisterMoveClick(1500, 402, 17).Should().BeFalse("a completed double-click starts over");

        decorations.RegisterMoveClick(5000, 400, 16).Should().BeFalse();
        decorations.RegisterMoveClick(5000 + ClientDecorations.DoubleClickMilliseconds + 1, 400, 16).Should().BeFalse("too slow");

        decorations.RegisterMoveClick(9000, 400, 16).Should().BeFalse();
        decorations.RegisterMoveClick(9100, 420, 16).Should().BeFalse("too far apart");

        decorations.RegisterMoveClick(12000, 400, 16).Should().BeFalse();
        decorations.ResetClickTracking();
        decorations.RegisterMoveClick(12100, 400, 16).Should().BeFalse("a press elsewhere breaks the double-click");
    }

    [Theory]
    [InlineData((int)CsdResizeEdge.Top, 1u, 1)]
    [InlineData((int)CsdResizeEdge.Bottom, 2u, 5)]
    [InlineData((int)CsdResizeEdge.Left, 4u, 7)]
    [InlineData((int)CsdResizeEdge.Right, 8u, 3)]
    [InlineData((int)CsdResizeEdge.TopLeft, 5u, 0)]
    [InlineData((int)CsdResizeEdge.TopRight, 9u, 2)]
    [InlineData((int)CsdResizeEdge.BottomLeft, 6u, 6)]
    [InlineData((int)CsdResizeEdge.BottomRight, 10u, 4)]
    [InlineData((int)CsdResizeEdge.None, 0u, -1)]
    public void Edges_map_to_the_xdg_and_EWMH_values(int edgeValue, uint xdg, int ewmh)
    {
        var edge = (CsdResizeEdge)edgeValue;
        ClientDecorations.ToXdgResizeEdge(edge).Should().Be(xdg);
        ClientDecorations.ToNetWmMoveResizeDirection(edge).Should().Be(ewmh);
    }

    #region Wayland decoration mode

    [Fact]
    public void Wayland_asks_for_client_side_mode_only_with_a_TitleBar()
    {
        WaylandDecorationPolicy.RequestedMode(titleBarWanted: true).Should().Be(WaylandDecorationPolicy.ModeClientSide);
        WaylandDecorationPolicy.RequestedMode(titleBarWanted: false).Should().Be(WaylandDecorationPolicy.ModeServerSide);
    }

    [Fact]
    public void Wayland_draws_its_decorations_as_the_compositor_answers()
    {
        // KDE/Sway honouring client_side for a TitleBar.
        WaylandDecorationPolicy.UsesClientDecorations(true, false, WaylandDecorationPolicy.ModeClientSide).Should().BeTrue();
        // A compositor that keeps server-side decorations: the TitleBar stays a strip.
        WaylandDecorationPolicy.UsesClientDecorations(true, false, WaylandDecorationPolicy.ModeServerSide).Should().BeFalse();
        // No answer yet.
        WaylandDecorationPolicy.UsesClientDecorations(true, false, null).Should().BeFalse();
        // No zxdg_decoration_manager_v1 (GNOME): always our own.
        WaylandDecorationPolicy.UsesClientDecorations(false, false, null).Should().BeTrue();
        // MAUI_PREFER_CSD=1.
        WaylandDecorationPolicy.UsesClientDecorations(true, true, WaylandDecorationPolicy.ModeServerSide).Should().BeTrue();
    }

    #endregion

    #region X11

    private sealed class RecordingX11Host : IX11DecorationHost
    {
        public readonly List<string> Calls = new();
        public readonly List<(string Property, string Type, long[] Values)> Properties = new();
        public readonly List<(string Type, long[] Data)> Messages = new();
        public bool SupportsWindowMenu { get; set; } = true;
        public int Closes, Iconifies;

        public long Atom(string name) => name switch
        {
            "_NET_WM_STATE_MAXIMIZED_VERT" => 301,
            "_NET_WM_STATE_MAXIMIZED_HORZ" => 302,
            _ => 999,
        };

        public void SetProperty32(string property, string type, long[] values)
        {
            Calls.Add("property " + property);
            Properties.Add((property, type, values));
        }

        public void SendRootClientMessage(string messageType, long[] data)
        {
            Calls.Add("message " + messageType);
            Messages.Add((messageType, data));
        }

        public void UngrabPointer(ulong time) => Calls.Add("ungrab");
        public bool WmSupports(string atom) => atom == "_GTK_SHOW_WINDOW_MENU" && SupportsWindowMenu;
        public void Iconify() { Iconifies++; Calls.Add("iconify"); }
        public void RequestClose() { Closes++; Calls.Add("close"); }
        public void Flush() { }
    }

    private static (X11ClientDecorations Decorations, RecordingX11Host Host) ActiveX11(Func<float, float, bool>? passthrough = null)
    {
        var host = new RecordingX11Host();
        var decorations = new X11ClientDecorations(host);
        decorations.SetWanted(true);
        decorations.State.TitlebarHeight = 32;
        decorations.State.CloseButtonBounds = new SKRect(768, 2, 796, 30);
        decorations.State.MaximizeButtonBounds = new SKRect(736, 2, 764, 30);
        decorations.State.MinimizeButtonBounds = new SKRect(704, 2, 732, 30);
        decorations.State.TitleBarPassthrough = passthrough;
        host.Calls.Clear();
        host.Messages.Clear();
        return (decorations, host);
    }

    [Fact]
    public void X11_turns_the_window_manager_frame_off_with_motif_hints()
    {
        var host = new RecordingX11Host();
        var decorations = new X11ClientDecorations(host);

        decorations.SetWanted(false).Should().BeFalse("a window without a TitleBar keeps its frame untouched");
        host.Properties.Should().BeEmpty();

        decorations.SetWanted(true).Should().BeTrue();
        decorations.State.Active.Should().BeTrue();
        host.Properties.Should().ContainSingle();
        host.Properties[0].Property.Should().Be("_MOTIF_WM_HINTS");
        host.Properties[0].Type.Should().Be("_MOTIF_WM_HINTS");
        host.Properties[0].Values.Should().Equal(X11ClientDecorations.MwmHintsDecorations, 0L, 0L, 0L, 0L);

        decorations.SetWanted(true).Should().BeFalse("no change, no request");
        host.Properties.Should().ContainSingle();

        decorations.SetWanted(false).Should().BeTrue();
        decorations.State.Active.Should().BeFalse();
        host.Properties[1].Values.Should().Equal(X11ClientDecorations.MwmHintsDecorations, 0L, 1L, 0L, 0L);
    }

    [Fact]
    public void X11_titlebar_drag_moves_the_window_through_the_window_manager()
    {
        var (decorations, host) = ActiveX11();
        decorations.HandlePress(PointerButton.Left, 400, 16, 1400, 316, 1000, Width, Height, false).Should().BeTrue();
        host.Calls.Should().Equal("ungrab", "message _NET_WM_MOVERESIZE");
        host.Messages[0].Data.Should().Equal(1400L, 316L, X11ClientDecorations.NetWmMoveResizeMove, 1L, 1L);
    }

    [Fact]
    public void X11_edge_drag_resizes_in_the_edges_direction()
    {
        var (decorations, host) = ActiveX11();
        decorations.HandlePress(PointerButton.Left, 798, 598, 1798, 898, 1000, Width, Height, false).Should().BeTrue();
        host.Messages.Should().ContainSingle();
        host.Messages[0].Type.Should().Be("_NET_WM_MOVERESIZE");
        host.Messages[0].Data[2].Should().Be(X11ClientDecorations.NetWmMoveResizeSizeBottomRight);
    }

    [Fact]
    public void X11_double_click_on_the_titlebar_toggles_maximize()
    {
        var (decorations, host) = ActiveX11();
        decorations.HandlePress(PointerButton.Left, 400, 16, 0, 0, 1000, Width, Height, false);
        host.Messages.Clear();
        decorations.HandlePress(PointerButton.Left, 400, 16, 0, 0, 1200, Width, Height, false).Should().BeTrue();
        host.Messages.Should().ContainSingle();
        host.Messages[0].Type.Should().Be("_NET_WM_STATE");
        host.Messages[0].Data.Should().Equal(1L, 301L, 302L, 1L, 0L);

        host.Messages.Clear();
        decorations.HandlePress(PointerButton.Left, 400, 16, 0, 0, 5000, Width, Height, true);
        decorations.HandlePress(PointerButton.Left, 400, 16, 0, 0, 5100, Width, Height, true);
        host.Messages.Last().Data[0].Should().Be(0L, "a maximized window is restored");
    }

    [Fact]
    public void X11_window_buttons_close_maximize_and_minimize()
    {
        var (decorations, host) = ActiveX11(passthrough: (_, _) => true);
        decorations.HandlePress(PointerButton.Left, 782, 16, 0, 0, 1, Width, Height, false).Should().BeTrue();
        host.Closes.Should().Be(1);
        decorations.HandlePress(PointerButton.Left, 718, 16, 0, 0, 2, Width, Height, false).Should().BeTrue();
        host.Iconifies.Should().Be(1);
        decorations.HandlePress(PointerButton.Left, 750, 16, 0, 0, 3, Width, Height, false).Should().BeTrue();
        host.Messages.Should().ContainSingle(m => m.Type == "_NET_WM_STATE" && m.Data[0] == 1);
    }

    [Fact]
    public void X11_leaves_TitleBar_content_and_the_page_to_the_views()
    {
        var (decorations, host) = ActiveX11(passthrough: (x, _) => x < 100);
        decorations.HandlePress(PointerButton.Left, 50, 16, 0, 0, 1, Width, Height, false).Should().BeFalse();
        decorations.HandlePress(PointerButton.Left, 400, 300, 0, 0, 2, Width, Height, false).Should().BeFalse();
        decorations.HandlePress(PointerButton.Middle, 400, 16, 0, 0, 3, Width, Height, false).Should().BeFalse();
        host.Calls.Should().BeEmpty();
    }

    [Fact]
    public void X11_right_click_on_the_titlebar_opens_the_window_menu_when_supported()
    {
        var (decorations, host) = ActiveX11(passthrough: (x, _) => x < 100);
        decorations.HandlePress(PointerButton.Right, 400, 16, 1400, 316, 1, Width, Height, false).Should().BeTrue();
        host.Calls.Should().Equal("ungrab", "message _GTK_SHOW_WINDOW_MENU");
        host.Messages[0].Data.Should().Equal(0L, 1400L, 316L, 0L, 0L);

        host.Calls.Clear();
        decorations.HandlePress(PointerButton.Right, 50, 16, 0, 0, 2, Width, Height, false).Should().BeFalse("the TitleBar's content gets its own right-click");
        decorations.HandlePress(PointerButton.Right, 400, 300, 0, 0, 3, Width, Height, false).Should().BeFalse("the page gets its right-click");

        host.SupportsWindowMenu = false;
        decorations.HandlePress(PointerButton.Right, 400, 16, 0, 0, 4, Width, Height, false).Should().BeFalse("no window menu without WM support");
        host.Calls.Should().BeEmpty();
    }

    [Fact]
    public void X11_without_a_TitleBar_takes_no_presses()
    {
        var host = new RecordingX11Host();
        var decorations = new X11ClientDecorations(host);
        decorations.HandlePress(PointerButton.Left, 400, 16, 0, 0, 1, Width, Height, false).Should().BeFalse();
        decorations.HandlePress(PointerButton.Left, 2, 2, 0, 0, 2, Width, Height, false).Should().BeFalse();
        host.Calls.Should().BeEmpty();
    }

    #endregion
}

/// <summary>The window context asks its native window for client-side decorations when the MAUI window has a TitleBar.</summary>
[Collection("LinuxApplication.Current")]
public class ClientDecorationsWindowTests
{
    public ClientDecorationsWindowTests() => WindowOverlayPatches.Install();

    [Fact]
    public void A_TitleBar_asks_for_client_side_decorations_and_fills_the_titlebar_when_granted()
    {
        var page = new ContentPage { BackgroundColor = Colors.White };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.DisplayWindow.GrantClientDecorations = true;
        host.Context.Render();
        IClientDecoratedWindow decorated = host.DisplayWindow;
        decorated.Decorations.Active.Should().BeFalse("a window without a TitleBar keeps the system decorations");
        host.DisplayWindow.ClientDecorationRequests.Should().NotContain(true);

        var leading = new Button { Text = "Menu", WidthRequest = 80 };
        host.Window.TitleBar = new TitleBar
        {
            HeightRequest = 48,
            Title = "My app",
            LeadingContent = leading,
        };
        host.Context.Render();
        host.Context.Render();

        host.DisplayWindow.ClientDecorationRequests.Last().Should().BeTrue();
        decorated.Decorations.Active.Should().BeTrue();
        decorated.Decorations.TitlebarHeight.Should().BeApproximately(48, 1, "the TitleBar fills the decoration's titlebar");
        page.Height.Should().BeApproximately(552, 1);
        decorated.Decorations.CloseButtonBounds.Right.Should().BeGreaterThan(700, "the window buttons are drawn at the right of the bar");

        var leadingBounds = ((SkiaView)leading.Handler!.PlatformView!).Bounds;
        float lx = (float)leadingBounds.Center.X, ly = (float)leadingBounds.Center.Y + 48;
        decorated.Decorations.HitTest(lx, ly, 800, 600, false).Region.Should().Be(CsdRegion.TitleBarContent);
        decorated.Decorations.HitTest(400, 24, 800, 600, false).Region.Should().Be(CsdRegion.Move);
        var close = decorated.Decorations.CloseButtonBounds;
        decorated.Decorations.HitTest(close.MidX, close.MidY, 800, 600, false).Region.Should().Be(CsdRegion.Close);
        decorated.Decorations.HitTest(400, 300, 800, 600, false).Region.Should().Be(CsdRegion.Content);

        host.Window.TitleBar = null;
        host.Context.Render();
        host.DisplayWindow.ClientDecorationRequests.Last().Should().BeFalse("without a TitleBar the system decorations come back");
        decorated.Decorations.Active.Should().BeFalse();
    }

    [Fact]
    public void When_the_compositor_keeps_its_decorations_the_TitleBar_stays_a_strip()
    {
        var page = new ContentPage { BackgroundColor = Colors.White };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.DisplayWindow.GrantClientDecorations = false;
        host.Window.TitleBar = new TitleBar { HeightRequest = 48, Title = "My app" };
        host.Context.Render();
        host.Context.Render();

        host.DisplayWindow.ClientDecorationRequests.Last().Should().BeTrue("the TitleBar still asks");
        IClientDecoratedWindow decorated = host.DisplayWindow;
        decorated.Decorations.Active.Should().BeFalse();
        page.Height.Should().BeApproximately(552, 1, "the strip above the page");
        decorated.Decorations.HitTest(400, 24, 800, 600, false).Region.Should().Be(CsdRegion.Content, "the strip's presses go to the views");
    }
}
