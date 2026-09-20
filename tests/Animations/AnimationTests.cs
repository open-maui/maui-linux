// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Moq;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Animations;

using Animation = Microsoft.Maui.Controls.Animation;
using Easing = Microsoft.Maui.Easing;

#region Test doubles

/// <summary>
/// An <see cref="ITicker"/> a test fires by hand against a fake clock. It
/// exposes <see cref="ITickerClock"/> so <see cref="LinuxAnimationManager"/>
/// measures frame deltas from the fake clock rather than wall time, which is
/// what makes every animation assertion below deterministic.
/// </summary>
internal sealed class FakeTicker : ITicker, ITickerClock
{
    public long Timestamp { get; private set; }
    public bool IsRunning { get; private set; }
    public bool SystemEnabled { get; set; } = true;
    public int MaxFps { get; set; } = 60;
    public Action? Fire { get; set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }

    public void Start() { IsRunning = true; StartCount++; }
    public void Stop() { IsRunning = false; StopCount++; }

    /// <summary>Advances the clock by <paramref name="milliseconds"/> and fires one frame.</summary>
    public void Advance(long milliseconds)
    {
        Timestamp += milliseconds;
        Fire?.Invoke();
    }

    /// <summary>Fires <paramref name="frames"/> frames of <paramref name="frameMs"/> each.</summary>
    public void AdvanceFrames(int frames, long frameMs = 16)
    {
        for (int i = 0; i < frames; i++)
            Advance(frameMs);
    }
}

/// <summary>Runs everything inline: the headless tests are the UI thread.</summary>
internal sealed class SynchronousDispatcher : IDispatcher
{
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
}

internal sealed class TestMauiContext : IMauiContext
{
    public TestMauiContext(IServiceProvider services)
    {
        Services = services;
        Handlers = Mock.Of<IMauiHandlersFactory>();
    }

    public IServiceProvider Services { get; }
    public IMauiHandlersFactory Handlers { get; }
}

/// <summary>
/// Builds the minimal headless MAUI context the animation stack needs: the
/// platform animation manager over a <see cref="FakeTicker"/>, a synchronous
/// dispatcher, and a Label attached to the platform LabelHandler so
/// <c>label.FadeTo</c> can find <see cref="IAnimationManager"/> through
/// <c>Handler.MauiContext.Services</c> exactly as MAUI does.
/// </summary>
internal sealed class AnimationFixture
{
    public FakeTicker Ticker { get; } = new();
    public LinuxAnimationManager Manager { get; }
    public IServiceProvider Services { get; }
    public IMauiContext MauiContext { get; }

    public AnimationFixture()
    {
        Manager = new LinuxAnimationManager(Ticker);
        var services = new ServiceCollection();
        services.AddSingleton<ITicker>(Ticker);
        services.AddSingleton<IAnimationManager>(Manager);
        services.AddSingleton<IDispatcher>(new SynchronousDispatcher());
        Services = services.BuildServiceProvider();
        MauiContext = new TestMauiContext(Services);
    }

    public (Label Label, SkiaLabel PlatformView) CreateLabel()
    {
        var label = new Label { Text = "animated" };
        var handler = new LabelHandler();
        handler.SetMauiContext(MauiContext);
        handler.SetVirtualView(label);
        label.Handler.Should().BeSameAs(handler);
        handler.PlatformView.Should().NotBeNull();
        return (label, handler.PlatformView);
    }
}

#endregion

#region MAUI animation API end to end

public class MauiViewAnimationTests
{
    private readonly AnimationFixture _fx = new();

    [Fact]
    public void FadeTo_ReachesZero_AfterEnoughTicks_AndPlatformViewFollows()
    {
        var (label, platform) = _fx.CreateLabel();
        label.Opacity.Should().Be(1.0);
        platform.Opacity.Should().Be(1f);

        var task = label.FadeToAsync(0, 100);

        _fx.Ticker.IsRunning.Should().BeTrue("committing an animation starts the ticker");
        _fx.Ticker.AdvanceFrames(7); // 112 ms >= 100 ms

        label.Opacity.Should().Be(0.0);
        platform.Opacity.Should().Be(0f);
        task.IsCompleted.Should().BeTrue();
        task.Result.Should().BeFalse("false means the animation ran to completion");
        _fx.Ticker.IsRunning.Should().BeFalse("the last animation finishing stops the ticker");
    }

