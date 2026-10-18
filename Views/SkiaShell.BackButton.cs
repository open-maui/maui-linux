// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The navigation bar's back arrow for assistive technology: a button named by the presented
/// page's Shell.BackButtonBehavior.AccessibilityLabel (MAUI's ShellToolbar carries it as the
/// toolbar's BackButtonAccessibilityLabel), else "Back", WinUI's default name for the back
/// button; its click action does what a click on the arrow does.
/// </summary>
public partial class SkiaShell
{
    private DrawnButtonAccessible? _backButtonAccessible;
    private bool _backButtonExposed;
    private string? _backButtonExposedName;

    /// <summary>The back button's accessible name.</summary>
    internal string BackButtonAccessibleName =>
        _backButtonBehavior?.AccessibilityLabel is { Length: > 0 } label ? label : "Back";

    /// <summary>The back button as assistive technology sees it (tests).</summary>
    internal IAccessible BackButtonAccessible => _backButtonAccessible ??= new DrawnButtonAccessible(
        this,
        () => BackButtonAccessibleName,
        () =>
        {
            // Where the arrow takes clicks: after a locked flyout (a rail), 56 wide.
            float left = FlyoutBehavior == ShellFlyoutBehavior.Locked ? FlyoutWidth : 0;
            return new SKRect(left, 0, left + 56, NavBarHeight);
        },
        () => IsBackButtonVisible && (_backButtonBehavior?.IsEnabled ?? true),
        PressBackButton);

    private bool BackButtonShown => IsNavBarShown && IsBackButtonVisible;

    /// <summary>
    /// The back button: the page's BackButtonBehavior.Command replaces the navigation when it
    /// has one (MAUI's ShellToolbar).
    /// </summary>
    private void PressBackButton()
    {
        if (_backButtonBehavior?.Command is { } command)
        {
            if (_backButtonBehavior.IsEnabled && command.CanExecute(_backButtonBehavior.CommandParameter))
                command.Execute(_backButtonBehavior.CommandParameter);
        }
        else
        {
            GoBack();
        }
    }

    /// <summary>
    /// The back button while the bar shows it, then the top navigation pane and its search box
    /// while shown, then the shell's views.
    /// </summary>
    protected override List<IAccessible> GetAccessibleChildren()
    {
        var children = base.GetAccessibleChildren();
        children.InsertRange(0, TopPaneAccessibles());
        if (BackButtonShown)
            children.Insert(0, BackButtonAccessible);
        return children;
    }

    /// <summary>
    /// After the back button was shown or hidden, or its label changed, assistive technology
    /// reads the children or the name again.
    /// </summary>
    private void SyncBackButtonAccessible()
    {
        bool shown = BackButtonShown;
        if (shown != _backButtonExposed)
        {
            _backButtonExposed = shown;
            InvalidateAccessibleChildren();
        }
        var name = BackButtonAccessibleName;
        if (_backButtonExposedName != null && name != _backButtonExposedName && _backButtonAccessible != null)
            NotifyAccessibleChanged(_backButtonAccessible, AccessibleProperty.Name);
        _backButtonExposedName = name;
    }
}
