// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for SfDataGrid's scroller (Syncfusion's <see cref="DataGridScrollViewExt"/>),
/// replacing the internal <c>DataGridScrollViewHandler</c>, which the platform-neutral build
/// ships as an empty class. The grid's rows scroll in OpenMaui's scroll view; on top of that the
/// handler reports the scroll state the Windows handler reports, which raises SfDataGrid's
/// <c>ScrollStateChanged</c> event: <c>Fling</c> while the wheel or a scroll bar scrolls it and
/// <c>Idle</c> once scrolling stops (<c>Dragging</c> is a touch manipulation state; a mouse
/// drag over the rows selects or drags them instead, as on Windows).
/// The Windows handler leaves the state at <c>Fling</c> after a wheel turn or a scroll-bar
/// drag (only a touch manipulation ends in <c>Idle</c>); here it settles back to <c>Idle</c>
/// when the scrolling stops, so the event always closes.
/// </summary>
public class SfDataGridScrollViewBridgeHandler : ScrollViewHandler
{
    public SfDataGridScrollViewBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaScrollView CreatePlatformView() => new SkiaSfDataGridScrollView();

    protected override void ConnectHandler(SkiaScrollView platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is SkiaSfDataGridScrollView view)
            view.Owner = VirtualView as DataGridScrollViewExt;
    }

    protected override void DisconnectHandler(SkiaScrollView platformView)
    {
        if (platformView is SkiaSfDataGridScrollView view)
            view.Owner = null;
        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// The scroll view of <see cref="SfDataGridScrollViewBridgeHandler"/>: OpenMaui's scroll view,
/// reporting the scroll state to the grid's scroller.
/// </summary>
public class SkiaSfDataGridScrollView : SkiaScrollView
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly PropertyInfo? s_scrollState = typeof(DataGridScrollViewExt).GetProperty("ScrollState", Any);
    private static readonly MethodInfo? s_setScrollingState = typeof(DataGridScrollViewExt).GetMethod("SetScrollingState", Any);
    private static readonly MethodInfo? s_isInDragging = typeof(DataGridScrollViewExt).GetMethod("IsInDragging", Any);

    /// <summary>How long the scroll position must stay still before the state returns to Idle.</summary>
    internal static TimeSpan IdleDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    private bool _pressed;
    private float _pressX, _pressY;
    private bool _wheel;
    private int _generation;

    internal DataGridScrollViewExt? Owner { get; set; }

    public SkiaSfDataGridScrollView()
    {
        Scrolled += OnScrolled;
    }

    /// <summary>The state last reported (Idle, Dragging or Fling), or empty before any.</summary>
    internal string State => Owner is { } owner ? s_scrollState?.GetValue(owner) as string ?? string.Empty : string.Empty;

    public override void OnScroll(ScrollEventArgs e)
    {
        _wheel = true;
        base.OnScroll(e);
        _wheel = false;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Reaches the scroll view itself only for its scroll bars and the area outside the
        // content; a press on a row goes to the row.
        _pressed = true;
        _pressX = e.X;
        _pressY = e.Y;
        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        // As DataGridScrollViewHandler.ScrollView_ManipulationDelta: a drag the grid takes for
        // itself (a column or row being dragged) does not scroll.
        if (_pressed && Owner is { } owner && s_isInDragging?.Invoke(owner, new object[] { (double)(e.X - _pressX), (double)(e.Y - _pressY) }) is true)
        {
            SetState("Idle", scrolling: false);
            return;
        }
        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;
        if (State is "Fling" or "Dragging")
            SetState("Idle", scrolling: false);
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (Owner == null)
            return;
        if (_wheel || _pressed)
        {
            // Wheel (ScrollView_PointerWheelChanged) and scroll bar (ScrollBar.Scroll) both
            // report Fling on Windows.
            SetState("Fling", scrolling: true);
        }
        else
        {
            return; // programmatic: SfDataGrid reports Programmatic itself
        }
        int generation = ++_generation;
        var dispatcher = MauiView?.Dispatcher;
        if (dispatcher == null || _pressed)
            return;
        dispatcher.DispatchDelayed(IdleDelay, () =>
        {
            if (generation == _generation && !_pressed && State == "Fling")
                SetState("Idle", scrolling: false);
        });
    }

    private void SetState(string state, bool scrolling)
    {
        if (Owner is not { } owner)
            return;
        try
        {
            s_setScrollingState?.Invoke(owner, new object[] { scrolling });
            s_scrollState?.SetValue(owner, state);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid scroll state failed", ex);
        }
    }
}
