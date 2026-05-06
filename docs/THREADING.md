# Threading Model and Native Handle Ownership

This document describes the threading rules, event loop architecture, and native
resource management patterns used in the maui-linux project.

## Overview

maui-linux uses a **single-threaded UI model** inherited from GTK. All UI
operations -- rendering, layout, input handling, and widget mutation -- must
execute on a single designated thread called the *GTK thread*. Background work
is permitted, but its results must be marshaled back to the GTK thread before
touching any UI state.

## The GTK Thread

At startup, `LinuxApplication.Run` initializes the dispatcher and records the
current managed thread ID:

```csharp
// LinuxApplication.cs
private static int _gtkThreadId;

private static void StartHeartbeat()
{
    _gtkThreadId = Environment.CurrentManagedThreadId;
    // ...
}
```

```csharp
// LinuxDispatcher.cs
public static void Initialize()
{
    _mainThreadId = Environment.CurrentManagedThreadId;
    _mainDispatcher = new LinuxDispatcher();
}
```

Both `_gtkThreadId` (in `LinuxApplication`) and `_mainThreadId` (in
`LinuxDispatcher`) capture the same thread -- the one that will run the event
loop. Diagnostic logging in `LogInvalidate` and `LogRequestRedraw` warns when
UI work is attempted from any other thread.

## Thread Marshaling

There are two mechanisms to marshal work onto the GTK thread.

### 1. `GLibNative.IdleAdd` / `GLibNative.TimeoutAdd` (low-level)

These wrap `g_idle_add` and `g_timeout_add` from GLib. The callback runs on the
next iteration of the GTK main loop. Returning `true` reschedules the callback;
returning `false` removes it.

```csharp
// Marshal a one-shot action to the GTK thread
GLibNative.IdleAdd(() =>
{
    // Runs on the GTK thread
    RequestRedrawInternal();
    return false; // do not repeat
});

// Schedule a recurring action every 250 ms
GLibNative.TimeoutAdd(250, () =>
{
    // heartbeat logic
    return true; // keep repeating
});
```

`GLibNative` prevents the delegate from being garbage-collected by storing it
in a static `_callbacks` list (protected by `_callbackLock`). The wrapper is
removed from the list when the callback returns `false`.

`GtkNative` has its own `IdleAdd` overload with the same prevent-GC pattern
(via `_idleCallbacks`). Prefer the `GLibNative` versions for new code -- they
have proper locking and error logging.

### 2. `LinuxDispatcher` (high-level, MAUI-compatible)

`LinuxDispatcher` implements `IDispatcher` so that MAUI's `Dispatcher.Dispatch`
works correctly on Linux.

```csharp
// From any thread:
dispatcher.Dispatch(() =>
{
    // Runs on the GTK thread via GLibNative.IdleAdd
});

dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
{
    // Runs on the GTK thread after a 500 ms delay via GLibNative.TimeoutAdd
});
```

If `Dispatch` is called from the GTK thread, the action runs **synchronously**
(no round-trip through the event loop). `DispatchDelayed` always goes through
`TimeoutAdd`, regardless of calling thread.

`LinuxDispatcherTimer` wraps `GLibNative.TimeoutAdd` to implement
`IDispatcherTimer`. Stopping a timer calls `GLibNative.SourceRemove` to cancel
the GLib source.

## Event Loop

The application supports two event-loop modes, selected by
`LinuxApplicationOptions.UseGtk`.

### X11 Mode (`RunX11`)

A manual polling loop on the main thread:

```
while (_mainWindow.IsRunning)
{
    _mainWindow.ProcessEvents();   // drain X11 event queue
    SkiaWebView.ProcessGtkEvents(); // pump GTK for WebView support
    UpdateAnimations();
    Render();
    Thread.Sleep(1);               // yield CPU
}
```

X11 events are read with `XNextEvent` / `XPending`. GTK is pre-initialized
(`gtk_init_check`) so that WebView (WebKitGTK) works, but GTK does not own the
main loop. GTK events are drained cooperatively via `ProcessGtkEvents`.

### GTK Mode (`RunGtk`)

GTK owns the main loop:

```csharp
StartHeartbeat();           // records _gtkThreadId, starts 250 ms heartbeat
PerformGtkLayout(w, h);
_gtkWindow.RequestRedraw();
_gtkWindow.Run();           // calls gtk_main() -- blocks until quit
GtkHostService.Instance.Shutdown();
```

All rendering is driven by GTK draw callbacks. `GLibNative.IdleAdd` and
`TimeoutAdd` integrate naturally because GTK's main loop processes GLib
sources.

## Native Handle Ownership

### Raw `IntPtr` Handles (current codebase)

