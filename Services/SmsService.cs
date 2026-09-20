// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux SMS service. Uses sms: URI handler on supported devices.
/// </summary>
public class SmsService : ISms
{
    public bool IsComposeSupported => true;

    public async Task ComposeAsync(SmsMessage? message)
    {
        if (message == null) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(BuildSmsUri(message));
            await ExternalProcess.RunAsync(psi);
        }
        catch { }
    }

    /// <summary>
    /// RFC 5724 sms: URI. Recipients are comma-separated telephone numbers
    /// (kept literal so the leading '+' survives); the body, when present, is
    /// percent-encoded in the query.
    /// </summary>
    internal static string BuildSmsUri(SmsMessage message)
    {
        var recipients = string.Join(",",
            (message.Recipients ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(PhoneDialerService.NormalizeNumber));
        var uri = $"sms:{recipients}";
        if (!string.IsNullOrEmpty(message.Body))
            uri += $"?body={Uri.EscapeDataString(message.Body)}";
        return uri;
    }
}
