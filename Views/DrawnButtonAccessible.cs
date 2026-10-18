// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A button a view draws itself (a navigation bar's back arrow) as assistive technology sees
/// it: an accessible child of the view with the button role, a name, its place on screen, and
/// a click action, where the platforms MAUI runs on have a native button (WinUI's
/// NavigationView back button, whose automation name is "Back" unless the app names it).
/// </summary>
internal sealed class DrawnButtonAccessible : IAccessible
{
    private readonly SkiaView _owner;
    private readonly Func<string> _name;
    private readonly Func<SKRect> _bounds;
    private readonly Func<bool> _isEnabled;
    private readonly Action _click;
    private readonly string _id = Guid.NewGuid().ToString();

    /// <param name="owner">The view that draws the button.</param>
    /// <param name="name">The accessible name, read each time it is asked for.</param>
    /// <param name="bounds">Where the button is, relative to the owner's top-left.</param>
    /// <param name="isEnabled">Whether the button can be used.</param>
    /// <param name="click">What the click action does.</param>
    internal DrawnButtonAccessible(SkiaView owner, Func<string> name, Func<SKRect> bounds, Func<bool> isEnabled, Action click)
    {
        _owner = owner;
        _name = name;
        _bounds = bounds;
        _isEnabled = isEnabled;
        _click = click;
    }

    public string AccessibleId => _id;
    public string AccessibleName => _name() ?? string.Empty;
    public string AccessibleDescription => string.Empty;
    public AccessibleRole Role => AccessibleRole.Button;

    public AccessibleStates States
    {
        get
        {
            var states = AccessibleStates.Visible | AccessibleStates.Showing;
            if (_owner.IsEnabled && _isEnabled())
                states |= AccessibleStates.Enabled | AccessibleStates.Sensitive;
            return states;
        }
    }

    public IAccessible? Parent => _owner;
    public IReadOnlyList<IAccessible> Children => Array.Empty<IAccessible>();

    public AccessibleRect Bounds
    {
        get
        {
            var origin = _owner.ScreenBounds;
            var b = _bounds();
            return new AccessibleRect((int)(origin.Left + b.Left), (int)(origin.Top + b.Top), (int)b.Width, (int)b.Height);
        }
    }

    public IReadOnlyList<AccessibleAction> Actions =>
        [new AccessibleAction { Name = "click", Description = AccessibleName }];

    public double? Value => null;
    public double? MinValue => null;
    public double? MaxValue => null;

    public bool DoAction(string actionName)
    {
        if (actionName != "click" || !_owner.IsEnabled || !_isEnabled())
            return false;
        try
        {
            _click();
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Accessibility", $"The click of '{AccessibleName}' failed", ex);
            return false;
        }
    }

    public bool SetValue(double value) => false;
}
