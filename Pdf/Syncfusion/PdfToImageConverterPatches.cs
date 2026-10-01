// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Pdf.Syncfusion;

/// <summary>
/// Syncfusion's <c>PdfToImageConverter</c> (behind SfPdfViewer) renders through the platform's
/// PDF engine, and its platform-neutral build, the one a Linux app runs, has none: page count,
/// rendering and disposal all throw NotImplementedException, so SfPdfViewer showed nothing.
/// Patched to render with PDFium, with the Windows build's sizing: pages at 96 DPI (points × 4/3)
/// unless a size is given, a crop rectangle in points, times the scale factor, as PNG. No
/// compile-time reference: the converter is optional.
/// </summary>
internal static class PdfToImageConverterPatches
{
    private const string ConverterType = "Syncfusion.Maui.PdfToImageConverter.PdfToImageConverter, Syncfusion.Maui.PdfToImageConverter";
    private const double PixelsPerPoint = 96.0 / 72.0;

    private static int s_installed;
    private static FieldInfo? s_password;
    private static readonly ConditionalWeakTable<object, PdfiumDocument> s_documents = new();

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var converter = Type.GetType(ConverterType);
            if (converter == null)
                return; // the converter is not part of the app

            // Only the platform-neutral build lacks a renderer; a platform one is left alone.
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var export = converter.GetMethod("ExportAsImage", Instance);
            if (export == null || !IsStub(export))
                return;

            s_password = converter.GetField("m_password", Instance);
            var harmony = new Harmony("com.openmaui.pdf.pdftoimageconverter");
            Patch(harmony, converter, "ExportAsImage", nameof(ExportAsImage_Prefix));
            Patch(harmony, converter, "ExportAsImageAsync", nameof(ExportAsImageAsync_Prefix));
            Patch(harmony, converter, "GetPageCount", nameof(GetPageCount_Prefix));
            Patch(harmony, converter, "DisposeDocument", nameof(DisposeDocument_Prefix));
            Patch(harmony, converter, "ResetDisposedState", nameof(ResetDisposedState_Prefix));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Pdf", "Patching Syncfusion's PdfToImageConverter failed", ex);
        }
    }

    private static void Patch(Harmony harmony, Type converter, string method, string prefix)
    {
        var target = converter.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        if (target == null)
        {
            DiagnosticLog.Warn("Pdf", $"PdfToImageConverter.{method} not found; this Syncfusion version is not covered");
            return;
        }
        harmony.Patch(target, new HarmonyMethod(typeof(PdfToImageConverterPatches).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    /// <summary>True when the method's body only throws NotImplementedException (the platform-neutral stub).</summary>
    private static bool IsStub(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        // newobj NotImplementedException::.ctor (0x73 + token), throw (0x7A): 6 bytes, nothing else.
        if (il == null || il.Length != 6 || il[0] != 0x73 || il[5] != 0x7A)
            return false;
        try
        {
            var ctor = method.Module.ResolveMethod(BitConverter.ToInt32(il, 1));
            return ctor?.DeclaringType == typeof(NotImplementedException);
        }
        catch
        {
            return false;
        }
    }

    private static PdfiumDocument? DocumentFor(object converter, Stream? input)
    {
        if (s_documents.TryGetValue(converter, out var open))
            return open;
        if (input == null)
            return null;
        try
        {
            byte[] bytes;
            long position = input.CanSeek ? input.Position : 0;
            if (input.CanSeek)
                input.Position = 0;
            using (var copy = new MemoryStream())
            {
                input.CopyTo(copy);
                bytes = copy.ToArray();
            }
            if (input.CanSeek)
                input.Position = position;

            var document = PdfiumDocument.Open(bytes, s_password?.GetValue(converter) as string);
            s_documents.AddOrUpdate(converter, document);
            return document;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Pdf", "Opening the PDF for PdfToImageConverter failed", ex);
            return null;
        }
    }

    private static Stream[]? Export(object converter, Stream inputStream, int? pageIndex, int? endIndex, Size? size, Rect? cropRect, float scaleFactor, CancellationToken cancellationToken)
    {
        var document = DocumentFor(converter, inputStream);
        if (document == null || document.PageCount == 0)
            return null;

        int pageCount = document.PageCount;
        List<int> pages = pageIndex is not int first ? Enumerable.Range(0, pageCount).ToList()
            : endIndex is int last && first <= last ? Enumerable.Range(first, last - first + 1).ToList()
            : new List<int> { first };

        var result = new Stream[pages.Count];
        for (int i = 0; i < pages.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var done in result)
                    done?.Dispose();
                return null;
            }
            var stream = new MemoryStream();
            int page = pages[i];
            if (page >= 0 && page < pageCount)
            {
                var points = document.GetPageSize(page);
                double width = points.Width * PixelsPerPoint;
                double height = points.Height * PixelsPerPoint;
                bool hasSize = size is Size s && s != Size.Zero;
                if (hasSize)
                {
                    width = size!.Value.Width;
                    height = size.Value.Height;
                }

                SKRect? source = null;
                if (cropRect is Rect crop && crop != Rect.Zero)
                {
                    source = new SKRect((float)crop.X, (float)crop.Y, (float)(crop.X + crop.Width), (float)(crop.Y + crop.Height));
                    if (!hasSize)
                    {
                        width = crop.Width * PixelsPerPoint;
                        height = crop.Height * PixelsPerPoint;
                    }
                }

                int pixelWidth = Math.Max(1, (int)(width * scaleFactor));
                int pixelHeight = Math.Max(1, (int)(height * scaleFactor));
                var png = document.RenderPng(page, source, pixelWidth, pixelHeight);
                stream.Write(png, 0, png.Length);
                stream.Position = 0;
            }
            result[i] = stream;
        }
        return result;
    }

    private static bool ExportAsImage_Prefix(object __instance, Stream inputStream, int? pageIndex, int? endIndex, Size? size, Rect? cropRect, float scaleFactor, ref Stream[]? __result)
    {
        try
        {
            __result = Export(__instance, inputStream, pageIndex, endIndex, size, cropRect, scaleFactor, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Pdf", "Rendering PDF pages failed", ex);
            __result = null;
        }
        return false;
    }

    private static bool ExportAsImageAsync_Prefix(object __instance, Stream inputStream, int? pageIndex, int? endIndex, Size? size, Rect? cropRect, float scaleFactor, CancellationToken cancellationToken, ref Task<Stream[]?> __result)
    {
        __result = Task.Run(() =>
        {
            try
            {
                return Export(__instance, inputStream, pageIndex, endIndex, size, cropRect, scaleFactor, cancellationToken);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Pdf", "Rendering PDF pages failed", ex);
                return null;
            }
        }, cancellationToken);
        return false;
    }

    private static bool GetPageCount_Prefix(object __instance, Stream inputStream, ref int __result)
    {
        __result = DocumentFor(__instance, inputStream)?.PageCount ?? 0;
        return false;
    }

    private static bool DisposeDocument_Prefix(object __instance)
    {
        if (s_documents.TryGetValue(__instance, out var document))
        {
            s_documents.Remove(__instance);
            document.Dispose();
        }
        return false;
    }

    private static bool ResetDisposedState_Prefix() => false;
}
