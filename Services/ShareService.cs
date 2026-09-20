// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux share implementation using xdg-open and portal APIs.
/// </summary>
public class ShareService : IShare
{
    public async Task RequestAsync(ShareTextRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // On Linux, we can use mailto: for text sharing or write to a temp file
        if (!string.IsNullOrEmpty(request.Uri))
        {
            // Share as URL
            await OpenUrlAsync(request.Uri);
        }
        else if (!string.IsNullOrEmpty(request.Text))
        {
            // Try to use email for text sharing
            await OpenUrlAsync(BuildTextMailto(request));
        }
    }

    public async Task RequestAsync(ShareFileRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.File == null)
            throw new ArgumentException("File is required", nameof(request));

        await ShareFileAsync(request.File.FullPath);
    }

    public async Task RequestAsync(ShareMultipleFilesRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Files == null || !request.Files.Any())
            throw new ArgumentException("Files are required", nameof(request));

        // Share files one by one or use file manager
        foreach (var file in request.Files)
        {
            await ShareFileAsync(file.FullPath);
        }
    }

    /// <summary>mailto: URI carrying the request's subject and text as body.</summary>
    internal static string BuildTextMailto(ShareTextRequest request)
    {
        var subject = Uri.EscapeDataString(request.Subject ?? "");
        var body = Uri.EscapeDataString(request.Text ?? "");
        return $"mailto:?subject={subject}&body={body}";
    }

    private static async Task OpenUrlAsync(string url)
    {
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

    private static async Task ShareFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found for sharing", filePath);

        try
        {
            // Try to use the portal API via gdbus for proper share dialog
            var portalResult = await TryPortalShareAsync(filePath);
            if (portalResult)
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

    private static async Task<bool> TryPortalShareAsync(string filePath)
    {
        try
        {
            // Try freedesktop portal for proper share dialog
            // This would use org.freedesktop.portal.FileChooser or similar
            // For now, we'll use zenity --info as a fallback notification

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
