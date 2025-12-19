// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides drag and drop functionality using the X11 XDND protocol.
/// </summary>
public class DragDropService : IDisposable
{
    private nint _display;
    private nint _window;
    private bool _isDragging;
    private DragData? _currentDragData;
    private nint _dragSource;
    private nint _dragTarget;
    private bool _disposed;

    // XDND atoms
    private nint _xdndAware;
    private nint _xdndEnter;
    private nint _xdndPosition;
    private nint _xdndStatus;
    private nint _xdndLeave;
    private nint _xdndDrop;
    private nint _xdndFinished;
    private nint _xdndSelection;
    private nint _xdndActionCopy;
    private nint _xdndActionMove;
    private nint _xdndActionLink;
    private nint _xdndTypeList;

    // Common MIME types
    private nint _textPlain;
    private nint _textUri;
    private nint _applicationOctetStream;

    /// <summary>
    /// Gets whether a drag operation is in progress.
    /// </summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// Event raised when a drag enters the window.
    /// </summary>
    public event EventHandler<DragEventArgs>? DragEnter;

    /// <summary>
    /// Event raised when dragging over the window.
    /// </summary>
    public event EventHandler<DragEventArgs>? DragOver;

    /// <summary>
    /// Event raised when a drag leaves the window.
    /// </summary>
    public event EventHandler? DragLeave;

    /// <summary>
    /// Event raised when a drop occurs.
    /// </summary>
    public event EventHandler<DropEventArgs>? Drop;

    /// <summary>
    /// Initializes the drag drop service for the specified window.
    /// </summary>
    public void Initialize(nint display, nint window)
    {
        _display = display;
        _window = window;

        InitializeAtoms();
        SetXdndAware();
    }

    private void InitializeAtoms()
    {
        _xdndAware = XInternAtom(_display, "XdndAware", false);
        _xdndEnter = XInternAtom(_display, "XdndEnter", false);
        _xdndPosition = XInternAtom(_display, "XdndPosition", false);
        _xdndStatus = XInternAtom(_display, "XdndStatus", false);
        _xdndLeave = XInternAtom(_display, "XdndLeave", false);
        _xdndDrop = XInternAtom(_display, "XdndDrop", false);
        _xdndFinished = XInternAtom(_display, "XdndFinished", false);
        _xdndSelection = XInternAtom(_display, "XdndSelection", false);
        _xdndActionCopy = XInternAtom(_display, "XdndActionCopy", false);
        _xdndActionMove = XInternAtom(_display, "XdndActionMove", false);
        _xdndActionLink = XInternAtom(_display, "XdndActionLink", false);
        _xdndTypeList = XInternAtom(_display, "XdndTypeList", false);

        _textPlain = XInternAtom(_display, "text/plain", false);
        _textUri = XInternAtom(_display, "text/uri-list", false);
        _applicationOctetStream = XInternAtom(_display, "application/octet-stream", false);
    }

    private void SetXdndAware()
    {
        // Set XdndAware property to indicate we support XDND version 5
        int version = 5;
        XChangeProperty(_display, _window, _xdndAware, XA_ATOM, 32,
            PropModeReplace, ref version, 1);
    }

    /// <summary>
    /// Processes an X11 client message for drag and drop.
    /// </summary>
    public bool ProcessClientMessage(nint messageType, nint[] data)
    {
        if (messageType == _xdndEnter)
        {
            return HandleXdndEnter(data);
        }
        else if (messageType == _xdndPosition)
        {
            return HandleXdndPosition(data);
        }
        else if (messageType == _xdndLeave)
        {
            return HandleXdndLeave(data);
        }
        else if (messageType == _xdndDrop)
        {
            return HandleXdndDrop(data);
        }

        return false;
    }

    private bool HandleXdndEnter(nint[] data)
    {
        _dragSource = data[0];
        int version = (int)((data[1] >> 24) & 0xFF);
        bool hasTypeList = (data[1] & 1) != 0;

        var types = new List<nint>();

        if (hasTypeList)
        {
            // Get types from XdndTypeList property
            types = GetTypeList(_dragSource);
        }
        else
        {
            // Types are in the message
            for (int i = 2; i < 5; i++)
            {
                if (data[i] != IntPtr.Zero)
                {
                    types.Add(data[i]);
                }
            }
        }

        _currentDragData = new DragData
        {
            SourceWindow = _dragSource,
            SupportedTypes = types.ToArray()
        };

        DragEnter?.Invoke(this, new DragEventArgs(_currentDragData, 0, 0));
        return true;
    }

