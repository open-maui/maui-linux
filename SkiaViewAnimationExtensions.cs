// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;
using MauiAnimation = Microsoft.Maui.Animations.Animation;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Animation helpers for the Skia platform view tree. These exist for
/// platform-internal and custom-control code that manipulates a
/// <see cref="SkiaView"/> directly; MAUI applications should animate their
/// <see cref="VisualElement"/>s with the standard
/// <see cref="Microsoft.Maui.Controls.ViewExtensions"/> (<c>label.FadeTo(...)</c>) or
/// <see cref="Microsoft.Maui.Controls.Animation"/> API, which reach the Skia
/// view through the handler mappers.
/// <para>
/// Both routes share one <see cref="IAnimationManager"/> and ticker, so they
/// never fight over a property: when the Skia view is backed by a MAUI
/// element (<see cref="SkiaView.MauiView"/>) every helper here forwards to the
/// corresponding <see cref="Microsoft.Maui.Controls.ViewExtensions"/> call, using the same animation
/// handle names, so a <c>FadeTo</c> started from either side cancels the
/// other. A Skia view with no MAUI element (a standalone Skia tree) is
/// animated with a <see cref="Microsoft.Maui.Animations.Animation"/> committed
/// to the application's animation manager.
/// </para>
/// <para>
/// Return values follow the MAUI convention: the task completes with
/// <see langword="true"/> when the animation was cancelled before reaching its
/// target, <see langword="false"/> when it ran to completion.
/// </para>
/// </summary>
public static class SkiaViewAnimationExtensions
{
    private sealed class RunningAnimation
    {
        public required MauiAnimation Animation { get; init; }
        public required TaskCompletionSource<bool> Completion { get; init; }
    }

    // Standalone-view animations keyed by property name, so re-animating a
    // property replaces the animation in flight (as MAUI's handles do).
    private static readonly ConditionalWeakTable<SkiaView, Dictionary<string, RunningAnimation>> s_running = new();
    private static readonly Lock s_lock = new();
    private static IAnimationManager? s_fallbackManager;

