using System;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Type of JavaScript dialog.
/// </summary>
public enum ScriptDialogType
{
    Alert = 0,
    Confirm = 1,
    Prompt = 2,
    BeforeUnloadConfirm = 3
}

/// <summary>
/// GTK-based WebView platform view using WebKitGTK.
/// Provides web browsing capabilities within MAUI applications.
/// </summary>
public sealed class GtkWebViewPlatformView : IDisposable
{
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate void DestroyCallback(IntPtr widget, IntPtr userData);

    private IntPtr _widget;
    private bool _disposed;
    private string? _currentUri;
    private ulong _loadChangedSignalId;
    private ulong _scriptDialogSignalId;
    private ulong _destroySignalId;
    private WebKitNative.LoadChangedCallback? _loadChangedCallback;
    private WebKitNative.ScriptDialogCallback? _scriptDialogCallback;
    private DestroyCallback? _destroyCallback;
    private EventHandler<Microsoft.Maui.Controls.AppThemeChangedEventArgs>? _themeChangedHandler;

    // Pending theme-change idle source (0 when none). Removed on Dispose so a
    // queued theme update can never run against a torn-down WebView.
    private uint _themeIdleSourceId;

    public IntPtr Widget => _widget;
    public string? CurrentUri => _currentUri;

    public event EventHandler<string>? NavigationStarted;
    public event EventHandler<(string Url, bool Success)>? NavigationCompleted;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<(ScriptDialogType Type, string Message, Action<bool> Callback)>? ScriptDialogRequested;

    public GtkWebViewPlatformView()
    {
        if (!WebKitNative.Initialize())
        {
            throw new InvalidOperationException("Failed to initialize WebKitGTK. Is libwebkit2gtk-4.x installed?");
        }
        _widget = WebKitNative.WebViewNew();
        if (_widget == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create WebKitWebView widget");
        }
        WebKitNative.ConfigureSettings(_widget);
        _loadChangedCallback = OnLoadChanged;
        _loadChangedSignalId = WebKitNative.ConnectLoadChanged(_widget, _loadChangedCallback);

        // Connect to script-dialog signal to intercept JavaScript alerts/confirms/prompts
        _scriptDialogCallback = OnScriptDialog;
        _scriptDialogSignalId = WebKitNative.ConnectScriptDialog(_widget, _scriptDialogCallback);

        // "destroy" zeroes the widget pointer at the authoritative moment. The
        // widget is owned by the GTK host window tree; when that tree is
        // destroyed (window close / GtkHostService.Shutdown) this instance may
        // still be reachable from the handler, and every method here guards on
        // _widget == IntPtr.Zero.
        _destroyCallback = OnWidgetDestroyed;
        _destroySignalId = GtkNative.g_signal_connect_data(
            _widget,
            "destroy",
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_destroyCallback),
            IntPtr.Zero, IntPtr.Zero, 0);

        // Set initial background color based on theme
        UpdateBackgroundForTheme();

