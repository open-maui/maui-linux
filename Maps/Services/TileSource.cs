// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Maps.Services;

/// <summary>
/// One raster tile layer: a URL template, the attribution its provider
/// requires, and the deepest zoom it serves. A map layer (street / satellite /
/// hybrid) is one or more of these stacked bottom-to-top.
///
/// <para><b>Axis order.</b> The tile-grid axis order is encoded purely by the
/// position of the <c>{x}</c> and <c>{y}</c> placeholders in the template, so
/// no separate flag is needed: OSM-style servers use
/// <c>.../{z}/{x}/{y}.png</c>; ArcGIS/Esri REST servers use
/// <c>.../{z}/{y}/{x}</c>. <see cref="BuildUrl"/> substitutes each coordinate
/// wherever its placeholder appears.</para>
/// </summary>
public sealed class TileSource
{
    private string _urlTemplate;
    private string _key;

    public TileSource(string urlTemplate, string attribution, int maxZoom = 19, bool isOverlay = false)
    {
        _urlTemplate = urlTemplate ?? string.Empty;
        _key = HashTemplate(_urlTemplate);
        Attribution = attribution ?? string.Empty;
        MaxZoom = maxZoom;
        IsOverlay = isOverlay;
    }

    /// <summary>
    /// URL template with <c>{z}</c>/<c>{x}</c>/<c>{y}</c> placeholders. Settable
    /// so apps can redirect a layer at a self-hosted or commercial endpoint —
    /// the escape hatch that <c>OsmTileService.UrlTemplate</c> used to provide,
    /// now per layer.
    /// </summary>
    public string UrlTemplate
    {
        get => _urlTemplate;
        set
        {
            var v = value ?? string.Empty;
            if (_urlTemplate == v) return;
            _urlTemplate = v;
            // Cache keys embed this hash, so tiles cached under the old template
            // are simply never matched again after a redirect — no eviction.
            _key = HashTemplate(v);
        }
    }

    /// <summary>Provider attribution string, rendered in the on-map overlay when this layer is active.</summary>
    public string Attribution { get; set; }

    /// <summary>Deepest zoom this provider serves; deeper requests are skipped for this layer.</summary>
    public int MaxZoom { get; set; }

    /// <summary>
    /// True for a transparent reference layer (labels / roads / boundaries)
    /// drawn on top of an opaque base — the "hybrid" overlay.
    /// </summary>
    public bool IsOverlay { get; set; }

    /// <summary>
    /// Stable short hash of the current <see cref="UrlTemplate"/>. Part of the
    /// tile cache key so every source occupies its own disk/memory namespace
    /// and switching layers can never serve the wrong style.
    /// </summary>
    public string Key => _key;

    /// <summary>Substitute tile coordinates into the template (axis order per placeholder position).</summary>
    public string BuildUrl(int z, int x, int y) =>
        _urlTemplate
            .Replace("{z}", z.ToString())
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString());

    private static string HashTemplate(string template)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(template));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}