    [Fact]
    public void FadeTo_IsMidway_AtHalfTheDuration()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.FadeToAsync(0, 100);
        _fx.Ticker.Advance(25);
        _fx.Ticker.Advance(25);

        label.Opacity.Should().BeApproximately(0.5, 0.01);
        platform.Opacity.Should().BeApproximately(0.5f, 0.01f);
        task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void TranslateTo_UpdatesTranslation_OnViewAndPlatformView()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.TranslateToAsync(40, -20, 100);
        _fx.Ticker.AdvanceFrames(8);

        label.TranslationX.Should().Be(40);
        label.TranslationY.Should().Be(-20);
        platform.TranslationX.Should().Be(40);
        platform.TranslationY.Should().Be(-20);
        task.Result.Should().BeFalse();
    }

    [Fact]
    public void ScaleTo_UpdatesScale_OnViewAndPlatformView()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.ScaleToAsync(2.0, 100);
        _fx.Ticker.AdvanceFrames(8);

        label.Scale.Should().Be(2.0);
        platform.Scale.Should().Be(2.0);
        task.Result.Should().BeFalse();
    }

    [Fact]
    public void RotateTo_UpdatesRotation_OnViewAndPlatformView()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.RotateToAsync(90, 100);
        _fx.Ticker.AdvanceFrames(8);

        label.Rotation.Should().Be(90);
        platform.Rotation.Should().Be(90);
        task.Result.Should().BeFalse();
    }

    [Fact]
    public void RelRotateTo_AddsToCurrentRotation()
    {
        var (label, platform) = _fx.CreateLabel();
        label.Rotation = 30;

        label.RelRotateToAsync(45, 100);
        _fx.Ticker.AdvanceFrames(8);

        label.Rotation.Should().Be(75);
        platform.Rotation.Should().Be(75);
    }

    [Fact]
    public void RelScaleTo_AddsToCurrentScale()
    {
        var (label, platform) = _fx.CreateLabel();
        label.Scale = 1.5;

        label.RelScaleToAsync(0.5, 100);
        _fx.Ticker.AdvanceFrames(8);

        label.Scale.Should().Be(2.0);
        platform.Scale.Should().Be(2.0);
    }

    [Fact]
    public void LayoutTo_AnimatesBounds()
    {
        var (label, _) = _fx.CreateLabel();
        label.Layout(new Rect(0, 0, 100, 40));

        var task = label.LayoutToAsync(new Rect(50, 20, 200, 80), 100);
        _fx.Ticker.AdvanceFrames(8);

        label.Bounds.X.Should().BeApproximately(50, 0.001);
        label.Bounds.Y.Should().BeApproximately(20, 0.001);
        label.Bounds.Width.Should().BeApproximately(200, 0.001);
        label.Bounds.Height.Should().BeApproximately(80, 0.001);
        task.Result.Should().BeFalse();
    }

    [Fact]
    public void CancelAnimations_StopsMidway_AndReportsCancellation()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.FadeToAsync(0, 100);
        _fx.Ticker.Advance(50);
        label.Opacity.Should().BeApproximately(0.5, 0.01);

        label.CancelAnimations();

        task.IsCompleted.Should().BeTrue();
        task.Result.Should().BeTrue("true means the animation was cancelled");
        _fx.Ticker.IsRunning.Should().BeFalse();

        _fx.Ticker.AdvanceFrames(10);
        label.Opacity.Should().BeApproximately(0.5, 0.01, "a cancelled animation must not keep driving the property");
        platform.Opacity.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact]
    public void StartingTheSameAnimationAgain_CancelsThePreviousOne()
    {
        var (label, _) = _fx.CreateLabel();

        var first = label.FadeToAsync(0, 100);
        _fx.Ticker.Advance(50);
        var second = label.FadeToAsync(1, 100);

        first.IsCompleted.Should().BeTrue();
        first.Result.Should().BeTrue();

        _fx.Ticker.AdvanceFrames(8);
        label.Opacity.Should().Be(1.0);
        second.Result.Should().BeFalse();
    }

    [Fact]
    public void AnimationCommit_DrivesCallbackFromZeroToOne_AndInvokesFinished()
    {
        var (label, _) = _fx.CreateLabel();
        var values = new List<double>();
        double finishedValue = -1;
        bool? finishedCancelled = null;

        new Animation(v => values.Add(v), 0, 1)
            .Commit(label, "custom", 16, 250, Easing.Linear, (v, c) => { finishedValue = v; finishedCancelled = c; });

        values.Should().ContainSingle().Which.Should().Be(0.0, "the start value is applied immediately");

        _fx.Ticker.Advance(125);
        values[^1].Should().BeApproximately(0.5, 0.001);

        _fx.Ticker.Advance(125);
        values[^1].Should().Be(1.0);
        finishedValue.Should().Be(1.0);
        finishedCancelled.Should().BeFalse();
        label.AnimationIsRunning("custom").Should().BeFalse();
    }

    [Fact]
    public void AnimationCommit_AppliesEasing_ToCallbackValues()
    {
        var (label, _) = _fx.CreateLabel();
        var values = new List<double>();

        new Animation(v => values.Add(v), 0, 1)
            .Commit(label, "eased", 16, 200, Easing.CubicIn);

        _fx.Ticker.Advance(50); // 25 % of the way
        values[^1].Should().BeApproximately(Easing.CubicIn.Ease(0.25), 1e-9);
        values[^1].Should().BeApproximately(0.015625, 1e-9);

        _fx.Ticker.Advance(50); // 50 %
        values[^1].Should().BeApproximately(0.125, 1e-9);
    }

    [Fact]
    public void AnimationCommit_WithRepeat_RestartsUntilRepeatReturnsFalse()
    {
        var (label, _) = _fx.CreateLabel();
        int completions = 0;

        new Animation(_ => { }, 0, 1)
            .Commit(label, "looping", 16, 100, Easing.Linear,
                finished: (_, _) => completions++,
                repeat: () => completions < 1); // evaluated before `finished` runs

        _fx.Ticker.AdvanceFrames(7);
        completions.Should().Be(1);
        label.AnimationIsRunning("looping").Should().BeTrue();

        _fx.Ticker.AdvanceFrames(7);
        completions.Should().Be(2);
        label.AnimationIsRunning("looping").Should().BeFalse();
        _fx.Ticker.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void AbortAnimation_StopsANamedAnimation()
    {
        var (label, _) = _fx.CreateLabel();
        double last = -1;

        new Animation(v => last = v, 0, 1).Commit(label, "abortable", 16, 100);
        _fx.Ticker.Advance(50);
        last.Should().BeApproximately(0.5, 0.001);

        label.AbortAnimation("abortable").Should().BeTrue();
        _fx.Ticker.AdvanceFrames(8);

        last.Should().BeApproximately(0.5, 0.001);
        label.AnimationIsRunning("abortable").Should().BeFalse();
    }

    [Fact]
    public void ReducedMotion_CompletesFadeImmediately()
    {
        var (label, platform) = _fx.CreateLabel();
        _fx.Ticker.SystemEnabled = false;

        var task = label.FadeToAsync(0, 100);

        label.Opacity.Should().Be(0.0);
        platform.Opacity.Should().Be(0f);
        task.IsCompleted.Should().BeTrue();
        _fx.Ticker.StartCount.Should().Be(0, "nothing to tween when animations are disabled");
    }
}

