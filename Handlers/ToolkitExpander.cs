// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Keyboard and screen-reader access to CommunityToolkit's Expander header, as its Windows build
/// gives it (CommunityToolkit 17's <c>ExpanderLayoutPanel</c> and its automation peer): the
/// Expander's layout is a tab stop while the header can be activated (visible, enabled, not
/// InputTransparent: <c>Expander.IsHeaderActivatable</c>), with the system focus visual around
/// the header's row for keyboard focus. Enter or Space, pressed without auto-repeat and released,
/// activates the header as a tap would (<c>Expander.HandleHeaderTap</c>, the position at the
/// header's centre). Assistive technology sees a push button named after the header's content
/// (its own name, else its descendants' names joined), described by the header's description,
/// expandable, expanded with the Expander, enabled while the header can be activated, with
/// click, expand and collapse actions; a change of IsExpanded is announced as a state change.
/// The toolkit is optional, so its types are looked up by name.
/// </summary>
internal static class ToolkitExpander
{
    private static int s_installed;
    private static Type? s_expanderType, s_layoutType;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentView, Link> s_links = new();
    private static Func<object, bool>? s_isHeaderActivatable;
    private static MethodInfo? s_handleHeaderTap, s_setIsExpandedFromHeader;
    private static PropertyInfo? s_isExpanded, s_header;

