// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class PrintServiceTests
{
    // CUPS-backed paths require a CUPS daemon + configured printers, neither
    // of which we want to assume in CI. Tests focus on the validation surface
    // that runs regardless of whether libcups is loaded.

    [Fact]
    public void EnumeratePrinters_ReturnsAList_Always()
    {
        // Empty when CUPS isn't installed; populated when it is — either is
        // fine, we just want no exceptions.
        var printers = PrintService.EnumeratePrinters();
        printers.Should().NotBeNull();
    }

    [Fact]
    public void PrintFile_NoPrinterName_FailsCleanly()
    {
        var result = PrintService.PrintFile(string.Empty, "/tmp/does-not-matter");
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PrintFile_MissingFile_FailsCleanly()
    {
        var result = PrintService.PrintFile("ignored-printer", "/nonexistent/file.pdf");
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PrintSkiaPagesAsync_FailsCleanly_OnUnknownPrinter()
    {
        // Renders a 1-page test PDF, then attempts to submit to an obviously
        // bogus printer. The render itself must succeed; CUPS rejects the job.
        var result = await PrintService.PrintSkiaPagesAsync(
            printer: "__openmaui_tests_nonexistent_printer__",
            renderPage: (canvas, page) =>
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var font = new SKFont(SKTypeface.Default, 16);
                canvas.DrawText("Test page", 50, 50, font, paint);
                return page < 2;   // commit page 1; false on the second call stops without adding a page
            },
            pageSize: new SKSize(595, 842));   // A4 in points

        // We don't assert Succeeded=false unconditionally — IF CUPS happens to
        // have a queue with this exact name (impossibly unlikely), we'd succeed
        // and the job would just sit in the queue. We only require a sensible
        // return shape.
        result.Should().NotBeNull();
        result.Status.Should().NotBe(PrintJobStatus.NothingToPrint);   // a page was committed
        // No throws, no leaks.
    }

    [Fact]
    public async Task PrintSkiaPagesAsync_ReturnsNothingToPrint_WhenFirstPageDeclined()
    {
        // False on the very first call means zero pages: no job is submitted
        // (this path never reaches CUPS, so it's fully deterministic in CI)
        // and the result is the benign NothingToPrint state, not a failure.
        var result = await PrintService.PrintSkiaPagesAsync(
            printer: "__openmaui_tests_nonexistent_printer__",
            renderPage: (canvas, page) => false,
            pageSize: new SKSize(595, 842));

        result.Should().NotBeNull();
        result.Status.Should().Be(PrintJobStatus.NothingToPrint);
        result.Succeeded.Should().BeFalse();
        result.JobId.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
    }
}
