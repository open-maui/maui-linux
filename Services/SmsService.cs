// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var recipients = string.Join(",", message.Recipients ?? new List<string>());
            var body = Uri.EscapeDataString(message.Body ?? "");
            var uri = $"sms:{recipients}?body={body}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = uri,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }
}
