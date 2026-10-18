// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Keyboard activation of a tappable Border, as MAUI 11's GesturePlatformManager does it on
/// Windows: a Border with a TapGestureRecognizer that fires on a single primary-button tap
/// (<c>NumberOfTapsRequired == 1</c>, <c>Buttons</c> including Primary), that is enabled and not
/// InputTransparent, is a tab stop with the system focus visual. Enter or Space, without Ctrl,
/// Shift or Alt and not auto-repeated, raises those recognizers' taps (Command, then Tapped) at
/// <c>Point.Zero</c>. The stop follows the recognizers, their NumberOfTapsRequired and Buttons,
/// and the Border's IsEnabled and InputTransparent as they change.
/// </summary>
public partial class SkiaBorder
{
    private View? _tapStopView;
    private readonly List<TapGestureRecognizer> _tapStopRecognizers = new();
    private bool _isTapStop;
    private Key? _heldActivationKey;
    private bool _keyboardFocus;

    /// <summary>True while the Border is a tab stop because of its tap recognizers.</summary>
    internal bool IsTapStop => _isTapStop;

    /// <summary>True while the focus visual is shown (the Border has keyboard focus).</summary>
    internal bool ShowsKeyboardFocusVisual => _isTapStop && IsFocused && _keyboardFocus;

    /// <summary>Follows <paramref name="view"/>'s tap recognizers (null stops following).</summary>
    internal void AttachTapStop(View? view)
    {
        if (ReferenceEquals(_tapStopView, view))
            return;
        if (_tapStopView != null)
        {
            if (_tapStopView.GestureRecognizers is INotifyCollectionChanged oldGestures)
                oldGestures.CollectionChanged -= OnTapStopGesturesChanged;
            _tapStopView.PropertyChanged -= OnTapStopViewPropertyChanged;
        }
        _tapStopView = view;
        if (view != null)
        {
            if (view.GestureRecognizers is INotifyCollectionChanged newGestures)
                newGestures.CollectionChanged += OnTapStopGesturesChanged;
            view.PropertyChanged += OnTapStopViewPropertyChanged;
        }
        UpdateTapStop();
    }

    private void OnTapStopGesturesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateTapStop();

    private void OnTapStopViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VisualElement.IsEnabledProperty.PropertyName
            || e.PropertyName == VisualElement.InputTransparentProperty.PropertyName)
            UpdateTapStop();
    }

    private void OnTapStopRecognizerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TapGestureRecognizer.NumberOfTapsRequired) or nameof(TapGestureRecognizer.Buttons))
            UpdateTapStop();
    }

    /// <summary>The recognizers Enter and Space fire: single-tap, primary button.</summary>
    private static bool IsKeyboardTap(TapGestureRecognizer recognizer) =>
        recognizer.NumberOfTapsRequired == 1 && (recognizer.Buttons & ButtonsMask.Primary) == ButtonsMask.Primary;

    private void UpdateTapStop()
    {
        foreach (var old in _tapStopRecognizers)
            old.PropertyChanged -= OnTapStopRecognizerPropertyChanged;
        _tapStopRecognizers.Clear();

        bool hasPrimarySingleTap = false;
        if (_tapStopView?.GestureRecognizers is { } gestures)
        {
            foreach (var recognizer in gestures.OfType<TapGestureRecognizer>())
            {
                recognizer.PropertyChanged += OnTapStopRecognizerPropertyChanged;
                _tapStopRecognizers.Add(recognizer);
                hasPrimarySingleTap |= IsKeyboardTap(recognizer);
            }
        }

        // A disabled or input-transparent Border is not a stop, whatever its recognizers.
        bool shouldBeStop = hasPrimarySingleTap && _tapStopView is { IsEnabled: true, InputTransparent: false };
        if (shouldBeStop == _isTapStop)
            return;
        _isTapStop = shouldBeStop;
        IsFocusable = shouldBeStop;
        if (!shouldBeStop)
            _heldActivationKey = null;
        Invalidate();
    }

    public override void OnFocusGained()
    {
        // The focus visual shows for keyboard focus (Tab), not for a click.
        _keyboardFocus = WindowContext.IsMovingFocusByKeyboard;
        base.OnFocusGained();
    }

    public override void OnFocusLost()
    {
        _keyboardFocus = false;
        _heldActivationKey = null;
        base.OnFocusLost();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !_isTapStop)
            return;
        var key = e.Key == Key.NumPadEnter ? Key.Enter : e.Key;
        if (key != Key.Enter && key != Key.Space)
            return;

        // A held key repeats its key-down: act once per press.
        if (_heldActivationKey == key)
            return;
        _heldActivationKey = key;

        // Modifier chords (Ctrl+Enter, Shift+Space, Alt+Space) do not activate.
        if ((e.Modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)) != 0)
            return;
        if (_tapStopView is not { IsEnabled: true, InputTransparent: false } view)
            return;

        if (GestureManager.SendKeyboardTaps(view, IsKeyboardTap))
            e.Handled = true;
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        var key = e.Key == Key.NumPadEnter ? Key.Enter : e.Key;
        if (_heldActivationKey == key)
            _heldActivationKey = null;
        base.OnKeyUp(e);
    }

    /// <summary>
    /// WinUI's system focus visual (high visibility): a 2-pixel outer ring and a 1-pixel inner
    /// ring along the element's edges, dark outside and light inside on a light theme, and the
    /// reverse on a dark one.
    /// </summary>
    private void DrawKeyboardFocusVisual(SKCanvas canvas, SKRect bounds)
    {
        if (!ShowsKeyboardFocusVisual || bounds.Width < 6 || bounds.Height < 6)
            return;
        bool dark = SkiaTheme.IsDarkMode;
        using var outer = new SKPaint
        {
            Color = dark ? new SKColor(0xFF, 0xFF, 0xFF, 0xFF) : new SKColor(0x00, 0x00, 0x00, 0xE4),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
        using var inner = new SKPaint
        {
            Color = dark ? new SKColor(0x00, 0x00, 0x00, 0xB3) : new SKColor(0xFF, 0xFF, 0xFF, 0xB3),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true,
        };
        var outerRect = bounds;
        outerRect.Inflate(-1f, -1f);
        canvas.DrawRect(outerRect, outer);
        var innerRect = bounds;
        innerRect.Inflate(-2.5f, -2.5f);
        canvas.DrawRect(innerRect, inner);
    }
}
