// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Pdf.Native;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Pdf;

/// <summary>Why a PDF could not be opened or read.</summary>
public enum PdfiumError
{
    Unknown,
    /// <summary>Not a readable file.</summary>
    File,
    /// <summary>Not a PDF, or damaged.</summary>
    Format,
    /// <summary>Encrypted, and the password is missing or wrong.</summary>
    Password,
    /// <summary>An unsupported security handler.</summary>
    Security,
    /// <summary>A page that does not exist or cannot be read.</summary>
    Page,
}

/// <summary>A PDF that PDFium could not open or read.</summary>
public sealed class PdfiumException : Exception
{
    public PdfiumError Error { get; }

    public PdfiumException(PdfiumError error, string message) : base(message) => Error = error;
}

/// <summary>
/// A PDF document, rendered and read by Google's PDFium (bundled for linux-x64 and linux-arm64).
/// Sizes are in PDF points (1/72 inch). Thread-safe: calls are serialized, as PDFium requires.
/// </summary>
public sealed class PdfiumDocument : IDisposable
{
    private IntPtr _document;
    private IntPtr _data; // PDFium reads the document from this buffer for as long as it is open
    private readonly int _pageCount;

    private PdfiumDocument(IntPtr document, IntPtr data, int pageCount)
    {
        _document = document;
        _data = data;
        _pageCount = pageCount;
    }

