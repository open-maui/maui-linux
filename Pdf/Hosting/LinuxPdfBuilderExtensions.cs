// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Pdf.Syncfusion;

namespace Microsoft.Maui.Platform.Linux.Pdf.Hosting;

/// <summary>PDF support for OpenMaui apps on Linux.</summary>
public static class LinuxPdfBuilderExtensions
{
    /// <summary>
    /// Renders PDFs with PDFium where a library's platform-neutral build has no renderer:
    /// Syncfusion's PdfToImageConverter, and so SfPdfViewer, when the app uses them.
    /// <see cref="PdfiumDocument"/> works without it. A no-op off Linux.
    /// </summary>
    public static MauiAppBuilder UseLinuxPdf(this MauiAppBuilder builder)
    {
        if (OperatingSystem.IsLinux())
            PdfToImageConverterPatches.Install();
        return builder;
    }
}
