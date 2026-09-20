// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Dispatching;

/// <summary>
/// The UI-thread dispatcher: synchronous on the main thread, GLib-idle
/// marshalled from other threads, delayed dispatch through GLib timeouts, and
/// the dispatcher timer. The GLib main context is pumped explicitly here.
/// </summary>
[Collection("GLibMainLoop")]
public class LinuxDispatcherTests
{
    private static LinuxDispatcher Dispatcher()
    {
        LinuxDispatcher.Initialize();
        return LinuxDispatcher.Main!;
    }

    private static void Pump(Func<bool> until, int maxMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!until() && DateTime.UtcNow < deadline)
        {
            GLibNative.ProcessPendingEvents();
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void On_the_main_thread_dispatch_runs_synchronously()
    {
        var d = Dispatcher();
        d.IsDispatchRequired.Should().BeFalse();
        bool ran = false;
        d.Dispatch(() => ran = true).Should().BeTrue();
        ran.Should().BeTrue();
    }

    [Fact]
    public void From_another_thread_dispatch_is_marshalled_to_the_main_thread()
    {
        var d = Dispatcher();
        int mainThread = Environment.CurrentManagedThreadId;
        int? ranOn = null;
        var t = new Thread(() =>
        {
            d.IsDispatchRequired.Should().BeTrue();
            d.Dispatch(() => ranOn = Environment.CurrentManagedThreadId);
        });
        t.Start(); t.Join();

        Pump(() => ranOn != null);
        ranOn.Should().Be(mainThread);
    }

    [Fact]
    public void Delayed_dispatch_runs_after_the_delay_on_the_main_thread()
    {
        var d = Dispatcher();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long? elapsed = null;
        d.DispatchDelayed(TimeSpan.FromMilliseconds(60), () => elapsed = sw.ElapsedMilliseconds).Should().BeTrue();

        Pump(() => elapsed != null);
        elapsed.Should().NotBeNull();
        elapsed!.Value.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void Exceptions_in_dispatched_work_do_not_kill_the_loop()
    {
        var d = Dispatcher();
        bool second = false;
        var t = new Thread(() =>
        {
            d.Dispatch(() => throw new InvalidOperationException("boom"));
            d.Dispatch(() => second = true);
        });
        t.Start(); t.Join();

        Pump(() => second);
        second.Should().BeTrue();
    }

    [Fact]
    public void Timer_ticks_repeatedly_and_stops()
    {
        var d = Dispatcher();
        var timer = d.CreateTimer();
        int ticks = 0;
        timer.Interval = TimeSpan.FromMilliseconds(20);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        timer.IsRunning.Should().BeTrue();

        Pump(() => ticks >= 3);
        ticks.Should().BeGreaterThanOrEqualTo(3);

        timer.Stop();
        timer.IsRunning.Should().BeFalse();
        int frozen = ticks;
        Pump(() => false, 80);
        ticks.Should().Be(frozen);
    }

    [Fact]
    public void One_shot_timer_fires_once()
    {
        var d = Dispatcher();
        var timer = d.CreateTimer();
        int ticks = 0;
        timer.Interval = TimeSpan.FromMilliseconds(15);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => ticks++;
        timer.Start();

        Pump(() => ticks >= 1);
        Pump(() => false, 80);
        ticks.Should().Be(1);
        timer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Await_continuations_resume_on_the_main_thread_via_the_synchronization_context()
    {
        Dispatcher();
        // xunit installs its own context in tests, so Initialize() leaves it alone;
        // install the platform's explicitly (in an app, Main has none and it is installed).
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new LinuxSynchronizationContext());
        int mainThread = Environment.CurrentManagedThreadId;
        try
        {

        // Force a genuine thread switch, then let the pump run the continuation.
        var tcs = new TaskCompletionSource();
        var continuation = ContinueAsync(tcs.Task);
        new Thread(() => tcs.SetResult()).Start();
        Pump(() => continuation.IsCompleted);

        (await continuation).Should().Be(mainThread);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task<int> ContinueAsync(Task t)
    {
        await t;
        return Environment.CurrentManagedThreadId;
    }
}
