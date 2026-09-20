// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using Moq;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Headless coverage of the pure-logic parts of the Essentials services.
/// Anything that would need a display server, keyring or network is exercised
/// only on the paths that stay in-process.
/// </summary>
public class EssentialsTests
{
    private static string TempDir(string tag)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"openmaui-{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    #region FileSystemService

    [Fact]
    public void FileSystem_AppDataDirectory_IsXdgDataHomeSlashApp()
    {
        var root = TempDir("data");
        try
        {
            var fs = new FileSystemService("MyApp", root, null, null, null);
            fs.AppDataDirectory.Should().Be(Path.Combine(root, "MyApp"));
            Directory.Exists(fs.AppDataDirectory).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FileSystem_CacheDirectory_IsXdgCacheHomeSlashApp()
    {
        var root = TempDir("cache");
        try
        {
            var fs = new FileSystemService("MyApp", null, root, null, null);
            fs.CacheDirectory.Should().Be(Path.Combine(root, "MyApp"));
            Directory.Exists(fs.CacheDirectory).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FileSystem_Directories_HonourXdgEnvironment()
    {
        var data = TempDir("xdgdata");
        var cache = TempDir("xdgcache");
        var oldData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var oldCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", data);
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cache);
            var fs = new FileSystemService("EnvApp", null, null, null, null);
            fs.AppDataDirectory.Should().Be(Path.Combine(data, "EnvApp"));
            fs.CacheDirectory.Should().Be(Path.Combine(cache, "EnvApp"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldData);
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", oldCache);
            Directory.Delete(data, true);
            Directory.Delete(cache, true);
        }
    }

    [Fact]
    public void FileSystem_DefaultAppName_FallsBackToEntryAssemblyOrMauiApp()
    {
        var fs = new FileSystemService(null, null, null, null, null);
        fs.AppName.Should().NotBeNullOrWhiteSpace();
        fs.AppName.Should().NotContain("/");
    }

    [Fact]
    public async Task FileSystem_OpenAppPackageFile_ReadsRelativeToPackageRoot()
    {
        var root = TempDir("pkg");
        try
        {
            File.WriteAllText(Path.Combine(root, "hello.txt"), "hi there");
            var fs = new FileSystemService("A", null, null, root, null);

            (await fs.AppPackageFileExistsAsync("hello.txt")).Should().BeTrue();
            using var stream = await fs.OpenAppPackageFileAsync("hello.txt");
            using var reader = new StreamReader(stream);
            (await reader.ReadToEndAsync()).Should().Be("hi there");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileSystem_OpenAppPackageFile_FallsBackToResourcesRaw()
    {
        var root = TempDir("pkgraw");
        try
        {
            var raw = Path.Combine(root, "Resources", "Raw");
            Directory.CreateDirectory(raw);
            File.WriteAllText(Path.Combine(raw, "data.json"), "{}");
            var fs = new FileSystemService("A", null, null, root, null);

            (await fs.AppPackageFileExistsAsync("data.json")).Should().BeTrue();
            (await fs.AppPackageFileExistsAsync("Resources/Raw/data.json")).Should().BeTrue();
            using var stream = await fs.OpenAppPackageFileAsync("data.json");
            using var reader = new StreamReader(stream);
            (await reader.ReadToEndAsync()).Should().Be("{}");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileSystem_MissingPackageFile_ReportsFalseAndThrows()
    {
        var root = TempDir("pkgmissing");
        try
        {
            var fs = new FileSystemService("A", null, null, root, typeof(EssentialsTests).Assembly);
            (await fs.AppPackageFileExistsAsync("nope.bin")).Should().BeFalse();
            (await fs.AppPackageFileExistsAsync("")).Should().BeFalse();
            var act = () => fs.OpenAppPackageFileAsync("nope.bin");
            await act.Should().ThrowAsync<FileNotFoundException>();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileSystem_PackageFile_RejectsPathTraversal()
    {
        var root = TempDir("pkgtraversal");
        var outside = Path.Combine(Path.GetDirectoryName(root)!, "outside-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(outside, "secret");
            var fs = new FileSystemService("A", null, null, root, typeof(EssentialsTests).Assembly);
            (await fs.AppPackageFileExistsAsync("../" + Path.GetFileName(outside))).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(outside);
        }
    }

    #endregion

    #region SemanticScreenReaderService

    [Fact]
    public void ScreenReader_NoService_IsNoOpThatRecordsText()
    {
        var reader = new SemanticScreenReaderService((IAccessibilityService?)null);
        var act = () => reader.Announce("Clicked 1 time");
        act.Should().NotThrow();
        reader.LastAnnouncement.Should().Be("Clicked 1 time");
    }

    [Fact]
    public void ScreenReader_DisabledService_DoesNotForward()
    {
        var mock = new Mock<IAccessibilityService>();
        mock.SetupGet(s => s.IsEnabled).Returns(false);
        var reader = new SemanticScreenReaderService(mock.Object);

        reader.Announce("hello");

        mock.Verify(s => s.Announce(It.IsAny<string>(), It.IsAny<AnnouncementPriority>()), Times.Never);
        new SemanticScreenReaderService(new NullAccessibilityService()).Invoking(r => r.Announce("x")).Should().NotThrow();
    }

    [Fact]
    public void ScreenReader_EnabledService_ForwardsPolitely()
    {
        var mock = new Mock<IAccessibilityService>();
        mock.SetupGet(s => s.IsEnabled).Returns(true);
        var reader = new SemanticScreenReaderService(mock.Object);

        reader.Announce("Volume: 50");

        mock.Verify(s => s.Announce("Volume: 50", AnnouncementPriority.Polite), Times.Once);
    }

    [Fact]
    public void ScreenReader_EmptyText_IsIgnored()
    {
        var mock = new Mock<IAccessibilityService>(MockBehavior.Strict);
        var reader = new SemanticScreenReaderService(mock.Object);

        reader.Announce("");
        reader.Announce("   ");
        reader.Announce(null!);

        reader.LastAnnouncement.Should().BeNull();
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ScreenReader_ResolverThrows_StillNoOp()
    {
        var reader = new SemanticScreenReaderService(() => throw new InvalidOperationException("no bus"));
        reader.Invoking(r => r.Announce("x")).Should().NotThrow();
    }

    #endregion

    #region Unsupported sensors

    public static IEnumerable<object[]> Sensors()
    {
        yield return new object[] { new UnsupportedAccelerometer(), "Accelerometer" };
        yield return new object[] { new UnsupportedBarometer(), "Barometer" };
        yield return new object[] { new UnsupportedCompass(), "Compass" };
        yield return new object[] { new UnsupportedGyroscope(), "Gyroscope" };
        yield return new object[] { new UnsupportedMagnetometer(), "Magnetometer" };
        yield return new object[] { new UnsupportedOrientationSensor(), "OrientationSensor" };
    }

    [Theory]
    [MemberData(nameof(Sensors))]
    public void Sensor_ReportsUnsupported_AndStartThrowsFeatureNotSupported(object sensor, string name)
    {
        var isSupported = (bool)sensor.GetType().GetProperty("IsSupported")!.GetValue(sensor)!;
        var isMonitoring = (bool)sensor.GetType().GetProperty("IsMonitoring")!.GetValue(sensor)!;
        isSupported.Should().BeFalse();
        isMonitoring.Should().BeFalse();

        var start = sensor.GetType().GetMethod("Start", new[] { typeof(SensorSpeed) })!;
        var act = () => start.Invoke(sensor, new object[] { SensorSpeed.Default });
        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<FeatureNotSupportedException>()
            .WithMessage($"*{name}*");

        var stop = sensor.GetType().GetMethod("Stop", Type.EmptyTypes)!;
        stop.Invoking(m => m.Invoke(sensor, null)).Should().NotThrow();
    }

    [Fact]
    public void Compass_LowPassFilterOverload_AlsoThrows()
    {
        ICompass compass = new UnsupportedCompass();
        compass.Invoking(c => c.Start(SensorSpeed.UI, applyLowPassFilter: true)).Should().Throw<FeatureNotSupportedException>();
    }

    [Fact]
    public void Sensors_ImplementEssentialsInterfaces()
    {
        new UnsupportedAccelerometer().Should().BeAssignableTo<IAccelerometer>();
        new UnsupportedBarometer().Should().BeAssignableTo<IBarometer>();
        new UnsupportedCompass().Should().BeAssignableTo<ICompass>();
        new UnsupportedGyroscope().Should().BeAssignableTo<IGyroscope>();
        new UnsupportedMagnetometer().Should().BeAssignableTo<IMagnetometer>();
        new UnsupportedOrientationSensor().Should().BeAssignableTo<IOrientationSensor>();
    }

    #endregion

    #region WebAuthenticatorService

    [Theory]
    [InlineData("myapp://callback")]
    [InlineData("https://localhost:5001/callback")]
    [InlineData("http://example.com/callback")]
    public async Task WebAuthenticator_UnsupportedCallback_ThrowsNotSupported(string callback)
    {
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true));
        var options = new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/authorize"), CallbackUrl = new Uri(callback) };

        var act = () => auth.AuthenticateAsync(options);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void WebAuthenticator_BuildPrefix_UsesHostAndPort()
    {
        WebAuthenticatorService.BuildPrefix(new Uri("http://localhost:4567/cb")).Should().Be("http://localhost:4567/");
        WebAuthenticatorService.BuildPrefix(new Uri("http://127.0.0.1:80/")).Should().Be("http://127.0.0.1:80/");
    }

    [Fact]
    public async Task WebAuthenticator_LoopbackFlow_ReturnsQueryProperties()
    {
        var port = FreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        Uri? opened = null;

        // "Browser" that immediately hits the callback the way an IdP redirect would.
        var auth = new WebAuthenticatorService(async url =>
        {
            opened = url;
            _ = Task.Run(async () =>
            {
                using var http = new HttpClient();
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        var response = await http.GetAsync($"{callback}?code=abc123&state=xyz");
                        response.IsSuccessStatusCode.Should().BeTrue();
                        return;
                    }
                    catch (HttpRequestException) { await Task.Delay(50); }
                }
            });
            await Task.CompletedTask;
            return true;
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        var result = await auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize?client_id=1"),
            CallbackUrl = callback,
        });

        opened.Should().Be(new Uri("https://issuer.example/authorize?client_id=1"));
        result.Get("code").Should().Be("abc123");
        result.Get("state").Should().Be("xyz");
        result.CallbackUri.Should().NotBeNull();
        result.CallbackUri!.AbsolutePath.Should().Be("/callback");
    }

    [Fact]
    public async Task WebAuthenticator_WrongPath_IsIgnoredUntilCallbackArrives()
    {
        var port = FreePort();
        var callback = new Uri($"http://localhost:{port}/auth/done");

        var auth = new WebAuthenticatorService(url =>
        {
            _ = Task.Run(async () =>
            {
                using var http = new HttpClient();
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        var wrong = await http.GetAsync($"http://localhost:{port}/favicon.ico");
                        ((int)wrong.StatusCode).Should().Be(404);
                        await http.GetAsync($"{callback}?access_token=tok&expires_in=3600");
                        return;
                    }
                    catch (HttpRequestException) { await Task.Delay(50); }
                }
            });
            return Task.FromResult(true);
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        var result = await auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize"),
            CallbackUrl = callback,
        }, CancellationToken.None);

        result.AccessToken.Should().Be("tok");
        result.ExpiresIn.Should().NotBeNull();
    }

    [Fact]
    public async Task WebAuthenticator_Timeout_Cancels()
    {
        var port = FreePort();
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true)) { Timeout = TimeSpan.FromMilliseconds(300) };

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize"),
            CallbackUrl = new Uri($"http://127.0.0.1:{port}/cb"),
        });

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WebAuthenticator_BrowserFailsToOpen_Throws()
    {
        var port = FreePort();
        var auth = new WebAuthenticatorService(_ => Task.FromResult(false));

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize"),
            CallbackUrl = new Uri($"http://127.0.0.1:{port}/cb"),
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*browser*");
    }

    [Fact]
    public async Task WebAuthenticator_MissingOptions_Throws()
    {
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true));
        await auth.Invoking(a => a.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://x/") }))
            .Should().ThrowAsync<ArgumentException>();
        await auth.Invoking(a => a.AuthenticateAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    private static int FreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        socket.Start();
        return ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
    }

    #endregion

    #region PreferencesService

    [Fact]
    public void Preferences_RoundTrip_AllSupportedTypes()
    {
        var dir = TempDir("prefs");
        try
        {
            var prefs = new PreferencesService(Path.Combine(dir, "preferences.json"));
            prefs.Set("s", "text");
            prefs.Set("i", 42);
            prefs.Set("b", true);
            prefs.Set("d", 1.5);
            prefs.Set("f", 2.5f);
            prefs.Set("l", 1234567890123L);
            var when = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            prefs.Set("dt", when);

            prefs.Get("s", "").Should().Be("text");
            prefs.Get("i", 0).Should().Be(42);
            prefs.Get("b", false).Should().BeTrue();
            prefs.Get("d", 0.0).Should().Be(1.5);
            prefs.Get("f", 0f).Should().Be(2.5f);
            prefs.Get("l", 0L).Should().Be(1234567890123L);
            prefs.Get("dt", DateTime.MinValue).Should().Be(when);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Preferences_DefaultsWhenMissing_ContainsKey_Remove_Clear()
    {
        var dir = TempDir("prefs2");
        try
        {
            var prefs = new PreferencesService(Path.Combine(dir, "preferences.json"));
            prefs.Get("missing", "dflt").Should().Be("dflt");
            prefs.ContainsKey("missing").Should().BeFalse();

            prefs.Set("k", 1);
            prefs.ContainsKey("k").Should().BeTrue();
            prefs.Remove("k");
            prefs.ContainsKey("k").Should().BeFalse();

            prefs.Set("a", 1);
            prefs.Set("b", 2);
            prefs.Clear();
            prefs.ContainsKey("a").Should().BeFalse();
            prefs.ContainsKey("b").Should().BeFalse();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Preferences_SharedName_IsolatesContainers()
    {
        var dir = TempDir("prefs3");
        try
        {
            var prefs = new PreferencesService(Path.Combine(dir, "preferences.json"));
            prefs.Set("k", "default");
            prefs.Set("k", "shared", "other");

            prefs.Get("k", "").Should().Be("default");
            prefs.Get("k", "", "other").Should().Be("shared");
            prefs.Clear("other");
            prefs.ContainsKey("k", "other").Should().BeFalse();
            prefs.ContainsKey("k").Should().BeTrue();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Preferences_PersistAcrossInstances()
    {
        var dir = TempDir("prefs4");
        try
        {
            var path = Path.Combine(dir, "preferences.json");
            new PreferencesService(path).Set("persisted", 99);
            File.Exists(path).Should().BeTrue();

            new PreferencesService(path).Get("persisted", 0).Should().Be(99);
        }
        finally { Directory.Delete(dir, true); }
    }

    #endregion

    #region VersionTrackingService

    [Fact]
    public void VersionTracking_ReportsCurrentVersionAndHistory()
    {
        var dir = TempDir("vt");
        try
        {
            var path = Path.Combine(dir, "version-tracking.json");
            var tracking = new VersionTrackingService(path);

            tracking.CurrentVersion.Should().NotBeNullOrWhiteSpace();
            tracking.CurrentBuild.Should().NotBeNullOrWhiteSpace();
            tracking.VersionHistory.Should().Contain(tracking.CurrentVersion);
            tracking.BuildHistory.Should().Contain(tracking.CurrentBuild);
            tracking.IsFirstLaunchEver.Should().BeTrue();
            tracking.IsFirstLaunchForCurrentVersion.Should().BeTrue();
            tracking.FirstInstalledVersion.Should().Be(tracking.CurrentVersion);
            tracking.IsFirstLaunchForVersion(tracking.CurrentVersion).Should().BeTrue();
            tracking.IsFirstLaunchForBuild(tracking.CurrentBuild).Should().BeTrue();
            tracking.IsFirstLaunchForVersion("0.0.0-never").Should().BeFalse();
            tracking.Invoking(t => t.Track()).Should().NotThrow();
            File.Exists(path).Should().BeTrue();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void VersionTracking_SecondLaunch_IsNotFirstLaunchEver()
    {
        var dir = TempDir("vt2");
        try
        {
            var path = Path.Combine(dir, "version-tracking.json");
            new VersionTrackingService(path).Track();

            var second = new VersionTrackingService(path);
            second.IsFirstLaunchEver.Should().BeFalse();
            second.IsFirstLaunchForCurrentVersion.Should().BeFalse();
            second.PreviousVersion.Should().Be(second.CurrentVersion);
            second.VersionHistory.Should().ContainSingle();
        }
        finally { Directory.Delete(dir, true); }
    }

    #endregion

    #region DeviceInfoService / AppInfoService

    [Fact]
    public void DeviceInfo_DescribesLinuxDesktop()
    {
        var info = DeviceInfoService.Instance;
        info.Platform.Should().Be(DevicePlatform.Create("Linux"));
        info.Platform.ToString().Should().Be("Linux");
        info.Idiom.Should().Be(DeviceIdiom.Desktop);
        info.DeviceType.Should().Be(DeviceType.Physical);
        info.Model.Should().NotBeNullOrWhiteSpace();
        info.Manufacturer.Should().NotBeNullOrWhiteSpace();
        info.Name.Should().NotBeNullOrWhiteSpace();
        info.VersionString.Should().NotBeNullOrWhiteSpace();
        info.Version.Should().NotBeNull();
    }

    [Fact]
    public void AppInfo_HasNameAndVersion()
    {
        var info = AppInfoService.Instance;
        info.Name.Should().NotBeNullOrWhiteSpace();
        info.PackageName.Should().NotBeNullOrWhiteSpace();
        info.VersionString.Should().NotBeNullOrWhiteSpace();
        info.BuildString.Should().NotBeNullOrWhiteSpace();
        info.Version.Should().NotBeNull();
        Enum.IsDefined(info.PackagingModel).Should().BeTrue();
        Enum.IsDefined(info.RequestedTheme).Should().BeTrue();
        info.RequestedLayoutDirection.Should().Be(LayoutDirection.LeftToRight);
    }

    #endregion

    #region ConnectivityService / ClipboardService

    [Fact]
    public void Connectivity_ReturnsProfilesWithoutThrowing()
    {
        var connectivity = ConnectivityService.Instance;
        var profiles = connectivity.ConnectionProfiles;
        profiles.Should().NotBeNull();
        var list = profiles.ToList();
        list.Should().OnlyContain(p => Enum.IsDefined(p));
        Enum.IsDefined(connectivity.NetworkAccess).Should().BeTrue();
    }

    [Fact]
    public void Clipboard_HasText_FalseWhenNothingSetInProcess()
    {
        var clipboard = new ClipboardService();
        clipboard.Invoking(c => c.HasText).Should().NotThrow();
        clipboard.HasText.Should().BeFalse();
    }

    #endregion

    #region SecureStorageService

    [Fact]
    public async Task SecureStorage_FallbackRoundTrip()
    {
        var dir = Path.Combine(TempDir("secure"), "store");
        try
        {
            var storage = new SecureStorageService(dir, useSecretService: false);

            await storage.SetAsync("token", "s3cr3t");
            (await storage.GetAsync("token")).Should().Be("s3cr3t");

            // Encrypted at rest: the plaintext must not appear in the file.
            var files = Directory.GetFiles(dir);
            files.Should().HaveCount(1);
            File.ReadAllText(files[0]).Should().NotContain("s3cr3t");

            storage.Remove("token").Should().BeTrue();
            (await storage.GetAsync("token")).Should().BeNull();
            storage.Remove("token").Should().BeFalse();
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task SecureStorage_RemoveAll_ClearsFallbackDirectory()
    {
        var dir = Path.Combine(TempDir("secure2"), "store");
        try
        {
            var storage = new SecureStorageService(dir, useSecretService: false);
            await storage.SetAsync("a", "1");
            await storage.SetAsync("b", "2");
            Directory.GetFiles(dir).Should().HaveCount(2);

            storage.RemoveAll();
            Directory.Exists(dir).Should().BeFalse();
            (await storage.GetAsync("a")).Should().BeNull();
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task SecureStorage_EmptyKey_Throws()
    {
        var storage = new SecureStorageService(Path.Combine(Path.GetTempPath(), "unused"), useSecretService: false);
        await storage.Invoking(s => s.GetAsync("")).Should().ThrowAsync<ArgumentNullException>();
        await storage.Invoking(s => s.SetAsync("", "v")).Should().ThrowAsync<ArgumentNullException>();
        storage.Invoking(s => s.Remove("")).Should().Throw<ArgumentNullException>();
    }

    #endregion
    #region Process seam helpers

    /// <summary>
    /// Captures every ProcessStartInfo the services hand to ExternalProcess and
    /// answers with a fixed launch result, so nothing reaches the desktop.
    /// </summary>
    private sealed class LaunchCapture : IDisposable
    {
        public List<System.Diagnostics.ProcessStartInfo> Launches { get; } = new();
        public Func<System.Diagnostics.ProcessStartInfo, bool> Result { get; set; }

        public LaunchCapture(bool result = true) : this(_ => result) { }

        public LaunchCapture(Func<System.Diagnostics.ProcessStartInfo, bool> result)
        {
            Result = result;
            ExternalProcess.LaunchOverride = psi =>
            {
                Launches.Add(psi);
                return Result(psi);
            };
        }

        public System.Diagnostics.ProcessStartInfo Single => Launches.Should().ContainSingle().Subject;

        public void Dispose() => ExternalProcess.LaunchOverride = null;
    }

    /// <summary>Points the sysfs readers at a scratch tree for the test's lifetime.</summary>
    private sealed class FakeSysfs : IDisposable
    {
        private readonly string _previous;
        public string Root { get; }

        public FakeSysfs()
        {
            _previous = Sysfs.Root;
            Root = TempDir("sysfs");
            Sysfs.Root = Root;
        }

        public string Node(params string[] segments)
        {
            var path = Path.Combine(new[] { Root }.Concat(segments).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }

        public void Write(string content, params string[] segments) => File.WriteAllText(Node(segments), content);

        public void Dispose()
        {
            Sysfs.Root = _previous;
            try { Directory.Delete(Root, true); } catch { }
        }
    }

    #endregion

    #region LauncherService

    [Fact]
    public async Task Launcher_OpenUri_UsesXdgOpenAndReportsLaunchResult()
    {
        using var capture = new LaunchCapture(true);
        var launcher = new LauncherService();

        (await launcher.CanOpenAsync(new Uri("https://example.com/a b"))).Should().BeTrue();
        (await launcher.OpenAsync(new Uri("https://example.com/a b?q=1"))).Should().BeTrue();

        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("https://example.com/a%20b?q=1");
        psi.UseShellExecute.Should().BeFalse();
    }

    [Fact]
    public async Task Launcher_OpenUri_ReturnsFalseWhenLaunchFails()
    {
        using var capture = new LaunchCapture(false);
        var launcher = new LauncherService();

        (await launcher.OpenAsync(new Uri("mailto:someone@example.com"))).Should().BeFalse();
        (await launcher.TryOpenAsync(new Uri("mailto:someone@example.com"))).Should().BeFalse();
        capture.Launches.Should().HaveCount(2);
        capture.Launches.Should().OnlyContain(p => p.FileName == "xdg-open");
    }

    [Fact]
    public async Task Launcher_OpenFile_PassesFullPathAsSingleArgument()
    {
        using var capture = new LaunchCapture(true);
        var launcher = new LauncherService();
        var path = "/tmp/my report \"final\".pdf";

        (await launcher.OpenAsync(new OpenFileRequest("Open", new ReadOnlyFile(path)))).Should().BeTrue();

        capture.Single.ArgumentList.Should().ContainSingle().Which.Should().Be(path);
    }

    [Fact]
    public async Task Launcher_OpenFile_WithoutFile_ReturnsFalseWithoutLaunching()
    {
        using var capture = new LaunchCapture(true);
        var launcher = new LauncherService();

        (await launcher.OpenAsync(new OpenFileRequest())).Should().BeFalse();
        capture.Launches.Should().BeEmpty();
    }

    #endregion

    #region BrowserService

    [Fact]
    public async Task Browser_Open_UsesXdgOpenWithAbsoluteUri()
    {
        using var capture = new LaunchCapture(true);
        var browser = new BrowserService();

        (await browser.OpenAsync("https://example.com/path?x=1&y=two words")).Should().BeTrue();

        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("https://example.com/path?x=1&y=two%20words");
    }

    [Fact]
    public async Task Browser_Open_ReturnsFalseWhenXdgOpenFails()
    {
        using var capture = new LaunchCapture(false);
        var browser = new BrowserService();

        (await browser.OpenAsync(new Uri("https://example.com"), BrowserLaunchMode.External)).Should().BeFalse();
        (await browser.OpenAsync(new Uri("https://example.com"), new BrowserLaunchOptions())).Should().BeFalse();
        capture.Launches.Should().HaveCount(2);
    }

    [Fact]
    public async Task Browser_NullUri_Throws()
    {
        using var capture = new LaunchCapture(true);
        var browser = new BrowserService();
        await browser.Invoking(b => b.OpenAsync((Uri)null!, BrowserLaunchMode.SystemPreferred)).Should().ThrowAsync<ArgumentNullException>();
        capture.Launches.Should().BeEmpty();
    }

    [Fact]
    public async Task Browser_StaticFacade_ForwardsToDefault()
    {
        using var capture = new LaunchCapture(true);
        var original = Microsoft.Maui.Platform.Linux.Services.Browser.Default;
        try
        {
            var mock = new Mock<IBrowser>();
            mock.Setup(b => b.OpenAsync(It.IsAny<Uri>(), It.IsAny<BrowserLaunchOptions>())).ReturnsAsync(true);
            Microsoft.Maui.Platform.Linux.Services.Browser.Default = mock.Object;

            (await Microsoft.Maui.Platform.Linux.Services.Browser.OpenAsync(new Uri("https://example.com"), BrowserLaunchMode.External)).Should().BeTrue();
            mock.Verify(b => b.OpenAsync(new Uri("https://example.com"), It.Is<BrowserLaunchOptions>(o => o.LaunchMode == BrowserLaunchMode.External)), Times.Once);
            capture.Launches.Should().BeEmpty();
        }
        finally { Microsoft.Maui.Platform.Linux.Services.Browser.Default = original; }
    }

    #endregion

    #region EmailService

    [Fact]
    public void Email_BuildMailto_EncodesHeadersAndKeepsAddressesReadable()
    {
        var message = new EmailMessage
        {
            Subject = "Hello & welcome",
            Body = "Line 1\nLine 2 with spaces",
            To = new List<string> { "a@example.com", "b+tag@example.org" },
            Cc = new List<string> { "cc@example.com" },
            Bcc = new List<string> { "bcc@example.com" },
        };

        var uri = EmailService.BuildMailtoUri(message);

        uri.Should().Be("mailto:a@example.com,b%2Btag@example.org?subject=Hello%20%26%20welcome&body=Line%201%0ALine%202%20with%20spaces&cc=cc@example.com&bcc=bcc@example.com");
    }

    [Fact]
    public void Email_BuildMailto_EmptyMessage_IsBareScheme()
    {
        EmailService.BuildMailtoUri(new EmailMessage()).Should().Be("mailto:");
        EmailService.BuildMailtoUri(null).Should().Be("mailto:");
        EmailService.BuildMailtoUri(new EmailMessage { Subject = "Only subject" }).Should().Be("mailto:?subject=Only%20subject");
    }

    [Fact]
    public async Task Email_Compose_LaunchesXdgOpenWithMailto()
    {
        using var capture = new LaunchCapture(true);
        var email = new EmailService();
        email.IsComposeSupported.Should().BeTrue();

        await email.ComposeAsync("Subj", "Body text", "x@example.com");

        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("mailto:x@example.com?subject=Subj&body=Body%20text");
    }

    [Fact]
    public async Task Email_Compose_NullMessage_ThrowsAndLaunchFailureIsSilent()
    {
        using var capture = new LaunchCapture(false);
        var email = new EmailService();

        await email.Invoking(e => e.ComposeAsync((EmailMessage?)null)).Should().ThrowAsync<ArgumentNullException>();
        // A helper that cannot be started is not an error for the caller.
        await email.Invoking(e => e.ComposeAsync()).Should().NotThrowAsync();
        capture.Single.ArgumentList.Should().ContainSingle().Which.Should().Be("mailto:");
    }

    #endregion

    #region SmsService

    [Fact]
    public void Sms_BuildUri_JoinsRecipientsAndEncodesBody()
    {
        var uri = SmsService.BuildSmsUri(new SmsMessage("Hi there & bye", new[] { "+1 555 010 1234", "0800-123" }));
        uri.Should().Be("sms:+15550101234,0800-123?body=Hi%20there%20%26%20bye");
    }

    [Fact]
    public void Sms_BuildUri_OmitsBodyWhenEmpty()
    {
        SmsService.BuildSmsUri(new SmsMessage(null, new[] { "12345" })).Should().Be("sms:12345");
        SmsService.BuildSmsUri(new SmsMessage("only text", Array.Empty<string>())).Should().Be("sms:?body=only%20text");
    }

    [Fact]
    public async Task Sms_Compose_LaunchesXdgOpen_AndIgnoresNullMessage()
    {
        using var capture = new LaunchCapture(true);
        var sms = new SmsService();
        sms.IsComposeSupported.Should().BeTrue();

        await sms.ComposeAsync(null);
        capture.Launches.Should().BeEmpty();

        await sms.ComposeAsync(new SmsMessage("yo", new[] { "555" }));
        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("sms:555?body=yo");
    }

    #endregion

    #region PhoneDialerService

    [Theory]
    [InlineData("+1 (555) 010-1234", "tel:+1(555)010-1234")]
    [InlineData("0800 123 456", "tel:0800123456")]
    [InlineData("*100#", "tel:*100#")]
    [InlineData("555 ext 12", "tel:555ext12")]
    [InlineData("555;7", "tel:555%3B7")]
    public void PhoneDialer_BuildTelUri_KeepsPlusAndSeparators(string number, string expected)
    {
        PhoneDialerService.BuildTelUri(number).Should().Be(expected);
    }

    [Fact]
    public void PhoneDialer_Open_LaunchesXdgOpenWithTelUri()
    {
        using var capture = new LaunchCapture(true);
        var dialer = new PhoneDialerService();
        dialer.IsSupported.Should().BeTrue();

        dialer.Open("+49 30 1234567");

        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("tel:+49301234567");
    }

    [Fact]
    public void PhoneDialer_Open_EmptyNumber_Throws()
    {
        using var capture = new LaunchCapture(true);
        var dialer = new PhoneDialerService();
        dialer.Invoking(d => d.Open("")).Should().Throw<ArgumentNullException>();
        dialer.Invoking(d => d.Open(null!)).Should().Throw<ArgumentNullException>();
        capture.Launches.Should().BeEmpty();
    }

    #endregion

    #region MapService

    [Fact]
    public void Map_BuildUrl_Coordinates_UsesInvariantDecimalPoint()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var url = MapService.BuildUrl(48.8566, 2.3522, new MapLaunchOptions());
            url.Should().Be("https://www.openstreetmap.org/?mlat=48.8566&mlon=2.3522#map=15/48.8566/2.3522");
            MapService.BuildUrl(-33.9, -151.2, null).Should().Contain("mlat=-33.9&mlon=-151.2");
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(NavigationMode.Driving, "fossgis_osrm_car")]
    [InlineData(NavigationMode.Default, "fossgis_osrm_car")]
    [InlineData(NavigationMode.Walking, "fossgis_osrm_foot")]
    [InlineData(NavigationMode.Bicycling, "fossgis_osrm_bike")]
    public void Map_BuildUrl_NavigationMode_ProducesDirectionsUrl(NavigationMode mode, string engine)
    {
        var url = MapService.BuildUrl(51.5, -0.12, new MapLaunchOptions { NavigationMode = mode });
        url.Should().Be($"https://www.openstreetmap.org/directions?engine={engine}&route=;51.5,-0.12");
    }

    [Fact]
    public void Map_BuildUrl_Placemark_SearchesNonEmptyAddressParts()
    {
        var placemark = new Placemark
        {
            SubThoroughfare = "1600",
            Thoroughfare = "Pennsylvania Ave NW",
            Locality = "Washington",
            AdminArea = "DC",
            PostalCode = "20500",
            CountryName = "United States",
        };

        MapService.BuildUrl(placemark, null).Should()
            .Be("https://www.openstreetmap.org/search?query=1600%20Pennsylvania%20Ave%20NW%20Washington%20DC%2020500%20United%20States");

        // Missing parts do not leave gaps, and an empty placemark falls back to the option name.
        MapService.BuildUrl(new Placemark { Locality = "Paris", CountryName = "France" }, null).Should().EndWith("query=Paris%20France");
        MapService.BuildUrl(new Placemark(), new MapLaunchOptions { Name = "Eiffel Tower" }).Should().EndWith("query=Eiffel%20Tower");
    }

    [Fact]
    public async Task Map_Open_LaunchesXdgOpen_AndTryOpenReportsResult()
    {
        using var capture = new LaunchCapture(true);
        var map = new MapService();

        await map.OpenAsync(10.5, 20.25, new MapLaunchOptions());
        (await map.TryOpenAsync(new Placemark { Locality = "Oslo" }, new MapLaunchOptions())).Should().BeTrue();

        capture.Launches.Should().HaveCount(2);
        capture.Launches[0].FileName.Should().Be("xdg-open");
        capture.Launches[0].ArgumentList.Should().ContainSingle().Which.Should().Be("https://www.openstreetmap.org/?mlat=10.5&mlon=20.25#map=15/10.5/20.25");
        capture.Launches[1].ArgumentList.Should().ContainSingle().Which.Should().Be("https://www.openstreetmap.org/search?query=Oslo");

        capture.Result = _ => false;
        (await map.TryOpenAsync(1, 2, new MapLaunchOptions())).Should().BeFalse();
    }

    #endregion

    #region ShareService

    [Fact]
    public async Task Share_Text_OpensMailtoWithSubjectAndBody()
    {
        using var capture = new LaunchCapture(true);
        var share = new ShareService();

        await share.RequestAsync(new ShareTextRequest("Some text & more", "Title") { Subject = "Subj" });

        var psi = capture.Single;
        psi.FileName.Should().Be("xdg-open");
        psi.ArgumentList.Should().ContainSingle().Which.Should().Be("mailto:?subject=Subj&body=Some%20text%20%26%20more");
    }

    [Fact]
    public async Task Share_Uri_OpensTheUriDirectly()
    {
        using var capture = new LaunchCapture(true);
        var share = new ShareService();

        await share.RequestAsync(new ShareTextRequest { Uri = "https://example.com/x" });

        capture.Single.ArgumentList.Should().ContainSingle().Which.Should().Be("https://example.com/x");
        capture.Launches.Clear();

        await share.RequestAsync(new ShareTextRequest());
        capture.Launches.Should().BeEmpty("an empty request has nothing to share");
    }

    [Fact]
    public async Task Share_File_UsesZenityThenFallsBackToFileManager()
    {
        var dir = TempDir("share");
        try
        {
            var file = Path.Combine(dir, "doc.txt");
            File.WriteAllText(file, "x");
            var share = new ShareService();

            using (var capture = new LaunchCapture(true))
            {
                await share.RequestAsync(new ShareFileRequest("Doc", new ShareFile(file)));
                var psi = capture.Single;
                psi.FileName.Should().Be("zenity");
                psi.ArgumentList.Should().Contain("--info");
                psi.ArgumentList.Should().Contain(a => a.Contains(file));
            }

            using (var capture = new LaunchCapture(psi => psi.FileName != "zenity"))
            {
                await share.RequestAsync(new ShareFileRequest("Doc", new ShareFile(file)));
                capture.Launches.Should().HaveCount(2);
                capture.Launches[0].FileName.Should().Be("zenity");
                capture.Launches[1].FileName.Should().Be("xdg-open");
                capture.Launches[1].ArgumentList.Should().ContainSingle().Which.Should().Be(dir);
            }

            using (var capture = new LaunchCapture(true))
            {
                await share.RequestAsync(new ShareMultipleFilesRequest("Docs", new[] { new ShareFile(file), new ShareFile(file) }));
                capture.Launches.Should().HaveCount(2);
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Share_File_Validation()
    {
        using var capture = new LaunchCapture(true);
        var share = new ShareService();

        await share.Invoking(s => s.RequestAsync((ShareTextRequest)null!)).Should().ThrowAsync<ArgumentNullException>();
        await share.Invoking(s => s.RequestAsync(new ShareFileRequest())).Should().ThrowAsync<ArgumentException>();
        await share.Invoking(s => s.RequestAsync(new ShareMultipleFilesRequest())).Should().ThrowAsync<ArgumentException>();
        await share.Invoking(s => s.RequestAsync(new ShareFileRequest("x", new ShareFile("/nonexistent/file.bin"))))
            .Should().ThrowAsync<FileNotFoundException>();
        capture.Launches.Should().BeEmpty();
    }

    #endregion

    #region TextToSpeechService

    [Fact]
    public async Task TextToSpeech_GetLocales_ReturnsCurrentCulture()
    {
        var tts = new TextToSpeechService();
        var locales = (await tts.GetLocalesAsync()).ToList();

        locales.Should().NotBeEmpty();
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        var expected = string.IsNullOrEmpty(culture.Name) ? "en-US" : culture.Name;
        locales.Should().Contain(l => l.Id == expected);
        locales[0].Language.Should().NotBeNullOrEmpty();
        locales[0].Name.Should().NotBeNullOrEmpty();

        var enGb = TextToSpeechService.ToLocale(new System.Globalization.CultureInfo("en-GB"));
        enGb.Language.Should().Be("en");
        enGb.Country.Should().Be("GB");
        enGb.Id.Should().Be("en-GB");
    }

    [Fact]
    public async Task TextToSpeech_Speak_GoesThroughSeamWithTextAsSingleArgument()
    {
        var previousProbe = TextToSpeechService.ToolExists;
        using var capture = new LaunchCapture(true);
        try
        {
            TextToSpeechService.ToolExists = path => path == "/usr/bin/spd-say";
            var tts = new TextToSpeechService();

            await tts.SpeakAsync("Hello \"world\" it's me");

            var psi = capture.Single;
            psi.FileName.Should().Be("spd-say");
            psi.ArgumentList.Should().ContainSingle().Which.Should().Be("Hello \"world\" it's me");

            capture.Launches.Clear();
            TextToSpeechService.ToolExists = _ => false;
            await tts.SpeakAsync("Louder", new SpeechOptions { Pitch = 1.0f, Volume = 0.5f });

            psi = capture.Single;
            psi.FileName.Should().Be("espeak-ng");
            psi.ArgumentList.Should().Equal("-p", "50", "-a", "100", "Louder");
        }
        finally { TextToSpeechService.ToolExists = previousProbe; }
    }

    [Fact]
    public async Task TextToSpeech_Speak_EmptyTextOrCancelledToken_DoesNotLaunch()
    {
        using var capture = new LaunchCapture(true);
        var tts = new TextToSpeechService();

        await tts.SpeakAsync("");
        await tts.SpeakAsync(null!);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var speak = tts.SpeakAsync("never spoken", cancellationToken: cts.Token);
        (await Task.WhenAny(speak, Task.Delay(2000))).Should().BeSameAs(speak, "a cancelled token must return promptly");
        await speak;

        capture.Launches.Should().BeEmpty();
    }

    #endregion

    #region DeviceDisplayService

    [Fact]
    public void DeviceDisplay_MainDisplayInfo_IsSane()
    {
        var display = new DeviceDisplayService();
        var info = display.MainDisplayInfo;

        info.Width.Should().BeGreaterThan(0);
        info.Height.Should().BeGreaterThan(0);
        info.Density.Should().BeGreaterThan(0);
        Enum.IsDefined(info.Orientation).Should().BeTrue();
        info.Orientation.Should().Be(info.Width <= info.Height ? DisplayOrientation.Portrait : DisplayOrientation.Landscape);
        info.Rotation.Should().Be(DisplayRotation.Rotation0);
        info.RefreshRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DeviceDisplay_KeepScreenOn_TogglesWithoutTouchingDesktopWhenNoWindow()
    {
        using var capture = new LaunchCapture(true);
        var display = new DeviceDisplayService();

        display.KeepScreenOn.Should().BeFalse();
        display.KeepScreenOn = true;
        display.KeepScreenOn.Should().BeTrue();
        display.KeepScreenOn = false;
        display.KeepScreenOn.Should().BeFalse();

        // Headless: there is no X11 window to hand to xdg-screensaver.
        capture.Launches.Should().BeEmpty();

        var psi = DeviceDisplayService.BuildScreenSaverStartInfo(true, 0x3a00001);
        psi.FileName.Should().Be("xdg-screensaver");
        psi.ArgumentList.Should().Equal("suspend", "60817409");
        DeviceDisplayService.BuildScreenSaverStartInfo(false, 7).ArgumentList.Should().Equal("resume", "7");
    }

    [Fact]
    public void DeviceDisplay_MainDisplayInfoChanged_FiresOnRefresh()
    {
        var display = new DeviceDisplayService();
        DisplayInfoChangedEventArgs? received = null;
        display.MainDisplayInfoChanged += (_, e) => received = e;

        display.OnDisplayInfoChanged();

        received.Should().NotBeNull();
        received!.DisplayInfo.Width.Should().Be(display.MainDisplayInfo.Width);
    }

    [Theory]
    [InlineData("2", 2.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("0", null)]
    public void DeviceDisplay_ParseScaleFactor_IsCultureInvariant(string? value, double? expected)
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            DeviceDisplayService.ParseScaleFactor(value).Should().Be(expected);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    #endregion

    #region BatteryService

    [Fact]
    public void Battery_NoPowerSupplies_ReportsDesktopDefaults()
    {
        using var sysfs = new FakeSysfs();
        var battery = new BatteryService();

        battery.ChargeLevel.Should().Be(1.0);
        battery.State.Should().Be(BatteryState.Unknown);
        battery.PowerSource.Should().Be(BatteryPowerSource.AC, "a machine without a battery is wall-powered");
        battery.EnergySaverStatus.Should().Be(EnergySaverStatus.Unknown);
    }

    [Theory]
    [InlineData("Discharging", "73", 0.73, BatteryState.Discharging, BatteryPowerSource.Battery)]
    [InlineData("Charging", "5", 0.05, BatteryState.Charging, BatteryPowerSource.AC)]
    [InlineData("Full", "100", 1.0, BatteryState.Full, BatteryPowerSource.AC)]
    [InlineData("Not charging", "88", 0.88, BatteryState.NotCharging, BatteryPowerSource.Battery)]
    [InlineData("Weird", "abc", 1.0, BatteryState.Unknown, BatteryPowerSource.Battery)]
    public void Battery_ReadsSysfsBatteryNodes(string status, string capacity, double level, BatteryState state, BatteryPowerSource source)
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("Battery\n", "class", "power_supply", "BAT0", "type");
        sysfs.Write(status + "\n", "class", "power_supply", "BAT0", "status");
        sysfs.Write(capacity + "\n", "class", "power_supply", "BAT0", "capacity");

        var battery = new BatteryService();
        battery.ChargeLevel.Should().BeApproximately(level, 0.0001);
        battery.State.Should().Be(state);
        battery.PowerSource.Should().Be(source);
    }

    [Fact]
    public void Battery_SkipsNonBatterySupplies_AndUsesMainsOnline()
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("Mains\n", "class", "power_supply", "AC", "type");
        sysfs.Write("1\n", "class", "power_supply", "AC", "online");
        sysfs.Write("USB\n", "class", "power_supply", "hidpp_battery_0", "type");
        sysfs.Write("42\n", "class", "power_supply", "hidpp_battery_0", "capacity");
        sysfs.Write("Battery\n", "class", "power_supply", "BAT1", "type");
        sysfs.Write("Not charging\n", "class", "power_supply", "BAT1", "status");
        sysfs.Write("97\n", "class", "power_supply", "BAT1", "capacity");

        var battery = new BatteryService();
        battery.ChargeLevel.Should().BeApproximately(0.97, 0.0001);
        battery.State.Should().Be(BatteryState.NotCharging);
        battery.PowerSource.Should().Be(BatteryPowerSource.AC, "the mains supply reports online");
    }

    #endregion

    #region VibrationService / HapticFeedbackService

    [Fact]
    public void Vibration_NotSupportedWithoutVibratorNode_AndCallsAreNoOps()
    {
        using var sysfs = new FakeSysfs();
        var vibration = new VibrationService();

        vibration.IsSupported.Should().BeFalse();
        vibration.Invoking(v => v.Vibrate()).Should().NotThrow();
        vibration.Invoking(v => v.Vibrate(TimeSpan.FromMilliseconds(10))).Should().NotThrow();
        vibration.Invoking(v => v.Cancel()).Should().NotThrow();
        Directory.Exists(Path.Combine(sysfs.Root, "class", "leds")).Should().BeFalse();
    }

    [Fact]
    public void Vibration_WritesDurationAndActivate_ToFakeNode()
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("none\n", "class", "leds", "vibrator", "trigger");
        sysfs.Write("0", "class", "leds", "vibrator", "duration");
        sysfs.Write("0", "class", "leds", "vibrator", "activate");
        var vibration = new VibrationService();

        vibration.IsSupported.Should().BeTrue();

        vibration.Vibrate(TimeSpan.FromMilliseconds(1234.9));
        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "duration")).Should().Be("1234");
        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "activate")).Should().Be("1");

        vibration.Vibrate();
        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "duration")).Should().Be("500");

        vibration.Cancel();
        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "activate")).Should().Be("0");
    }

    [Fact]
    public void Haptic_NotSupportedWithoutVibratorNode()
    {
        using var sysfs = new FakeSysfs();
        var haptic = new HapticFeedbackService();

        haptic.IsSupported.Should().BeFalse();
        haptic.Invoking(h => h.Perform(HapticFeedbackType.Click)).Should().NotThrow();
        haptic.Invoking(h => h.Perform(HapticFeedbackType.LongPress)).Should().NotThrow();
    }

    [Theory]
    [InlineData(HapticFeedbackType.Click, "50")]
    [InlineData(HapticFeedbackType.LongPress, "200")]
    public void Haptic_Perform_WritesPulseToFakeNode(HapticFeedbackType type, string expectedDuration)
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("none\n", "class", "leds", "vibrator", "trigger");
        var haptic = new HapticFeedbackService();

        haptic.IsSupported.Should().BeTrue();
        haptic.Perform(type);

        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "duration")).Should().Be(expectedDuration);
        File.ReadAllText(sysfs.Node("class", "leds", "vibrator", "activate")).Should().Be("1");
    }

    #endregion

    #region FlashlightService

    [Fact]
    public async Task Flashlight_NotSupportedWithoutTorchLed_AndTurnOnThrows()
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("0", "class", "leds", "input3::capslock", "brightness");
        var flashlight = new FlashlightService();

        (await flashlight.IsSupportedAsync()).Should().BeFalse();
        await flashlight.Invoking(f => f.TurnOnAsync()).Should().ThrowAsync<FeatureNotSupportedException>();
        await flashlight.Invoking(f => f.TurnOffAsync()).Should().ThrowAsync<FeatureNotSupportedException>();
    }

    [Fact]
    public async Task Flashlight_TogglesBrightnessOfTorchLed()
    {
        using var sysfs = new FakeSysfs();
        sysfs.Write("0", "class", "leds", "input3::capslock", "brightness");
        sysfs.Write("0", "class", "leds", "white:torch", "brightness");
        var flashlight = new FlashlightService();

        (await flashlight.IsSupportedAsync()).Should().BeTrue();

        await flashlight.TurnOnAsync();
        File.ReadAllText(sysfs.Node("class", "leds", "white:torch", "brightness")).Should().Be("1");
        File.ReadAllText(sysfs.Node("class", "leds", "input3::capslock", "brightness")).Should().Be("0");

        await flashlight.TurnOffAsync();
        File.ReadAllText(sysfs.Node("class", "leds", "white:torch", "brightness")).Should().Be("0");
    }

    #endregion

    #region GeolocationService / GeocodingService / ContactsService

    [Fact]
    public async Task Geolocation_WithoutProvider_ReturnsNullPromptly()
    {
        using var capture = new LaunchCapture(false);
        var geo = new GeolocationService();

        (await geo.GetLastKnownLocationAsync()).Should().BeNull();

        var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(1));
        var locate = geo.GetLocationAsync(request);
        (await Task.WhenAny(locate, Task.Delay(3000))).Should().BeSameAs(locate);
        (await locate).Should().BeNull();

        capture.Single.FileName.Should().Be("gdbus");
        capture.Single.Arguments.Should().Contain("org.freedesktop.GeoClue2");
    }

    [Fact]
    public async Task Geolocation_CancelledToken_ReturnsNullWithoutLaunching()
    {
        using var capture = new LaunchCapture(true);
        var geo = new GeolocationService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        (await geo.GetLocationAsync(new GeolocationRequest(), cts.Token)).Should().BeNull();
        capture.Launches.Should().BeEmpty();
    }

    [Fact]
    public async Task Geolocation_Listening_IsNotSupported()
    {
        var geo = new GeolocationService();
        geo.IsListening.Should().BeFalse();
        geo.IsListeningForeground.Should().BeFalse();
        geo.IsEnabled.Should().BeTrue();
        (await geo.StartListeningForegroundAsync(new GeolocationListeningRequest())).Should().BeFalse();
        geo.Invoking(g => g.StopListeningForeground()).Should().NotThrow();
        geo.IsListeningForeground.Should().BeFalse();
    }

    [Fact]
    public async Task Geocoding_ReturnsEmptyResults()
    {
        var geocoding = new GeocodingService();
        (await geocoding.GetPlacemarksAsync(48.85, 2.35)).Should().BeEmpty();
        (await geocoding.GetLocationsAsync("Paris, France")).Should().BeEmpty();
    }

    [Fact]
    public async Task Contacts_PickReturnsNull_AndGetAllIsEmpty()
    {
        var contacts = new ContactsService();
        (await contacts.PickContactAsync()).Should().BeNull();
        (await contacts.GetAllAsync()).Should().BeEmpty();
    }

    #endregion

    #region ScreenshotService

    [Fact]
    public async Task Screenshot_WithoutRootView_IsUnsupportedAndCaptureReturnsNull()
    {
        var screenshot = new ScreenshotService(() => null);
        screenshot.IsCaptureSupported.Should().BeFalse();
        (await screenshot.CaptureAsync()).Should().BeNull();

        var empty = new ScreenshotService(() => new SkiaBoxView { Bounds = new Rect(0, 0, 0, 0) });
        empty.IsCaptureSupported.Should().BeFalse();
        (await empty.CaptureAsync()).Should().BeNull();

        var throwing = new ScreenshotService(() => throw new InvalidOperationException("no app"));
        throwing.IsCaptureSupported.Should().BeFalse();
        (await throwing.CaptureAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Screenshot_RendersRootViewToPngAndJpeg()
    {
        var root = new SkiaBoxView { Color = Colors.Red, Bounds = new Rect(0, 0, 40, 30) };
        var screenshot = new ScreenshotService(() => root, () => 2f);

        screenshot.IsCaptureSupported.Should().BeTrue();
        var result = await screenshot.CaptureAsync();

        result.Should().NotBeNull();
        result!.Width.Should().Be(80, "the capture is at physical pixels");
        result.Height.Should().Be(60);

        using var png = await result.OpenReadAsync(ScreenshotFormat.Png);
        var pngBytes = new byte[8];
        png.Read(pngBytes, 0, 8).Should().Be(8);
        pngBytes.Should().StartWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        using var jpeg = new MemoryStream();
        await result.CopyToAsync(jpeg, ScreenshotFormat.Jpeg, 80);
        jpeg.ToArray().Take(3).Should().Equal(new byte[] { 0xFF, 0xD8, 0xFF });

        using var decoded = SkiaSharp.SKBitmap.Decode(((MemoryStream)await result.OpenReadAsync()).ToArray());
        decoded.GetPixel(10, 10).Should().Be(SkiaSharp.SKColors.Red);
    }

    [Fact]
    public void Screenshot_DefaultCtor_HeadlessIsUnsupported()
    {
        // No LinuxApplication in this test process, so the default provider has no root view.
        var screenshot = new ScreenshotService();
        screenshot.Invoking(s => s.IsCaptureSupported).Should().NotThrow();
    }

    #endregion

    #region FilePickerService / MediaPickerService

    private static FilePickerFileType LinuxFileType(params string[] extensions)
        => new(new Dictionary<DevicePlatform, IEnumerable<string>> { { DevicePlatform.Create("Linux"), extensions } });

    [Fact]
    public void FilePicker_NormalizeExtensions_MapsMixedEntriesToDottedExtensions()
    {
        PortalFilePickerService.NormalizeExtensions(new[] { "png", ".jpg", "*.gif", "image/png", "public.png", " .PNG ", "*", "", "*.*" })
            .Should().Equal(".png", ".jpg", ".gif");
        PortalFilePickerService.NormalizeExtensions(null).Should().BeEmpty();
    }

    [Fact]
    public void FilePicker_FileTypes_ResolvedFromLinuxEntry_OrFromAnyPlatform()
    {
        // The portable DeviceInfo reports "Unknown" in this process, so Value throws;
        // the dictionary is read directly and the Linux entry wins.
        var linux = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "image/png" } },
            { DevicePlatform.WinUI, new[] { ".bmp" } },
            { DevicePlatform.Create("Linux"), new[] { "png", "svg" } },
        });
        PortalFilePickerService.GetExtensionsFromFileType(linux).Should().Equal(".png", ".svg");

        var other = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "application/pdf" } },
            { DevicePlatform.WinUI, new[] { ".pdf" } },
            { DevicePlatform.iOS, new[] { "com.adobe.pdf" } },
        });
        PortalFilePickerService.GetExtensionsFromFileType(other).Should().Equal(".pdf", ".com.adobe.pdf");

        PortalFilePickerService.GetExtensionsFromFileType(null).Should().BeEmpty();
    }

    [Fact]
    public void FilePicker_ZenityArguments_CarryTitleFiltersAndMultiSelect()
    {
        var options = new PickOptions { PickerTitle = "Pick \"one\"", FileTypes = LinuxFileType("txt", ".md") };

        FilePickerService.BuildZenityArguments(options, multiple: false)
            .Should().Be("--file-selection --title=\"Pick \\\"one\\\"\" --file-filter='*.txt' --file-filter='*.md'");
        FilePickerService.BuildZenityArguments(null, multiple: true)
            .Should().Be("--file-selection --multiple --separator='|'");
    }

    [Fact]
    public void FilePicker_KdialogArguments_CarryTitleFiltersAndMultiSelect()
    {
        var options = new PickOptions { PickerTitle = "Docs", FileTypes = LinuxFileType("pdf") };

        FilePickerService.BuildKdialogArguments(options, multiple: true)
            .Should().Be("--multiple --getopenfilename . \"*.pdf\" --title \"Docs\"");
        FilePickerService.BuildKdialogArguments(new PickOptions(), multiple: false)
            .Should().Be("--getopenfilename .");
    }

    [Fact]
    public void FilePicker_PortalFilterArgs_AreGVariantGlobFilters()
    {
        PortalFilePickerService.BuildPortalFilterArgs(LinuxFileType("png", "jpg"))
            .Should().Be("[('Files', [(uint32 0, '*.png'), (uint32 0, '*.jpg')])]");
        PortalFilePickerService.BuildPortalFilterArgs((FilePickerFileType?)null).Should().BeNull();
        PortalFilePickerService.BuildZenityFilter("Images", new[] { ".png", ".gif" }).Should().Be("Images | *.png *.gif");
    }

    [Fact]
    public void FilePicker_ParseRequestPath_ExtractsPortalObjectPath()
    {
        PortalFilePickerService.ParseRequestPath("(objectpath '/org/freedesktop/portal/desktop/request/1_23/t1',)")
            .Should().Be("/org/freedesktop/portal/desktop/request/1_23/t1");
        PortalFilePickerService.ParseRequestPath("Error: no reply").Should().BeNull();
        PortalFilePickerService.ParseRequestPath("").Should().BeNull();
        PortalFilePickerService.EscapeForShell("a \"b\" 'c'").Should().Be("a \\\"b\\\" \\'c\\'");
    }

    [Fact]
    public void MediaPicker_PhotoAndVideo_UseZenityWithMediaFilters()
    {
        var photo = MediaPickerService.BuildPickerStartInfo(new MediaPickerOptions { Title = "Choose" }, MediaPickerService.ImageExtensions, "Images", "Select a photo");
        photo.FileName.Should().Be("zenity");
        photo.ArgumentList.Should().HaveCount(3);
        photo.ArgumentList[0].Should().Be("--file-selection");
        photo.ArgumentList[1].Should().Be("--title=Choose");
        photo.ArgumentList[2].Should().StartWith("--file-filter=Images | *.png *.jpg *.jpeg");

        var video = MediaPickerService.BuildPickerStartInfo(null, MediaPickerService.VideoExtensions, "Videos", "Select a video");
        video.ArgumentList[1].Should().Be("--title=Select a video");
        video.ArgumentList[2].Should().Be("--file-filter=" + PortalFilePickerService.BuildZenityFilter("Videos", MediaPickerService.VideoExtensions));
        video.ArgumentList[2].Should().Contain("*.mp4").And.Contain("*.webm");
    }

    [Fact]
    public async Task MediaPicker_CaptureIsUnsupported()
    {
        var picker = new MediaPickerService();
        picker.IsCaptureSupported.Should().BeFalse();
        (await picker.CapturePhotoAsync()).Should().BeNull();
        (await picker.CaptureVideoAsync()).Should().BeNull();
    }

    #endregion

    #region AppActionsService

    [Fact]
    public async Task AppActions_SetAndGet_RoundTrip_AndActivation()
    {
        var service = new AppActionsService();
        service.IsSupported.Should().BeTrue();
        (await service.GetAsync()).Should().BeEmpty();

        var actions = new[]
        {
            new AppAction("new-window", "New Window", "Opens a window", "icon"),
            new AppAction("compose", "Compose"),
        };
        await service.SetAsync(actions);

        var stored = (await service.GetAsync()).ToList();
        stored.Select(a => a.Id).Should().Equal("new-window", "compose");
        stored[0].Title.Should().Be("New Window");
        stored[0].Subtitle.Should().Be("Opens a window");

        AppAction? activated = null;
        service.AppActionActivated += (_, e) => activated = e.AppAction;
        service.HandleActionArgument("compose");
        activated.Should().NotBeNull();
        activated!.Id.Should().Be("compose");

        activated = null;
        service.HandleActionArgument("missing");
        activated.Should().BeNull();

        await service.Invoking(s => s.SetAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        await service.SetAsync(Array.Empty<AppAction>());
        (await service.GetAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AppActions_CreateDesktopFile_WritesActionsUnderXdgDataHome()
    {
        var dataHome = TempDir("xdgdata-actions");
        var previous = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);
            var service = new AppActionsService();
            await service.SetAsync(new[]
            {
                new AppAction("new-window", "New Window", "Opens a window"),
                new AppAction("compose", "Compose"),
            });

            service.CreateDesktopFile("My App", "/opt/myapp/MyApp");

            var path = Path.Combine(dataHome, "applications", "my-app.desktop");
            AppActionsService.DesktopFilesPath.Should().Be(Path.Combine(dataHome, "applications"));
            File.Exists(path).Should().BeTrue();

            var content = File.ReadAllText(path);
            content.Should().StartWith("[Desktop Entry]\nType=Application\nName=My App\nExec=/opt/myapp/MyApp %U\n");
            content.Should().Contain("Actions=new-window;compose;\n");
            content.Should().Contain("[Desktop Action new-window]\nName=New Window\nComment=Opens a window\nExec=/opt/myapp/MyApp --action=new-window\n");
            content.Should().Contain("[Desktop Action compose]\nName=Compose\nExec=/opt/myapp/MyApp --action=compose\n");
            content.Should().NotContain("Icon=", "no icon path was given");
            (File.GetUnixFileMode(path) & UnixFileMode.UserExecute).Should().Be(UnixFileMode.UserExecute);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previous);
            Directory.Delete(dataHome, true);
        }
    }

    [Fact]
    public void AppActions_DesktopFileContent_WithoutActions_HasNoActionSections()
    {
        var service = new AppActionsService();
        var content = service.GenerateDesktopFileContent("Plain", "/usr/bin/plain", "/nonexistent/icon.png");
        content.Should().NotContain("Actions=");
        content.Should().NotContain("[Desktop Action");
        content.Should().NotContain("Icon=", "a missing icon file is skipped");
        content.Should().Contain("Terminal=false");
        AppActionsService.DesktopFileNameFor("My Cool App").Should().Be("my-cool-app.desktop");
    }

    #endregion

    #region AppInfoService settings launch

    [Fact]
    public void AppInfo_ShowSettingsUI_PrefersControlCenterThenXdgOpen()
    {
        var info = AppInfoService.Instance;

        using (var capture = new LaunchCapture(true))
        {
            info.ShowSettingsUI();
            capture.Single.FileName.Should().Be("gnome-control-center");
        }

        using (var capture = new LaunchCapture(false))
        {
            info.Invoking(i => i.ShowSettingsUI()).Should().NotThrow();
            capture.Launches.Select(p => p.FileName).Should().Equal("gnome-control-center", "xdg-open");
            capture.Launches[1].ArgumentList.Should().Equal("x-settings:");
        }
    }

    #endregion

    #region Per-sensor unsupported checks

    [Fact] public void Accelerometer_IsUnsupported() => AssertUnsupported(new UnsupportedAccelerometer(), s => s.Start(SensorSpeed.Default), "Accelerometer");
    [Fact] public void Barometer_IsUnsupported() => AssertUnsupported(new UnsupportedBarometer(), s => s.Start(SensorSpeed.Default), "Barometer");
    [Fact] public void Compass_IsUnsupported() => AssertUnsupported(new UnsupportedCompass(), s => s.Start(SensorSpeed.Default), "Compass");
    [Fact] public void Gyroscope_IsUnsupported() => AssertUnsupported(new UnsupportedGyroscope(), s => s.Start(SensorSpeed.Default), "Gyroscope");
    [Fact] public void Magnetometer_IsUnsupported() => AssertUnsupported(new UnsupportedMagnetometer(), s => s.Start(SensorSpeed.Default), "Magnetometer");
    [Fact] public void OrientationSensor_IsUnsupported() => AssertUnsupported(new UnsupportedOrientationSensor(), s => s.Start(SensorSpeed.Default), "OrientationSensor");

    private static void AssertUnsupported<T>(T sensor, Action<T> start, string name)
    {
        var isSupported = (bool)typeof(T).GetProperty("IsSupported")!.GetValue(sensor)!;
        var isMonitoring = (bool)typeof(T).GetProperty("IsMonitoring")!.GetValue(sensor)!;
        isSupported.Should().BeFalse();
        isMonitoring.Should().BeFalse();
        sensor.Invoking(s => start(s)).Should().Throw<FeatureNotSupportedException>().WithMessage($"*{name}*");
        typeof(T).GetMethod("Stop", Type.EmptyTypes)!.Invoke(sensor, null);
    }

    #endregion
}