#endregion

#region SkiaView animation helpers

/// <summary>
/// The platform's own SkiaView.FadeTo family must ride the same animation
/// manager as MAUI's VisualElement extensions so the two never fight.
/// Needs a LinuxApplication (for the DI animation manager lookup), hence
/// the shared "LinuxApplication.Current" collection.
/// </summary>
[Collection("LinuxApplication.Current")]
public class SkiaViewAnimationExtensionsTests : IDisposable
{
    private readonly AnimationFixture _fx = new();
    private readonly LinuxApplication _app = new();

    public SkiaViewAnimationExtensionsTests()
    {
        _app.MauiContext = _fx.MauiContext;
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void StandaloneSkiaView_FadeTo_UsesTheSharedAnimationManager()
    {
        var view = new SkiaLabel();
        view.MauiView.Should().BeNull();
        view.Opacity = 1f;

        var task = view.FadeTo(0, 100);

        _fx.Manager.Count.Should().Be(1, "the DI animation manager owns the animation");
        _fx.Ticker.IsRunning.Should().BeTrue();

        _fx.Ticker.Advance(50);
        view.Opacity.Should().BeApproximately(0.5f, 0.01f);

        _fx.Ticker.AdvanceFrames(4);
        view.Opacity.Should().Be(0f);
        task.IsCompleted.Should().BeTrue();
        task.Result.Should().BeFalse();
        _fx.Ticker.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void StandaloneSkiaView_CancelAnimations_StopsMidway()
    {
        var view = new SkiaLabel();

        var task = view.TranslateTo(100, 50, 100);
        _fx.Ticker.Advance(50);
        view.TranslationX.Should().BeApproximately(50, 0.01);

        view.CancelAnimations();

        task.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        task.Result.Should().BeTrue();
        _fx.Ticker.AdvanceFrames(8);
        view.TranslationX.Should().BeApproximately(50, 0.01);
        view.TranslationY.Should().BeApproximately(25, 0.01);
    }

    [Fact]
    public void StandaloneSkiaView_ReanimatingTheSameProperty_CancelsThePreviousAnimation()
    {
        var view = new SkiaLabel();

        var first = view.ScaleTo(3, 100);
        _fx.Ticker.Advance(50);
        var second = view.ScaleTo(1, 100);

        first.IsCompleted.Should().BeTrue();
        first.Result.Should().BeTrue();

        _fx.Ticker.AdvanceFrames(8);
        view.Scale.Should().Be(1.0);
        second.Result.Should().BeFalse();
    }

    [Fact]
    public void SkiaViewWithMauiElement_ForwardsToMauiExtensions_SharingTheHandle()
    {
        var (label, platform) = _fx.CreateLabel();
        platform.MauiView.Should().BeSameAs(label);

        var mauiTask = label.FadeToAsync(0, 100);
        _fx.Ticker.Advance(50);

        // Starting the same animation from the Skia side replaces MAUI's
        // instead of running a second tween against the same property.
        var skiaTask = platform.FadeTo(1, 100);
        mauiTask.IsCompleted.Should().BeTrue();
        mauiTask.Result.Should().BeTrue();

        _fx.Ticker.AdvanceFrames(8);
        label.Opacity.Should().Be(1.0);
        platform.Opacity.Should().Be(1f);
        skiaTask.Result.Should().BeFalse();
    }

    [Fact]
    public void SkiaViewWithMauiElement_CancelAnimations_CancelsMauiAnimations()
    {
        var (label, platform) = _fx.CreateLabel();

        var task = label.RotateToAsync(180, 100);
        _fx.Ticker.Advance(50);

        platform.CancelAnimations();

        task.Result.Should().BeTrue();
        _fx.Ticker.AdvanceFrames(8);
        label.Rotation.Should().BeApproximately(90, 0.01);
    }
}

#endregion

#region Ticker

public class LinuxTickerTests
{
    private static (LinuxTicker Ticker, Func<double> Clock, Action<double> AdvanceTo) CreateTicker()
    {
        double now = 0;
        var ticker = new LinuxTicker(() => now);
        return (ticker, () => now, t => now = t);
    }

    [Fact]
    public void Start_And_Stop_ToggleIsRunning()
    {
        var (ticker, _, _) = CreateTicker();

        ticker.IsRunning.Should().BeFalse();
        ticker.Start();
        ticker.IsRunning.Should().BeTrue();
        ticker.Start();
        ticker.IsRunning.Should().BeTrue("Start is idempotent");
        ticker.Stop();
        ticker.IsRunning.Should().BeFalse();
        ticker.Stop();
        ticker.IsRunning.Should().BeFalse("Stop is idempotent");
    }

    [Fact]
    public void Pump_DoesNotFire_WhenStopped()
    {
        var (ticker, _, advanceTo) = CreateTicker();
        int fires = 0;
        ticker.Fire = () => fires++;

        advanceTo(1000);
        ticker.Pump().Should().BeFalse();
        fires.Should().Be(0);

        ticker.Start();
        ticker.Stop();
        advanceTo(2000);
        ticker.Pump().Should().BeFalse();
        fires.Should().Be(0);
    }

    [Fact]
    public void Pump_FiresOncePerFrameInterval_HonouringMaxFps()
    {
        var (ticker, _, advanceTo) = CreateTicker();
        int fires = 0;
        ticker.Fire = () => fires++;
        ticker.MaxFps = 10; // 100 ms frames

        ticker.Start();
        ticker.Pump().Should().BeTrue("the first pump after Start fires immediately");
        fires.Should().Be(1);

        advanceTo(50);
        ticker.Pump().Should().BeFalse();
        fires.Should().Be(1);

        advanceTo(100);
        ticker.Pump().Should().BeTrue();
        fires.Should().Be(2);

        advanceTo(150);
        ticker.Pump().Should().BeFalse();
        advanceTo(200);
        ticker.Pump().Should().BeTrue();
        fires.Should().Be(3);
    }

    [Fact]
    public void Pump_ResynchronisesAfterAStall_InsteadOfBurstFiring()
    {
        var (ticker, _, advanceTo) = CreateTicker();
        int fires = 0;
        ticker.Fire = () => fires++;
        ticker.MaxFps = 100; // 10 ms frames

        ticker.Start();
        ticker.Pump();
        advanceTo(500); // the loop stalled for 50 frames

        ticker.Pump().Should().BeTrue();
        ticker.Pump().Should().BeFalse("only one catch-up frame fires");
        fires.Should().Be(2);

        advanceTo(509);
        ticker.Pump().Should().BeFalse();
        advanceTo(510);
        ticker.Pump().Should().BeTrue();
    }

    [Fact]
    public void MaxFps_IsClamped_AndDrivesTheInterval()
    {
        var (ticker, _, _) = CreateTicker();

        ticker.MaxFps.Should().Be(60);
        ticker.IntervalMilliseconds.Should().BeApproximately(1000.0 / 60, 1e-9);

        ticker.MaxFps = 0;
        ticker.MaxFps.Should().Be(1);
        ticker.IntervalMilliseconds.Should().Be(1000);

        ticker.MaxFps = 100000;
        ticker.MaxFps.Should().Be(240);

        ticker.MaxFps = 30;
        ticker.IntervalMilliseconds.Should().BeApproximately(1000.0 / 30, 1e-9);
    }

    [Fact]
    public void PumpAll_FiresEveryRunningTicker_AndDropsStoppedOnes()
    {
        var (a, _, advanceA) = CreateTicker();
        var (b, _, advanceB) = CreateTicker();
        int firesA = 0, firesB = 0;
        a.Fire = () => firesA++;
        b.Fire = () => firesB++;

        a.Start();
        b.Start();
        LinuxTicker.PumpAll();
        firesA.Should().Be(1);
        firesB.Should().Be(1);

        b.Stop();
        advanceA(100);
        advanceB(100);
        LinuxTicker.PumpAll();
        firesA.Should().Be(2);
        firesB.Should().Be(1, "a stopped ticker leaves the pumped set");

        a.Stop();
    }

    [Fact]
    public void Fire_ThatThrows_DoesNotUnwindThePump()
    {
        var (ticker, _, advanceTo) = CreateTicker();
        int fires = 0;
        ticker.Fire = () => { fires++; throw new InvalidOperationException("boom"); };

        ticker.Start();
        var act = () => ticker.Pump();
        act.Should().NotThrow();
        fires.Should().Be(1);

        advanceTo(100);
        act.Should().NotThrow();
        fires.Should().Be(2);
        ticker.Stop();
    }

    [Fact]
    public void Timestamp_ReflectsTheClock()
    {
        var (ticker, _, advanceTo) = CreateTicker();
        ticker.Timestamp.Should().Be(0);
        advanceTo(1234.9);
        ticker.Timestamp.Should().Be(1234);
    }

    [Fact]
    public void SystemEnabled_IsTrue_WithoutAReduceMotionRequest()
    {
        // The probe is process-wide and evaluated once; in the test process
        // OPENMAUI_REDUCE_MOTION is not set, so animations must be enabled.
        Environment.GetEnvironmentVariable("OPENMAUI_REDUCE_MOTION").Should().BeNullOrEmpty();
        var (ticker, _, _) = CreateTicker();
        ticker.SystemEnabled.Should().BeTrue();
    }
}

#endregion

#region Animation manager

public class LinuxAnimationManagerTests
{
    [Fact]
    public void Add_StartsTicker_AndFinishingLastAnimationStopsIt()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker);
        ticker.Fire.Should().NotBeNull("the manager wires itself to the ticker");

        var animation = new Microsoft.Maui.Animations.Animation(_ => { }, 0, 0.1);
        animation.Commit(manager);

        ticker.IsRunning.Should().BeTrue();
        ticker.StartCount.Should().Be(1);
        manager.Count.Should().Be(1);

        ticker.AdvanceFrames(8);

        animation.HasFinished.Should().BeTrue();
        manager.Count.Should().Be(0);
        ticker.IsRunning.Should().BeFalse();
        ticker.StopCount.Should().Be(1);
    }