    /// <summary>Animates <see cref="SkiaView.Opacity"/> to <paramref name="opacity"/>.</summary>
    public static Task<bool> FadeTo(this SkiaView view, double opacity, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.FadeToAsync(opacity, length, easing);

        return AnimateAsync(view, nameof(SkiaView.Opacity), view.Opacity, opacity, v => view.Opacity = (float)v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.Scale"/> to <paramref name="scale"/>.</summary>
    public static Task<bool> ScaleTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.ScaleToAsync(scale, length, easing);

        return AnimateAsync(view, nameof(SkiaView.Scale), view.Scale, scale, v => view.Scale = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.ScaleX"/> to <paramref name="scale"/>.</summary>
    public static Task<bool> ScaleXTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.ScaleXToAsync(scale, length, easing);

        return AnimateAsync(view, nameof(SkiaView.ScaleX), view.ScaleX, scale, v => view.ScaleX = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.ScaleY"/> to <paramref name="scale"/>.</summary>
    public static Task<bool> ScaleYTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.ScaleYToAsync(scale, length, easing);

        return AnimateAsync(view, nameof(SkiaView.ScaleY), view.ScaleY, scale, v => view.ScaleY = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.Rotation"/> to <paramref name="rotation"/> degrees.</summary>
    public static Task<bool> RotateTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.RotateToAsync(rotation, length, easing);

        return AnimateAsync(view, nameof(SkiaView.Rotation), view.Rotation, rotation, v => view.Rotation = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.RotationX"/> to <paramref name="rotation"/> degrees.</summary>
    public static Task<bool> RotateXTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.RotateXToAsync(rotation, length, easing);

        return AnimateAsync(view, nameof(SkiaView.RotationX), view.RotationX, rotation, v => view.RotationX = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.RotationY"/> to <paramref name="rotation"/> degrees.</summary>
    public static Task<bool> RotateYTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.RotateYToAsync(rotation, length, easing);

        return AnimateAsync(view, nameof(SkiaView.RotationY), view.RotationY, rotation, v => view.RotationY = v, length, easing);
    }

    /// <summary>Animates <see cref="SkiaView.TranslationX"/>/<see cref="SkiaView.TranslationY"/> to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static async Task<bool> TranslateTo(this SkiaView view, double x, double y, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return await ve.TranslateToAsync(x, y, length, easing);

        var taskX = AnimateAsync(view, nameof(SkiaView.TranslationX), view.TranslationX, x, v => view.TranslationX = v, length, easing);
        var taskY = AnimateAsync(view, nameof(SkiaView.TranslationY), view.TranslationY, y, v => view.TranslationY = v, length, easing);

        await Task.WhenAll(taskX, taskY);
        return taskX.Result || taskY.Result;
    }

    /// <summary>Rotates the view by <paramref name="dRotation"/> degrees relative to its current rotation.</summary>
    public static Task<bool> RelRotateTo(this SkiaView view, double dRotation, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.RelRotateToAsync(dRotation, length, easing);

        return view.RotateTo(view.Rotation + dRotation, length, easing);
    }

    /// <summary>Scales the view by <paramref name="dScale"/> relative to its current scale.</summary>
    public static Task<bool> RelScaleTo(this SkiaView view, double dScale, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            return ve.RelScaleToAsync(dScale, length, easing);

        return view.ScaleTo(view.Scale + dScale, length, easing);
    }

    /// <summary>
    /// Cancels every animation running on the view, both those started here
    /// and (when the view is backed by a MAUI element) those started through
    /// <see cref="Microsoft.Maui.Controls.ViewExtensions"/>.
    /// </summary>
    public static void CancelAnimations(this SkiaView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.MauiView is VisualElement ve)
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(ve);

        RunningAnimation[] running;
        lock (s_lock)
        {
            if (!s_running.TryGetValue(view, out var map) || map.Count == 0)
                return;
            running = map.Values.ToArray();
            map.Clear();
        }

        foreach (var entry in running)
            Cancel(entry);
    }

    private static Task<bool> AnimateAsync(
        SkiaView view,
        string propertyName,
        double start,
        double end,
        Action<double> apply,
        uint length,
        Easing? easing)
    {
        var manager = ResolveAnimationManager();
        var tcs = new TaskCompletionSource<bool>();
        RunningAnimation entry = null!;

        var animation = new MauiAnimation(
            callback: p => apply(start + (end - start) * p),
            start: 0.0,
            duration: length / 1000.0,
            easing: easing ?? Easing.Linear,
            finished: () =>
            {
                Unregister(view, propertyName, entry);
                tcs.TrySetResult(false);
            });

        entry = new RunningAnimation { Animation = animation, Completion = tcs };

        RunningAnimation? previous;
        lock (s_lock)
        {
            var map = s_running.GetOrCreateValue(view);
            map.TryGetValue(propertyName, out previous);
            map[propertyName] = entry;
        }

        if (previous is not null)
            Cancel(previous);

        // Publish the start value immediately, as MAUI's tweener does, so the
        // first frame is consistent even before the ticker fires.
        apply(start);
        animation.Commit(manager);
        return tcs.Task;
    }

    private static void Cancel(RunningAnimation entry)
    {
        entry.Animation.AnimationManager?.Remove(entry.Animation);
        entry.Completion.TrySetResult(true);
    }

    private static void Unregister(SkiaView view, string propertyName, RunningAnimation entry)
    {
        lock (s_lock)
        {
            if (s_running.TryGetValue(view, out var map) &&
                map.TryGetValue(propertyName, out var current) &&
                ReferenceEquals(current, entry))
            {
                map.Remove(propertyName);
            }
        }
    }

    /// <summary>
    /// The application's <see cref="IAnimationManager"/> (the DI singleton the
    /// MAUI extensions use), or a process-wide fallback for Skia trees hosted
    /// without a MAUI application.
    /// </summary>
    private static IAnimationManager ResolveAnimationManager()
    {
        var manager = LinuxApplication.Current?.MauiContext?.Services.GetService<IAnimationManager>();
        if (manager is not null)
            return manager;

        lock (s_lock)
        {
            return s_fallbackManager ??= new LinuxAnimationManager(new LinuxTicker());
        }
    }
}