    private bool HandleXdndPosition(nint[] data)
    {
        if (_currentDragData == null) return false;

        int x = (int)((data[2] >> 16) & 0xFFFF);
        int y = (int)(data[2] & 0xFFFF);
        nint action = data[4];

        var eventArgs = new DragEventArgs(_currentDragData, x, y)
        {
            AllowedAction = GetDragAction(action)
        };

        DragOver?.Invoke(this, eventArgs);

        // Send XdndStatus reply
        SendXdndStatus(eventArgs.Accepted, eventArgs.AcceptedAction);

        return true;
    }

    private bool HandleXdndLeave(nint[] data)
    {
        _currentDragData = null;
        _dragSource = IntPtr.Zero;
        DragLeave?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool HandleXdndDrop(nint[] data)
    {
        if (_currentDragData == null) return false;

        uint timestamp = (uint)data[2];

        // Request the data
        string? droppedData = RequestDropData(timestamp);

        var eventArgs = new DropEventArgs(_currentDragData, droppedData);
        Drop?.Invoke(this, eventArgs);

        // Send XdndFinished
        SendXdndFinished(eventArgs.Handled);

        _currentDragData = null;
        _dragSource = IntPtr.Zero;

        return true;
    }

    private List<nint> GetTypeList(nint window)
    {
        var types = new List<nint>();

        nint actualType;
        int actualFormat;
        nint nitems, bytesAfter;
        nint data;

        int result = XGetWindowProperty(_display, window, _xdndTypeList, 0, 1024, false,
            XA_ATOM, out actualType, out actualFormat, out nitems, out bytesAfter, out data);

        if (result == 0 && data != IntPtr.Zero)
        {
            for (int i = 0; i < (int)nitems; i++)
            {
                nint atom = Marshal.ReadIntPtr(data, i * IntPtr.Size);
                types.Add(atom);
            }
            XFree(data);
        }

        return types;
    }

    private void SendXdndStatus(bool accepted, DragAction action)
    {
        var ev = new XClientMessageEvent
        {
            type = ClientMessage,
            window = _dragSource,
            message_type = _xdndStatus,
            format = 32
        };

        ev.data0 = _window;
        ev.data1 = accepted ? 1 : 0;
        ev.data2 = 0; // x, y of rectangle
        ev.data3 = 0; // width, height of rectangle
        ev.data4 = GetActionAtom(action);

        XSendEvent(_display, _dragSource, false, 0, ref ev);
        XFlush(_display);
    }

    private void SendXdndFinished(bool accepted)
    {
        var ev = new XClientMessageEvent
        {
            type = ClientMessage,
            window = _dragSource,
            message_type = _xdndFinished,
            format = 32
        };

        ev.data0 = _window;
        ev.data1 = accepted ? 1 : 0;
        ev.data2 = accepted ? _xdndActionCopy : IntPtr.Zero;

        XSendEvent(_display, _dragSource, false, 0, ref ev);
        XFlush(_display);
    }

    private string? RequestDropData(uint timestamp)
    {
        // Convert selection to get the data
        nint targetType = _textPlain;

        // Check if text/uri-list is available
        if (_currentDragData != null)
        {
            foreach (var type in _currentDragData.SupportedTypes)
            {
                if (type == _textUri)
                {
                    targetType = _textUri;
                    break;
                }
            }
        }

        // Request selection conversion
        XConvertSelection(_display, _xdndSelection, targetType, _xdndSelection, _window, timestamp);
        XFlush(_display);

        // In a real implementation, we would wait for SelectionNotify event
        // and then get the data. For simplicity, we return null here.
        // The actual data retrieval requires an event loop integration.

        return null;
    }

    private DragAction GetDragAction(nint atom)
    {
        if (atom == _xdndActionCopy) return DragAction.Copy;
        if (atom == _xdndActionMove) return DragAction.Move;
        if (atom == _xdndActionLink) return DragAction.Link;
        return DragAction.None;
    }

    private nint GetActionAtom(DragAction action)
    {
        return action switch
        {
            DragAction.Copy => _xdndActionCopy,
            DragAction.Move => _xdndActionMove,
            DragAction.Link => _xdndActionLink,
            _ => IntPtr.Zero
        };
    }

    /// <summary>
    /// Starts a drag operation.
    /// </summary>
    public void StartDrag(DragData data)
    {
        if (_isDragging) return;

        _isDragging = true;
        _currentDragData = data;

        // Set the drag cursor and initiate the drag
        // This requires integration with the X11 event loop
    }

    /// <summary>
    /// Cancels the current drag operation.
    /// </summary>
    public void CancelDrag()
    {
        _isDragging = false;
        _currentDragData = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    #region X11 Interop

    private const int ClientMessage = 33;
    private const int PropModeReplace = 0;
    private static readonly nint XA_ATOM = (nint)4;

    [StructLayout(LayoutKind.Sequential)]
    private struct XClientMessageEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint window;
        public nint message_type;
        public int format;
        public nint data0;
        public nint data1;
        public nint data2;
        public nint data3;
        public nint data4;
    }

    [DllImport("libX11.so.6")]
    private static extern nint XInternAtom(nint display, string atomName, bool onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(nint display, nint window, nint property, nint type,
        int format, int mode, ref int data, int nelements);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowProperty(nint display, nint window, nint property,
        long offset, long length, bool delete, nint reqType,
        out nint actualType, out int actualFormat, out nint nitems, out nint bytesAfter, out nint data);

    [DllImport("libX11.so.6")]
    private static extern int XSendEvent(nint display, nint window, bool propagate, long eventMask, ref XClientMessageEvent xevent);

    [DllImport("libX11.so.6")]
    private static extern int XConvertSelection(nint display, nint selection, nint target, nint property, nint requestor, uint time);

    [DllImport("libX11.so.6")]
    private static extern void XFree(nint ptr);

    [DllImport("libX11.so.6")]
    private static extern void XFlush(nint display);

    #endregion
}

/// <summary>
/// Contains data for a drag operation.
/// </summary>
public class DragData
{
    /// <summary>
    /// Gets or sets the source window.
    /// </summary>
    public nint SourceWindow { get; set; }

    /// <summary>
    /// Gets or sets the supported MIME types.
    /// </summary>
    public nint[] SupportedTypes { get; set; } = Array.Empty<nint>();

    /// <summary>
    /// Gets or sets the text data.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the file paths.
    /// </summary>
    public string[]? FilePaths { get; set; }

    /// <summary>
    /// Gets or sets custom data.
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Event args for drag events.
/// </summary>
public class DragEventArgs : EventArgs
{
    /// <summary>
    /// Gets the drag data.
    /// </summary>
    public DragData Data { get; }

    /// <summary>
    /// Gets the X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets or sets whether the drop is accepted.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Gets or sets the allowed action.
    /// </summary>
    public DragAction AllowedAction { get; set; }

    /// <summary>
    /// Gets or sets the accepted action.
    /// </summary>
    public DragAction AcceptedAction { get; set; } = DragAction.Copy;

    public DragEventArgs(DragData data, int x, int y)
    {
        Data = data;
        X = x;
        Y = y;
    }
}

/// <summary>
/// Event args for drop events.
/// </summary>
public class DropEventArgs : EventArgs
{
    /// <summary>
    /// Gets the drag data.
    /// </summary>
    public DragData Data { get; }

    /// <summary>
    /// Gets the dropped data as string.
    /// </summary>
    public string? DroppedData { get; }

    /// <summary>
    /// Gets or sets whether the drop was handled.
    /// </summary>
    public bool Handled { get; set; }

    public DropEventArgs(DragData data, string? droppedData)
    {
        Data = data;
        DroppedData = droppedData;
    }
}

/// <summary>
/// Drag action types.
/// </summary>
public enum DragAction
{
    None,
    Copy,
    Move,
    Link
}