    [Fact]
    public void Tick_AdvancesByTheTickerClockDelta()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker);
        double progress = -1;

        new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear).Commit(manager);

        ticker.Advance(250);
        progress.Should().BeApproximately(0.25, 1e-9);
        ticker.Advance(500);
        progress.Should().BeApproximately(0.75, 1e-9);
        ticker.Advance(1000);
        progress.Should().Be(1.0);
    }

    [Fact]
    public void SpeedModifier_ScalesElapsedTime()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker) { SpeedModifier = 2.0 };
        double progress = -1;

        new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear).Commit(manager);

        ticker.Advance(250);
        progress.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Remove_DropsAnimation_AndStopsTickerWhenEmpty()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker);
        double progress = -1;
        var animation = new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear);
        animation.Commit(manager);
        ticker.Advance(100);

        manager.Remove(animation);

        manager.Count.Should().Be(0);
        ticker.IsRunning.Should().BeFalse();
        ticker.Advance(1000);
        progress.Should().BeApproximately(0.1, 1e-9, "a removed animation is never ticked again");
    }

    [Fact]
    public void AutoStartTicker_False_LeavesTickerStopped()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker) { AutoStartTicker = false };

        new Microsoft.Maui.Animations.Animation(_ => { }, 0, 1.0).Commit(manager);

        ticker.IsRunning.Should().BeFalse();
        manager.Count.Should().Be(1);
    }

    [Fact]
    public void SystemDisabled_ForceFinishesOnAdd()
    {
        var ticker = new FakeTicker { SystemEnabled = false };
        var manager = new LinuxAnimationManager(ticker);
        double progress = -1;
        bool finished = false;

        new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear, finished: () => finished = true).Commit(manager);

        progress.Should().Be(1.0);
        finished.Should().BeTrue();
        manager.Count.Should().Be(0);
        ticker.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void SystemDisabled_MidFlight_ForceFinishesOnNextFire()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker);
        double progress = -1;

        new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear).Commit(manager);
        ticker.Advance(100);
        progress.Should().BeApproximately(0.1, 1e-9);

        ticker.SystemEnabled = false;
        ticker.Advance(100);

        progress.Should().Be(1.0);
        manager.Count.Should().Be(0);
        ticker.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ParentAnimation_TicksChildren()
    {
        var ticker = new FakeTicker();
        var manager = new LinuxAnimationManager(ticker);
        double a = -1, b = -1;

        var parent = new Microsoft.Maui.Animations.Animation();
        parent.Add(0, 1, new Microsoft.Maui.Animations.Animation(v => a = v, 0, 1.0, Easing.Linear));
        parent.Add(0, 1, new Microsoft.Maui.Animations.Animation(v => b = v, 0, 1.0, Easing.Linear));
        parent.Commit(manager);

        ticker.Advance(500);
        a.Should().BeApproximately(0.5, 1e-9);
        b.Should().BeApproximately(0.5, 1e-9);

        ticker.Advance(500);
        parent.HasFinished.Should().BeTrue();
        manager.Count.Should().Be(0);
    }
}

