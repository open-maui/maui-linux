using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.Maui.Platform.Linux.Interop;

internal static class X11
{
	private const string LibX11 = "libX11.so.6";

	private const string LibXext = "libXext.so.6";

	public const int ZPixmap = 2;

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XOpenDisplay(IntPtr displayName);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XCloseDisplay(IntPtr display);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDefaultScreen(IntPtr display);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XRootWindow(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDisplayWidth(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDisplayHeight(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDefaultDepth(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XDefaultVisual(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XDefaultColormap(IntPtr display, int screenNumber);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XFlush(IntPtr display);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public static int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard)
	{
		int _discard_native = (discard ? 1 : 0);
		return __PInvoke(display, _discard_native);
		[DllImport("libX11.so.6", EntryPoint = "XSync", ExactSpelling = true)]
		static extern int __PInvoke(IntPtr __display_native, int __discard_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, ulong border, ulong background);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static IntPtr XCreateWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, int depth, uint windowClass, IntPtr visual, ulong valueMask, ref XSetWindowAttributes attributes)
	{
		IntPtr result;
		fixed (XSetWindowAttributes* _attributes_native = &attributes)
		{
			result = __PInvoke(display, parent, x, y, width, height, borderWidth, depth, windowClass, visual, valueMask, _attributes_native);
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XCreateWindow", ExactSpelling = true)]
		static extern unsafe IntPtr __PInvoke(IntPtr __display_native, IntPtr __parent_native, int __x_native, int __y_native, uint __width_native, uint __height_native, uint __borderWidth_native, int __depth_native, uint __windowClass_native, IntPtr __visual_native, ulong __valueMask_native, XSetWindowAttributes* __attributes_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDestroyWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XMapWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XUnmapWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XResizeWindow(IntPtr display, IntPtr window, uint width, uint height);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

	[LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XStoreName(IntPtr display, IntPtr window, string windowName)
	{
		byte* ptr = default(byte*);
		int num = 0;
		Utf8StringMarshaller.ManagedToUnmanagedIn managedToUnmanagedIn = default(Utf8StringMarshaller.ManagedToUnmanagedIn);
		try
		{
			Span<byte> buffer = stackalloc byte[Utf8StringMarshaller.ManagedToUnmanagedIn.BufferSize];
			managedToUnmanagedIn.FromManaged(windowName, buffer);
			ptr = managedToUnmanagedIn.ToUnmanaged();
			return __PInvoke(display, window, ptr);
		}
		finally
		{
			managedToUnmanagedIn.Free();
		}
		[DllImport("libX11.so.6", EntryPoint = "XStoreName", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, IntPtr __window_native, byte* __windowName_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XRaiseWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XLowerWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XNextEvent(IntPtr display, out XEvent eventReturn)
	{
		eventReturn = default(XEvent);
		int result;
		fixed (XEvent* _eventReturn_native = &eventReturn)
		{
			result = __PInvoke(display, _eventReturn_native);
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XNextEvent", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, XEvent* __eventReturn_native);
	}

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XPeekEvent(IntPtr display, out XEvent eventReturn)
	{
		eventReturn = default(XEvent);
		int result;
		fixed (XEvent* _eventReturn_native = &eventReturn)
		{
			result = __PInvoke(display, _eventReturn_native);
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XPeekEvent", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, XEvent* __eventReturn_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XPending(IntPtr display);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool XCheckTypedWindowEvent(IntPtr display, IntPtr window, int eventType, out XEvent eventReturn)
	{
		eventReturn = default(XEvent);
		int num;
		fixed (XEvent* _eventReturn_native = &eventReturn)
		{
			num = __PInvoke(display, window, eventType, _eventReturn_native);
		}
		return num != 0;
		[DllImport("libX11.so.6", EntryPoint = "XCheckTypedWindowEvent", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, IntPtr __window_native, int __eventType_native, XEvent* __eventReturn_native);
	}

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XSendEvent(IntPtr display, IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool propagate, long eventMask, ref XEvent eventSend)
	{
		int _propagate_native = (propagate ? 1 : 0);
		int result;
		fixed (XEvent* _eventSend_native = &eventSend)
		{
			result = __PInvoke(display, window, _propagate_native, eventMask, _eventSend_native);
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XSendEvent", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, IntPtr __window_native, int __propagate_native, long __eventMask_native, XEvent* __eventSend_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern ulong XKeycodeToKeysym(IntPtr display, int keycode, int index);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XLookupString(ref XKeyEvent keyEvent, IntPtr bufferReturn, int bytesBuffer, out ulong keysymReturn, IntPtr statusInOut)
	{
		keysymReturn = 0uL;
		int result;
		fixed (ulong* _keysymReturn_native = &keysymReturn)
		{
			fixed (XKeyEvent* _keyEvent_native = &keyEvent)
			{
				result = __PInvoke(_keyEvent_native, bufferReturn, bytesBuffer, _keysymReturn_native, statusInOut);
			}
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XLookupString", ExactSpelling = true)]
		static extern unsafe int __PInvoke(XKeyEvent* __keyEvent_native, IntPtr __bufferReturn_native, int __bytesBuffer_native, ulong* __keysymReturn_native, IntPtr __statusInOut_native);
	}

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public static int XGrabKeyboard(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode, ulong time)
	{
		int _ownerEvents_native = (ownerEvents ? 1 : 0);
		return __PInvoke(display, grabWindow, _ownerEvents_native, pointerMode, keyboardMode, time);
		[DllImport("libX11.so.6", EntryPoint = "XGrabKeyboard", ExactSpelling = true)]
		static extern int __PInvoke(IntPtr __display_native, IntPtr __grabWindow_native, int __ownerEvents_native, int __pointerMode_native, int __keyboardMode_native, ulong __time_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XUngrabKeyboard(IntPtr display, ulong time);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public static int XGrabPointer(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, uint eventMask, int pointerMode, int keyboardMode, IntPtr confineTo, IntPtr cursor, ulong time)
	{
		int _ownerEvents_native = (ownerEvents ? 1 : 0);
		return __PInvoke(display, grabWindow, _ownerEvents_native, eventMask, pointerMode, keyboardMode, confineTo, cursor, time);
		[DllImport("libX11.so.6", EntryPoint = "XGrabPointer", ExactSpelling = true)]
		static extern int __PInvoke(IntPtr __display_native, IntPtr __grabWindow_native, int __ownerEvents_native, uint __eventMask_native, int __pointerMode_native, int __keyboardMode_native, IntPtr __confineTo_native, IntPtr __cursor_native, ulong __time_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XUngrabPointer(IntPtr display, ulong time);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool XQueryPointer(IntPtr display, IntPtr window, out IntPtr rootReturn, out IntPtr childReturn, out int rootX, out int rootY, out int winX, out int winY, out uint maskReturn)
	{
		rootReturn = (IntPtr)0;
		childReturn = (IntPtr)0;
		rootX = 0;
		rootY = 0;
		winX = 0;
		winY = 0;
		maskReturn = 0u;
		int num;
		fixed (uint* _maskReturn_native = &maskReturn)
		{
			fixed (int* _winY_native = &winY)
			{
				fixed (int* _winX_native = &winX)
				{
					fixed (int* _rootY_native = &rootY)
					{
						fixed (int* _rootX_native = &rootX)
						{
							fixed (IntPtr* _childReturn_native = &childReturn)
							{
								fixed (IntPtr* _rootReturn_native = &rootReturn)
								{
									num = __PInvoke(display, window, _rootReturn_native, _childReturn_native, _rootX_native, _rootY_native, _winX_native, _winY_native, _maskReturn_native);
								}
							}
						}
					}
				}
			}
		}
		return num != 0;
		[DllImport("libX11.so.6", EntryPoint = "XQueryPointer", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, IntPtr __window_native, IntPtr* __rootReturn_native, IntPtr* __childReturn_native, int* __rootX_native, int* __rootY_native, int* __winX_native, int* __winY_native, uint* __maskReturn_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XWarpPointer(IntPtr display, IntPtr srcWindow, IntPtr destWindow, int srcX, int srcY, uint srcWidth, uint srcHeight, int destX, int destY);

	[LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static IntPtr XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists)
	{
		byte* ptr = default(byte*);
		int num = 0;
		nint num2 = 0;
		Utf8StringMarshaller.ManagedToUnmanagedIn managedToUnmanagedIn = default(Utf8StringMarshaller.ManagedToUnmanagedIn);
		try
		{
			num = (onlyIfExists ? 1 : 0);
			Span<byte> buffer = stackalloc byte[Utf8StringMarshaller.ManagedToUnmanagedIn.BufferSize];
			managedToUnmanagedIn.FromManaged(atomName, buffer);
			ptr = managedToUnmanagedIn.ToUnmanaged();
			return __PInvoke(display, ptr, num);
		}
		finally
		{
			managedToUnmanagedIn.Free();
		}
		[DllImport("libX11.so.6", EntryPoint = "XInternAtom", ExactSpelling = true)]
		static extern unsafe IntPtr __PInvoke(IntPtr __display_native, byte* __atomName_native, int __onlyIfExists_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int nelements);

	[LibraryImport("libX11.so.6")]
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "9.0.11.2809")]
	[SkipLocalsInit]
	public unsafe static int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, long longOffset, long longLength, [MarshalAs(UnmanagedType.Bool)] bool delete, IntPtr reqType, out IntPtr actualTypeReturn, out int actualFormatReturn, out IntPtr nitemsReturn, out IntPtr bytesAfterReturn, out IntPtr propReturn)
	{
		actualTypeReturn = (IntPtr)0;
		actualFormatReturn = 0;
		nitemsReturn = (IntPtr)0;
		bytesAfterReturn = (IntPtr)0;
		propReturn = (IntPtr)0;
		int _delete_native = (delete ? 1 : 0);
		int result;
		fixed (IntPtr* _propReturn_native = &propReturn)
		{
			fixed (IntPtr* _bytesAfterReturn_native = &bytesAfterReturn)
			{
				fixed (IntPtr* _nitemsReturn_native = &nitemsReturn)
				{
					fixed (int* _actualFormatReturn_native = &actualFormatReturn)
					{
						fixed (IntPtr* _actualTypeReturn_native = &actualTypeReturn)
						{
							result = __PInvoke(display, window, property, longOffset, longLength, _delete_native, reqType, _actualTypeReturn_native, _actualFormatReturn_native, _nitemsReturn_native, _bytesAfterReturn_native, _propReturn_native);
						}
					}
				}
			}
		}
		return result;
		[DllImport("libX11.so.6", EntryPoint = "XGetWindowProperty", ExactSpelling = true)]
		static extern unsafe int __PInvoke(IntPtr __display_native, IntPtr __window_native, IntPtr __property_native, long __longOffset_native, long __longLength_native, int __delete_native, IntPtr __reqType_native, IntPtr* __actualTypeReturn_native, int* __actualFormatReturn_native, IntPtr* __nitemsReturn_native, IntPtr* __bytesAfterReturn_native, IntPtr* __propReturn_native);
	}

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XSetSelectionOwner(IntPtr display, IntPtr selection, IntPtr owner, ulong time);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XGetSelectionOwner(IntPtr display, IntPtr selection);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XConvertSelection(IntPtr display, IntPtr selection, IntPtr target, IntPtr property, IntPtr requestor, ulong time);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XFree(IntPtr data);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong valueMask, IntPtr values);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XFreeGC(IntPtr display, IntPtr gc);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XCopyArea(IntPtr display, IntPtr src, IntPtr dest, IntPtr gc, int srcX, int srcY, uint width, uint height, int destX, int destY);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XCreateFontCursor(IntPtr display, uint shape);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XFreeCursor(IntPtr display, IntPtr cursor);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XUndefineCursor(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XConnectionNumber(IntPtr display);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XCreateImage(IntPtr display, IntPtr visual, uint depth, int format, int offset, IntPtr data, uint width, uint height, int bitmapPad, int bytesPerLine);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XPutImage(IntPtr display, IntPtr drawable, IntPtr gc, IntPtr image, int srcX, int srcY, int destX, int destY, uint width, uint height);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern int XDestroyImage(IntPtr image);

	[DllImport("libX11.so.6", ExactSpelling = true)]
	[LibraryImport("libX11.so.6")]
	public static extern IntPtr XDefaultGC(IntPtr display, int screen);
}
