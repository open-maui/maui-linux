using System;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Dispatching;

public class LinuxDispatcher : IDispatcher
{
    private static int _mainThreadId;

    private static LinuxDispatcher? _mainDispatcher;

    private static readonly object _lock = new object();

    public static LinuxDispatcher? Main => _mainDispatcher;

    public static bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    public bool IsDispatchRequired => !IsMainThread;

    public static void Initialize()
    {
        lock (_lock)
        {
            _mainThreadId = Environment.CurrentManagedThreadId;
            _mainDispatcher = new LinuxDispatcher();
            Console.WriteLine($"[LinuxDispatcher] Initialized on thread {_mainThreadId}");
        }
    }

    public bool Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action, "action");
        if (!IsDispatchRequired)
        {
            action();
            return true;
        }
        GLibNative.IdleAdd(delegate
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[LinuxDispatcher] Error in dispatched action: " + ex.Message);
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
                Console.WriteLine("[LinuxDispatcher] Error in delayed action: " + ex.Message);
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
