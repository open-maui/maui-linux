// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// One printer the host knows about, as reported by CUPS. The default printer
/// has <see cref="IsDefault"/> set; <see cref="InstanceName"/> covers per-user
/// "instances" (a saved option set on top of the printer queue, e.g.
/// "HP-LaserJet/draft").
/// </summary>
public sealed class PrinterInfo
{
    public string Name { get; init; } = string.Empty;
    public string? InstanceName { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();

    public string DisplayName =>
        string.IsNullOrEmpty(InstanceName) ? Name : $"{Name}/{InstanceName}";
}

/// <summary>
/// Result of submitting a print job. <see cref="JobId"/> is 0 when CUPS refused
/// the job (printer unknown, file missing, permission denied, …); the matching
/// <see cref="ErrorMessage"/> carries the libcups error string.
/// </summary>
public sealed class PrintJobResult
{
    public int JobId { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Succeeded => JobId > 0;
}

/// <summary>
/// CUPS-backed Linux print service. Three primary capabilities:
///
/// <list type="bullet">
///   <item>Enumerate the host's printers (<see cref="EnumeratePrinters"/>).</item>
///   <item>Submit a file (PDF, PostScript, image, or raw) to a queue (<see cref="PrintFile"/>).</item>
///   <item>Render a Skia surface to PDF and print it (<see cref="PrintSkiaPagesAsync"/>) —
///         the high-level path most apps want.</item>
/// </list>
///
/// libcups is part of every desktop Linux install (Fedora ships it in the base
/// system, Debian/Ubuntu install it via the cups package, which is pulled in by
/// the standard desktop metapackages). If it's missing entirely,
/// <see cref="IsAvailable"/> reports false and the methods return an empty
/// printer list / a failed job result without throwing.
/// </summary>
public static class PrintService
{
    private const string LibCups = "libcups.so.2";

    [StructLayout(LayoutKind.Sequential)]
    private struct cups_dest_t
    {
        public IntPtr name;
        public IntPtr instance;
        public int is_default;
        public int num_options;
        public IntPtr options;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct cups_option_t
    {
        public IntPtr name;
        public IntPtr value;
    }

    [DllImport(LibCups, EntryPoint = "cupsGetDests2")]
    private static extern int cupsGetDests2(IntPtr http, out IntPtr dests);

    [DllImport(LibCups, EntryPoint = "cupsFreeDests")]
    private static extern void cupsFreeDests(int num_dests, IntPtr dests);

    [DllImport(LibCups, EntryPoint = "cupsPrintFile2", CharSet = CharSet.Ansi)]
    private static extern int cupsPrintFile2(IntPtr http, string printer, string filename, string title, int num_options, IntPtr options);

    [DllImport(LibCups, EntryPoint = "cupsLastErrorString")]
    private static extern IntPtr cupsLastErrorString();

    [DllImport(LibCups, EntryPoint = "cupsAddOption", CharSet = CharSet.Ansi)]
    private static extern int cupsAddOption(string name, string value, int num_options, ref IntPtr options);

    [DllImport(LibCups, EntryPoint = "cupsFreeOptions")]
    private static extern void cupsFreeOptions(int num_options, IntPtr options);

    private static bool? s_available;

    /// <summary>True when libcups is present and queryable.</summary>
    public static bool IsAvailable
    {
        get
        {
            if (s_available.HasValue) return s_available.Value;
            try
            {
                NativeLibrary.Load(LibCups).ToString();   // throws DllNotFoundException if missing
                s_available = true;
            }
            catch
            {
                s_available = false;
            }
            return s_available.Value;
        }
    }