        // Subscribe to theme changes to update background color. The idle
        // source is tracked so Dispose can remove it if it has not fired yet.
        _themeChangedHandler = (sender, args) =>
        {
            if (_disposed || _widget == IntPtr.Zero) return;
            if (_themeIdleSourceId != 0) return; // one pending update is enough
            _themeIdleSourceId = GLibNative.IdleAdd(() =>
            {
                _themeIdleSourceId = 0;
                if (!_disposed)
                    UpdateBackgroundForTheme();
                return false;
            });
        };
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += _themeChangedHandler;
        }

        DiagnosticLog.Debug("GtkWebViewPlatformView", "Created WebKitWebView widget");
    }

    private void OnWidgetDestroyed(IntPtr widget, IntPtr userData)
    {
        // Drop WebKitNative's per-widget callback bookkeeping (it is keyed by
        // the widget pointer, which becomes meaningless now), then zero our
        // pointer and ids so no managed call reaches freed memory. GTK
        // disconnects the remaining handlers itself during destroy.
        WebKitNative.DisconnectLoadChanged(widget);
        WebKitNative.DisconnectScriptDialog(widget);
        _widget = IntPtr.Zero;
        _loadChangedSignalId = 0;
        _scriptDialogSignalId = 0;
        _destroySignalId = 0;
    }

    /// <summary>
    /// Updates the WebView background color based on the current app theme.
    /// </summary>
    public void UpdateBackgroundForTheme()
    {
        if (_widget == IntPtr.Zero) return;

        var isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
        if (isDark)
        {
            // Dark theme: use a dark gray background
            WebKitNative.SetBackgroundColor(_widget, 0.12, 0.12, 0.12, 1.0); // #1E1E1E
        }
        else
        {
            // Light theme: use white background
            WebKitNative.SetBackgroundColor(_widget, 1.0, 1.0, 1.0, 1.0);
        }
    }

    private bool OnScriptDialog(IntPtr webView, IntPtr dialog, IntPtr userData)
    {
        try
        {
            var webkitDialogType = WebKitNative.GetScriptDialogType(dialog);
            var dialogType = (ScriptDialogType)(int)webkitDialogType;
            var message = WebKitNative.GetScriptDialogMessage(dialog) ?? "";

            DiagnosticLog.Debug("GtkWebViewPlatformView", $"Script dialog: type={dialogType}, message={message}");

            // Get the parent window for proper modal behavior
            IntPtr parentWindow = GtkHostService.Instance.HostWindow?.Window ?? IntPtr.Zero;

            // Handle prompt dialogs specially - they need a text entry
            if (dialogType == ScriptDialogType.Prompt)
            {
                return HandlePromptDialog(dialog, message, parentWindow);
            }

            // Determine dialog type and buttons based on JavaScript dialog type
            int messageType = GtkNative.GTK_MESSAGE_INFO;
            int buttons = GtkNative.GTK_BUTTONS_OK;

            switch (dialogType)
            {
                case ScriptDialogType.Alert:
                    messageType = GtkNative.GTK_MESSAGE_INFO;
                    buttons = GtkNative.GTK_BUTTONS_OK;
                    break;
                case ScriptDialogType.Confirm:
                case ScriptDialogType.BeforeUnloadConfirm:
                    messageType = GtkNative.GTK_MESSAGE_QUESTION;
                    buttons = GtkNative.GTK_BUTTONS_OK_CANCEL;
                    break;
            }

            // Create and show native GTK message dialog
            IntPtr gtkDialog = GtkNative.gtk_message_dialog_new(
                parentWindow,
                GtkNative.GTK_DIALOG_MODAL | GtkNative.GTK_DIALOG_DESTROY_WITH_PARENT,
                messageType,
                buttons,
                message,
                IntPtr.Zero);

            if (gtkDialog != IntPtr.Zero)
            {
                // Set dialog title based on type
                string title = dialogType switch
                {
                    ScriptDialogType.Alert => "Alert",
                    ScriptDialogType.Confirm => "Confirm",
                    ScriptDialogType.BeforeUnloadConfirm => "Leave Page?",
                    _ => "Message"
                };
                GtkNative.gtk_window_set_title(gtkDialog, title);

                // Apply theme-aware CSS styling based on app's current theme
                ApplyDialogTheme(gtkDialog);

                // Make dialog modal to parent if we have a parent
                if (parentWindow != IntPtr.Zero)
                {
                    GtkNative.gtk_window_set_transient_for(gtkDialog, parentWindow);
                    GtkNative.gtk_window_set_modal(gtkDialog, true);
                }

                // Run the dialog synchronously - this blocks until user responds
                int response = GtkNative.gtk_dialog_run(gtkDialog);
                DiagnosticLog.Debug("GtkWebViewPlatformView", $"Dialog response: {response}");

                // Set the confirmed state for confirm dialogs
                if (dialogType == ScriptDialogType.Confirm || dialogType == ScriptDialogType.BeforeUnloadConfirm)
                {
                    bool confirmed = response == GtkNative.GTK_RESPONSE_OK || response == GtkNative.GTK_RESPONSE_YES;
                    WebKitNative.SetScriptDialogConfirmed(dialog, confirmed);
                }

                // Clean up
                GtkNative.gtk_widget_destroy(gtkDialog);
            }

            // Return true to indicate we handled the dialog (prevents WebKitGTK's default)
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewPlatformView", $"Error in OnScriptDialog: {ex.Message}", ex);
            // Return false on error to let WebKitGTK try its default handling
            return false;
        }
    }

    private bool HandlePromptDialog(IntPtr webkitDialog, string message, IntPtr parentWindow)
    {
        try
        {
            // Get the default text for the prompt
            string? defaultText = WebKitNative.GetScriptDialogPromptDefaultText(webkitDialog) ?? "";

            // Create a custom dialog with OK/Cancel buttons
            IntPtr gtkDialog = GtkNative.gtk_dialog_new_with_buttons(
                "Prompt",
                parentWindow,
                GtkNative.GTK_DIALOG_MODAL | GtkNative.GTK_DIALOG_DESTROY_WITH_PARENT,
                "_Cancel",
                GtkNative.GTK_RESPONSE_CANCEL,
                "_OK",
                GtkNative.GTK_RESPONSE_OK,
                IntPtr.Zero);

            if (gtkDialog == IntPtr.Zero)
            {
                DiagnosticLog.Error("GtkWebViewPlatformView", "Failed to create prompt dialog");
                return false;
            }

            // Apply theme-aware CSS styling
            ApplyDialogTheme(gtkDialog);

            // Get the content area
            IntPtr contentArea = GtkNative.gtk_dialog_get_content_area(gtkDialog);

            // Create a vertical box for the content
            IntPtr vbox = GtkNative.gtk_box_new(GtkNative.GTK_ORIENTATION_VERTICAL, 10);
            GtkNative.gtk_widget_set_margin_start(vbox, 12);
            GtkNative.gtk_widget_set_margin_end(vbox, 12);
            GtkNative.gtk_widget_set_margin_top(vbox, 12);
            GtkNative.gtk_widget_set_margin_bottom(vbox, 12);

            // Add the message label
            IntPtr label = GtkNative.gtk_label_new(message);
            GtkNative.gtk_box_pack_start(vbox, label, false, false, 0);

            // Add the text entry
            IntPtr entry = GtkNative.gtk_entry_new();
            GtkNative.gtk_entry_set_text(entry, defaultText);
            GtkNative.gtk_box_pack_start(vbox, entry, false, false, 0);

            // Add the vbox to content area
            GtkNative.gtk_box_pack_start(contentArea, vbox, true, true, 0);

            // Make dialog modal
            if (parentWindow != IntPtr.Zero)
            {
                GtkNative.gtk_window_set_transient_for(gtkDialog, parentWindow);
                GtkNative.gtk_window_set_modal(gtkDialog, true);
            }

            // Show all widgets
            GtkNative.gtk_widget_show_all(gtkDialog);

            // Run the dialog
            int response = GtkNative.gtk_dialog_run(gtkDialog);
            DiagnosticLog.Debug("GtkWebViewPlatformView", $"Prompt dialog response: {response}");

            if (response == GtkNative.GTK_RESPONSE_OK)
            {
                // Get the text from the entry
                IntPtr textPtr = GtkNative.gtk_entry_get_text(entry);
                string? enteredText = textPtr != IntPtr.Zero
                    ? System.Runtime.InteropServices.Marshal.PtrToStringUTF8(textPtr)
                    : "";

                DiagnosticLog.Debug("GtkWebViewPlatformView", $"Prompt text: {enteredText}");

                // Set the prompt response
                WebKitNative.SetScriptDialogPromptText(webkitDialog, enteredText ?? "");
            }
            else
            {
                // User cancelled - for prompts, not confirming means returning null
                // WebKit handles this by not calling prompt_set_text
            }

            // Clean up
            GtkNative.gtk_widget_destroy(gtkDialog);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewPlatformView", $"Error in HandlePromptDialog: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Applies theme-aware CSS styling to a GTK dialog based on the app's current theme.
    /// </summary>
    private void ApplyDialogTheme(IntPtr gtkDialog)
    {
        try
        {
            // Check the app's current theme (not the system theme)
            bool isDark = Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;

            // If UserAppTheme is Unspecified, fall back to RequestedTheme
            if (Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified)
            {
                isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
            }

            DiagnosticLog.Debug("GtkWebViewPlatformView", $"ApplyDialogTheme: isDark={isDark}, UserAppTheme={Microsoft.Maui.Controls.Application.Current?.UserAppTheme}");

            // Create comprehensive CSS based on the theme - targeting all dialog elements
            string css = isDark
                ? @"
                    * {
                        background-color: #303030;
                        color: #E0E0E0;
                    }
                    window, dialog, messagedialog, .background {
                        background-color: #303030;
                        color: #E0E0E0;
                    }
                    headerbar, headerbar *, .titlebar, .titlebar * {
                        background-color: #252525;
                        background-image: none;
                        color: #E0E0E0;
                        border-color: #404040;
                        box-shadow: none;
                    }
                    headerbar button, .titlebar button {
                        background-color: #353535;
                        background-image: none;
                        color: #E0E0E0;
                    }
                    .dialog-action-area, .dialog-action-box, actionbar {
                        background-color: #303030;
                    }
                    label, .message-dialog-message, .message-dialog-secondary-message {
                        color: #E0E0E0;
                    }
                    button {
                        background-image: none;
                        background-color: #505050;
                        color: #E0E0E0;
                        border-color: #606060;
                    }
                    button:hover {
                        background-color: #606060;
                    }
                    entry {
                        background-color: #404040;
                        color: #E0E0E0;
                    }
                "
                : @"
                    * {
                        background-color: #FFFFFF;
                        color: #212121;
                    }
                    window, dialog, messagedialog, .background {
                        background-color: #FFFFFF;
                        color: #212121;
                    }
                    headerbar, headerbar *, .titlebar, .titlebar * {
                        background-color: #F5F5F5;
                        background-image: none;
                        color: #212121;
                        border-color: #E0E0E0;
                        box-shadow: none;
                    }
                    headerbar button, .titlebar button {
                        background-color: #EBEBEB;
                        background-image: none;
                        color: #212121;
                    }
                    .dialog-action-area, .dialog-action-box, actionbar {
                        background-color: #FFFFFF;
                    }
                    label, .message-dialog-message, .message-dialog-secondary-message {
                        color: #212121;
                    }
                    button {
                        background-image: none;
                        background-color: #F5F5F5;
                        color: #212121;
                        border-color: #E0E0E0;
                    }
                    button:hover {
                        background-color: #E0E0E0;
                    }
                    entry {
                        background-color: #FFFFFF;
                        color: #212121;
                    }
                ";

            // Create CSS provider and apply to the screen so all child widgets inherit it
            IntPtr cssProvider = GtkNative.gtk_css_provider_new();
            if (cssProvider != IntPtr.Zero)
            {
                GtkNative.gtk_css_provider_load_from_data(cssProvider, css, -1, IntPtr.Zero);

                // Get the screen from the dialog and apply CSS to entire screen for this dialog
                IntPtr screen = GtkNative.gtk_widget_get_screen(gtkDialog);
                if (screen == IntPtr.Zero)
                {
                    screen = GtkNative.gdk_screen_get_default();
                }

                if (screen != IntPtr.Zero)
                {
                    GtkNative.gtk_style_context_add_provider_for_screen(screen, cssProvider, GtkNative.GTK_STYLE_PROVIDER_PRIORITY_USER);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewPlatformView", $"Error applying dialog theme: {ex.Message}", ex);
        }
    }

    private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        try
        {
            string uri = WebKitNative.GetUri(webView) ?? _currentUri ?? "";
            switch ((WebKitNative.WebKitLoadEvent)loadEvent)
            {
                case WebKitNative.WebKitLoadEvent.Started:
                    DiagnosticLog.Debug("GtkWebViewPlatformView", "Load started: " + uri);
                    NavigationStarted?.Invoke(this, uri);
                    break;
                case WebKitNative.WebKitLoadEvent.Finished:
                    _currentUri = uri;
                    DiagnosticLog.Debug("GtkWebViewPlatformView", "Load finished: " + uri);
                    NavigationCompleted?.Invoke(this, (uri, true));
                    break;
                case WebKitNative.WebKitLoadEvent.Committed:
                    _currentUri = uri;
                    DiagnosticLog.Debug("GtkWebViewPlatformView", "Load committed: " + uri);
                    break;
                case WebKitNative.WebKitLoadEvent.Redirected:
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewPlatformView", "Error in OnLoadChanged: " + ex.Message, ex);
        }
    }

    public void Navigate(string uri)
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.LoadUri(_widget, uri);
            DiagnosticLog.Debug("GtkWebViewPlatformView", "Navigate to: " + uri);
        }
    }

    public void LoadHtml(string html, string? baseUri = null)
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.LoadHtml(_widget, html, baseUri);
            DiagnosticLog.Debug("GtkWebViewPlatformView", "Load HTML content");
        }
    }

    public void GoBack()
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.GoBack(_widget);
        }
    }

    public void GoForward()
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.GoForward(_widget);
        }
    }

    public bool CanGoBack()
    {
        return _widget != IntPtr.Zero && WebKitNative.CanGoBack(_widget);
    }

    public bool CanGoForward()
    {
        return _widget != IntPtr.Zero && WebKitNative.CanGoForward(_widget);
    }

    public void Reload()
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.Reload(_widget);
        }
    }

    public void Stop()
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.StopLoading(_widget);
        }
    }

    public string? GetTitle()
    {
        return _widget == IntPtr.Zero ? null : WebKitNative.GetTitle(_widget);
    }

    public string? GetUri()
    {
        return _widget == IntPtr.Zero ? null : WebKitNative.GetUri(_widget);
    }

    public void SetJavascriptEnabled(bool enabled)
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.SetJavascriptEnabled(_widget, enabled);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Unsubscribe from theme changes
            if (_themeChangedHandler != null && Microsoft.Maui.Controls.Application.Current != null)
            {
                Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged -= _themeChangedHandler;
                _themeChangedHandler = null;
            }

            // Remove a queued-but-unfired theme update idle.
            if (_themeIdleSourceId != 0)
            {
                GLibNative.SourceRemove(_themeIdleSourceId);
                _themeIdleSourceId = 0;
            }

            if (_widget != IntPtr.Zero)
            {
                WebKitNative.DisconnectLoadChanged(_widget);
                WebKitNative.DisconnectScriptDialog(_widget);
                // Keep the "destroy" handler connected: the widget still lives
                // in the GTK host window tree and that handler is what zeroes
                // _widget when the tree is destroyed. _destroyCallback stays
                // rooted for the same reason.
            }
            _widget = IntPtr.Zero;
            _loadChangedCallback = null;
            _scriptDialogCallback = null;
        }
    }
}
