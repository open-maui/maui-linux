// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Pdf.Native;

/// <summary>
/// The part of PDFium's C API (fpdfview.h, fpdf_text.h) OpenMaui uses: open a document from
/// memory, read its pages' sizes and text, and render a page into a caller-owned bitmap.
/// PDFium is not thread-safe; every call goes through <see cref="Sync"/>.
/// </summary>
internal static partial class PdfiumNative
{
    private const string Lib = "pdfium";

    /// <summary>One lock for every PDFium call in the process.</summary>
    internal static readonly Lock Sync = new();

    // FPDFBitmap formats
    internal const int FPDFBitmap_BGRA = 4;

    // FPDF_RenderPageBitmap flags
    internal const int FPDF_ANNOT = 0x01;
    internal const int FPDF_LCD_TEXT = 0x02;
    internal const int FPDF_PRINTING = 0x800;

    // FPDF_GetLastError codes
    internal const uint FPDF_ERR_SUCCESS = 0;
    internal const uint FPDF_ERR_UNKNOWN = 1;
    internal const uint FPDF_ERR_FILE = 2;
    internal const uint FPDF_ERR_FORMAT = 3;
    internal const uint FPDF_ERR_PASSWORD = 4;
    internal const uint FPDF_ERR_SECURITY = 5;
    internal const uint FPDF_ERR_PAGE = 6;

    [LibraryImport(Lib)]
    internal static partial void FPDF_InitLibrary();

    [LibraryImport(Lib)]
    internal static partial IntPtr FPDF_LoadMemDocument64(IntPtr dataBuf, nuint size, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [LibraryImport(Lib)]
    internal static partial uint FPDF_GetLastError();

    [LibraryImport(Lib)]
    internal static partial int FPDF_GetPageCount(IntPtr document);

    [LibraryImport(Lib)]
    internal static partial void FPDF_CloseDocument(IntPtr document);

    [LibraryImport(Lib)]
    internal static partial IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [LibraryImport(Lib)]
    internal static partial void FPDF_ClosePage(IntPtr page);

    [LibraryImport(Lib)]
    internal static partial float FPDF_GetPageWidthF(IntPtr page);

    [LibraryImport(Lib)]
    internal static partial float FPDF_GetPageHeightF(IntPtr page);

    [LibraryImport(Lib)]
    internal static partial IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    [LibraryImport(Lib)]
    internal static partial int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(Lib)]
    internal static partial void FPDFBitmap_Destroy(IntPtr bitmap);

    [LibraryImport(Lib)]
    internal static partial void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(Lib)]
    internal static partial IntPtr FPDFText_LoadPage(IntPtr page);

    [LibraryImport(Lib)]
    internal static partial void FPDFText_ClosePage(IntPtr textPage);

    [LibraryImport(Lib)]
    internal static partial int FPDFText_CountChars(IntPtr textPage);

    [LibraryImport(Lib)]
    internal static unsafe partial int FPDFText_GetText(IntPtr textPage, int startIndex, int count, ushort* result);

    private static bool s_initialized;

    /// <summary>Initializes PDFium once; call with <see cref="Sync"/> held.</summary>
    internal static void EnsureInitialized()
    {
        if (s_initialized)
            return;
        FPDF_InitLibrary();
        s_initialized = true;
    }
}
