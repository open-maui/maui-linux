// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
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
        await TryOpenAsync(latitude, longitude, options);
    }

    public async Task OpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        await TryOpenAsync(placemark, options);
    }

    public Task<bool> TryOpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        return Task.Run(() => Launch(BuildUrl(latitude, longitude, options)));
    }

    public Task<bool> TryOpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        if (placemark == null)
            throw new ArgumentNullException(nameof(placemark));

        return Task.Run(() => Launch(BuildUrl(placemark, options)));
    }

    /// <summary>
    /// OpenStreetMap URL for a coordinate. Uses invariant formatting so the
    /// decimal separator is always '.', and switches to a directions URL when
    /// the caller asked for navigation.
    /// </summary>
    internal static string BuildUrl(double latitude, double longitude, MapLaunchOptions? options)
    {
        var lat = latitude.ToString("R", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("R", CultureInfo.InvariantCulture);

        if (options?.NavigationMode is { } mode && mode != NavigationMode.None)
        {
            var engine = mode switch
            {
                NavigationMode.Walking => "fossgis_osrm_foot",
                NavigationMode.Bicycling => "fossgis_osrm_bike",
                _ => "fossgis_osrm_car",
            };
            return $"https://www.openstreetmap.org/directions?engine={engine}&route=;{lat},{lon}";
        }

        return $"https://www.openstreetmap.org/?mlat={lat}&mlon={lon}#map=15/{lat}/{lon}";
    }

    /// <summary>
    /// OpenStreetMap search URL for a placemark: the non-empty address parts
    /// joined with single spaces and percent-encoded.
    /// </summary>
    internal static string BuildUrl(Placemark placemark, MapLaunchOptions? options)
    {
        var parts = new[]
        {
            placemark.SubThoroughfare,
            placemark.Thoroughfare,
            placemark.Locality,
            placemark.AdminArea,
            placemark.PostalCode,
            placemark.CountryName,
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim());

        var address = string.Join(" ", parts);
        if (string.IsNullOrEmpty(address) && !string.IsNullOrWhiteSpace(options?.Name))
            address = options!.Name.Trim();

        return $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(address)}";
    }

    private static bool Launch(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(url);
        return ExternalProcess.TryStart(psi);
    }
}
