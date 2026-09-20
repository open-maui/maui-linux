// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Drives MAUI's own <see cref="VisualStateManager"/> (the CommonStates a
/// XAML Style declares: Normal, PointerOver, Pressed, Focused, Disabled) from
/// the Skia platform view's interaction events.
/// </summary>
/// <remarks>
/// <para>
/// MAUI already computes the state itself in <c>VisualElement.ChangeVisualState()</c>
/// from <c>IsEnabled</c>, <c>IsFocused</c>, its private pointer-over flag and,
/// for buttons, <c>IsPressed</c>. On the shipped platforms the native view
/// feeds those inputs; on Linux nothing did, so styles with
/// VisualStateGroups never left "Normal". The bridge supplies exactly those
/// inputs and lets MAUI decide the state, so subclass overrides such as
/// <c>CheckBox.ChangeVisualState</c> (the IsChecked state) keep their semantics:
/// </para>
/// <list type="bullet">
/// <item>Pointer enter/exit sets MAUI's private pointer-over flag via
/// <c>VisualElement.SetPointerOver(bool, bool)</c> (reflection, cached), which
/// calls <c>ChangeVisualState()</c>. Without that flag MAUI would fall back to
/// "Normal" on the next recompute (e.g. a button release) while still hovered.</item>
/// <item>Focus gained/lost sets <see cref="IView.IsFocused"/>, which raises
/// <c>Focused</c>/<c>Unfocused</c> and recomputes the state.</item>
/// <item>Press/release goes straight to <c>GoToState("Pressed")</c>; the
/// button handlers additionally set <c>IsPressed</c> through <c>IButton.Pressed()</c>.
/// Release recomputes through <c>ChangeVisualStateInternal()</c>.</item>
/// <item>IsEnabled changes are already handled by MAUI's property-changed
/// callback; nothing to bridge.</item>
/// </list>
/// <para>
/// Two feeds are consumed: the base <see cref="SkiaView"/> pointer/focus
/// events (views that call base), and <see cref="SkiaView.VisualStateRequested"/>,
/// raised by every <see cref="SkiaVisualStateManager.GoToState"/> call
/// (interactive controls that override the pointer methods without calling
/// base but report their state to the Skia-side VSM). Both are idempotent.
/// </para>
/// </remarks>
public static class VisualStateBridge
{
    private const string Tag = "VisualStateBridge";

    /// <summary>MAUI declares no CommonStates constant for the button-only Pressed state.</summary>
    private const string PressedState = "Pressed";

    private static readonly ConditionalWeakTable<SkiaView, Binding> s_bindings = new();

    private static readonly MethodInfo? s_setPointerOver = typeof(VisualElement).GetMethod(
        "SetPointerOver", BindingFlags.Instance | BindingFlags.NonPublic, null,
        new[] { typeof(bool), typeof(bool) }, null);

    private static readonly MethodInfo? s_changeVisualState = typeof(VisualElement).GetMethod(
        "ChangeVisualStateInternal", BindingFlags.Instance | BindingFlags.NonPublic, null,
        Type.EmptyTypes, null);

    private static bool s_reflectionWarned;

    /// <summary>
    /// Connects <paramref name="platformView"/>'s interaction events to
    /// <paramref name="view"/>'s VisualStateManager. Safe to call more than
    /// once for the same pair; re-attaching to a different MAUI view replaces
    /// the previous binding.
    /// </summary>
    public static void Attach(IView? view, SkiaView? platformView)
    {
        if (platformView == null || view is not VisualElement element)
            return;

        if (s_bindings.TryGetValue(platformView, out var existing))
        {
            if (ReferenceEquals(existing.Element, element))
                return;
            existing.Dispose();
            s_bindings.Remove(platformView);
        }

        s_bindings.Add(platformView, new Binding(element, platformView));
    }

    /// <summary>
    /// Disconnects a binding made by <see cref="Attach"/>. No-op when none exists.
    /// </summary>
    public static void Detach(SkiaView? platformView)
    {
        if (platformView == null) return;
        if (s_bindings.TryGetValue(platformView, out var binding))
        {
            binding.Dispose();
            s_bindings.Remove(platformView);
        }
    }

    /// <summary>
    /// True when <paramref name="platformView"/> currently has a bridge binding.
    /// </summary>
    public static bool IsAttached(SkiaView platformView) => s_bindings.TryGetValue(platformView, out _);

    private static void SetPointerOver(VisualElement element, bool value)
    {
        if (s_setPointerOver != null)
        {
            try
            {
                s_setPointerOver.Invoke(element, new object[] { value, true });
                return;
            }
            catch (Exception ex)
            {
                WarnReflectionOnce(ex);
            }
        }

        // Fallback: compute the CommonStates transition ourselves.
        if (!element.IsEnabled)
            VisualStateManager.GoToState(element, VisualStateManager.CommonStates.Disabled);
        else if (value)
            VisualStateManager.GoToState(element, VisualStateManager.CommonStates.PointerOver);
        else if (element.IsFocused)
            VisualStateManager.GoToState(element, VisualStateManager.CommonStates.Focused);
        else
            VisualStateManager.GoToState(element, VisualStateManager.CommonStates.Normal);
    }

