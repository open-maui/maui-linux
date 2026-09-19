// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Xunit;
using DragPayload = Microsoft.Maui.Platform.Linux.Services.DragPayload;
using DragDropService = Microsoft.Maui.Platform.Linux.Services.DragDropService;
using ResolvedImage = Microsoft.Maui.Platform.Linux.Services.ResolvedImage;

namespace Microsoft.Maui.Platform.Tests;

public class DragPayloadTests
{
    // ---- uri-list encoding (RFC 2483) ----

    [Fact]
    public void BuildUriList_EncodesFileUris_CrlfTerminated()
    {
        var payload = DragPayload.FromFiles("/home/user/a.txt", "/tmp/b.png");

        var uriList = payload.BuildUriList();

        uriList.Should().Be("file:///home/user/a.txt\r\nfile:///tmp/b.png\r\n");
    }

    [Fact]
    public void BuildUriList_PercentEncodesSpacesAndUnicode_PerSegment()
    {
        var payload = DragPayload.FromFiles("/home/My Docs/ré sumé.pdf");

        var uriList = payload.BuildUriList();

        // Spaces and non-ASCII are percent-encoded; the path separators are not.
        uriList.Should().Be("file:///home/My%20Docs/r%C3%A9%20sum%C3%A9.pdf\r\n");
    }

    [Fact]
    public void BuildUriList_NoFiles_ReturnsNull()
    {
        DragPayload.FromText("hello").BuildUriList().Should().BeNull();
    }

    [Fact]
    public void PathToFileUri_PreservesLeadingSlash()
    {
        DragPayload.PathToFileUri("/a/b").Should().Be("file:///a/b");
    }

    // ---- MIME advertisement ordering ----

    [Fact]
    public void MimeTypes_RichestFirst_ImageThenUriListThenText()
    {
        var payload = new DragPayload
        {
            Text = "t",
            FilePaths = new[] { "/x" },
            ImageBytes = new byte[] { 1 },
            ImageMime = "image/png",
        };

        payload.MimeTypes.Should().ContainInOrder(
            "image/png", "text/uri-list",
            "text/plain;charset=utf-8", "text/plain", "UTF8_STRING", "STRING", "TEXT");
    }

    [Fact]
    public void MimeTypes_OnlyBackedTypesAdvertised()
    {
        DragPayload.FromFiles("/x").MimeTypes.Should().Equal("text/uri-list");
    }

    // ---- mime -> bytes resolution ----

    [Fact]
    public void GetBytes_TextVariants_AllReturnUtf8Text()
    {
        var payload = DragPayload.FromText("héllo");
        var expected = Encoding.UTF8.GetBytes("héllo");

        payload.GetBytes("text/plain;charset=utf-8").Should().Equal(expected);
        payload.GetBytes("text/plain").Should().Equal(expected);
        payload.GetBytes("UTF8_STRING").Should().Equal(expected);
        payload.GetBytes("STRING").Should().Equal(expected);
        payload.GetBytes("TEXT").Should().Equal(expected);
    }

    [Fact]
    public void GetBytes_UriList_ReturnsEncodedList()
    {
        var payload = DragPayload.FromFiles("/a b");

        var bytes = payload.GetBytes("text/uri-list");

        Encoding.UTF8.GetString(bytes!).Should().Be("file:///a%20b\r\n");
    }

    [Fact]
    public void GetBytes_ImageMime_ReturnsRawBytes()
    {
        var raw = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var payload = DragPayload.FromImage(raw, "image/png");

        payload.GetBytes("image/png").Should().BeSameAs(raw);
        // Case-insensitive on the MIME.
        payload.GetBytes("IMAGE/PNG").Should().BeSameAs(raw);
    }

    [Fact]
    public void GetBytes_UnsupportedMime_ReturnsNull()
    {
        DragPayload.FromText("t").GetBytes("image/png").Should().BeNull();
        DragPayload.FromImage(new byte[] { 1 }, "image/png").GetBytes("text/plain").Should().BeNull();
        DragPayload.FromText("t").GetBytes("").Should().BeNull();
    }

    [Fact]
    public void IsEmpty_TrueOnlyWhenNothingBacked()
    {
        new DragPayload().IsEmpty.Should().BeTrue();
        DragPayload.FromText("t").IsEmpty.Should().BeFalse();
        DragPayload.FromFiles("/x").IsEmpty.Should().BeFalse();
        DragPayload.FromImage(new byte[] { 1 }, "image/png").IsEmpty.Should().BeFalse();
        // Image with empty bytes or no mime does not count.
        new DragPayload { ImageBytes = new byte[0], ImageMime = "image/png" }.IsEmpty.Should().BeTrue();
        new DragPayload { ImageBytes = new byte[] { 1 }, ImageMime = null }.IsEmpty.Should().BeTrue();
    }

    // ---- outgoing INCR chunking math ----

    [Fact]
    public void NextIncrChunk_SplitsIntoFullChunksThenTerminator()
    {
        const int chunk = 4;
        var steps = new List<(int len, int off, bool term)>();

        int offset = 0;
        for (int i = 0; i < 10; i++)
        {
            var step = DragDropService.NextIncrChunk(10, offset, chunk);
            steps.Add(step);
            offset = step.newOffset;
            if (step.terminator) break;
        }

        steps.Should().Equal(
            (4, 4, false),   // bytes 0..3
            (4, 8, false),   // bytes 4..7
            (2, 10, false),  // bytes 8..9 (partial final data chunk)
            (0, 10, true));  // zero-length terminator
    }

