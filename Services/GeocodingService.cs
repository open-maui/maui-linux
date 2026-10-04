// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux geocoding through a Nominatim service (OpenStreetMap's geocoder, the one GNOME's
/// geocode-glib uses). Every MAUI platform geocodes online through its vendor's service (Bing
/// Maps on Windows, Google on Android, Apple on iOS); the desktop has no system geocoder, so
/// this is OpenMaui's. It returned no results at all before.
///
/// Geocoding is opt-in, as on Windows (where MAUI needs the app's Bing Maps key): it calls a
/// service only when the app names one, in <see cref="ServiceUrl"/> or the
/// <c>OPENMAUI_GEOCODING_URL</c> environment variable (a self-hosted or commercial Nominatim, or
/// <see cref="OpenStreetMapUrl"/>, whose usage policy then applies: no heavy use). Otherwise it
/// throws <see cref="FeatureNotSupportedException"/>, and no address or position leaves the
/// machine. Requests carry a User-Agent naming the app (AppInfo.PackageName) and are spaced at
/// least one second apart within the process.
/// Results are in the current UI culture's language where the service has them.
/// A network or service failure throws (HttpRequestException), as the platform geocoders do;
/// an address or position with no match returns an empty list.
/// </summary>
public class GeocodingService : IGeocoding
{
    /// <summary>OpenStreetMap's public Nominatim instance (light use only, under its usage policy).</summary>
    public const string OpenStreetMapUrl = "https://nominatim.openstreetmap.org/";
    internal const string EndpointVariable = "OPENMAUI_GEOCODING_URL";

    /// <summary>
    /// The Nominatim service geocoding uses (overrides <c>OPENMAUI_GEOCODING_URL</c>); null with
    /// no environment variable leaves geocoding unsupported.
    /// </summary>
    public static string? ServiceUrl { get; set; }

    private static readonly TimeSpan MinimumSpacing = TimeSpan.FromSeconds(1);
    private static readonly SemaphoreSlim s_throttle = new(1, 1);
    private static DateTime s_lastRequest = DateTime.MinValue;
    private static readonly Lazy<HttpClient> s_sharedClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

    private readonly HttpClient? _client;
    private readonly Uri? _endpoint;

    public GeocodingService()
    {
    }

    /// <summary>A geocoder over <paramref name="client"/> and <paramref name="endpoint"/> (tests, hosts).</summary>
    internal GeocodingService(HttpClient client, Uri endpoint)
    {
        _client = client;
        _endpoint = endpoint;
    }

    private HttpClient Client => _client ?? s_sharedClient.Value;

    private Uri Endpoint
    {
        get
        {
            if (_endpoint != null)
                return _endpoint;
            var configured = !string.IsNullOrWhiteSpace(ServiceUrl) ? ServiceUrl : Environment.GetEnvironmentVariable(EndpointVariable);
            if (string.IsNullOrWhiteSpace(configured))
                throw new FeatureNotSupportedException(
                    "Geocoding needs a service on Linux: set GeocodingService.ServiceUrl (or OPENMAUI_GEOCODING_URL) to a Nominatim instance.");
            var url = configured.Trim();
            if (!url.EndsWith('/'))
                url += "/";
            return new Uri(url);
        }
    }

    public async Task<IEnumerable<Placemark>> GetPlacemarksAsync(double latitude, double longitude)
    {
        var query = $"reverse?format=jsonv2&addressdetails=1&lat={Format(latitude)}&lon={Format(longitude)}";
        using var document = await QueryAsync(query).ConfigureAwait(false);
        var root = document.RootElement;
        // No match: {"error":"Unable to geocode"}.
        if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
            return Array.Empty<Placemark>();
        return ToPlacemark(root) is { } placemark ? new[] { placemark } : Array.Empty<Placemark>();
    }

    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        if (string.IsNullOrWhiteSpace(address))
            return Array.Empty<Location>();

        // Up to ten results, as Windows asks Bing Maps for.
        var query = $"search?format=jsonv2&limit=10&q={Uri.EscapeDataString(address)}";
        using var document = await QueryAsync(query).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<Location>();

        var locations = new List<Location>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (ToLocation(item) is { } location)
                locations.Add(location);
        }
        return locations;
    }

    private async Task<JsonDocument> QueryAsync(string relative)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Endpoint, relative));
        request.Headers.UserAgent.ParseAdd(UserAgent());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var language = CultureInfo.CurrentUICulture.Name;
        if (!string.IsNullOrEmpty(language))
            request.Headers.AcceptLanguage.ParseAdd(language);

        await s_throttle.WaitAsync().ConfigureAwait(false);
        try
        {
            var wait = s_lastRequest + MinimumSpacing - DateTime.UtcNow;
            if (wait > TimeSpan.Zero && _client == null)
                await Task.Delay(wait).ConfigureAwait(false);
            using var response = await Client.SendAsync(request).ConfigureAwait(false);
            s_lastRequest = DateTime.UtcNow;
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        }
        finally
        {
            s_throttle.Release();
        }
    }

    /// <summary>"OpenMaui/&lt;version&gt; (&lt;app id&gt;)": Nominatim's policy asks for an identifying User-Agent.</summary>
    internal static string UserAgent()
    {
        var version = typeof(GeocodingService).Assembly.GetName().Version?.ToString(3) ?? "1.0";
        string app;
        try { app = Microsoft.Maui.ApplicationModel.AppInfo.Current?.PackageName ?? "app"; }
        catch { app = "app"; }
        // A product comment may not contain parentheses.
        app = app.Replace('(', '_').Replace(')', '_');
        return $"OpenMaui/{version} ({app})";
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A Nominatim jsonv2 result (with addressdetails) as a MAUI Placemark.</summary>
    internal static Placemark? ToPlacemark(JsonElement item)
    {
        var location = ToLocation(item);
        if (location == null)
            return null;

        item.TryGetProperty("address", out var address);
        string? Part(params string[] keys)
        {
            if (address.ValueKind != JsonValueKind.Object)
                return null;
            foreach (var key in keys)
            {
                if (address.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
                    return text;
            }
            return null;
        }

        var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var display = item.TryGetProperty("display_name", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;

        return new Placemark
        {
            Location = location,
            FeatureName = string.IsNullOrEmpty(name) ? display : name,
            CountryCode = Part("country_code")?.ToUpperInvariant(),
            CountryName = Part("country"),
            AdminArea = Part("state", "region", "province"),
            SubAdminArea = Part("county", "state_district"),
            Locality = Part("city", "town", "village", "hamlet", "municipality"),
            SubLocality = Part("suburb", "city_district", "neighbourhood", "quarter", "borough"),
            Thoroughfare = Part("road", "pedestrian", "footway", "street"),
            SubThoroughfare = Part("house_number"),
            PostalCode = Part("postcode"),
        };
    }

    /// <summary>The "lat"/"lon" of a Nominatim result (strings in its JSON), or null.</summary>
    internal static Location? ToLocation(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;
        if (!TryCoordinate(item, "lat", out var latitude) || !TryCoordinate(item, "lon", out var longitude))
            return null;
        return new Location(latitude, longitude);
    }

    private static bool TryCoordinate(JsonElement item, string name, out double value)
    {
        value = 0;
        if (!item.TryGetProperty(name, out var element))
            return false;
        return element.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => element.TryGetDouble(out value),
            _ => false,
        };
    }
}
