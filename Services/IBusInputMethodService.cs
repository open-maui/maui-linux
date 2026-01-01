// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// IBus Input Method service using D-Bus interface.
/// Provides modern IME support on Linux desktops.
/// </summary>
public class IBusInputMethodService : IInputMethodService, IDisposable
{
    private nint _bus;
    private nint _context;
    private IInputContext? _currentContext;
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private bool _isActive;
    private bool _disposed;

    // Callback delegates (prevent GC)
    private IBusCommitTextCallback? _commitCallback;
    private IBusUpdatePreeditTextCallback? _preeditCallback;
    private IBusShowPreeditTextCallback? _showPreeditCallback;
    private IBusHidePreeditTextCallback? _hidePreeditCallback;

    public bool IsActive => _isActive;
    public string PreEditText => _preEditText;
    public int PreEditCursorPosition => _preEditCursorPosition;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    public void Initialize(nint windowHandle)
    {
        try
        {
            // Initialize IBus
            ibus_init();

            // Get the IBus bus connection
            _bus = ibus_bus_new();
            if (_bus == IntPtr.Zero)
            {
                Console.WriteLine("IBusInputMethodService: Failed to connect to IBus");
                return;
            }

            // Check if IBus is connected
            if (!ibus_bus_is_connected(_bus))
            {
                Console.WriteLine("IBusInputMethodService: IBus not connected");
                return;
            }

            // Create input context
            _context = ibus_bus_create_input_context(_bus, "maui-linux");
            if (_context == IntPtr.Zero)
            {
                Console.WriteLine("IBusInputMethodService: Failed to create input context");
                return;
            }

            // Set capabilities
            uint capabilities = IBUS_CAP_PREEDIT_TEXT | IBUS_CAP_FOCUS | IBUS_CAP_SURROUNDING_TEXT;
            ibus_input_context_set_capabilities(_context, capabilities);

            // Connect signals
            ConnectSignals();

            Console.WriteLine("IBusInputMethodService: Initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IBusInputMethodService: Initialization failed - {ex.Message}");
        }
    }

    private void ConnectSignals()
    {
        if (_context == IntPtr.Zero) return;

        // Set up callbacks for IBus signals
        _commitCallback = OnCommitText;
        _preeditCallback = OnUpdatePreeditText;
        _showPreeditCallback = OnShowPreeditText;
        _hidePreeditCallback = OnHidePreeditText;

        // Connect to commit-text signal
        g_signal_connect(_context, "commit-text",
            Marshal.GetFunctionPointerForDelegate(_commitCallback), IntPtr.Zero);

        // Connect to update-preedit-text signal
        g_signal_connect(_context, "update-preedit-text",
            Marshal.GetFunctionPointerForDelegate(_preeditCallback), IntPtr.Zero);

        // Connect to show-preedit-text signal
        g_signal_connect(_context, "show-preedit-text",
            Marshal.GetFunctionPointerForDelegate(_showPreeditCallback), IntPtr.Zero);

        // Connect to hide-preedit-text signal
        g_signal_connect(_context, "hide-preedit-text",
            Marshal.GetFunctionPointerForDelegate(_hidePreeditCallback), IntPtr.Zero);
    }

    private void OnCommitText(nint context, nint text, nint userData)
    {
        if (text == IntPtr.Zero) return;

        string committedText = GetIBusTextString(text);
        if (!string.IsNullOrEmpty(committedText))
        {
            _preEditText = string.Empty;
            _preEditCursorPosition = 0;
            _isActive = false;

            TextCommitted?.Invoke(this, new TextCommittedEventArgs(committedText));
            _currentContext?.OnTextCommitted(committedText);
        }
    }

