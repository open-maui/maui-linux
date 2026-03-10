using System;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Dispatching;

public class LinuxDispatcher : IDispatcher
{
    private static int _mainThreadId;

    private static LinuxDispatcher? _mainDispatcher;

    private static readonly Lock _lock = new();

    public static LinuxDispatcher? Main => _mainDispatcher;

    public static bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    public bool IsDispatchRequired => !IsMainThread;

    public static void Initialize()
    {
        lock (_lock)
        {
            _mainThreadId = Environment.CurrentManagedThreadId;
            _mainDispatcher = new LinuxDispatcher();

            // Install SynchronizationContext so that await continuations
            // (e.g., in LiveCharts' RunDrawingLoop) route back to the main thread
            // instead of resuming on thread pool threads.
            if (SynchronizationContext.Current == null)
            {
                SynchronizationContext.SetSynchronizationContext(new LinuxSynchronizationContext());
                DiagnosticLog.Debug("LinuxDispatcher", "Installed LinuxSynchronizationContext");
            }

            DiagnosticLog.Debug("LinuxDispatcher", $"Initialized on thread {_mainThreadId}");
        }
    }

    private static int _bgDispatchCount;

    public bool Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action, "action");
        if (!IsDispatchRequired)
        {
            action();
            return true;
        }
        int seq = System.Threading.Interlocked.Increment(ref _bgDispatchCount);
        if (seq <= 20)
            DiagnosticLog.Error("LinuxDispatcher", $"Dispatch #{seq}: queuing from thread {Environment.CurrentManagedThreadId} (main={_mainThreadId})");
        GLibNative.IdleAdd(delegate
        {
            try
            {
                if (seq <= 20)
                    DiagnosticLog.Error("LinuxDispatcher", $"Dispatch #{seq}: EXECUTING on thread {Environment.CurrentManagedThreadId}");
                action();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxDispatcher", $"Dispatch #{seq}: EXCEPTION: {ex.GetType().Name}: {ex.Message}", ex);
            }
            return false;
        });
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action, "action");
        GLibNative.TimeoutAdd((uint)Math.Max(0.0, delay.TotalMilliseconds), delegate
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxDispatcher", "Error in delayed action", ex);
            }
            return false;
        });
        return true;
    }

    public IDispatcherTimer CreateTimer()
    {
        return new LinuxDispatcherTimer(this);
    }
}
