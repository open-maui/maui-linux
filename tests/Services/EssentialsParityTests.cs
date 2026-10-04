// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The Essentials behaviours MAUI's own Essentials device tests (tests/Conformance/Essentials)
/// found different on Linux, each pinned here against MAUI's Windows/shared implementation:
/// exception types, return values and defaults.
/// </summary>
public class EssentialsParityTests : IDisposable
{
    private readonly List<string> _dirs = new();

    public EssentialsParityTests()
    {
        EssentialsPatches.Apply();
    }

    public void Dispose()
    {
        ExternalProcess.LaunchOverride = null;
        SchemeHandlers.SearchFilesOverride = null;
        MimeTypes.GlobFilesOverride = null;
        foreach (var dir in _dirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private string TempDir(string tag)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private string TempFile(string name, string content)
    {
        var path = Path.Combine(TempDir("file"), name);
        File.WriteAllText(path, content);
        return path;
    }

    private static List<System.Diagnostics.ProcessStartInfo> CaptureLaunches(bool result = true)
    {
        var launches = new List<System.Diagnostics.ProcessStartInfo>();
        ExternalProcess.LaunchOverride = psi =>
        {
            lock (launches)
                launches.Add(psi);
            return result;
        };
        return launches;
    }

    // ---------------- AppInfo ----------------

    private static Assembly AppAssembly(string name, params (string Key, string Value)[] metadata)
    {
        var builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name) { Version = new Version(3, 4, 5, 6) }, AssemblyBuilderAccess.Run);
        var ctor = typeof(AssemblyMetadataAttribute).GetConstructor(new[] { typeof(string), typeof(string) })!;
        foreach (var (key, value) in metadata)
            builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, new object[] { "Microsoft.Maui.ApplicationModel.AppInfo." + key, value }));
        return builder;
    }

    [Fact]
    public void AppInfo_reads_the_application_properties_the_build_targets_write()
    {
        var info = new AppInfoService(AppAssembly("MyApp",
            ("PackageName", "com.example.myapp"), ("Name", "My App"), ("Version", "1.0"), ("Build", "1")));

        info.PackageName.Should().Be("com.example.myapp");
        info.Name.Should().Be("My App");
        info.VersionString.Should().Be("1.0");
        info.Version.Should().Be(new Version(1, 0));
        info.BuildString.Should().Be("1");
        info.StorageName.Should().Be("MyApp", "the data directory keeps the name it had before ApplicationTitle was read");
    }

    [Fact]
    public void AppInfo_without_application_properties_uses_the_assembly()
    {
        var info = new AppInfoService(AppAssembly("PlainApp"));

        info.PackageName.Should().Be("PlainApp");
        info.Name.Should().Be("PlainApp");
        info.Version.Should().Be(new Version(3, 4, 5, 6));
        info.VersionString.Should().Be("3.4.5.6");
        info.StorageName.Should().Be("PlainApp");
    }

    [Theory]
    [InlineData("1.0", 1, 0, -1)]
    [InlineData("2", 2, 0, -1)]
    [InlineData("2.1.3", 2, 1, 3)]
    [InlineData("1.0.0-beta.2", 1, 0, 0)]
    [InlineData("4.5+sha.abc", 4, 5, -1)]
    public void AppInfo_display_versions_parse_like_MAUI(string text, int major, int minor, int build)
    {
        AppInfoService.TryParseVersion(text, out var version).Should().BeTrue();
        version.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
        version.Build.Should().Be(build);
    }

    // ---------------- VersionTracking / Launcher facades ----------------

    [Fact]
    public void VersionTracking_is_MAUIs_own_implementation()
    {
        // MAUI's VersionTrackingImplementation keeps its history in Preferences under the
        // app's private shared name; VersionTracking.InitVersionTracking only acts on it.
        VersionTracking.Default.GetType().FullName.Should().Be("Microsoft.Maui.ApplicationModel.VersionTrackingImplementation");
    }

    [Fact]
    public void Launcher_facade_is_the_Linux_launcher()
    {
        Launcher.Default.Should().BeOfType<LauncherService>();
    }

    [Fact]
    public void UseLinux_services_resolve_to_the_facade_instances()
    {
        var builder = MauiApp.CreateBuilder();
        LinuxPlatformRegistrar.Register(builder);
        using var services = builder.Services.BuildServiceProvider();

        services.GetRequiredService<IPreferences>().Should().BeSameAs(Preferences.Default);
        services.GetRequiredService<ISecureStorage>().Should().BeSameAs(SecureStorage.Default);
        services.GetRequiredService<IVersionTracking>().Should().BeSameAs(VersionTracking.Default);
        services.GetRequiredService<ILauncher>().Should().BeSameAs(Launcher.Default);
        services.GetRequiredService<IAppActions>().Should().BeSameAs(AppActions.Current);
        services.GetRequiredService<IFileSystem>().Should().BeSameAs(FileSystem.Current);
    }

    // ---------------- Launcher ----------------

    [Fact]
    public async Task Launcher_CanOpen_is_false_for_a_scheme_without_a_handler()
    {
        using var schemes = new FakeSchemeAssociations("tel");
        var launches = CaptureLaunches();
        var launcher = new LauncherService();

        (await launcher.CanOpenAsync(new Uri("ms-invalidurifortest:abc"))).Should().BeFalse();
        (await launcher.CanOpenAsync(new Uri("tel:+1 555 010 9999"))).Should().BeTrue();
        (await launcher.TryOpenAsync(new Uri("ms-invalidurifortest:abc"))).Should().BeFalse();
        launches.Should().BeEmpty("TryOpenAsync launches nothing for a URI it cannot open");

        (await launcher.TryOpenAsync(new Uri("tel:5550109999"))).Should().BeTrue();
        launches.Should().ContainSingle().Which.ArgumentList.Should().Equal("tel:5550109999");
    }

    [Fact]
    public async Task Launcher_CanOpen_a_file_uri_when_the_file_exists()
    {
        var path = TempFile("doc.txt", "x");
        var launcher = new LauncherService();

        (await launcher.CanOpenAsync(new Uri(path))).Should().BeTrue();
        (await launcher.CanOpenAsync(new Uri(path + ".missing"))).Should().BeFalse();
    }

    [Fact]
    public async Task Launcher_rejects_null_arguments_as_MAUI_does()
    {
        var launcher = new LauncherService();

        await launcher.Invoking(l => l.CanOpenAsync((Uri)null!)).Should().ThrowAsync<ArgumentNullException>();
        await launcher.Invoking(l => l.TryOpenAsync((Uri)null!)).Should().ThrowAsync<ArgumentNullException>();
        await launcher.Invoking(l => l.OpenAsync((OpenFileRequest)null!)).Should().ThrowAsync<ArgumentNullException>();
        await launcher.Invoking(l => l.OpenAsync(new OpenFileRequest())).Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------- Map / Share ----------------

    [Fact]
    public void Map_null_placemark_or_options_throw_before_launching()
    {
        var launches = CaptureLaunches();
        var map = new MapService();
        var placemark = new Placemark { Locality = "Redmond" };

        FluentActions.Invoking(() => { _ = map.OpenAsync(placemark, null!); }).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => { _ = map.OpenAsync((Placemark)null!, new MapLaunchOptions()); }).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => { _ = map.OpenAsync(1, 2, null!); }).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => { _ = map.TryOpenAsync(placemark, null!); }).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => { _ = map.TryOpenAsync(1, 2, null!); }).Should().Throw<ArgumentNullException>();
        launches.Should().BeEmpty();
    }

    [Fact]
    public void Share_rejects_requests_with_nothing_to_share()
    {
        var launches = CaptureLaunches();
        var share = new ShareService();

        FluentActions.Invoking(() => { _ = share.RequestAsync(new ShareTextRequest()); }).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => { _ = share.RequestAsync(new ShareFileRequest()); }).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => { _ = share.RequestAsync(new ShareMultipleFilesRequest { Files = new List<ShareFile>() }); }).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => { _ = share.RequestAsync(new ShareMultipleFilesRequest { Files = new List<ShareFile> { null! } }); }).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => { _ = share.RequestAsync((ShareTextRequest)null!); }).Should().Throw<ArgumentNullException>();
        launches.Should().BeEmpty();
    }

    // ---------------- Preferences ----------------

    [Fact]
    public void Preferences_reject_unsupported_types_and_null_removes()
    {
        var prefs = new PreferencesService(Path.Combine(TempDir("prefs"), "preferences.json"));

        prefs.Invoking(p => p.Set("key", new[] { 1 })).Should().Throw<NotSupportedException>()
            .WithMessage("Preferences using 'System.Int32[]' type is not supported");
        prefs.Invoking(p => p.Get("key", new object())).Should().Throw<NotSupportedException>();

        prefs.Set("text", "value");
        prefs.ContainsKey("text").Should().BeTrue();
        prefs.Set<string?>("text", null);
        prefs.ContainsKey("text").Should().BeFalse();
        prefs.Get<string?>("text", null).Should().BeNull();
    }

    // ---------------- FileBase ----------------

    [Fact]
    public async Task FileResult_opens_its_file_every_time()
    {
        var path = TempFile("sample.txt", "Sample content");
        var file = new FileResult(path);

        for (int i = 0; i < 2; i++)
        {
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            (await reader.ReadToEndAsync()).Should().Be("Sample content");
        }

        await new FileResult(path + ".missing").Invoking(f => f.OpenReadAsync()).Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.JPG", "image/jpeg")]
    [InlineData("clip.webp", "image/webp")]
    [InlineData("archive.tar.gz", "application/gzip")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.unknownext", "application/octet-stream")]
    [InlineData("noextension", "application/octet-stream")]
    public void FileResult_content_type_comes_from_the_extension(string name, string expected)
    {
        MimeTypes.GlobFilesOverride = Array.Empty<string>();
        new FileResult(Path.Combine(Path.GetTempPath(), name)).ContentType.Should().Be(expected);
    }

    [Fact]
    public void Mime_types_fall_back_to_the_shared_mime_info_globs()
    {
        var globs = TempFile("globs2", "# comment\n50:application/x-foo:*.foo\n80:application/x-foo-better:*.foo\n50:text/x-bar:*.bar:cs\n50:image/x-pattern:*.[ab]x\n");
        MimeTypes.GlobFilesOverride = new[] { globs };

        MimeTypes.FromExtension(".foo").Should().Be("application/x-foo-better", "the higher weight wins");
        MimeTypes.FromExtension("bar").Should().Be("text/x-bar");
        MimeTypes.FromExtension(" .PNG ").Should().Be("image/png", "the MAUI table comes first");
        MimeTypes.FromExtension(".ax").Should().BeNull("only plain *.ext globs are used");
    }

    [Fact]
    public async Task FileBase_copy_constructor_keeps_path_name_and_type()
    {
        var path = TempFile("report.pdf", "%PDF");
        var copy = new ReadOnlyFile(new FileResult(path));

        copy.FullPath.Should().Be(path);
        copy.FileName.Should().Be("report.pdf");
        copy.ContentType.Should().Be("application/pdf");
        using var stream = await copy.OpenReadAsync();
        stream.Length.Should().Be(4);
    }

    // ---------------- Permissions ----------------

    [Fact]
    public async Task Permissions_are_granted_without_declarations()
    {
        (await Permissions.CheckStatusAsync<Permissions.Battery>()).Should().Be(PermissionStatus.Granted);
        (await Permissions.CheckStatusAsync<Permissions.NetworkState>()).Should().Be(PermissionStatus.Granted);
        (await Permissions.RequestAsync<Permissions.Camera>()).Should().Be(PermissionStatus.Granted);
        new Permissions.LocationWhenInUse().Invoking(p => p.EnsureDeclared()).Should().NotThrow();
        new Permissions.Microphone().ShouldShowRationale().Should().BeFalse();
    }

    [Fact]
    public async Task Requesting_location_off_the_main_thread_throws_PermissionException()
    {
        await Task.Run(async () =>
        {
            await FluentActions.Awaiting(() => Permissions.RequestAsync<Permissions.LocationWhenInUse>()).Should().ThrowAsync<PermissionException>();
            await FluentActions.Awaiting(() => Permissions.RequestAsync<Permissions.LocationAlways>()).Should().ThrowAsync<PermissionException>();
        });
    }

    // ---------------- SecureStorage ----------------

    [Fact]
    public async Task SecureStorage_RemoveAll_clears_only_its_own_store()
    {
        var mine = new SecureStorageService(TempDir("secure-mine"), useSecretService: false);
        var other = new SecureStorageService(TempDir("secure-other"), useSecretService: false);
        await mine.SetAsync("a", "1");
        await mine.SetAsync("b", "2");
        await other.SetAsync("a", "other");

        mine.RemoveAll();

        (await mine.GetAsync("a")).Should().BeNull();
        (await mine.GetAsync("b")).Should().BeNull();
        (await other.GetAsync("a")).Should().Be("other");
    }

    /// <summary>A store whose legacy shared store is <paramref name="legacyDir"/> (file backend only).</summary>
    private static SecureStorageService WithLegacy(string dir, string legacyDir) =>
        new(dir, useSecretService: false, keyringNamespace: null, legacyFallbackPath: legacyDir, legacyNamespace: null);

    [Fact]
    public async Task SecureStorage_moves_only_the_legacy_keys_an_app_asks_for()
    {
        var legacyDir = TempDir("secure-legacy");
        var shared = new SecureStorageService(legacyDir, useSecretService: false);
        await shared.SetAsync("token", "mine");
        await shared.SetAsync("other-app-token", "not mine");

        var dir = TempDir("secure-app");
        var app = WithLegacy(dir, legacyDir);
        (await app.GetAsync("token")).Should().Be("mine");

        // Only the asked-for key is in the app's own store; the other app's value is not copied.
        var own = new SecureStorageService(dir, useSecretService: false);
        (await own.GetAsync("token")).Should().Be("mine");
        (await own.GetAsync("other-app-token")).Should().BeNull();
        // The shared store keeps its values (another app may still read them).
        (await shared.GetAsync("token")).Should().Be("mine");
    }

    [Fact]
    public async Task SecureStorage_a_removed_or_cleared_key_does_not_come_back_from_the_legacy_store()
    {
        var legacyDir = TempDir("secure-legacy");
        var shared = new SecureStorageService(legacyDir, useSecretService: false);
        await shared.SetAsync("removed", "old");
        await shared.SetAsync("cleared", "old");
        await shared.SetAsync("overwritten", "old");

        var dir = TempDir("secure-app");
        var app = WithLegacy(dir, legacyDir);
        app.Remove("removed");
        await app.SetAsync("overwritten", "new");
        (await app.GetAsync("removed")).Should().BeNull();
        (await app.GetAsync("overwritten")).Should().Be("new");

        app.RemoveAll();
        (await app.GetAsync("cleared")).Should().BeNull();

        // The settled keys are remembered across launches.
        var relaunched = WithLegacy(dir, legacyDir);
        (await relaunched.GetAsync("removed")).Should().BeNull();
        (await relaunched.GetAsync("cleared")).Should().BeNull();
        (await relaunched.GetAsync("overwritten")).Should().BeNull(); // cleared by RemoveAll
    }

    [Fact]
    public async Task SecureStorage_Remove_reports_whether_the_key_was_stored()
    {
        var storage = new SecureStorageService(TempDir("secure"), useSecretService: false);
        await storage.SetAsync("key", "value");

        storage.Remove("key").Should().BeTrue();
        storage.Remove("key").Should().BeFalse();
        (await storage.GetAsync("key")).Should().BeNull();
    }

    [Fact]
    public async Task SecureStorage_validates_like_MAUI()
    {
        var storage = new SecureStorageService(TempDir("secure"), useSecretService: false);

        await storage.Invoking(s => s.SetAsync("key", null!)).Should().ThrowAsync<ArgumentNullException>();
        await storage.Invoking(s => s.SetAsync(" ", "v")).Should().ThrowAsync<ArgumentNullException>();
        await storage.Invoking(s => s.GetAsync(" ")).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void SecureStorage_keyring_items_are_per_app()
    {
        SecureStorageService.NamespaceFor("com.example.app").Should().Be("maui-secure-storage/com.example.app");
        new SecureStorageService(TempDir("secure"), useSecretService: false, keyringNamespace: "maui-secure-storage/x")
            .KeyringNamespace.Should().Be("maui-secure-storage/x");
    }

    [Theory]
    [InlineData("secret-tool: Could not connect: No such file or directory", true)]
    [InlineData("The name org.freedesktop.secrets was not provided by any .service files", true)]
    [InlineData("Cannot autolaunch D-Bus without X11 $DISPLAY", true)]
    [InlineData("secret-tool: Can't find session /org/freedesktop/secrets/session/12", false)]
    [InlineData("", false)]
    public void SecureStorage_falls_back_to_files_only_when_no_Secret_Service_answers(string error, bool unreachable)
    {
        SecureStorageService.IsUnreachable(error).Should().Be(unreachable);
    }

    // ---------------- Vibration / Battery ----------------

    [Fact]
    public void Vibration_without_a_motor_throws_FeatureNotSupportedException()
    {
        var previous = Sysfs.Root;
        Sysfs.Root = TempDir("sysfs");
        try
        {
            var vibration = new VibrationService();
            vibration.IsSupported.Should().BeFalse();
            vibration.Invoking(v => v.Vibrate()).Should().Throw<FeatureNotSupportedException>();
            vibration.Invoking(v => v.Vibrate(TimeSpan.FromSeconds(1))).Should().Throw<FeatureNotSupportedException>();
            vibration.Invoking(v => v.Cancel()).Should().Throw<FeatureNotSupportedException>();
        }
        finally
        {
            Sysfs.Root = previous;
        }
    }

    [Fact]
    public void Vibration_duration_is_clamped_like_MAUI()
    {
        VibrationService.Clamp(TimeSpan.FromMilliseconds(-5)).Should().Be(TimeSpan.Zero);
        VibrationService.Clamp(TimeSpan.FromSeconds(9)).Should().Be(TimeSpan.FromSeconds(5));
        VibrationService.Clamp(TimeSpan.FromMilliseconds(250)).Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void Battery_of_a_peripheral_is_not_the_system_battery()
    {
        var previous = Sysfs.Root;
        var root = Sysfs.Root = TempDir("sysfs");
        try
        {
            void Write(string content, params string[] path)
            {
                var file = Path.Combine(new[] { root, "class", "power_supply" }.Concat(path).ToArray());
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, content);
            }
            Write("Battery\n", "hidpp_battery_0", "type");
            Write("Device\n", "hidpp_battery_0", "scope");
            Write("Discharging\n", "hidpp_battery_0", "status");
            Write("42\n", "hidpp_battery_0", "capacity");

            var battery = new BatteryService(new PowerProfilesMonitor(useDaemon: false));
            battery.State.Should().Be(BatteryState.NotPresent, "a wireless mouse's battery does not power the machine");
            battery.ChargeLevel.Should().Be(1.0);
            battery.PowerSource.Should().Be(BatteryPowerSource.AC);
        }
        finally
        {
            Sysfs.Root = previous;
        }
    }

    // ---------------- Screenshot / DeviceDisplay ----------------

    [Fact]
    public async Task Screenshot_without_a_window_throws_instead_of_returning_null()
    {
        await new ScreenshotService(() => null).Invoking(s => s.CaptureAsync()).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeviceDisplay_KeepScreenOn_goes_to_the_Linux_display_service()
    {
        CaptureLaunches();
        try
        {
            DeviceDisplay.KeepScreenOn = true;
            DeviceDisplay.KeepScreenOn.Should().BeTrue();
            DeviceDisplayService.Instance.KeepScreenOn.Should().BeTrue();
        }
        finally
        {
            DeviceDisplay.KeepScreenOn = false;
            await DeviceDisplayService.Instance.WhenInhibitSettled();
        }
        DeviceDisplay.KeepScreenOn.Should().BeFalse();
    }

    // ---------------- Geocoding ----------------

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _respond;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var (status, body) = _respond(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static GeocodingService Geocoder(StubHandler handler) =>
        new(new HttpClient(handler), new Uri("https://geocoder.test/"));

    [Fact]
    public async Task Geocoding_is_unsupported_until_the_app_names_a_service()
    {
        var saved = GeocodingService.ServiceUrl;
        var savedVariable = Environment.GetEnvironmentVariable("OPENMAUI_GEOCODING_URL");
        try
        {
            GeocodingService.ServiceUrl = null;
            Environment.SetEnvironmentVariable("OPENMAUI_GEOCODING_URL", null);
            var geocoder = new GeocodingService();
            await geocoder.Invoking(g => g.GetLocationsAsync("Berlin")).Should().ThrowAsync<FeatureNotSupportedException>();
            await geocoder.Invoking(g => g.GetPlacemarksAsync(52.5, 13.4)).Should().ThrowAsync<FeatureNotSupportedException>();
        }
        finally
        {
            GeocodingService.ServiceUrl = saved;
            Environment.SetEnvironmentVariable("OPENMAUI_GEOCODING_URL", savedVariable);
        }
    }

    [Fact]
    public async Task Geocoding_reverse_lookup_maps_the_address()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK, """
            {"lat":"47.6739","lon":"-122.1215","name":"","display_name":"16011, NE 36th Way, Redmond, King County, Washington, 98052, United States",
             "address":{"house_number":"16011","road":"NE 36th Way","city":"Redmond","county":"King County","state":"Washington","postcode":"98052","country":"United States","country_code":"us","suburb":"Overlake"}}
            """));

        var placemarks = (await Geocoder(handler).GetPlacemarksAsync(47.673988, -122.121513)).ToList();

        var p = placemarks.Should().ContainSingle().Subject;
        p.CountryCode.Should().Be("US");
        p.CountryName.Should().Be("United States");
        p.AdminArea.Should().Be("Washington");
        p.SubAdminArea.Should().Be("King County");
        p.Locality.Should().Be("Redmond");
        p.SubLocality.Should().Be("Overlake");
        p.Thoroughfare.Should().Be("NE 36th Way");
        p.SubThoroughfare.Should().Be("16011");
        p.PostalCode.Should().Be("98052");
        p.FeatureName.Should().StartWith("16011, NE 36th Way");
        p.Location.Latitude.Should().BeApproximately(47.6739, 1e-9);
        p.Location.Longitude.Should().BeApproximately(-122.1215, 1e-9);

        var uri = handler.Requests.Single().RequestUri!;
        uri.AbsolutePath.Should().Be("/reverse");
        uri.Query.Should().Contain("lat=47.673988").And.Contain("lon=-122.121513").And.Contain("format=jsonv2");
        handler.Requests.Single().Headers.UserAgent.ToString().Should().StartWith("OpenMaui/");
    }

    [Fact]
    public async Task Geocoding_forward_lookup_returns_every_match()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK, """[{"lat":"47.67","lon":"-122.12"},{"lat":"47.6","lon":"-122.3"},{"bad":1}]"""));

        var locations = (await Geocoder(handler).GetLocationsAsync("Redmond, WA, USA")).ToList();

        locations.Should().HaveCount(2);
        locations[0].Latitude.Should().BeApproximately(47.67, 1e-9);
        handler.Requests.Single().RequestUri!.Query.Should().Contain("q=Redmond%2C%20WA%2C%20USA");
    }

    [Fact]
    public async Task Geocoding_no_match_is_empty_and_a_failure_throws()
    {
        var noMatch = Geocoder(new StubHandler(_ => (HttpStatusCode.OK, """{"error":"Unable to geocode"}""")));
        (await noMatch.GetPlacemarksAsync(0, 0)).Should().BeEmpty();

        var failing = Geocoder(new StubHandler(_ => (HttpStatusCode.ServiceUnavailable, "")));
        await failing.Invoking(g => g.GetLocationsAsync("x")).Should().ThrowAsync<HttpRequestException>();

        await noMatch.Invoking(g => g.GetLocationsAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }
}
