using System.Collections.Generic;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class LinuxApplicationContext
{
	private readonly List<IWindow> _windows = new List<IWindow>();

	private IApplication? _application;

	public IApplication? Application
	{
		get
		{
			return _application;
		}
		set
		{
			_application = value;
			if (_application == null)
			{
				return;
			}
			foreach (IWindow window in _application.Windows)
			{
				if (!_windows.Contains(window))
				{
					_windows.Add(window);
				}
			}
		}
	}

	public IReadOnlyList<IWindow> Windows => _windows;

	public IWindow? MainWindow
	{
		get
		{
			if (_windows.Count <= 0)
			{
				return null;
			}
			return _windows[0];
		}
	}

	public void OpenWindow(IWindow window)
	{
		if (!_windows.Contains(window))
		{
			_windows.Add(window);
		}
	}

	public void CloseWindow(IWindow window)
	{
		_windows.Remove(window);
		if (_windows.Count == 0)
		{
			LinuxApplication.Current?.MainWindow?.Stop();
		}
	}
}
