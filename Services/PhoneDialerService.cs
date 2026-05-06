// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux phone dialer. Uses tel: URI handler on supported devices (PinePhone, Librem 5).
/// </summary>
public class PhoneDialerService : IPhoneDialer
{
    public bool IsSupported => true; // Most Linux desktops handle tel: via apps

    public void Open(string number)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"tel:{Uri.EscapeDataString(number)}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }
}
