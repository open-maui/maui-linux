// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shell.TitleView in the navigation bar, as MAUI's ShellToolbar resolves it: the presented
/// page's, else the nearest one set on its ShellContent, section, item or the Shell itself. The
/// view takes the title's place (between the navigation icon and the toolbar items, the bar's
/// full height) and is followed as pages change and as the property changes.
/// </summary>
public partial class SkiaShell
{
    private Microsoft.Maui.Controls.View? _mauiTitleView;
    private SkiaView? _titleView;
    private Rect _titleViewBounds;

    /// <summary>
    /// Realizes a MAUI view the shell shows in its own chrome (the TitleView). Set by the host
    /// (the renderer or handler); a view that already has a platform view keeps it.
    /// </summary>
    public Func<Microsoft.Maui.Controls.View, SkiaView?>? ViewRenderer
    {
        get => _viewRenderer;
        set
        {
            _viewRenderer = value;
            // The first page can be shown before the host supplies the renderer.
            if (value != null && _mauiTitleView != null && _titleView == null)
                UpdateTitleView(force: true);
        }
    }

    private Func<Microsoft.Maui.Controls.View, SkiaView?>? _viewRenderer;

    /// <summary>The platform view of the TitleView shown in the navigation bar, null when none is.</summary>
    public SkiaView? TitleView => _titleView;

    /// <summary>Where the navigation bar placed the TitleView in the last frame (tests).</summary>
    internal Rect TitleViewBounds => _titleViewBounds;

    /// <summary>
    /// The Shell.TitleView in effect for <paramref name="page"/>: set on the page, or on the
    /// nearest of its ancestors up to the Shell (MAUI's Shell.GetEffectiveValue).
    /// </summary>
    private Microsoft.Maui.Controls.View? ResolveMauiTitleView(Microsoft.Maui.Controls.Page? page)
    {
        Element? element = page;
        while (element != null)
        {
            if (element.IsSet(Shell.TitleViewProperty))
                return Shell.GetTitleView(element);
            if (element is Shell)
                break;
            element = element.Parent;
        }
        if (page == null && _mauiShell != null && _mauiShell.IsSet(Shell.TitleViewProperty))
            return Shell.GetTitleView(_mauiShell);
        return null;
    }

    /// <summary>Shows the TitleView in effect for the presented page.</summary>
    private void UpdateTitleView(bool force = false)
    {
        var mauiView = ResolveMauiTitleView(_lifecyclePage);
        if (!force && ReferenceEquals(mauiView, _mauiTitleView) && (mauiView == null || ReferenceEquals(mauiView.Handler?.PlatformView, _titleView)))
            return;

        if (_titleView != null && ReferenceEquals(_titleView.Parent, this))
            _titleView.Parent = null;
        _mauiTitleView = mauiView;
        _titleView = null;
        _titleViewBounds = Rect.Zero;
        if (mauiView != null)
        {
            try
            {
                _titleView = mauiView.Handler?.PlatformView as SkiaView ?? ViewRenderer?.Invoke(mauiView);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", $"Realizing the TitleView {mauiView.GetType().Name} failed", ex);
            }
            if (_titleView != null)
                _titleView.Parent = this; // invalidations reach the window
        }
        InvalidateMeasure();
        Invalidate();
    }

    private void OnMauiShellTitleViewChanged() => UpdateTitleView();

    /// <summary>
    /// Lays the TitleView out in <paramref name="slot"/> and draws it; false when there is none
    /// (the title text is drawn then).
    /// </summary>
    private bool DrawTitleView(SKCanvas canvas, SKRect slot)
    {
        if (_titleView == null)
            return false;
        var rect = new Rect(slot.Left, slot.Top, Math.Max(0, slot.Width), Math.Max(0, slot.Height));
        _titleViewBounds = rect;
        _titleView.Measure(new Size(rect.Width, rect.Height));
        _titleView.Arrange(rect);
        canvas.Save();
        canvas.ClipRect(slot);
        _titleView.Draw(canvas);
        canvas.Restore();
        return true;
    }
}