    /// <summary>True when PDFium's native library can be loaded on this machine.</summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                lock (PdfiumNative.Sync)
                    PdfiumNative.EnsureInitialized();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>Opens a PDF from its bytes.</summary>
    /// <exception cref="PdfiumException">Not a PDF, damaged, or the password is missing or wrong.</exception>
    public static PdfiumDocument Open(byte[] data, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new PdfiumException(PdfiumError.Format, "The PDF is empty.");

        var buffer = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, buffer, data.Length);
            lock (PdfiumNative.Sync)
            {
                PdfiumNative.EnsureInitialized();
                var document = PdfiumNative.FPDF_LoadMemDocument64(buffer, (nuint)data.Length, string.IsNullOrEmpty(password) ? null : password);
                if (document == IntPtr.Zero)
                    throw LastError("The PDF could not be opened");
                var pages = PdfiumNative.FPDF_GetPageCount(document);
                var opened = new PdfiumDocument(document, buffer, pages);
                buffer = IntPtr.Zero; // owned by the document now
                return opened;
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Opens a PDF from a stream, read from its current position to the end.</summary>
    /// <exception cref="PdfiumException">Not a PDF, damaged, or the password is missing or wrong.</exception>
    public static PdfiumDocument Open(Stream stream, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Open(copy.ToArray(), password);
    }

    /// <summary>The number of pages.</summary>
    public int PageCount
    {
        get
        {
            ThrowIfDisposed();
            return _pageCount;
        }
    }

    /// <summary>A page's size in points (1/72 inch), as it is shown (its rotation applied).</summary>
    public SKSize GetPageSize(int pageIndex)
    {
        lock (PdfiumNative.Sync)
        {
            var page = LoadPage(pageIndex);
            try
            {
                return new SKSize(PdfiumNative.FPDF_GetPageWidthF(page), PdfiumNative.FPDF_GetPageHeightF(page));
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(page);
            }
        }
    }

    /// <summary>Renders a whole page into a <paramref name="width"/> × <paramref name="height"/> pixel bitmap, on white, with its annotations.</summary>
    public SKBitmap RenderPage(int pageIndex, int width, int height) =>
        RenderRegion(pageIndex, null, width, height);

    /// <summary>Renders a whole page at <paramref name="pixelsPerPoint"/> (1 is 72 DPI, 96/72 is 96 DPI).</summary>
    public SKBitmap RenderPage(int pageIndex, float pixelsPerPoint)
    {
        if (!(pixelsPerPoint > 0))
            throw new ArgumentOutOfRangeException(nameof(pixelsPerPoint));
        var size = GetPageSize(pageIndex);
        return RenderRegion(pageIndex, null,
            Math.Max(1, (int)MathF.Round(size.Width * pixelsPerPoint)),
            Math.Max(1, (int)MathF.Round(size.Height * pixelsPerPoint)));
    }

    /// <summary>
    /// Renders part of a page: <paramref name="sourcePoints"/> (in points, from the page's top left;
    /// the whole page when null) scaled to fill a <paramref name="width"/> × <paramref name="height"/>
    /// pixel bitmap, on white, with its annotations.
    /// </summary>
    public SKBitmap RenderRegion(int pageIndex, SKRect? sourcePoints, int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            lock (PdfiumNative.Sync)
            {
                var page = LoadPage(pageIndex);
                var target = PdfiumNative.FPDFBitmap_CreateEx(width, height, PdfiumNative.FPDFBitmap_BGRA, bitmap.GetPixels(), bitmap.RowBytes);
                try
                {
                    if (target == IntPtr.Zero)
                        throw new PdfiumException(PdfiumError.Unknown, "PDFium could not create a bitmap.");
                    PdfiumNative.FPDFBitmap_FillRect(target, 0, 0, width, height, 0xFFFFFFFF);

                    // The page is placed so the source region lands on the bitmap; PDFium clips the rest.
                    float pageWidth = PdfiumNative.FPDF_GetPageWidthF(page);
                    float pageHeight = PdfiumNative.FPDF_GetPageHeightF(page);
                    var source = sourcePoints is { Width: > 0, Height: > 0 } r ? r : new SKRect(0, 0, pageWidth, pageHeight);
                    double scaleX = width / (double)source.Width;
                    double scaleY = height / (double)source.Height;
                    int startX = (int)Math.Round(-source.Left * scaleX);
                    int startY = (int)Math.Round(-source.Top * scaleY);
                    int sizeX = Math.Max(1, (int)Math.Round(pageWidth * scaleX));
                    int sizeY = Math.Max(1, (int)Math.Round(pageHeight * scaleY));
                    PdfiumNative.FPDF_RenderPageBitmap(target, page, startX, startY, sizeX, sizeY, 0,
                        PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);
                }
                finally
                {
                    if (target != IntPtr.Zero)
                        PdfiumNative.FPDFBitmap_Destroy(target);
                    PdfiumNative.FPDF_ClosePage(page);
                }
            }
            bitmap.NotifyPixelsChanged();
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>A page rendered as in <see cref="RenderRegion"/>, encoded as PNG.</summary>
    public byte[] RenderPng(int pageIndex, SKRect? sourcePoints, int width, int height)
    {
        using var bitmap = RenderRegion(pageIndex, sourcePoints, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A page's text, in reading order as PDFium extracts it.</summary>
    public unsafe string GetPageText(int pageIndex)
    {
        lock (PdfiumNative.Sync)
        {
            var page = LoadPage(pageIndex);
            var text = PdfiumNative.FPDFText_LoadPage(page);
            try
            {
                if (text == IntPtr.Zero)
                    return string.Empty;
                int count = PdfiumNative.FPDFText_CountChars(text);
                if (count <= 0)
                    return string.Empty;
                var buffer = new ushort[count + 1];
                int written;
                fixed (ushort* p = buffer)
                    written = PdfiumNative.FPDFText_GetText(text, 0, count, p);
                // The count includes the terminating NUL.
                int length = Math.Max(0, Math.Min(written - 1, count));
                fixed (ushort* p = buffer)
                    return new string((char*)p, 0, length);
            }
            finally
            {
                if (text != IntPtr.Zero)
                    PdfiumNative.FPDFText_ClosePage(text);
                PdfiumNative.FPDF_ClosePage(page);
            }
        }
    }

    /// <summary>The text of every page, separated by blank lines.</summary>
    public string GetText()
    {
        var pages = new string[PageCount];
        for (int i = 0; i < pages.Length; i++)
            pages[i] = GetPageText(i);
        return string.Join("\n\n", pages);
    }

    /// <summary>Loads a page; call with the lock held. The caller closes it.</summary>
    private IntPtr LoadPage(int pageIndex)
    {
        ThrowIfDisposed();
        if (pageIndex < 0 || pageIndex >= _pageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), $"The PDF has {_pageCount} pages.");
        var page = PdfiumNative.FPDF_LoadPage(_document, pageIndex);
        if (page == IntPtr.Zero)
            throw LastError($"Page {pageIndex} could not be read");
        return page;
    }

    private static PdfiumException LastError(string what)
    {
        var code = PdfiumNative.FPDF_GetLastError();
        var (error, reason) = code switch
        {
            PdfiumNative.FPDF_ERR_FILE => (PdfiumError.File, "the file could not be read"),
            PdfiumNative.FPDF_ERR_FORMAT => (PdfiumError.Format, "it is not a PDF or is damaged"),
            PdfiumNative.FPDF_ERR_PASSWORD => (PdfiumError.Password, "the password is missing or wrong"),
            PdfiumNative.FPDF_ERR_SECURITY => (PdfiumError.Security, "its security handler is not supported"),
            PdfiumNative.FPDF_ERR_PAGE => (PdfiumError.Page, "the page is missing or damaged"),
            _ => (PdfiumError.Unknown, $"PDFium error {code}"),
        };
        return new PdfiumException(error, $"{what}: {reason}.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_document == IntPtr.Zero, this);

    public void Dispose()
    {
        lock (PdfiumNative.Sync)
        {
            if (_document != IntPtr.Zero)
            {
                PdfiumNative.FPDF_CloseDocument(_document);
                _document = IntPtr.Zero;
            }
        }
        if (_data != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_data);
            _data = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~PdfiumDocument() => Dispose();
}
