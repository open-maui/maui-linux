using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Maui.Platform.Linux.Services;

public class GlobalHotkeyService : IDisposable
{
	[StructLayout(LayoutKind.Explicit)]
	private struct XEvent
	{
		[FieldOffset(0)]
		public int type;

		[FieldOffset(0)]
		public XKeyEvent KeyEvent;
	}

	private struct XKeyEvent
	{
		public int type;

		public ulong serial;

		public bool send_event;

		public IntPtr display;

		public IntPtr window;

		public IntPtr root;

		public IntPtr subwindow;

		public ulong time;

		public int x;

		public int y;

		public int x_root;

		public int y_root;

		public uint state;

		public int keycode;

		public bool same_screen;
	}

	private class HotkeyRegistration
	{
		public int Id { get; set; }

		public int KeyCode { get; set; }

		public uint Modifiers { get; set; }

		public HotkeyKey Key { get; set; }

		public HotkeyModifiers ModifierKeys { get; set; }
	}

	private IntPtr _display;

	private IntPtr _rootWindow;

	private readonly ConcurrentDictionary<int, HotkeyRegistration> _registrations = new ConcurrentDictionary<int, HotkeyRegistration>();

	private int _nextId = 1;

	private bool _disposed;

	private Thread? _eventThread;

	private bool _isListening;

	private const int KeyPress = 2;

	private const int GrabModeAsync = 1;

	private const uint ShiftMask = 1u;

	private const uint LockMask = 2u;

	private const uint ControlMask = 4u;

	private const uint Mod1Mask = 8u;

	private const uint Mod2Mask = 16u;

	private const uint Mod4Mask = 64u;

	private const uint NumLockMask = 16u;

	private const uint CapsLockMask = 2u;

	private const uint ScrollLockMask = 0u;

	public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

	public void Initialize()
	{
		_display = XOpenDisplay(IntPtr.Zero);
		if (_display == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to open X display");
		}
		_rootWindow = XDefaultRootWindow(_display);
		_isListening = true;
		_eventThread = new Thread(ListenForHotkeys)
		{
			IsBackground = true,
			Name = "GlobalHotkeyListener"
		};
		_eventThread.Start();
	}

	public int Register(HotkeyKey key, HotkeyModifiers modifiers)
	{
		if (_display == IntPtr.Zero)
		{
			throw new InvalidOperationException("Service not initialized");
		}
		int num = XKeysymToKeycode(_display, (nint)key);
		if (num == 0)
		{
			throw new ArgumentException($"Invalid key: {key}");
		}
		uint modifierMask = GetModifierMask(modifiers);
		uint[] modifierCombinations = GetModifierCombinations(modifierMask);
		foreach (uint modifiers2 in modifierCombinations)
		{
			if (XGrabKey(_display, num, modifiers2, _rootWindow, ownerEvents: true, 1, 1) == 0)
			{
				Console.WriteLine($"Failed to grab key {key} with modifiers {modifiers}");
			}
		}
		int num2 = _nextId++;
		_registrations[num2] = new HotkeyRegistration
		{
			Id = num2,
			KeyCode = num,
			Modifiers = modifierMask,
			Key = key,
			ModifierKeys = modifiers
		};
		XFlush(_display);
		return num2;
	}

	public void Unregister(int id)
	{
		if (_registrations.TryRemove(id, out HotkeyRegistration value))
		{
			uint[] modifierCombinations = GetModifierCombinations(value.Modifiers);
			foreach (uint modifiers in modifierCombinations)
			{
				XUngrabKey(_display, value.KeyCode, modifiers, _rootWindow);
			}
			XFlush(_display);
		}
	}

	public void UnregisterAll()
	{
		foreach (int item in _registrations.Keys.ToList())
		{
			Unregister(item);
		}
	}

	private void ListenForHotkeys()
	{
		while (_isListening && _display != IntPtr.Zero)
		{
			try
			{
				if (XPending(_display) > 0)
				{
					XEvent xevent = default(XEvent);
					XNextEvent(_display, ref xevent);
					if (xevent.type == 2)
					{
						XKeyEvent keyEvent = xevent.KeyEvent;
						ProcessKeyEvent(keyEvent.keycode, keyEvent.state);
					}
				}
				else
				{
					Thread.Sleep(10);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("GlobalHotkeyService error: " + ex.Message);
			}
		}
	}

	private void ProcessKeyEvent(int keyCode, uint state)
	{
		uint num = state & 0xFFFFFFEDu;
		foreach (HotkeyRegistration value in _registrations.Values)
		{
			if (value.KeyCode == keyCode && (value.Modifiers == num || value.Modifiers == (num & 0xFFFFFFEFu)))
			{
				OnHotkeyPressed(value);
				break;
			}
		}
	}

	private void OnHotkeyPressed(HotkeyRegistration registration)
	{
		this.HotkeyPressed?.Invoke(this, new HotkeyEventArgs(registration.Id, registration.Key, registration.ModifierKeys));
	}

	private uint GetModifierMask(HotkeyModifiers modifiers)
	{
		uint num = 0u;
		if (modifiers.HasFlag(HotkeyModifiers.Shift))
		{
			num |= 1;
		}
		if (modifiers.HasFlag(HotkeyModifiers.Control))
		{
			num |= 4;
		}
		if (modifiers.HasFlag(HotkeyModifiers.Alt))
		{
			num |= 8;
		}
		if (modifiers.HasFlag(HotkeyModifiers.Super))
		{
			num |= 0x40;
		}
		return num;
	}

	private uint[] GetModifierCombinations(uint baseMask)
	{
		return new uint[4]
		{
			baseMask,
			baseMask | 0x10,
			baseMask | 2,
			baseMask | 0x10 | 2
		};
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_isListening = false;
			UnregisterAll();
			if (_display != IntPtr.Zero)
			{
				XCloseDisplay(_display);
				_display = IntPtr.Zero;
			}
		}
	}

	[DllImport("libX11.so.6")]
	private static extern IntPtr XOpenDisplay(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern void XCloseDisplay(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XDefaultRootWindow(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern int XKeysymToKeycode(IntPtr display, IntPtr keysym);

	[DllImport("libX11.so.6")]
	private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow, bool ownerEvents, int pointerMode, int keyboardMode);

	[DllImport("libX11.so.6")]
	private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);

	[DllImport("libX11.so.6")]
	private static extern int XPending(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern int XNextEvent(IntPtr display, ref XEvent xevent);

	[DllImport("libX11.so.6")]
	private static extern void XFlush(IntPtr display);
}