    [Fact]
    public void NextIncrChunk_ExactMultiple_EndsWithTerminatorOnly()
    {
        DragDropService.NextIncrChunk(8, 0, 4).Should().Be((4, 4, false));
        DragDropService.NextIncrChunk(8, 4, 4).Should().Be((4, 8, false));
        DragDropService.NextIncrChunk(8, 8, 4).Should().Be((0, 8, true));
    }

    [Fact]
    public void NextIncrChunk_EmptyData_ImmediateTerminator()
    {
        DragDropService.NextIncrChunk(0, 0, 4).Should().Be((0, 0, true));
    }

    // ---- image format sniffing ----

    private static readonly byte[] PngHeader =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    private static readonly byte[] JpegHeader =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, "image/gif")]
    [InlineData(new byte[] { (byte)'B', (byte)'M', 0x3A, 0x00 }, "image/bmp")]
    public void SniffImageMime_RecognizesMagicNumbers(byte[] header, string expected)
    {
        DragPayload.SniffImageMime(header).Should().Be(expected);
    }

    [Fact]
    public void SniffImageMime_WebP_RequiresRiffAndWebpTags()
    {
        var webp = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P',
        };
        DragPayload.SniffImageMime(webp).Should().Be("image/webp");
    }

    [Fact]
    public void SniffImageMime_UnknownOrShort_ReturnsNull()
    {
        DragPayload.SniffImageMime(null).Should().BeNull();
        DragPayload.SniffImageMime(new byte[0]).Should().BeNull();
        DragPayload.SniffImageMime(new byte[] { 0x00, 0x01, 0x02, 0x03 }).Should().BeNull();
        // Truncated PNG magic must not match.
        DragPayload.SniffImageMime(new byte[] { 0x89, 0x50, 0x4E }).Should().BeNull();
    }

    // ---- lazy (pending) image resolution ----

    [Fact]
    public void PendingImage_AdvertisesOptimisticMimes_WhenFormatUnknown()
    {
        var payload = new DragPayload
        {
            PendingImage = new TaskCompletionSource<ResolvedImage?>().Task,
        };

        payload.IsEmpty.Should().BeFalse();
        payload.MimeTypes.Should().Equal("image/png", "image/jpeg");
    }

    [Fact]
    public void GetBytes_CompletedPendingImage_ServedForSniffedMimeOnly()
    {
        var payload = new DragPayload
        {
            PendingImage = Task.FromResult<ResolvedImage?>(new ResolvedImage(PngHeader, "image/png")),
        };

        // Sync path serves an already-completed task, gated on the sniffed MIME.
        payload.GetBytes("image/png").Should().BeSameAs(PngHeader);
        payload.GetBytes("image/jpeg").Should().BeNull(); // advertised, but not what resolved
    }

    [Fact]
    public void GetBytes_IncompletePendingImage_NeverBlocks_ReturnsNull()
    {
        var payload = new DragPayload
        {
            PendingImage = new TaskCompletionSource<ResolvedImage?>().Task,
        };

        payload.GetBytes("image/png").Should().BeNull();
    }

    [Fact]
    public async Task GetBytesAsync_AwaitsPendingImage()
    {
        var tcs = new TaskCompletionSource<ResolvedImage?>();
        var payload = new DragPayload { PendingImage = tcs.Task };

        var resolving = payload.GetBytesAsync("image/jpeg", TimeSpan.FromSeconds(5));
        tcs.SetResult(new ResolvedImage(JpegHeader, "image/jpeg"));

        (await resolving).Should().BeSameAs(JpegHeader);
    }

    [Fact]
    public async Task GetBytesAsync_Timeout_ReturnsNull()
    {
        var payload = new DragPayload
        {
            PendingImage = new TaskCompletionSource<ResolvedImage?>().Task, // never completes
        };

        var bytes = await payload.GetBytesAsync("image/png", TimeSpan.FromMilliseconds(50));

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task GetBytesAsync_FaultedTask_ReturnsNull_WithoutThrowing()
    {
        var payload = new DragPayload
        {
            PendingImage = Task.FromException<ResolvedImage?>(new InvalidOperationException("decode failed")),
        };

        var bytes = await payload.GetBytesAsync("image/png", TimeSpan.FromSeconds(1));

        bytes.Should().BeNull();
    }

    [Fact]
    public async Task GetBytesAsync_NullResolution_ReturnsNull()
    {
        var payload = new DragPayload { PendingImage = Task.FromResult<ResolvedImage?>(null) };

        (await payload.GetBytesAsync("image/png", TimeSpan.FromSeconds(1))).Should().BeNull();
    }

    [Fact]
    public async Task GetBytesAsync_TextMime_UnaffectedByPendingImage()
    {
        // A never-completing image must not stall or break the payload's other
        // MIMEs — they resolve synchronously.
        var payload = new DragPayload
        {
            Text = "hello",
            PendingImage = new TaskCompletionSource<ResolvedImage?>().Task,
        };

        var bytes = await payload.GetBytesAsync("text/plain", TimeSpan.FromSeconds(1));

        Encoding.UTF8.GetString(bytes!).Should().Be("hello");
    }

    [Fact]
    public void PendingImage_WithDeclaredMime_AdvertisesOnlyThat()
    {
        var payload = new DragPayload
        {
            ImageMime = "image/png",
            PendingImage = new TaskCompletionSource<ResolvedImage?>().Task,
        };

        payload.MimeTypes.Should().Equal("image/png");
    }
}
