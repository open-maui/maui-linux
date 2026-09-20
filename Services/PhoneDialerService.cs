// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentNullException(nameof(number));

        var psi = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(BuildTelUri(number));
        ExternalProcess.TryStart(psi);
    }

    /// <summary>RFC 3966 tel: URI for a number.</summary>
    internal static string BuildTelUri(string number) => $"tel:{NormalizeNumber(number)}";

    /// <summary>
    /// Strips whitespace and keeps the characters RFC 3966 allows in a
    /// telephone-subscriber (digits, '+', visual separators, '*', '#'); anything
    /// else is percent-encoded. '+' must stay literal for international numbers.
    /// </summary>
    internal static string NormalizeNumber(string number)
    {
        var sb = new System.Text.StringBuilder(number.Length);
        foreach (var c in number)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (char.IsAsciiDigit(c) || c is '+' or '-' or '.' or '(' or ')' or '*' or '#')
                sb.Append(c);
            else
                sb.Append(Uri.EscapeDataString(c.ToString()));
        }
        return sb.ToString();
    }
}
