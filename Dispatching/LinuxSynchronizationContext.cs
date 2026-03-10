// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Dispatching;

/// <summary>
/// SynchronizationContext that routes continuations (from await) to the GLib main loop.
/// Without this, async methods that use await (e.g., in LiveCharts' RunDrawingLoop)
/// would resume on thread pool threads, causing SIGSEGV from concurrent SkiaSharp access.
/// </summary>
public class LinuxSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (LinuxDispatcher.IsMainThread)
        {
            d(state);
        }
        else
        {
            GLibNative.IdleAdd(() =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("LinuxSyncContext", $"Post callback failed: {ex.Message}", ex);
                }
                return false;
            });
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (LinuxDispatcher.IsMainThread)
        {
            d(state);
        }
        else
        {
            using var waitHandle = new ManualResetEventSlim(false);
            Exception? caught = null;
            GLibNative.IdleAdd(() =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    waitHandle.Set();
                }
                return false;
            });
            waitHandle.Wait();
            if (caught != null)
                throw caught;
        }
    }

    public override SynchronizationContext CreateCopy() => new LinuxSynchronizationContext();
}