    private void OnUpdatePreeditText(nint context, nint text, uint cursorPos, bool visible, nint userData)
    {
        if (!visible)
        {
            OnHidePreeditText(context, userData);
            return;
        }

        _isActive = true;
        _preEditText = text != IntPtr.Zero ? GetIBusTextString(text) : string.Empty;
        _preEditCursorPosition = (int)cursorPos;

        var attributes = GetPreeditAttributes(text);
        PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition, attributes));
        _currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
    }

    private void OnShowPreeditText(nint context, nint userData)
    {
        _isActive = true;
    }

    private void OnHidePreeditText(nint context, nint userData)
    {
        _isActive = false;
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;

        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
    }

    private string GetIBusTextString(nint ibusText)
    {
        if (ibusText == IntPtr.Zero) return string.Empty;

        nint textPtr = ibus_text_get_text(ibusText);
        if (textPtr == IntPtr.Zero) return string.Empty;

        return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
    }

    private List<PreEditAttribute> GetPreeditAttributes(nint ibusText)
    {
        var attributes = new List<PreEditAttribute>();
        if (ibusText == IntPtr.Zero) return attributes;

        nint attrList = ibus_text_get_attributes(ibusText);
        if (attrList == IntPtr.Zero) return attributes;

        uint count = ibus_attr_list_size(attrList);

        for (uint i = 0; i < count; i++)
        {
            nint attr = ibus_attr_list_get(attrList, i);
            if (attr == IntPtr.Zero) continue;

            var type = ibus_attribute_get_attr_type(attr);
            var start = ibus_attribute_get_start_index(attr);
            var end = ibus_attribute_get_end_index(attr);

            attributes.Add(new PreEditAttribute
            {
                Start = (int)start,
                Length = (int)(end - start),
                Type = ConvertAttributeType(type)
            });
        }

        return attributes;
    }

    private PreEditAttributeType ConvertAttributeType(uint ibusType)
    {
        return ibusType switch
        {
            IBUS_ATTR_TYPE_UNDERLINE => PreEditAttributeType.Underline,
            IBUS_ATTR_TYPE_FOREGROUND => PreEditAttributeType.Highlighted,
            IBUS_ATTR_TYPE_BACKGROUND => PreEditAttributeType.Reverse,
            _ => PreEditAttributeType.None
        };
    }

    public void SetFocus(IInputContext? context)
    {
        _currentContext = context;

        if (_context != IntPtr.Zero)
        {
            if (context != null)
            {
                ibus_input_context_focus_in(_context);
            }
            else
            {
                ibus_input_context_focus_out(_context);
            }
        }
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
        if (_context == IntPtr.Zero) return;

        ibus_input_context_set_cursor_location(_context, x, y, width, height);
    }

    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
    {
        if (_context == IntPtr.Zero) return false;

        uint state = ConvertModifiers(modifiers);
        if (!isKeyDown)
        {
            state |= IBUS_RELEASE_MASK;
        }

        return ibus_input_context_process_key_event(_context, keyCode, keyCode, state);
    }

    private uint ConvertModifiers(KeyModifiers modifiers)
    {
        uint state = 0;
        if (modifiers.HasFlag(KeyModifiers.Shift)) state |= IBUS_SHIFT_MASK;
        if (modifiers.HasFlag(KeyModifiers.Control)) state |= IBUS_CONTROL_MASK;
        if (modifiers.HasFlag(KeyModifiers.Alt)) state |= IBUS_MOD1_MASK;
        if (modifiers.HasFlag(KeyModifiers.Super)) state |= IBUS_SUPER_MASK;
        if (modifiers.HasFlag(KeyModifiers.CapsLock)) state |= IBUS_LOCK_MASK;
        return state;
    }

    public void Reset()
    {
        if (_context != IntPtr.Zero)
        {
            ibus_input_context_reset(_context);
        }

        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        _isActive = false;

        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_context != IntPtr.Zero)
        {
            ibus_input_context_focus_out(_context);
            g_object_unref(_context);
            _context = IntPtr.Zero;
        }

        if (_bus != IntPtr.Zero)
        {
            g_object_unref(_bus);
            _bus = IntPtr.Zero;
        }
    }

    #region IBus Constants

    private const uint IBUS_CAP_PREEDIT_TEXT = 1 << 0;
    private const uint IBUS_CAP_FOCUS = 1 << 3;
    private const uint IBUS_CAP_SURROUNDING_TEXT = 1 << 5;

    private const uint IBUS_SHIFT_MASK = 1 << 0;
    private const uint IBUS_LOCK_MASK = 1 << 1;
    private const uint IBUS_CONTROL_MASK = 1 << 2;
    private const uint IBUS_MOD1_MASK = 1 << 3;
    private const uint IBUS_SUPER_MASK = 1 << 26;
    private const uint IBUS_RELEASE_MASK = 1 << 30;

    private const uint IBUS_ATTR_TYPE_UNDERLINE = 1;
    private const uint IBUS_ATTR_TYPE_FOREGROUND = 2;
    private const uint IBUS_ATTR_TYPE_BACKGROUND = 3;

    #endregion

    #region IBus Interop

    private delegate void IBusCommitTextCallback(nint context, nint text, nint userData);
    private delegate void IBusUpdatePreeditTextCallback(nint context, nint text, uint cursorPos, bool visible, nint userData);
    private delegate void IBusShowPreeditTextCallback(nint context, nint userData);
    private delegate void IBusHidePreeditTextCallback(nint context, nint userData);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_init();

    [DllImport("libibus-1.0.so.5")]
    private static extern nint ibus_bus_new();

    [DllImport("libibus-1.0.so.5")]
    private static extern bool ibus_bus_is_connected(nint bus);

    [DllImport("libibus-1.0.so.5")]
    private static extern nint ibus_bus_create_input_context(nint bus, string clientName);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_input_context_set_capabilities(nint context, uint capabilities);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_input_context_focus_in(nint context);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_input_context_focus_out(nint context);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_input_context_reset(nint context);

    [DllImport("libibus-1.0.so.5")]
    private static extern void ibus_input_context_set_cursor_location(nint context, int x, int y, int w, int h);

    [DllImport("libibus-1.0.so.5")]
    private static extern bool ibus_input_context_process_key_event(nint context, uint keyval, uint keycode, uint state);

    [DllImport("libibus-1.0.so.5")]
    private static extern nint ibus_text_get_text(nint text);

    [DllImport("libibus-1.0.so.5")]
    private static extern nint ibus_text_get_attributes(nint text);

    [DllImport("libibus-1.0.so.5")]
    private static extern uint ibus_attr_list_size(nint attrList);

    [DllImport("libibus-1.0.so.5")]
    private static extern nint ibus_attr_list_get(nint attrList, uint index);

    [DllImport("libibus-1.0.so.5")]
    private static extern uint ibus_attribute_get_attr_type(nint attr);

    [DllImport("libibus-1.0.so.5")]
    private static extern uint ibus_attribute_get_start_index(nint attr);

    [DllImport("libibus-1.0.so.5")]
    private static extern uint ibus_attribute_get_end_index(nint attr);

    [DllImport("libgobject-2.0.so.0")]
    private static extern void g_object_unref(nint obj);

    [DllImport("libgobject-2.0.so.0")]
    private static extern ulong g_signal_connect(nint instance, string signal, nint handler, nint data);

    #endregion
}
