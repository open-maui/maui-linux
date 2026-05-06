using System;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Dispatching;

public class LinuxDispatcherTimer : IDispatcherTimer
{
    private readonly LinuxDispatcher _dispatcher;

    private uint _sourceId;

    private TimeSpan _interval = TimeSpan.FromMilliseconds(100);

    private bool _isRepeating = true;

    private bool _isRunning;

    public TimeSpan Interval
    {
        get
        {
            return _interval;
        }
        set
        {
            _interval = value;
            if (_isRunning)
            {
                Stop();
                Start();
            }
        }
    }

    public bool IsRepeating
    {
        get
        {
            return _isRepeating;
        }
        set
        {
            _isRepeating = value;
        }
    }

    public bool IsRunning => _isRunning;

    public event EventHandler? Tick;

    public LinuxDispatcherTimer(LinuxDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Start()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            ScheduleNext();
        }
    }

    public void Stop()
    {
        if (_isRunning)
        {
            _isRunning = false;
            if (_sourceId != 0)
            {
                GLibNative.SourceRemove(_sourceId);
                _sourceId = 0;
            }
        }
    }

    private void ScheduleNext()
    {
        if (!_isRunning)
        {
            return;
        }
        uint intervalMs = (uint)Math.Max(1.0, _interval.TotalMilliseconds);
        _sourceId = GLibNative.TimeoutAdd(intervalMs, delegate
        {
            if (!_isRunning)
            {
                return false;
            }
            try
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxDispatcherTimer", "Error in Tick handler", ex);
            }
            if (_isRepeating && _isRunning)
            {
                return true;
            }
            _isRunning = false;
            _sourceId = 0;
            return false;
        });
    }
}