#endregion

#region Easing

public class EasingTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.0625)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.75, 0.9375)]
    [InlineData(1.0, 1.0)]
    public void CubicInOut_MatchesExpectedCurve(double t, double expected)
    {
        Easing.CubicInOut.Ease(t).Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(0.5, 0.125)]
    [InlineData(1.0, 1.0)]
    public void CubicIn_MatchesExpectedCurve(double t, double expected)
    {
        Easing.CubicIn.Ease(t).Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(0.5, 0.875)]
    [InlineData(0.0, 0.0)]
    public void CubicOut_MatchesExpectedCurve(double t, double expected)
    {
        Easing.CubicOut.Ease(t).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Linear_IsIdentity()
    {
        Easing.Linear.Ease(0.3).Should().Be(0.3);
        Easing.Linear.Ease(0.0).Should().Be(0.0);
        Easing.Linear.Ease(1.0).Should().Be(1.0);
    }

    [Fact]
    public void SinCurves_HitTheirEndpoints_AndMeetInTheMiddle()
    {
        Easing.SinIn.Ease(0).Should().BeApproximately(0, 1e-9);
        Easing.SinIn.Ease(1).Should().BeApproximately(1, 1e-9);
        Easing.SinOut.Ease(0).Should().BeApproximately(0, 1e-9);
        Easing.SinOut.Ease(1).Should().BeApproximately(1, 1e-9);
        Easing.SinInOut.Ease(0.5).Should().BeApproximately(0.5, 1e-9);
        Easing.SinIn.Ease(0.5).Should().BeApproximately(1 - Math.Cos(Math.PI / 4), 1e-9);
    }

    [Fact]
    public void BounceAndSpring_EndWhereTheyShould()
    {
        Easing.BounceIn.Ease(0).Should().BeApproximately(0, 1e-6);
        Easing.BounceIn.Ease(1).Should().BeApproximately(1, 1e-6);
        Easing.BounceOut.Ease(0).Should().BeApproximately(0, 1e-6);
        Easing.BounceOut.Ease(1).Should().BeApproximately(1, 1e-6);
        Easing.SpringIn.Ease(1).Should().BeApproximately(1, 1e-6);
        Easing.SpringOut.Ease(0).Should().BeApproximately(0, 1e-6);
        Easing.SpringIn.Ease(0.5).Should().BeLessThan(0.5, "spring-in overshoots below the line early on");
    }

    [Fact]
    public void PlatformNoLongerShadowsMauiEasing()
    {
        // Apps commonly import both Microsoft.Maui and Microsoft.Maui.Platform.Linux;
        // the platform must not define its own Easing type that makes the
        // unqualified name ambiguous.
        typeof(SkiaView).Assembly.GetType("Microsoft.Maui.Platform.Linux.Easing").Should().BeNull();
        typeof(SkiaView).Assembly.GetType("Microsoft.Maui.Platform.Linux.AnimationManager").Should().BeNull();
    }
}

#endregion
