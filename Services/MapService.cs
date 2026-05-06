// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux map service. Opens locations in the default browser using OpenStreetMap.
/// </summary>
public class MapService : IMap
{
    public async Task OpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        var url = $"https://www.openstreetmap.org/?mlat={latitude}&mlon={longitude}#map=15/{latitude}/{longitude}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

    public async Task OpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        var query = Uri.EscapeDataString(
            $"{placemark.Thoroughfare} {placemark.Locality} {placemark.AdminArea} {placemark.CountryName}".Trim());
        var url = $"https://www.openstreetmap.org/search?query={query}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

    public async Task<bool> TryOpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        await OpenAsync(latitude, longitude, options);
        return true;
    }

    public async Task<bool> TryOpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        await OpenAsync(placemark, options);
        return true;
    }
}
