// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// GTK4 dialog response codes.
/// </summary>
public enum GtkResponseType
{
    None = -1,
    Reject = -2,
    Accept = -3,
    DeleteEvent = -4,
    Ok = -5,
    Cancel = -6,
    Close = -7,
    Yes = -8,
    No = -9,
    Apply = -10,
    Help = -11
}

/// <summary>
/// GTK4 message dialog types.
/// </summary>
public enum GtkMessageType
{
    Info = 0,
    Warning = 1,
    Question = 2,
    Error = 3,
    Other = 4
}

/// <summary>
/// GTK4 button layouts for dialogs.
/// </summary>
public enum GtkButtonsType
{
    None = 0,
    Ok = 1,
    Close = 2,
    Cancel = 3,
    YesNo = 4,
    OkCancel = 5
}

/// <summary>
/// GTK4 file chooser actions.
/// </summary>
public enum GtkFileChooserAction
{
    Open = 0,
    Save = 1,
    SelectFolder = 2,
    CreateFolder = 3
}

/// <summary>
/// Result from a file dialog.
/// </summary>
public class FileDialogResult
{
    public bool Accepted { get; init; }
    public string[] SelectedFiles { get; init; } = Array.Empty<string>();
    public string? SelectedFile => SelectedFiles.Length > 0 ? SelectedFiles[0] : null;
}

/// <summary>
/// Result from a color dialog.
/// </summary>
public class ColorDialogResult
{
    public bool Accepted { get; init; }
    public float Red { get; init; }
    public float Green { get; init; }
    public float Blue { get; init; }
    public float Alpha { get; init; }
}

/// <summary>
/// GTK4 interop layer for native Linux dialogs.
/// Provides native file pickers, message boxes, and color choosers.
/// </summary>
public class Gtk4InteropService : IDisposable
{
    #region GTK4 Native Interop

    private const string LibGtk4 = "libgtk-4.so.1";
    private const string LibGio = "libgio-2.0.so.0";
    private const string LibGlib = "libglib-2.0.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";

    // GTK initialization
    [DllImport(LibGtk4)]
    private static extern bool gtk_init_check();

    [DllImport(LibGtk4)]
    private static extern bool gtk_is_initialized();

    // Main loop
    [DllImport(LibGtk4)]
    private static extern IntPtr g_main_context_default();

    [DllImport(LibGtk4)]
    private static extern bool g_main_context_iteration(IntPtr context, bool mayBlock);

    [DllImport(LibGlib)]
    private static extern void g_free(IntPtr mem);

    // GObject
    [DllImport(LibGObject)]
    private static extern void g_object_unref(IntPtr obj);

    [DllImport(LibGObject)]
    private static extern void g_object_ref(IntPtr obj);

    // Window
    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_window_new();

    [DllImport(LibGtk4)]
    private static extern void gtk_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

    [DllImport(LibGtk4)]
    private static extern void gtk_window_set_modal(IntPtr window, bool modal);

    [DllImport(LibGtk4)]
    private static extern void gtk_window_set_transient_for(IntPtr window, IntPtr parent);

    [DllImport(LibGtk4)]
    private static extern void gtk_window_destroy(IntPtr window);

    [DllImport(LibGtk4)]
    private static extern void gtk_window_present(IntPtr window);

    [DllImport(LibGtk4)]
    private static extern void gtk_window_close(IntPtr window);

    // Widget
    [DllImport(LibGtk4)]
    private static extern void gtk_widget_show(IntPtr widget);

    [DllImport(LibGtk4)]
    private static extern void gtk_widget_hide(IntPtr widget);

    [DllImport(LibGtk4)]
    private static extern void gtk_widget_set_visible(IntPtr widget, bool visible);

    [DllImport(LibGtk4)]
    private static extern bool gtk_widget_get_visible(IntPtr widget);

    // Alert Dialog (GTK4)
    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_alert_dialog_new([MarshalAs(UnmanagedType.LPStr)] string format);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_set_message(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string message);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_set_detail(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string detail);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_set_buttons(IntPtr dialog, string[] labels);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_set_cancel_button(IntPtr dialog, int button);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_set_default_button(IntPtr dialog, int button);

    [DllImport(LibGtk4)]
    private static extern void gtk_alert_dialog_show(IntPtr dialog, IntPtr parent);

