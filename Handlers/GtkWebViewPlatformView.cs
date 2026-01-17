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
    private IntPtr _widget;
    private bool _disposed;
    private string? _currentUri;
    private ulong _loadChangedSignalId;
    private ulong _scriptDialogSignalId;
    private WebKitNative.LoadChangedCallback? _loadChangedCallback;
    private WebKitNative.ScriptDialogCallback? _scriptDialogCallback;
    private EventHandler<Microsoft.Maui.Controls.AppThemeChangedEventArgs>? _themeChangedHandler;

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

        // Set initial background color based on theme
        UpdateBackgroundForTheme();

        // Subscribe to theme changes to update background color
        _themeChangedHandler = (sender, args) =>
        {
            GLibNative.IdleAdd(() =>
            {
                UpdateBackgroundForTheme();
                return false;
            });
        };
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += _themeChangedHandler;
        }

        Console.WriteLine("[GtkWebViewPlatformView] Created WebKitWebView widget");
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

            Console.WriteLine($"[GtkWebViewPlatformView] Script dialog: type={dialogType}, message={message}");

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

                // Make dialog modal to parent if we have a parent
                if (parentWindow != IntPtr.Zero)
                {
                    GtkNative.gtk_window_set_transient_for(gtkDialog, parentWindow);
                    GtkNative.gtk_window_set_modal(gtkDialog, true);
                }

                // Run the dialog synchronously - this blocks until user responds
                int response = GtkNative.gtk_dialog_run(gtkDialog);
                Console.WriteLine($"[GtkWebViewPlatformView] Dialog response: {response}");

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
            Console.WriteLine($"[GtkWebViewPlatformView] Error in OnScriptDialog: {ex.Message}");
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
                Console.WriteLine("[GtkWebViewPlatformView] Failed to create prompt dialog");
                return false;
            }

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
            Console.WriteLine($"[GtkWebViewPlatformView] Prompt dialog response: {response}");

            if (response == GtkNative.GTK_RESPONSE_OK)
            {
                // Get the text from the entry
                IntPtr textPtr = GtkNative.gtk_entry_get_text(entry);
                string? enteredText = textPtr != IntPtr.Zero
                    ? System.Runtime.InteropServices.Marshal.PtrToStringUTF8(textPtr)
                    : "";

                Console.WriteLine($"[GtkWebViewPlatformView] Prompt text: {enteredText}");

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
            Console.WriteLine($"[GtkWebViewPlatformView] Error in HandlePromptDialog: {ex.Message}");
            return false;
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
                    Console.WriteLine("[GtkWebViewPlatformView] Load started: " + uri);
                    NavigationStarted?.Invoke(this, uri);
                    break;
                case WebKitNative.WebKitLoadEvent.Finished:
                    _currentUri = uri;
                    Console.WriteLine("[GtkWebViewPlatformView] Load finished: " + uri);
                    NavigationCompleted?.Invoke(this, (uri, true));
                    break;
                case WebKitNative.WebKitLoadEvent.Committed:
                    _currentUri = uri;
                    Console.WriteLine("[GtkWebViewPlatformView] Load committed: " + uri);
                    break;
                case WebKitNative.WebKitLoadEvent.Redirected:
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[GtkWebViewPlatformView] Error in OnLoadChanged: " + ex.Message);
            Console.WriteLine("[GtkWebViewPlatformView] Stack trace: " + ex.StackTrace);
        }
    }

    public void Navigate(string uri)
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.LoadUri(_widget, uri);
            Console.WriteLine("[GtkWebViewPlatformView] Navigate to: " + uri);
        }
    }

    public void LoadHtml(string html, string? baseUri = null)
    {
        if (_widget != IntPtr.Zero)
        {
            WebKitNative.LoadHtml(_widget, html, baseUri);
            Console.WriteLine("[GtkWebViewPlatformView] Load HTML content");
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

            if (_widget != IntPtr.Zero)
            {
                WebKitNative.DisconnectLoadChanged(_widget);
                WebKitNative.DisconnectScriptDialog(_widget);
            }
            _widget = IntPtr.Zero;
            _loadChangedCallback = null;
            _scriptDialogCallback = null;
        }
    }
}
