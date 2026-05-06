// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux geocoding stub. Could use Nominatim (OpenStreetMap) API.
/// </summary>
public class GeocodingService : IGeocoding
{
    public async Task<IEnumerable<Placemark>> GetPlacemarksAsync(double latitude, double longitude)
        => Array.Empty<Placemark>();

    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
        => Array.Empty<Location>();
}
