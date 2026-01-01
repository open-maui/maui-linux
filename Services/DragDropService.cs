using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragDropService : IDisposable
{
	private struct XClientMessageEvent
	{
		public int type;

		public ulong serial;

		public bool send_event;

		public IntPtr display;

		public IntPtr window;

		public IntPtr message_type;

		public int format;

		public IntPtr data0;

		public IntPtr data1;

		public IntPtr data2;

		public IntPtr data3;

		public IntPtr data4;
	}

	private IntPtr _display;

	private IntPtr _window;

	private bool _isDragging;

	private DragData? _currentDragData;

	private IntPtr _dragSource;

	private IntPtr _dragTarget;

	private bool _disposed;

	private IntPtr _xdndAware;

	private IntPtr _xdndEnter;

	private IntPtr _xdndPosition;

	private IntPtr _xdndStatus;

	private IntPtr _xdndLeave;

	private IntPtr _xdndDrop;

	private IntPtr _xdndFinished;

	private IntPtr _xdndSelection;

	private IntPtr _xdndActionCopy;

	private IntPtr _xdndActionMove;

	private IntPtr _xdndActionLink;

	private IntPtr _xdndTypeList;

	private IntPtr _textPlain;

	private IntPtr _textUri;

	private IntPtr _applicationOctetStream;

	private const int ClientMessage = 33;

	private const int PropModeReplace = 0;

	private static readonly IntPtr XA_ATOM = (IntPtr)4;

	public bool IsDragging => _isDragging;

	public event EventHandler<DragEventArgs>? DragEnter;

	public event EventHandler<DragEventArgs>? DragOver;

	public event EventHandler? DragLeave;

	public event EventHandler<DropEventArgs>? Drop;

	public void Initialize(IntPtr display, IntPtr window)
	{
		_display = display;
		_window = window;
		InitializeAtoms();
		SetXdndAware();
	}

	private void InitializeAtoms()
	{
		_xdndAware = XInternAtom(_display, "XdndAware", onlyIfExists: false);
		_xdndEnter = XInternAtom(_display, "XdndEnter", onlyIfExists: false);
		_xdndPosition = XInternAtom(_display, "XdndPosition", onlyIfExists: false);
		_xdndStatus = XInternAtom(_display, "XdndStatus", onlyIfExists: false);
		_xdndLeave = XInternAtom(_display, "XdndLeave", onlyIfExists: false);
		_xdndDrop = XInternAtom(_display, "XdndDrop", onlyIfExists: false);
		_xdndFinished = XInternAtom(_display, "XdndFinished", onlyIfExists: false);
		_xdndSelection = XInternAtom(_display, "XdndSelection", onlyIfExists: false);
		_xdndActionCopy = XInternAtom(_display, "XdndActionCopy", onlyIfExists: false);
		_xdndActionMove = XInternAtom(_display, "XdndActionMove", onlyIfExists: false);
		_xdndActionLink = XInternAtom(_display, "XdndActionLink", onlyIfExists: false);
		_xdndTypeList = XInternAtom(_display, "XdndTypeList", onlyIfExists: false);
		_textPlain = XInternAtom(_display, "text/plain", onlyIfExists: false);
		_textUri = XInternAtom(_display, "text/uri-list", onlyIfExists: false);
		_applicationOctetStream = XInternAtom(_display, "application/octet-stream", onlyIfExists: false);
	}

	private void SetXdndAware()
	{
		int data = 5;
		XChangeProperty(_display, _window, _xdndAware, XA_ATOM, 32, 0, ref data, 1);
	}

	public bool ProcessClientMessage(IntPtr messageType, IntPtr[] data)
	{
		if (messageType == _xdndEnter)
		{
			return HandleXdndEnter(data);
		}
		if (messageType == _xdndPosition)
		{
			return HandleXdndPosition(data);
		}
		if (messageType == _xdndLeave)
		{
			return HandleXdndLeave(data);
		}
		if (messageType == _xdndDrop)
		{
			return HandleXdndDrop(data);
		}
		return false;
	}

	private bool HandleXdndEnter(IntPtr[] data)
	{
		_dragSource = data[0];
		_ = data[1];
		bool num = ((nint)data[1] & 1) != 0;
		List<IntPtr> list = new List<IntPtr>();
		if (num)
		{
			list = GetTypeList(_dragSource);
		}
		else
		{
			for (int i = 2; i < 5; i++)
			{
				if (data[i] != IntPtr.Zero)
				{
					list.Add(data[i]);
				}
			}
		}
		_currentDragData = new DragData
		{
			SourceWindow = _dragSource,
			SupportedTypes = list.ToArray()
		};
		this.DragEnter?.Invoke(this, new DragEventArgs(_currentDragData, 0, 0));
		return true;
	}

	private bool HandleXdndPosition(IntPtr[] data)
	{
		if (_currentDragData == null)
		{
			return false;
		}
		int x = (int)(((nint)data[2] >> 16) & 0xFFFF);
		int y = (int)((nint)data[2] & 0xFFFF);
		IntPtr atom = data[4];
		DragEventArgs e = new DragEventArgs(_currentDragData, x, y)
		{
			AllowedAction = GetDragAction(atom)
		};
		this.DragOver?.Invoke(this, e);
		SendXdndStatus(e.Accepted, e.AcceptedAction);
		return true;
	}

	private bool HandleXdndLeave(IntPtr[] data)
	{
		_currentDragData = null;
		_dragSource = IntPtr.Zero;
		this.DragLeave?.Invoke(this, EventArgs.Empty);
		return true;
	}

	private bool HandleXdndDrop(IntPtr[] data)
	{
		if (_currentDragData == null)
		{
			return false;
		}
		uint timestamp = (uint)(nint)data[2];
		string droppedData = RequestDropData(timestamp);
		DropEventArgs e = new DropEventArgs(_currentDragData, droppedData);
		this.Drop?.Invoke(this, e);
		SendXdndFinished(e.Handled);
		_currentDragData = null;
		_dragSource = IntPtr.Zero;
		return true;
	}

	private List<IntPtr> GetTypeList(IntPtr window)
	{
		List<IntPtr> list = new List<IntPtr>();
		if (XGetWindowProperty(_display, window, _xdndTypeList, 0L, 1024L, delete: false, XA_ATOM, out var _, out var _, out var nitems, out var _, out var data) == 0 && data != IntPtr.Zero)
		{
			for (int i = 0; i < (int)(nint)nitems; i++)
			{
				IntPtr item = Marshal.ReadIntPtr(data, i * IntPtr.Size);
				list.Add(item);
			}
			XFree(data);
		}
		return list;
	}

	private void SendXdndStatus(bool accepted, DragAction action)
	{
		XClientMessageEvent xevent = new XClientMessageEvent
		{
			type = 33,
			window = _dragSource,
			message_type = _xdndStatus,
			format = 32
		};
		xevent.data0 = _window;
		xevent.data1 = (IntPtr)(accepted ? 1 : 0);
		xevent.data2 = (IntPtr)0;
		xevent.data3 = (IntPtr)0;
		xevent.data4 = GetActionAtom(action);
		XSendEvent(_display, _dragSource, propagate: false, 0L, ref xevent);
		XFlush(_display);
	}

	private void SendXdndFinished(bool accepted)
	{
		XClientMessageEvent xevent = new XClientMessageEvent
		{
			type = 33,
			window = _dragSource,
			message_type = _xdndFinished,
			format = 32
		};
		xevent.data0 = _window;
		xevent.data1 = (IntPtr)(accepted ? 1 : 0);
		xevent.data2 = (accepted ? _xdndActionCopy : IntPtr.Zero);
		XSendEvent(_display, _dragSource, propagate: false, 0L, ref xevent);
		XFlush(_display);
	}

	private string? RequestDropData(uint timestamp)
	{
		IntPtr target = _textPlain;
		if (_currentDragData != null)
		{
			IntPtr[] supportedTypes = _currentDragData.SupportedTypes;
			for (int i = 0; i < supportedTypes.Length; i++)
			{
				if (supportedTypes[i] == _textUri)
				{
					target = _textUri;
					break;
				}
			}
		}
		XConvertSelection(_display, _xdndSelection, target, _xdndSelection, _window, timestamp);
		XFlush(_display);
		return null;
	}

	private DragAction GetDragAction(IntPtr atom)
	{
		if (atom == _xdndActionCopy)
		{
			return DragAction.Copy;
		}
		if (atom == _xdndActionMove)
		{
			return DragAction.Move;
		}
		if (atom == _xdndActionLink)
		{
			return DragAction.Link;
		}
		return DragAction.None;
	}

	private IntPtr GetActionAtom(DragAction action)
	{
		return action switch
		{
			DragAction.Copy => _xdndActionCopy, 
			DragAction.Move => _xdndActionMove, 
			DragAction.Link => _xdndActionLink, 
			_ => IntPtr.Zero, 
		};
	}

	public void StartDrag(DragData data)
	{
		if (!_isDragging)
		{
			_isDragging = true;
			_currentDragData = data;
		}
	}

	public void CancelDrag()
	{
		_isDragging = false;
		_currentDragData = null;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
		}
	}

	[DllImport("libX11.so.6")]
	private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

	[DllImport("libX11.so.6")]
	private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, ref int data, int nelements);

	[DllImport("libX11.so.6")]
	private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, long offset, long length, bool delete, IntPtr reqType, out IntPtr actualType, out int actualFormat, out IntPtr nitems, out IntPtr bytesAfter, out IntPtr data);

	[DllImport("libX11.so.6")]
	private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, long eventMask, ref XClientMessageEvent xevent);

	[DllImport("libX11.so.6")]
	private static extern int XConvertSelection(IntPtr display, IntPtr selection, IntPtr target, IntPtr property, IntPtr requestor, uint time);

	[DllImport("libX11.so.6")]
	private static extern void XFree(IntPtr ptr);

	[DllImport("libX11.so.6")]
	private static extern void XFlush(IntPtr display);
}