    /// <summary>
    /// Enumerate every printer queue this host knows about, including the
    /// system-default printer (flagged via <see cref="PrinterInfo.IsDefault"/>)
    /// and any per-user instances saved with <c>lpoptions</c>.
    /// Returns an empty list when CUPS isn't installed.
    /// </summary>
    public static IReadOnlyList<PrinterInfo> EnumeratePrinters()
    {
        if (!IsAvailable) return Array.Empty<PrinterInfo>();

        IntPtr destsPtr = IntPtr.Zero;
        int count = 0;
        try
        {
            count = cupsGetDests2(IntPtr.Zero, out destsPtr);
            if (count <= 0 || destsPtr == IntPtr.Zero) return Array.Empty<PrinterInfo>();

            var results = new List<PrinterInfo>(count);
            var destSize = Marshal.SizeOf<cups_dest_t>();
            for (int i = 0; i < count; i++)
            {
                var ptr = IntPtr.Add(destsPtr, i * destSize);
                var dest = Marshal.PtrToStructure<cups_dest_t>(ptr);
                var name = Marshal.PtrToStringUTF8(dest.name) ?? string.Empty;
                var instance = Marshal.PtrToStringUTF8(dest.instance);

                var options = new Dictionary<string, string>(dest.num_options);
                if (dest.num_options > 0 && dest.options != IntPtr.Zero)
                {
                    var optSize = Marshal.SizeOf<cups_option_t>();
                    for (int j = 0; j < dest.num_options; j++)
                    {
                        var optPtr = IntPtr.Add(dest.options, j * optSize);
                        var opt = Marshal.PtrToStructure<cups_option_t>(optPtr);
                        var k = Marshal.PtrToStringUTF8(opt.name);
                        var v = Marshal.PtrToStringUTF8(opt.value) ?? string.Empty;
                        if (!string.IsNullOrEmpty(k)) options[k] = v;
                    }
                }

                results.Add(new PrinterInfo
                {
                    Name = name,
                    InstanceName = instance,
                    IsDefault = dest.is_default != 0,
                    Options = options,
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("PrintService", $"EnumeratePrinters failed: {ex.Message}");
            return Array.Empty<PrinterInfo>();
        }
        finally
        {
            if (destsPtr != IntPtr.Zero) cupsFreeDests(count, destsPtr);
        }
    }

    /// <summary>
    /// Async wrapper for <see cref="EnumeratePrinters"/>. The enumeration is a
    /// blocking round-trip to cupsd — prefer this overload from UI code.
    /// </summary>
    public static Task<IReadOnlyList<PrinterInfo>> EnumeratePrintersAsync()
        => Task.Run(EnumeratePrinters);

    /// <summary>
    /// Submit <paramref name="filePath"/> as a print job to
    /// <paramref name="printer"/>. CUPS sniffs the file's MIME type and runs
    /// the matching filters (PDF, PostScript, PNG/JPEG, plain text…) so any
    /// printable format the queue supports just works. <paramref name="options"/>
    /// maps directly onto IPP options — common keys: <c>media</c>, <c>copies</c>,
    /// <c>sides</c>, <c>print-color-mode</c>, <c>orientation-requested</c>.
    /// </summary>
    public static PrintJobResult PrintFile(string printer, string filePath, string jobTitle = "OpenMaui Print Job", IReadOnlyDictionary<string, string>? options = null)
    {
        if (!IsAvailable)
            return new PrintJobResult { ErrorMessage = "CUPS (libcups.so.2) not installed on this system" };
        if (string.IsNullOrWhiteSpace(printer))
            return new PrintJobResult { ErrorMessage = "Printer name is required" };
        if (!File.Exists(filePath))
            return new PrintJobResult { ErrorMessage = $"File not found: {filePath}" };

        IntPtr opts = IntPtr.Zero;
        int numOpts = 0;
        try
        {
            if (options != null)
                foreach (var kv in options)
                    numOpts = cupsAddOption(kv.Key, kv.Value, numOpts, ref opts);

            var jobId = cupsPrintFile2(IntPtr.Zero, printer, filePath, jobTitle, numOpts, opts);
            if (jobId <= 0)
            {
                var errPtr = cupsLastErrorString();
                var err = errPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(errPtr) : null;
                return new PrintJobResult { JobId = 0, ErrorMessage = err ?? "Unknown CUPS error" };
            }
            return new PrintJobResult { JobId = jobId };
        }
        catch (Exception ex)
        {
            return new PrintJobResult { ErrorMessage = ex.Message };
        }
        finally
        {
            if (opts != IntPtr.Zero) cupsFreeOptions(numOpts, opts);
        }
    }

    /// <summary>
    /// Async wrapper for <see cref="PrintFile"/>. cupsPrintFile2 synchronously
    /// streams the whole document to cupsd — prefer this overload from UI code
    /// so large jobs don't freeze the main thread.
    /// </summary>
    public static Task<PrintJobResult> PrintFileAsync(string printer, string filePath, string jobTitle = "OpenMaui Print Job", IReadOnlyDictionary<string, string>? options = null)
        => Task.Run(() => PrintFile(printer, filePath, jobTitle, options));

    /// <summary>
    /// Render Skia drawing operations to a multi-page PDF in a temp file, then
    /// hand the file to CUPS. <paramref name="renderPage"/> is invoked once per
    /// page with an <see cref="SkiaSharp.SKCanvas"/> and the page number
    /// (1-based). Return <c>true</c> to commit that page and be called again;
    /// return <c>false</c> to stop — anything drawn during a <c>false</c> call
    /// is discarded, so <c>false</c> on the first call prints nothing and no
    /// job is submitted. Rendering and job submission both run off the
    /// caller's thread.
    /// </summary>
    public static async Task<PrintJobResult> PrintSkiaPagesAsync(
        string printer,
        Func<SkiaSharp.SKCanvas, int, bool> renderPage,
        SkiaSharp.SKSize pageSize,
        string jobTitle = "OpenMaui Print Job",
        IReadOnlyDictionary<string, string>? options = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"openmaui-print-{Guid.NewGuid():N}.pdf");
        try
        {
            var hasPages = await Task.Run(() =>
            {
                // SKFileWStream can't set a file mode, so create the inode
                // owner-only first; its fopen("wb") truncates but keeps the
                // mode, so the spooled document isn't world-readable in /tmp.
                new FileStream(tempPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
                }).Dispose();

                using var stream = new SkiaSharp.SKFileWStream(tempPath);
                using var document = SkiaSharp.SKDocument.CreatePdf(stream);
                if (document == null) throw new InvalidOperationException("SKDocument.CreatePdf returned null");

                var bounds = SkiaSharp.SKRect.Create(pageSize.Width, pageSize.Height);
                int page = 1;
                while (true)
                {
                    // Record the page first: renderPage's return decides
                    // whether this call produced a page at all, and a begun
                    // SKDocument page can't be discarded once EndPage runs.
                    using var recorder = new SkiaSharp.SKPictureRecorder();
                    var isPage = renderPage(recorder.BeginRecording(bounds), page);
                    using var picture = recorder.EndRecording();
                    if (!isPage) break;

                    var pageCanvas = document.BeginPage(pageSize.Width, pageSize.Height);
                    if (pageCanvas == null) break;
                    pageCanvas.DrawPicture(picture);
                    document.EndPage();
                    page++;
                }

                if (page == 1)
                {
                    document.Abort();   // no pages — caller had nothing to print
                    return false;
                }
                document.Close();
                return true;
            });

            if (!hasPages)
                return new PrintJobResult { ErrorMessage = "renderPage returned false on the first page — nothing to print" };

            return await PrintFileAsync(printer, tempPath, jobTitle, options);
        }
        catch (Exception ex)
        {
            return new PrintJobResult { ErrorMessage = ex.Message };
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}
