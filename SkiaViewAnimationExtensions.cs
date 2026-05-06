// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux;

public static class SkiaViewAnimationExtensions
{
    public static Task<bool> FadeTo(this SkiaView view, double opacity, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.Opacity), opacity, length, easing);
    }

    public static Task<bool> ScaleTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.Scale), scale, length, easing);
    }

    public static Task<bool> ScaleXTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.ScaleX), scale, length, easing);
    }

    public static Task<bool> ScaleYTo(this SkiaView view, double scale, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.ScaleY), scale, length, easing);
    }

    public static Task<bool> RotateTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.Rotation), rotation, length, easing);
    }

    public static Task<bool> RotateXTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.RotationX), rotation, length, easing);
    }

    public static Task<bool> RotateYTo(this SkiaView view, double rotation, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.RotationY), rotation, length, easing);
    }

    public static async Task<bool> TranslateTo(this SkiaView view, double x, double y, uint length = 250, Easing? easing = null)
    {
        var taskX = AnimationManager.AnimateAsync(view, nameof(SkiaView.TranslationX), x, length, easing);
        var taskY = AnimationManager.AnimateAsync(view, nameof(SkiaView.TranslationY), y, length, easing);

        await Task.WhenAll(taskX, taskY);

        return await taskX && await taskY;
    }

    public static Task<bool> RelRotateTo(this SkiaView view, double dRotation, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.Rotation), view.Rotation + dRotation, length, easing);
    }

    public static Task<bool> RelScaleTo(this SkiaView view, double dScale, uint length = 250, Easing? easing = null)
    {
        return AnimationManager.AnimateAsync(view, nameof(SkiaView.Scale), view.Scale + dScale, length, easing);
    }

    public static void CancelAnimations(this SkiaView view)
    {
        AnimationManager.CancelAnimations(view);
    }
}