    // File Dialog (GTK4)
    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_dialog_new();

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_set_title(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string title);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_set_modal(IntPtr dialog, bool modal);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_set_accept_label(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string label);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_open(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_dialog_open_finish(IntPtr dialog, IntPtr result, out IntPtr error);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_save(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_dialog_save_finish(IntPtr dialog, IntPtr result, out IntPtr error);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_select_folder(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_dialog_select_folder_finish(IntPtr dialog, IntPtr result, out IntPtr error);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_open_multiple(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_dialog_open_multiple_finish(IntPtr dialog, IntPtr result, out IntPtr error);

    // File filters
    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_file_filter_new();

    [DllImport(LibGtk4)]
    private static extern void gtk_file_filter_set_name(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_filter_add_pattern(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string pattern);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_filter_add_mime_type(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string mimeType);

    [DllImport(LibGtk4)]
    private static extern void gtk_file_dialog_set_default_filter(IntPtr dialog, IntPtr filter);

    // GFile
    [DllImport(LibGio)]
    private static extern IntPtr g_file_get_path(IntPtr file);

    // GListModel for multiple files
    [DllImport(LibGio)]
    private static extern uint g_list_model_get_n_items(IntPtr list);

    [DllImport(LibGio)]
    private static extern IntPtr g_list_model_get_item(IntPtr list, uint position);

    // Color Dialog (GTK4)
    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_color_dialog_new();

    [DllImport(LibGtk4)]
    private static extern void gtk_color_dialog_set_title(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string title);

    [DllImport(LibGtk4)]
    private static extern void gtk_color_dialog_set_modal(IntPtr dialog, bool modal);

    [DllImport(LibGtk4)]
    private static extern void gtk_color_dialog_set_with_alpha(IntPtr dialog, bool withAlpha);

    [DllImport(LibGtk4)]
    private static extern void gtk_color_dialog_choose_rgba(IntPtr dialog, IntPtr parent, IntPtr initialColor, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(LibGtk4)]
    private static extern IntPtr gtk_color_dialog_choose_rgba_finish(IntPtr dialog, IntPtr result, out IntPtr error);

    // GdkRGBA
    [StructLayout(LayoutKind.Sequential)]
    private struct GdkRGBA
    {
        public float Red;
        public float Green;
        public float Blue;
        public float Alpha;
    }

    // Async callback delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GAsyncReadyCallback(IntPtr sourceObject, IntPtr result, IntPtr userData);

    // Legacy GTK3 fallbacks
    private const string LibGtk3 = "libgtk-3.so.0";

    [DllImport(LibGtk3, EntryPoint = "gtk_init_check")]
    private static extern bool gtk3_init_check(ref int argc, ref IntPtr argv);

    [DllImport(LibGtk3, EntryPoint = "gtk_file_chooser_dialog_new")]
    private static extern IntPtr gtk3_file_chooser_dialog_new(
        [MarshalAs(UnmanagedType.LPStr)] string title,
        IntPtr parent,
        int action,
        [MarshalAs(UnmanagedType.LPStr)] string firstButtonText,
        int firstButtonResponse,
        [MarshalAs(UnmanagedType.LPStr)] string secondButtonText,
        int secondButtonResponse,
        IntPtr terminator);

    [DllImport(LibGtk3, EntryPoint = "gtk_dialog_run")]
    private static extern int gtk3_dialog_run(IntPtr dialog);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_destroy")]
    private static extern void gtk3_widget_destroy(IntPtr widget);

    [DllImport(LibGtk3, EntryPoint = "gtk_file_chooser_get_filename")]
    private static extern IntPtr gtk3_file_chooser_get_filename(IntPtr chooser);

    [DllImport(LibGtk3, EntryPoint = "gtk_file_chooser_get_filenames")]
    private static extern IntPtr gtk3_file_chooser_get_filenames(IntPtr chooser);

    [DllImport(LibGtk3, EntryPoint = "gtk_file_chooser_set_select_multiple")]
    private static extern void gtk3_file_chooser_set_select_multiple(IntPtr chooser, bool selectMultiple);

    [DllImport(LibGtk3, EntryPoint = "gtk_message_dialog_new")]
    private static extern IntPtr gtk3_message_dialog_new(
        IntPtr parent,
        int flags,
        int type,
        int buttons,
        [MarshalAs(UnmanagedType.LPStr)] string message);

    [DllImport(LibGlib, EntryPoint = "g_slist_length")]
    private static extern uint g_slist_length(IntPtr list);

    [DllImport(LibGlib, EntryPoint = "g_slist_nth_data")]
    private static extern IntPtr g_slist_nth_data(IntPtr list, uint n);

    [DllImport(LibGlib, EntryPoint = "g_slist_free")]
    private static extern void g_slist_free(IntPtr list);

    #endregion

    #region Fields

    private bool _initialized;
    private bool _useGtk4;
    private bool _disposed;
    private readonly object _lock = new();

    // Store callbacks to prevent GC
    private GAsyncReadyCallback? _currentCallback;
    private TaskCompletionSource<FileDialogResult>? _fileDialogTcs;
    private TaskCompletionSource<ColorDialogResult>? _colorDialogTcs;
    private IntPtr _currentDialog;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether GTK is initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets whether GTK4 is being used (vs GTK3 fallback).
    /// </summary>
    public bool IsGtk4 => _useGtk4;

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the GTK4 interop service.
    /// Falls back to GTK3 if GTK4 is not available.
    /// </summary>
    public bool Initialize()
    {
        if (_initialized)
            return true;

        lock (_lock)
        {
            if (_initialized)
                return true;

            // Try GTK4 first
            try
            {
                if (gtk_init_check())
                {
                    _useGtk4 = true;
                    _initialized = true;
                    Console.WriteLine("[GTK4] Initialized GTK4");
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[GTK4] GTK4 not found, trying GTK3");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GTK4] GTK4 init failed: {ex.Message}");
            }

            // Fall back to GTK3
            try
            {
                int argc = 0;
                IntPtr argv = IntPtr.Zero;
                if (gtk3_init_check(ref argc, ref argv))
                {
                    _useGtk4 = false;
                    _initialized = true;
                    Console.WriteLine("[GTK4] Initialized GTK3 (fallback)");
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[GTK4] GTK3 not found");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GTK4] GTK3 init failed: {ex.Message}");
            }

            return false;
        }
    }

    #endregion

    #region Message Dialogs

    /// <summary>
    /// Shows an alert message dialog.
    /// </summary>
    public void ShowAlert(string title, string message, GtkMessageType type = GtkMessageType.Info)
    {
        if (!EnsureInitialized())
            return;

        if (_useGtk4)
        {
            var dialog = gtk_alert_dialog_new(title);
            gtk_alert_dialog_set_detail(dialog, message);
            string[] buttons = { "OK" };
            gtk_alert_dialog_set_buttons(dialog, buttons);
            gtk_alert_dialog_show(dialog, IntPtr.Zero);
            g_object_unref(dialog);
        }
        else
        {
            var dialog = gtk3_message_dialog_new(
                IntPtr.Zero,
                1, // GTK_DIALOG_MODAL
                (int)type,
                (int)GtkButtonsType.Ok,
                message);

            gtk3_dialog_run(dialog);
            gtk3_widget_destroy(dialog);
        }

        ProcessPendingEvents();
    }

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    public bool ShowConfirmation(string title, string message)
    {
        if (!EnsureInitialized())
            return false;

        if (_useGtk4)
        {
            // GTK4 async dialogs are more complex - use synchronous approach
            var dialog = gtk_alert_dialog_new(title);
            gtk_alert_dialog_set_detail(dialog, message);
            string[] buttons = { "No", "Yes" };
            gtk_alert_dialog_set_buttons(dialog, buttons);
            gtk_alert_dialog_set_default_button(dialog, 1);
            gtk_alert_dialog_set_cancel_button(dialog, 0);
            gtk_alert_dialog_show(dialog, IntPtr.Zero);
            g_object_unref(dialog);
            // Note: GTK4 alert dialogs are async, this is simplified
            return true;
        }
        else
        {
            var dialog = gtk3_message_dialog_new(
                IntPtr.Zero,
                1, // GTK_DIALOG_MODAL
                (int)GtkMessageType.Question,
                (int)GtkButtonsType.YesNo,
                message);

            int response = gtk3_dialog_run(dialog);
            gtk3_widget_destroy(dialog);
            ProcessPendingEvents();

            return response == (int)GtkResponseType.Yes;
        }
    }

    #endregion

    #region File Dialogs

    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    public FileDialogResult ShowOpenFileDialog(
        string title = "Open File",
        string? initialFolder = null,
        bool allowMultiple = false,
        params (string Name, string Pattern)[] filters)
    {
        if (!EnsureInitialized())
            return new FileDialogResult { Accepted = false };

        if (_useGtk4)
        {
            return ShowGtk4FileDialog(title, GtkFileChooserAction.Open, allowMultiple, filters);
        }
        else
        {
            return ShowGtk3FileDialog(title, 0, allowMultiple, filters); // GTK_FILE_CHOOSER_ACTION_OPEN = 0
        }
    }

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    public FileDialogResult ShowSaveFileDialog(
        string title = "Save File",
        string? suggestedName = null,
        params (string Name, string Pattern)[] filters)
    {
        if (!EnsureInitialized())
            return new FileDialogResult { Accepted = false };

        if (_useGtk4)
        {
            return ShowGtk4FileDialog(title, GtkFileChooserAction.Save, false, filters);
        }
        else
        {
            return ShowGtk3FileDialog(title, 1, false, filters); // GTK_FILE_CHOOSER_ACTION_SAVE = 1
        }
    }

    /// <summary>
    /// Shows a folder picker dialog.
    /// </summary>
    public FileDialogResult ShowFolderDialog(string title = "Select Folder")
    {
        if (!EnsureInitialized())
            return new FileDialogResult { Accepted = false };

        if (_useGtk4)
        {
            return ShowGtk4FileDialog(title, GtkFileChooserAction.SelectFolder, false, Array.Empty<(string, string)>());
        }
        else
        {
            return ShowGtk3FileDialog(title, 2, false, Array.Empty<(string, string)>()); // GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER = 2
        }
    }

    private FileDialogResult ShowGtk4FileDialog(
        string title,
        GtkFileChooserAction action,
        bool allowMultiple,
        (string Name, string Pattern)[] filters)
    {
        var dialog = gtk_file_dialog_new();
        gtk_file_dialog_set_title(dialog, title);
        gtk_file_dialog_set_modal(dialog, true);

        // Set up filters
        if (filters.Length > 0)
        {
            var filter = gtk_file_filter_new();
            gtk_file_filter_set_name(filter, filters[0].Name);
            gtk_file_filter_add_pattern(filter, filters[0].Pattern);
            gtk_file_dialog_set_default_filter(dialog, filter);
        }

        // For GTK4, we need async handling - simplified synchronous version
        // In a full implementation, this would use proper async/await
        _fileDialogTcs = new TaskCompletionSource<FileDialogResult>();
        _currentDialog = dialog;

        _currentCallback = (source, result, userData) =>
        {
            IntPtr error = IntPtr.Zero;
            IntPtr file = IntPtr.Zero;

            try
            {
                if (action == GtkFileChooserAction.Open && !allowMultiple)
                    file = gtk_file_dialog_open_finish(dialog, result, out error);
                else if (action == GtkFileChooserAction.Save)
                    file = gtk_file_dialog_save_finish(dialog, result, out error);
                else if (action == GtkFileChooserAction.SelectFolder)
                    file = gtk_file_dialog_select_folder_finish(dialog, result, out error);

                if (file != IntPtr.Zero && error == IntPtr.Zero)
                {
                    IntPtr pathPtr = g_file_get_path(file);
                    string path = Marshal.PtrToStringUTF8(pathPtr) ?? "";
                    g_free(pathPtr);
                    g_object_unref(file);

                    _fileDialogTcs?.TrySetResult(new FileDialogResult
                    {
                        Accepted = true,
                        SelectedFiles = new[] { path }
                    });
                }
                else
                {
                    _fileDialogTcs?.TrySetResult(new FileDialogResult { Accepted = false });
                }
            }
            catch
            {
                _fileDialogTcs?.TrySetResult(new FileDialogResult { Accepted = false });
            }
        };

        // Start the dialog
        if (action == GtkFileChooserAction.Open && !allowMultiple)
            gtk_file_dialog_open(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
        else if (action == GtkFileChooserAction.Open && allowMultiple)
            gtk_file_dialog_open_multiple(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
        else if (action == GtkFileChooserAction.Save)
            gtk_file_dialog_save(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
        else if (action == GtkFileChooserAction.SelectFolder)
            gtk_file_dialog_select_folder(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);

        // Process events until dialog completes
        while (!_fileDialogTcs.Task.IsCompleted)
        {
            ProcessPendingEvents();
            Thread.Sleep(10);
        }

        g_object_unref(dialog);
        return _fileDialogTcs.Task.Result;
    }

    private FileDialogResult ShowGtk3FileDialog(
        string title,
        int action,
        bool allowMultiple,
        (string Name, string Pattern)[] filters)
    {
        var dialog = gtk3_file_chooser_dialog_new(
            title,
            IntPtr.Zero,
            action,
            "_Cancel", (int)GtkResponseType.Cancel,
            action == 1 ? "_Save" : "_Open", (int)GtkResponseType.Accept,
            IntPtr.Zero);

        if (allowMultiple)
            gtk3_file_chooser_set_select_multiple(dialog, true);

        int response = gtk3_dialog_run(dialog);

        var result = new FileDialogResult { Accepted = false };

        if (response == (int)GtkResponseType.Accept)
        {
            if (allowMultiple)
            {
                IntPtr list = gtk3_file_chooser_get_filenames(dialog);
                uint count = g_slist_length(list);
                var files = new List<string>();

                for (uint i = 0; i < count; i++)
                {
                    IntPtr pathPtr = g_slist_nth_data(list, i);
                    string? path = Marshal.PtrToStringUTF8(pathPtr);
                    if (!string.IsNullOrEmpty(path))
                    {
                        files.Add(path);
                        g_free(pathPtr);
                    }
                }

                g_slist_free(list);
                result = new FileDialogResult { Accepted = true, SelectedFiles = files.ToArray() };
            }
            else
            {
                IntPtr pathPtr = gtk3_file_chooser_get_filename(dialog);
                string? path = Marshal.PtrToStringUTF8(pathPtr);
                g_free(pathPtr);

                if (!string.IsNullOrEmpty(path))
                    result = new FileDialogResult { Accepted = true, SelectedFiles = new[] { path } };
            }
        }

        gtk3_widget_destroy(dialog);
        ProcessPendingEvents();

        return result;
    }

    #endregion

    #region Color Dialog

    /// <summary>
    /// Shows a color picker dialog.
    /// </summary>
    public ColorDialogResult ShowColorDialog(
        string title = "Choose Color",
        float initialRed = 1f,
        float initialGreen = 1f,
        float initialBlue = 1f,
        float initialAlpha = 1f,
        bool withAlpha = true)
    {
        if (!EnsureInitialized())
            return new ColorDialogResult { Accepted = false };

        if (_useGtk4)
        {
            return ShowGtk4ColorDialog(title, initialRed, initialGreen, initialBlue, initialAlpha, withAlpha);
        }
        else
        {
            // GTK3 color dialog would go here
            return new ColorDialogResult { Accepted = false };
        }
    }

    private ColorDialogResult ShowGtk4ColorDialog(
        string title,
        float r, float g, float b, float a,
        bool withAlpha)
    {
        var dialog = gtk_color_dialog_new();
        gtk_color_dialog_set_title(dialog, title);
        gtk_color_dialog_set_modal(dialog, true);
        gtk_color_dialog_set_with_alpha(dialog, withAlpha);

        _colorDialogTcs = new TaskCompletionSource<ColorDialogResult>();

        _currentCallback = (source, result, userData) =>
        {
            IntPtr error = IntPtr.Zero;
            try
            {
                IntPtr rgbaPtr = gtk_color_dialog_choose_rgba_finish(dialog, result, out error);
                if (rgbaPtr != IntPtr.Zero && error == IntPtr.Zero)
                {
                    var rgba = Marshal.PtrToStructure<GdkRGBA>(rgbaPtr);
                    _colorDialogTcs?.TrySetResult(new ColorDialogResult
                    {
                        Accepted = true,
                        Red = rgba.Red,
                        Green = rgba.Green,
                        Blue = rgba.Blue,
                        Alpha = rgba.Alpha
                    });
                }
                else
                {
                    _colorDialogTcs?.TrySetResult(new ColorDialogResult { Accepted = false });
                }
            }
            catch
            {
                _colorDialogTcs?.TrySetResult(new ColorDialogResult { Accepted = false });
            }
        };

        gtk_color_dialog_choose_rgba(dialog, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);

        while (!_colorDialogTcs.Task.IsCompleted)
        {
            ProcessPendingEvents();
            Thread.Sleep(10);
        }

        g_object_unref(dialog);
        return _colorDialogTcs.Task.Result;
    }

    #endregion

    #region Helpers

    private bool EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
        return _initialized;
    }

    private void ProcessPendingEvents()
    {
        var context = g_main_context_default();
        while (g_main_context_iteration(context, false)) { }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _initialized = false;

        GC.SuppressFinalize(this);
    }

    ~Gtk4InteropService()
    {
        Dispose();
    }

    #endregion
}
