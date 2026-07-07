// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// What the user picked in the print dialog. <see cref="PrinterName"/> matches
/// the CUPS destination names returned by <see cref="PrintService.EnumeratePrinters"/>
/// (GTK's printer list comes from the same CUPS backend), and
/// <see cref="Options"/> is ready to pass to <see cref="PrintService.PrintFile"/> /
/// <see cref="PrintService.PrintSkiaPagesAsync"/> unchanged.
/// </summary>
public sealed class PrintDialogResult
{
    public string PrinterName { get; init; } = string.Empty;

    /// <summary>CUPS job options (copies, page-ranges, sides, …) reflecting the dialog selections.</summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// True when the user clicked Preview instead of Print. Standalone
    /// GtkPrintUnixDialog leaves preview generation to the app (only
    /// GtkPrintOperation automates the handoff), so render the document —
    /// e.g. the same pages you'd give <see cref="PrintService.PrintSkiaPagesAsync"/>,
    /// written to a PDF — and open it in the default viewer.
    /// </summary>
    public bool PreviewRequested { get; init; }
}

/// <summary>
/// GtkPrintUnixDialog binding behind <see cref="PrintService.ShowPrintDialogAsync"/>.
/// GTK3, same library the rest of the app runs on (GtkHostWindow / tray / context
/// menus). All GTK calls are marshalled to the GLib main thread; the dialog is
/// non-blocking — the "response" signal resolves the task, so the main loop
/// keeps running while it's open. If libgtk-3 (or a GTK built without printing
/// support) isn't loadable, ShowAsync resolves to null after a single warning
/// and PrintService's CUPS paths are unaffected.
/// </summary>
internal static class GtkPrintDialog
{
    private const string LibGtk3 = "libgtk-3.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGlib = "libglib-2.0.so.0";

    // GtkResponseType
    private const int ResponseOk = -5;       // Print
    private const int ResponseApply = -10;   // Preview

    // GtkPrintCapabilities the app can honor. PREVIEW shows the button (we
    // surface it via PrintDialogResult.PreviewRequested); GENERATE_PDF is true
    // for our Skia render path.
    private const int Capabilities =
        0x001    // GTK_PRINT_CAPABILITY_PAGE_SET
        | 0x002  // GTK_PRINT_CAPABILITY_COPIES
        | 0x004  // GTK_PRINT_CAPABILITY_COLLATE
        | 0x008  // GTK_PRINT_CAPABILITY_REVERSE
        | 0x010  // GTK_PRINT_CAPABILITY_SCALE
        | 0x020  // GTK_PRINT_CAPABILITY_GENERATE_PDF
        | 0x080  // GTK_PRINT_CAPABILITY_PREVIEW
        | 0x100; // GTK_PRINT_CAPABILITY_NUMBER_UP

    // GtkPrintPages / GtkPageSet / GtkPrintDuplex values used below
    private const int PrintPagesRanges = 2;
    private const int PageSetEven = 1;
    private const int PageSetOdd = 2;
    private const int DuplexHorizontal = 1;   // long edge
    private const int DuplexVertical = 2;     // short edge

    [DllImport(LibGtk3, CharSet = CharSet.Ansi)]
    private static extern IntPtr gtk_print_unix_dialog_new(string? title, IntPtr parent);
    [DllImport(LibGtk3)] private static extern void gtk_print_unix_dialog_set_manual_capabilities(IntPtr dialog, int capabilities);
    [DllImport(LibGtk3)] private static extern IntPtr gtk_print_unix_dialog_get_selected_printer(IntPtr dialog);   // transfer none
    [DllImport(LibGtk3)] private static extern IntPtr gtk_print_unix_dialog_get_settings(IntPtr dialog);           // transfer full
    [DllImport(LibGtk3)] private static extern IntPtr gtk_printer_get_name(IntPtr printer);                        // transfer none