Most native resources are currently held as raw `IntPtr` fields. The owning
class implements `IDisposable` and frees resources in `Dispose(bool)`.

**X11 resources** (`X11Window`):

| Resource | Acquire | Release |
|---|---|---|
| `Display*` | `XOpenDisplay` | `XCloseDisplay` |
| `Window` | `XCreateSimpleWindow` | `XDestroyWindow` |
| `Cursor` | `XCreateFontCursor` | `XFreeCursor` |
| `XImage*` | `XCreateImage` | `XDestroyImage` (also frees pixel buffer) |

Release order matters: cursors must be freed **before** the display is closed,
and the window must be destroyed before the display is closed.

**GObject resources** (`GtkHostWindow` and others):

| Resource | Acquire | Release |
|---|---|---|
| GTK widget | `gtk_window_new`, `gtk_drawing_area_new`, etc. | `gtk_widget_destroy` |
| GdkPixbuf | `gdk_pixbuf_new_from_file` | `g_object_unref` |
| GtkCssProvider | `gtk_css_provider_new` | `g_object_unref` |

GObject uses **reference counting**. `g_object_unref` decrements the ref
count; when it reaches zero the object is freed. If you receive an object with
a "floating" reference (common for newly created GTK widgets), adding it to a
container sinks the reference -- the container then owns it. Only call
`g_object_unref` on objects you have explicitly ref'd or that you own.

### Signal Connection / Disconnection

GTK signals are connected with `g_signal_connect_data` and return a `ulong`
handler ID. Signals **must** be disconnected before the widget is destroyed,
or the pointers backing the managed delegates become dangling.

```csharp
// Connect (store the handler ID)
_deleteSignalId = GtkNative.g_signal_connect_data(
    _window, "delete-event",
    Marshal.GetFunctionPointerForDelegate(_deleteEventHandler),
    IntPtr.Zero, IntPtr.Zero, 0);

// Disconnect (in Dispose, before gtk_widget_destroy)
if (_deleteSignalId != 0)
    GtkNative.g_signal_handler_disconnect(_window, _deleteSignalId);
```

Keep the delegate instance alive for as long as the signal is connected (store
it as a field). If the GC collects the delegate while the signal is still
connected, GTK will call into freed memory.

### SafeHandle Wrappers (for new code)

`Native/SafeHandles.cs` provides `SafeHandle`-derived wrappers that automate
release:

| Type | Releases via |
|---|---|
| `SafeGtkWidgetHandle` | `gtk_widget_destroy` |
| `SafeGObjectHandle` | `g_object_unref` |
| `SafeX11DisplayHandle` | `XCloseDisplay` |
| `SafeX11CursorHandle` | `XFreeCursor` (requires display ptr at construction) |
| `SafeCssProviderHandle` | `g_object_unref` |

New code should prefer these over raw `IntPtr` where practical. They guarantee
release even if `Dispose` is not called (via the CLR release mechanism),
and they prevent double-free by tracking validity.

## Common Pitfalls

1. **UI work off the GTK thread.** Any call to GTK or X11 APIs, or mutation of
   `SkiaView` state, from a background thread is undefined behavior. Always
   marshal with `GLibNative.IdleAdd` or `LinuxDispatcher.Dispatch`. The
   diagnostic warnings from `LogInvalidate` / `LogRequestRedraw` exist to catch
   this -- do not ignore them.

2. **Delegate collection during signal connection.** A `GSourceFunc` or signal
   callback delegate passed to native code must be stored in a field or a static
   list for its entire connected lifetime. `GLibNative._callbacks` and
   `GtkNative._idleCallbacks` exist for this purpose.

3. **Reentrancy in `RequestRedraw`.** The `_isRedrawing` flag guards against
   recursive invalidation. Calling `RequestRedraw` from within a render
   callback is safe (it will be a no-op), but scheduling further idle callbacks
   from within an idle callback can lead to unbounded queue growth if not gated.

4. **X11 resource release ordering.** Cursors and windows depend on the display
   connection. Always free them before calling `XCloseDisplay`. The
   `X11Window.Dispose` method shows the correct order:
   cursors first, then the window, then the display.

5. **`XDestroyImage` frees the pixel buffer.** When you call `XCreateImage`
   with a pointer to pixel data, `XDestroyImage` will free that memory. If you
   allocated the buffer with `Marshal.AllocHGlobal`, do **not** free it yourself
   after `XDestroyImage`. If `XCreateImage` fails, you **must** free it
   yourself.

6. **`Thread.Sleep(1)` in X11 mode.** The X11 event loop uses a 1 ms sleep to
   yield CPU. This limits frame rate and increases input latency compared to
   GTK mode. Be aware of this when profiling rendering performance.
