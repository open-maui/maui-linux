// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux email implementation using mailto: URI.
/// </summary>
public class EmailService : IEmail
{
    public bool IsComposeSupported => true;

    public async Task ComposeAsync()
    {
        await ComposeAsync(new EmailMessage());
    }

    public async Task ComposeAsync(string subject, string body, params string[] to)
    {
        var message = new EmailMessage
        {
            Subject = subject,
            Body = body
        };

        if (to != null && to.Length > 0)
        {
            message.To = new List<string>(to);
        }

        await ComposeAsync(message);
    }

    public async Task ComposeAsync(EmailMessage? message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var mailto = BuildMailtoUri(message);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{mailto}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open email client", ex);
        }
    }

    private static string BuildMailtoUri(EmailMessage? message)
    {
        var sb = new StringBuilder("mailto:");

        // Add recipients
        if (message.To?.Count > 0)
        {
            sb.Append(string.Join(",", message.To.Select(Uri.EscapeDataString)));
        }

        var queryParams = new List<string>();

        // Add subject
        if (!string.IsNullOrEmpty(message.Subject))
        {
            queryParams.Add($"subject={Uri.EscapeDataString(message.Subject)}");
        }

        // Add body
        if (!string.IsNullOrEmpty(message.Body))
        {
            queryParams.Add($"body={Uri.EscapeDataString(message.Body)}");
        }

        // Add CC
        if (message.Cc?.Count > 0)
        {
            queryParams.Add($"cc={string.Join(",", message.Cc.Select(Uri.EscapeDataString))}");
        }

        // Add BCC
        if (message.Bcc?.Count > 0)
        {
            queryParams.Add($"bcc={string.Join(",", message.Bcc.Select(Uri.EscapeDataString))}");
        }

        if (queryParams.Count > 0)
        {
            sb.Append('?');
            sb.Append(string.Join("&", queryParams));
        }

        return sb.ToString();
    }
}