    [DllImport(LibGtk3)] private static extern int gtk_print_settings_get_n_copies(IntPtr settings);
    [DllImport(LibGtk3)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool gtk_print_settings_get_collate(IntPtr settings);
    [DllImport(LibGtk3)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool gtk_print_settings_get_reverse(IntPtr settings);
    [DllImport(LibGtk3)] private static extern int gtk_print_settings_get_number_up(IntPtr settings);
    [DllImport(LibGtk3)] private static extern int gtk_print_settings_get_print_pages(IntPtr settings);
    [DllImport(LibGtk3)] private static extern int gtk_print_settings_get_page_set(IntPtr settings);
    [DllImport(LibGtk3)] private static extern int gtk_print_settings_get_duplex(IntPtr settings);
    [DllImport(LibGtk3)] private static extern IntPtr gtk_print_settings_get_page_ranges(IntPtr settings, out int numRanges);   // g_free
    [DllImport(LibGtk3)] private static extern void gtk_print_settings_foreach(IntPtr settings, SettingsForeachDelegate func, IntPtr userData);

    [DllImport(LibGObject, CharSet = CharSet.Ansi, EntryPoint = "g_signal_connect_data")]
    private static extern ulong g_signal_connect_data(IntPtr instance, string detailedSignal, IntPtr cHandler, IntPtr data, IntPtr destroyData, int connectFlags);
    [DllImport(LibGObject)] private static extern void g_object_unref(IntPtr obj);
    [DllImport(LibGlib)] private static extern void g_free(IntPtr mem);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ResponseDelegate(IntPtr dialog, int responseId, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SettingsForeachDelegate(IntPtr key, IntPtr value, IntPtr userData);

    private static bool? s_available;
    private static bool s_warnedUnavailable;

    // Present + exports the print dialog symbols (GTK can be built without
    // printing support, in which case libgtk-3 loads but the symbol is absent).
    private static bool IsAvailable
    {
        get
        {
            if (s_available.HasValue) return s_available.Value;
            s_available = NativeLibrary.TryLoad(LibGtk3, out var handle)
                && NativeLibrary.TryGetExport(handle, "gtk_print_unix_dialog_new", out _);
            return s_available.Value;
        }
    }

    // Same rationale as the other GTK-backed services: GTK is not thread-safe,
    // so marshal to the GLib main loop; before the dispatcher exists the app
    // is single-threaded startup code and inline is safe.
    private static void RunOnMain(Action action)
    {
        if (LinuxDispatcher.IsMainThread || LinuxDispatcher.Main is not { } main) action();
        else main.Dispatch(action);
    }

    public static Task<PrintDialogResult?> ShowAsync(string title, IntPtr parentWindow)
    {
        if (!IsAvailable)
        {
            if (!s_warnedUnavailable)
            {
                s_warnedUnavailable = true;
                DiagnosticLog.Warn("GtkPrintDialog", "GTK3 print dialog unavailable (libgtk-3.so.0 missing or built without printing) — ShowPrintDialogAsync returns null");
            }
            return Task.FromResult<PrintDialogResult?>(null);
        }

        var tcs = new TaskCompletionSource<PrintDialogResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnMain(() =>
        {
            try
            {
                // No-op when the app (GtkHostWindow path) already initialized GTK.
                int argc = 0;
                IntPtr argv = IntPtr.Zero;
                if (!GtkNative.gtk_init_check(ref argc, ref argv))
                {
                    DiagnosticLog.Warn("GtkPrintDialog", "gtk_init_check failed (no display?) — cannot show print dialog");
                    tcs.TrySetResult(null);
                    return;
                }

                var dialog = gtk_print_unix_dialog_new(title, parentWindow);
                if (dialog == IntPtr.Zero)
                {
                    tcs.TrySetResult(null);
                    return;
                }
                gtk_print_unix_dialog_set_manual_capabilities(dialog, Capabilities);

                // Pin the response handler until the (single) response arrives;
                // freeing at the end of the callback is safe because the
                // executing frame keeps the delegate reachable.
                GCHandle handlerHandle = default;
                ResponseDelegate handler = (d, responseId, _) =>
                {
                    PrintDialogResult? result = null;
                    try
                    {
                        if (responseId is ResponseOk or ResponseApply)
                            result = HarvestSelection(d, previewRequested: responseId == ResponseApply);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.Error("GtkPrintDialog", $"Reading print dialog selection failed: {ex.Message}");
                    }
                    finally
                    {
                        GtkNative.gtk_widget_destroy(d);
                        if (handlerHandle.IsAllocated) handlerHandle.Free();
                        tcs.TrySetResult(result);
                    }
                };
                handlerHandle = GCHandle.Alloc(handler);
                g_signal_connect_data(dialog, "response", Marshal.GetFunctionPointerForDelegate(handler), IntPtr.Zero, IntPtr.Zero, 0);

                GtkNative.gtk_widget_show(dialog);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("GtkPrintDialog", $"Showing print dialog failed: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });
        return tcs.Task;
    }

    private static PrintDialogResult? HarvestSelection(IntPtr dialog, bool previewRequested)
    {
        var printer = gtk_print_unix_dialog_get_selected_printer(dialog);
        var name = printer != IntPtr.Zero ? Marshal.PtrToStringUTF8(gtk_printer_get_name(printer)) : null;
        if (string.IsNullOrEmpty(name)) return null;

        var settings = gtk_print_unix_dialog_get_settings(dialog);   // transfer full
        try
        {
            return new PrintDialogResult
            {
                PrinterName = name,
                Options = MapSettingsToCupsOptions(settings),
                PreviewRequested = previewRequested,
            };
        }
        finally
        {
            if (settings != IntPtr.Zero) g_object_unref(settings);
        }
    }

    private static Dictionary<string, string> MapSettingsToCupsOptions(IntPtr settings)
    {
        var options = new Dictionary<string, string>();
        if (settings == IntPtr.Zero) return options;

        // GTK's CUPS print backend stores every driver/PPD option the user
        // picked in the printer-specific tabs under "cups-<option>" keys —
        // copy those verbatim; they're already in cupsPrintFile shape.
        SettingsForeachDelegate collect = (keyPtr, valuePtr, _) =>
        {
            var key = Marshal.PtrToStringUTF8(keyPtr);
            var value = Marshal.PtrToStringUTF8(valuePtr);
            if (key != null && value != null && key.StartsWith("cups-", StringComparison.Ordinal))
                options[key.Substring("cups-".Length)] = value;
        };
        gtk_print_settings_foreach(settings, collect, IntPtr.Zero);
        GC.KeepAlive(collect);

        // Core dialog selections GTK keeps outside the cups- namespace. The
        // cups- keys win when both exist (TryAdd), since they reflect the
        // printer's actual PPD choices.
        var copies = gtk_print_settings_get_n_copies(settings);
        if (copies > 1) options.TryAdd("copies", copies.ToString());
        if (gtk_print_settings_get_collate(settings)) options.TryAdd("collate", "true");
        if (gtk_print_settings_get_reverse(settings)) options.TryAdd("outputorder", "reverse");

        var numberUp = gtk_print_settings_get_number_up(settings);
        if (numberUp > 1) options.TryAdd("number-up", numberUp.ToString());

        var pageSet = gtk_print_settings_get_page_set(settings);
        if (pageSet == PageSetEven) options.TryAdd("page-set", "even");
        else if (pageSet == PageSetOdd) options.TryAdd("page-set", "odd");

        var duplex = gtk_print_settings_get_duplex(settings);
        if (duplex == DuplexHorizontal) options.TryAdd("sides", "two-sided-long-edge");
        else if (duplex == DuplexVertical) options.TryAdd("sides", "two-sided-short-edge");

        if (gtk_print_settings_get_print_pages(settings) == PrintPagesRanges)
        {
            var rangesPtr = gtk_print_settings_get_page_ranges(settings, out var numRanges);
            if (rangesPtr != IntPtr.Zero)
            {
                try
                {
                    var parts = new List<string>(numRanges);
                    for (int i = 0; i < numRanges; i++)
                    {
                        // GtkPageRange { gint start; gint end; } — 0-based;
                        // CUPS page-ranges are 1-based.
                        var start = Marshal.ReadInt32(rangesPtr, i * 8) + 1;
                        var end = Marshal.ReadInt32(rangesPtr, i * 8 + 4) + 1;
                        if (end < start) end = start;
                        parts.Add(start == end ? start.ToString() : $"{start}-{end}");
                    }
                    if (parts.Count > 0) options.TryAdd("page-ranges", string.Join(",", parts));
                }
                finally
                {
                    g_free(rangesPtr);
                }
            }
        }

        return options;
    }
}