    internal static void Install(Harmony _)
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        s_expanderType = Type.GetType("CommunityToolkit.Maui.Views.Expander, CommunityToolkit.Maui");
        if (s_expanderType == null)
            return; // the toolkit is not part of the app
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var activatable = s_expanderType.GetProperty("IsHeaderActivatable", All)?.GetMethod;
        s_handleHeaderTap = s_expanderType.GetMethod("HandleHeaderTap", All, [typeof(TappedEventArgs)]);
        s_setIsExpandedFromHeader = s_expanderType.GetMethod("SetIsExpandedFromHeader", All, [typeof(bool), typeof(TappedEventArgs)]);
        s_isExpanded = s_expanderType.GetProperty("IsExpanded", All);
        s_header = s_expanderType.GetProperty("Header", All);
        var layoutType = s_expanderType.GetNestedType("ExpanderLayout", BindingFlags.Public | BindingFlags.NonPublic);
        if (activatable == null || s_handleHeaderTap == null || s_isExpanded == null || s_header == null || layoutType == null)
        {
            DiagnosticLog.Warn("ToolkitExpander", "This CommunityToolkit.Maui release lacks the Expander members the header's keyboard access needs; it is not bridged");
            return;
        }
        s_isHeaderActivatable = target => activatable.Invoke(target, null) is true;
        s_layoutType = layoutType;
    }

    /// <summary>
    /// A Linux handler connected to <paramref name="view"/>: an Expander's layout is followed from
    /// then on, as the Windows build's ExpanderLayoutHandler.ConnectHandler starts its access.
    /// </summary>
    internal static void OnHandlerConnected(object? view)
    {
        if (s_layoutType == null || view?.GetType() != s_layoutType)
            return;
        try
        {
            if (view is Layout { Parent: ContentView expander } layout && expander.GetType() == s_expanderType
                && !s_links.TryGetValue(expander, out _))
                s_links.Add(expander, new Link(expander, layout));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ToolkitExpander", "Following an Expander failed", ex);
        }
    }

    private static bool IsExpanded(object expander) => s_isExpanded!.GetValue(expander) is true;

    private static View? HeaderOf(object expander) => s_header!.GetValue(expander) as View;

    /// <summary>One Expander and its layout (the toolkit's ExpanderLayout, which holds the header and the content).</summary>
    private sealed class Link : SkiaViewDecorator
    {
        private readonly ContentView _expander;
        private readonly Layout _layout;
        private SkiaView? _view;
        private View? _header;
        private Key? _heldKey;
        private bool _activationPressed;
        private bool _keyboardFocus;
        private bool _isStop;
        private bool _expanded;

        public Link(ContentView expander, Layout layout)
        {
            _expander = expander;
            _layout = layout;
            _expanded = IsExpanded(expander);
            expander.PropertyChanged += OnExpanderPropertyChanged;
            layout.HandlerChanged += (_, _) => Attach();
            FollowHeader();
            Attach();
        }

        private bool IsActivatable => s_isHeaderActivatable!(_expander);

        private void Attach()
        {
            var view = _layout.Handler?.PlatformView as SkiaView;
            if (ReferenceEquals(view, _view))
                return;
            if (_view != null)
            {
                _view.KeyDown -= OnKeyDown;
                _view.KeyUp -= OnKeyUp;
                _view.FocusGained -= OnFocusGained;
                _view.FocusLost -= OnFocusLost;
                if (ReferenceEquals(_view.Decorator, this))
                    _view.Decorator = null;
                if (_isStop)
                    _view.IsFocusable = false;
                _isStop = false;
            }
            _view = view;
            _activationPressed = false;
            _heldKey = null;
            _keyboardFocus = false;
            if (view == null)
                return;
            view.Decorator = this;
            view.KeyDown += OnKeyDown;
            view.KeyUp += OnKeyUp;
            view.FocusGained += OnFocusGained;
            view.FocusLost += OnFocusLost;
            UpdateStop();
        }

        private void FollowHeader()
        {
            var header = HeaderOf(_expander);
            if (ReferenceEquals(header, _header))
                return;
            if (_header != null)
                _header.PropertyChanged -= OnHeaderPropertyChanged;
            _header = header;
            if (header != null)
                header.PropertyChanged += OnHeaderPropertyChanged;
            UpdateStop();
        }

        private void OnExpanderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == s_header!.Name)
            {
                FollowHeader();
            }
            else if (e.PropertyName == s_isExpanded!.Name)
            {
                bool expanded = IsExpanded(_expander);
                if (expanded != _expanded)
                {
                    _expanded = expanded;
                    if (_view != null)
                        SkiaView.NotifyAccessibleStateChanged(_view, AccessibleState.Expanded, expanded);
                }
            }
        }

        private void OnHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName
                || e.PropertyName == VisualElement.IsEnabledProperty.PropertyName
                || e.PropertyName == VisualElement.InputTransparentProperty.PropertyName)
                UpdateStop();
            else if (e.PropertyName is "Height" or "Y" && _view is { IsFocused: true })
                _view.Invalidate(); // the focus visual follows the header's row
        }

        /// <summary>ExpanderLayoutPanel.UpdateHeaderAccessibility: a tab stop while the header can be activated.</summary>
        private void UpdateStop()
        {
            if (_view == null)
                return;
            bool stop = IsActivatable;
            if (stop != _isStop)
            {
                _isStop = stop;
                _view.IsFocusable = stop;
                if (!stop)
                {
                    _activationPressed = false;
                    _heldKey = null;
                }
                SkiaView.NotifyAccessibleStateChanged(_view, AccessibleState.Enabled, stop);
            }
            _view.Invalidate();
        }

        private void OnFocusGained(object? sender, EventArgs e) => _keyboardFocus = WindowContext.IsMovingFocusByKeyboard;

        private void OnFocusLost(object? sender, EventArgs e)
        {
            _keyboardFocus = false;
            _activationPressed = false;
            _heldKey = null;
            _view?.Invalidate();
        }

        // ExpanderLayoutPanel.HandleKey: Enter or Space pressed (not auto-repeated) and released
        // activates the header; any other key pressed in between cancels it.
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!_isStop)
                return;
            var key = e.Key == Key.NumPadEnter ? Key.Enter : e.Key;
            if (key != Key.Enter && key != Key.Space)
            {
                _activationPressed = false;
                _heldKey = null;
                return;
            }
            bool repeat = _heldKey == key;
            _heldKey = key;
            _activationPressed |= !repeat;
            e.Handled = true;
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            var key = e.Key == Key.NumPadEnter ? Key.Enter : e.Key;
            if (key != Key.Enter && key != Key.Space)
                return;
            if (_heldKey == key)
                _heldKey = null;
            if (!_activationPressed || !_isStop)
                return;
            _activationPressed = false;
            e.Handled = true;
            Activate(null);
        }

        /// <summary>A tap on the header (null), or the header set expanded or collapsed (Expand and Collapse).</summary>
        private bool Activate(bool? expand)
        {
            if (!IsActivatable)
                return false;
            try
            {
                var args = new HeaderCenterTappedEventArgs(_header);
                if (expand is { } isExpanded && s_setIsExpandedFromHeader != null)
                    s_setIsExpandedFromHeader.Invoke(_expander, [isExpanded, args]);
                else if (expand is null || IsExpanded(_expander) != expand)
                    s_handleHeaderTap!.Invoke(_expander, [args]);
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("ToolkitExpander", "Activating the Expander header failed", ex);
                return false;
            }
        }

        // ---- Decorator: the focus visual and the automation peer -------------------------------

        /// <summary>ExpanderLayoutPanel's focus visual: the full width, from the header's top margin to its bottom margin.</summary>
        public override void DrawOverlay(SkiaView view, SKCanvas canvas, SKRect bounds)
        {
            if (!_isStop || !_keyboardFocus || !view.IsFocused || _header == null)
                return;
            var frame = _header.Frame;
            float top = (float)Math.Max(0, frame.Top - _header.Margin.Top);
            float bottom = (float)Math.Min(bounds.Height, frame.Bottom + _header.Margin.Bottom);
            if (bottom <= top)
                return;
            SkiaFocusVisual.Draw(canvas, new SKRect(bounds.Left, bounds.Top + top, bounds.Right, bounds.Top + bottom));
        }

        public override AccessibleRole? Role => AccessibleRole.Button;

        public override string? Name => _header != null ? NameOf(_header) : null;

        public override string? Description => HeaderAccessible is { AccessibleDescription: { Length: > 0 } description } ? description : null;

        public override AccessibleStates States(AccessibleStates states)
        {
            states |= AccessibleStates.Expandable;
            if (IsExpanded(_expander))
                states |= AccessibleStates.Expanded;
            if (!IsActivatable)
                states &= ~(AccessibleStates.Enabled | AccessibleStates.Focusable);
            return states;
        }

        public override IReadOnlyList<AccessibleAction>? Actions =>
        [
            new AccessibleAction { Name = "click", Description = "Expands or collapses the content" },
            new AccessibleAction { Name = "expand", Description = "Expands the content" },
            new AccessibleAction { Name = "collapse", Description = "Collapses the content" },
        ];

        public override bool? DoAction(string actionName) => actionName switch
        {
            "click" => Activate(null),
            "expand" => Activate(true),
            "collapse" => Activate(false),
            _ => null,
        };

        private IAccessible? HeaderAccessible => _header?.Handler?.PlatformView as IAccessible;

        /// <summary>
        /// The automation peer's GetNameOf: an element's own accessible name, a text element's text
        /// (WinUI's TextBlock peer is named by its text), else its children's names joined.
        /// </summary>
        private static string NameOf(Element element)
        {
            if (element is VisualElement { Handler.PlatformView: IAccessible accessible } && !string.IsNullOrWhiteSpace(accessible.AccessibleName))
                return accessible.AccessibleName;
            switch (element)
            {
                case Label label:
                    return label.FormattedText?.ToString() is { Length: > 0 } formatted ? formatted : label.Text ?? string.Empty;
                case Button button:
                    return button.Text ?? string.Empty;
            }
            if (element is not IVisualTreeElement tree)
                return string.Empty;
            return string.Join(", ", tree.GetVisualChildren().OfType<Element>().Select(NameOf).Where(n => !string.IsNullOrWhiteSpace(n)));
        }
    }

    /// <summary>The Windows build's HeaderCenterTappedEventArgs: the position is the header's centre.</summary>
    private sealed class HeaderCenterTappedEventArgs(View? header) : TappedEventArgs(null)
    {
        public override Point? GetPosition(Element? relativeTo)
        {
            if (header?.Handler?.PlatformView is not SkiaView headerView)
                return null;
            var b = headerView.ScreenBounds;
            var center = new Point(b.Left + b.Width / 2, b.Top + b.Height / 2);
            if (relativeTo == null)
                return center;
            if (relativeTo.Handler?.PlatformView is not SkiaView target)
                return null;
            var t = target.ScreenBounds;
            return new Point(center.X - t.Left, center.Y - t.Top);
        }
    }
}
