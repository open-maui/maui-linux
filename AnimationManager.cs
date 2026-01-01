// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux;

public static class AnimationManager
{
    private class RunningAnimation
    {
        public required SkiaView View { get; set; }
        public string PropertyName { get; set; } = "";
        public double StartValue { get; set; }
        public double EndValue { get; set; }
        public DateTime StartTime { get; set; }
        public uint Duration { get; set; }
        public Easing Easing { get; set; } = Easing.Linear;
        public required TaskCompletionSource<bool> Completion { get; set; }
        public CancellationToken Token { get; set; }
    }

    private static readonly List<RunningAnimation> _animations = new();
    private static bool _isRunning;
    private static CancellationTokenSource? _cts;

    private static void EnsureRunning()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _ = RunAnimationLoop(_cts.Token);
        }
    }

    private static async Task RunAnimationLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _animations.Count > 0)
        {
            var now = DateTime.UtcNow;
            var completed = new List<RunningAnimation>();

            foreach (var animation in _animations.ToList())
            {
                if (animation.Token.IsCancellationRequested)
                {
                    completed.Add(animation);
                    animation.Completion.TrySetResult(false);
                    continue;
                }

                var progress = Math.Clamp(
                    (now - animation.StartTime).TotalMilliseconds / animation.Duration,
                    0.0, 1.0);

                var easedProgress = animation.Easing.Ease(progress);
                var value = animation.StartValue + (animation.EndValue - animation.StartValue) * easedProgress;

                SetProperty(animation.View, animation.PropertyName, value);

                if (progress >= 1.0)
                {
                    completed.Add(animation);
                    animation.Completion.TrySetResult(true);
                }
            }

            foreach (var animation in completed)
            {
                _animations.Remove(animation);
            }

            if (_animations.Count == 0)
            {
                _isRunning = false;
                return;
            }

            await Task.Delay(16, token);
        }

        _isRunning = false;
    }

    private static void SetProperty(SkiaView view, string propertyName, double value)
    {
        switch (propertyName)
        {
            case nameof(SkiaView.Opacity):
                view.Opacity = (float)value;
                break;
            case nameof(SkiaView.Scale):
                view.Scale = value;
                break;
            case nameof(SkiaView.ScaleX):
                view.ScaleX = value;
                break;
            case nameof(SkiaView.ScaleY):
                view.ScaleY = value;
                break;
            case nameof(SkiaView.Rotation):
                view.Rotation = value;
                break;
            case nameof(SkiaView.RotationX):
                view.RotationX = value;
                break;
            case nameof(SkiaView.RotationY):
                view.RotationY = value;
                break;
            case nameof(SkiaView.TranslationX):
                view.TranslationX = value;
                break;
            case nameof(SkiaView.TranslationY):
                view.TranslationY = value;
                break;
        }
    }

    private static double GetProperty(SkiaView view, string propertyName)
    {
        return propertyName switch
        {
            nameof(SkiaView.Opacity) => view.Opacity,
            nameof(SkiaView.Scale) => view.Scale,
            nameof(SkiaView.ScaleX) => view.ScaleX,
            nameof(SkiaView.ScaleY) => view.ScaleY,
            nameof(SkiaView.Rotation) => view.Rotation,
            nameof(SkiaView.RotationX) => view.RotationX,
            nameof(SkiaView.RotationY) => view.RotationY,
            nameof(SkiaView.TranslationX) => view.TranslationX,
            nameof(SkiaView.TranslationY) => view.TranslationY,
            _ => 0.0
        };
    }

    public static Task<bool> AnimateAsync(
        SkiaView view,
        string propertyName,
        double targetValue,
        uint length = 250,
        Easing? easing = null,
        CancellationToken cancellationToken = default)
    {
        CancelAnimation(view, propertyName);

        var animation = new RunningAnimation
        {
            View = view,
            PropertyName = propertyName,
            StartValue = GetProperty(view, propertyName),
            EndValue = targetValue,
            StartTime = DateTime.UtcNow,
            Duration = length,
            Easing = easing ?? Easing.Linear,
            Completion = new TaskCompletionSource<bool>(),
            Token = cancellationToken
        };

        _animations.Add(animation);
        EnsureRunning();

        return animation.Completion.Task;
    }

    public static void CancelAnimation(SkiaView view, string propertyName)
    {
        var animation = _animations.FirstOrDefault(a => a.View == view && a.PropertyName == propertyName);
        if (animation != null)
        {
            _animations.Remove(animation);
            animation.Completion.TrySetResult(false);
        }
    }

    public static void CancelAnimations(SkiaView view)
    {
        foreach (var animation in _animations.Where(a => a.View == view).ToList())
        {
            _animations.Remove(animation);
            animation.Completion.TrySetResult(false);
        }
    }
}
