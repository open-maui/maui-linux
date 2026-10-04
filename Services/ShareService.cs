// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux share implementation. Inside a sandbox (or with
/// OPENMAUI_PORTALS=prefer) it uses the xdg-desktop-portal OpenURI interface:
/// URIs through OpenURI, files through OpenFile with the "Open with" chooser
/// (there is no dedicated share portal). Otherwise, and whenever the portal is
/// unavailable, xdg-open and zenity as before.
/// </summary>
public class ShareService : IShare
{
    private readonly IDesktopPortal _portal;

    public ShareService()
        : this(DesktopPortal.Current)
    {
    }

    internal ShareService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    // The request checks are MAUI's ShareImplementation's, thrown before anything starts:
    // ArgumentNullException for a null request, ArgumentException for a request with nothing
    // to share (no text and no URI, no file, an empty file list or a null file in it).

    public Task RequestAsync(ShareTextRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrEmpty(request.Text) && string.IsNullOrEmpty(request.Uri))
            throw new ArgumentException($"Both the {nameof(request.Text)} and {nameof(request.Uri)} are invalid. Make sure to include at least one of them in the request.");

        return ShareTextAsync(request);
    }

    private async Task ShareTextAsync(ShareTextRequest request)
    {
        // On Linux, we can use mailto: for text sharing or write to a temp file
        if (!string.IsNullOrEmpty(request.Uri))
        {
            // Share as URL
            await OpenUrlAsync(request.Uri);
        }
        else
        {
            // Try to use email for text sharing
            await OpenUrlAsync(BuildTextMailto(request));
        }
    }

    public Task RequestAsync(ShareFileRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.File == null)
            throw new ArgumentException(FileNullException(nameof(request.File)));

        return ShareFileAsync(request.File.FullPath);
    }

    public Task RequestAsync(ShareMultipleFilesRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (!(request.Files?.Count > 0))
            throw new ArgumentException(FileNullException(nameof(request.Files)));

        if (request.Files.Any(file => file == null))
            throw new ArgumentException(FileNullException(nameof(request.Files)));

        return ShareFilesAsync(request.Files);
    }

    private async Task ShareFilesAsync(IEnumerable<ShareFile> files)
    {
        // Share files one by one or use file manager
        foreach (var file in files)
        {
            await ShareFileAsync(file.FullPath);
        }
    }

    private static string FileNullException(string file)
        => $"The {file} parameter in the request files is invalid";

    /// <summary>mailto: URI carrying the request's subject and text as body.</summary>
    internal static string BuildTextMailto(ShareTextRequest request)
    {
        var subject = Uri.EscapeDataString(request.Subject ?? "");
        var body = Uri.EscapeDataString(request.Text ?? "");
        return $"mailto:?subject={subject}&body={body}";
    }

    private async Task OpenUrlAsync(string url)
    {
        if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred)
            && !(await new PortalLauncher(_portal).OpenUriAsync(url).ConfigureAwait(false)).ShouldFallBack())
            return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(url);
            await ExternalProcess.RunAsync(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open URL for sharing", ex);
        }
    }

    private async Task ShareFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found for sharing", filePath);

        try
        {
            // Portal "Open with" chooser for the file, when the policy allows it.
            if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred)
                && !(await new PortalLauncher(_portal).OpenFileAsync(filePath, ask: true).ConfigureAwait(false)).ShouldFallBack())
                return;

            // Otherwise tell the user where the file is (zenity)
            var noticeShown = await TryZenityShareNoticeAsync(filePath);
            if (noticeShown)
                return;

            // Fall back to opening with default file manager
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(Path.GetDirectoryName(filePath) ?? filePath);
            await ExternalProcess.RunAsync(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to share file", ex);
        }
    }

    private static async Task<bool> TryZenityShareNoticeAsync(string filePath)
    {
        try
        {
            // No share dialog without a portal: show where the file is.

            var startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--info");
            startInfo.ArgumentList.Add($"--text=File ready to share:\\n{Path.GetFileName(filePath)}\\n\\nPath: {filePath}");
            startInfo.ArgumentList.Add("--title=Share File");

            return await ExternalProcess.RunAsync(startInfo) != null;
        }
        catch
        {
            return false;
        }
    }
}