    private static void ChangeVisualState(VisualElement element, bool pointerOver)
    {
        if (s_changeVisualState != null)
        {
            try
            {
                s_changeVisualState.Invoke(element, null);
                return;
            }
            catch (Exception ex)
            {
                WarnReflectionOnce(ex);
            }
        }

        SetPointerOver(element, pointerOver);
    }

    private static void WarnReflectionOnce(Exception ex)
    {
        if (s_reflectionWarned) return;
        s_reflectionWarned = true;
        DiagnosticLog.Warn(Tag, $"MAUI VisualElement internals unavailable, using computed states: {ex.Message}");
    }

    private sealed class Binding : IDisposable
    {
        private readonly SkiaView _platformView;
        private bool _pointerOver;
        private bool _pressed;
        private bool _disposed;

        public VisualElement Element { get; }

        public Binding(VisualElement element, SkiaView platformView)
        {
            Element = element;
            _platformView = platformView;

            platformView.PointerEntered += OnPointerEntered;
            platformView.PointerExited += OnPointerExited;
            platformView.PointerPressed += OnPointerPressed;
            platformView.PointerReleased += OnPointerReleased;
            platformView.FocusGained += OnFocusGained;
            platformView.FocusLost += OnFocusLost;
            platformView.VisualStateRequested += OnVisualStateRequested;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _platformView.PointerEntered -= OnPointerEntered;
            _platformView.PointerExited -= OnPointerExited;
            _platformView.PointerPressed -= OnPointerPressed;
            _platformView.PointerReleased -= OnPointerReleased;
            _platformView.FocusGained -= OnFocusGained;
            _platformView.FocusLost -= OnFocusLost;
            _platformView.VisualStateRequested -= OnVisualStateRequested;
        }

        private void OnPointerEntered(object? sender, PointerEventArgs e) => Sync(pointerOver: true, pressed: _pressed);

        private void OnPointerExited(object? sender, PointerEventArgs e) => Sync(pointerOver: false, pressed: false);

        private void OnPointerPressed(object? sender, PointerEventArgs e) => Sync(pointerOver: _pointerOver, pressed: true);

        private void OnPointerReleased(object? sender, PointerEventArgs e) => Sync(pointerOver: _pointerOver, pressed: false);

        private void OnFocusGained(object? sender, EventArgs e) => Focus(true);

        private void OnFocusLost(object? sender, EventArgs e) => Focus(false);

        private void OnVisualStateRequested(object? sender, string stateName)
        {
            switch (stateName)
            {
                case SkiaVisualStateManager.CommonStates.PointerOver:
                    Sync(pointerOver: true, pressed: false);
                    break;
                case SkiaVisualStateManager.CommonStates.Pressed:
                    Sync(pointerOver: _pointerOver, pressed: true);
                    break;
                case SkiaVisualStateManager.CommonStates.Focused:
                    Sync(pointerOver: _pointerOver, pressed: false);
                    break;
                case SkiaVisualStateManager.CommonStates.Normal:
                case SkiaVisualStateManager.CommonStates.Disabled:
                    // Interactive controls report pointer exit and release as
                    // a return to Normal (or Disabled when IsEnabled is false).
                    Sync(pointerOver: false, pressed: false);
                    break;
            }
        }

        /// <summary>
        /// Applies a new (pointerOver, pressed) pair. Only transitions that
        /// actually change something touch the element, so the two feeds can
        /// report the same interaction twice without side effects.
        /// </summary>
        private void Sync(bool pointerOver, bool pressed)
        {
            if (_disposed) return;

            bool overChanged = _pointerOver != pointerOver;
            bool pressChanged = _pressed != pressed;
            _pointerOver = pointerOver;
            _pressed = pressed;

            if (!overChanged && !pressChanged) return;

            try
            {
                if (overChanged)
                    SetPointerOver(Element, pointerOver);   // recomputes via ChangeVisualState
                else
                    ChangeVisualState(Element, pointerOver); // release: leave "Pressed"

                if (pressed && pressChanged && Element.IsEnabled)
                    VisualStateManager.GoToState(Element, PressedState);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, $"State sync (over={pointerOver}, pressed={pressed}) failed for {Element.GetType().Name}: {ex.Message}");
            }
        }

        private void Focus(bool value)
        {
            if (_disposed) return;
            if (Element.IsFocused == value) return;

            try
            {
                // The IView setter is the platform's channel into VisualElement:
                // it raises Focused/Unfocused and recomputes the visual state.
                ((IView)Element).IsFocused = value;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, $"Focus({value}) failed for {Element.GetType().Name}: {ex.Message}");
            }
        }
    }
}
