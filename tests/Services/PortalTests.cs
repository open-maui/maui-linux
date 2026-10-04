// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Microsoft.Maui.Storage;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// A scriptable <see cref="IDesktopPortal"/>: per-interface versions, recorded
/// calls and canned responses. Nothing here touches D-Bus.
/// </summary>
internal sealed class FakeDesktopPortal : IDesktopPortal
{
    public Dictionary<string, uint> Versions { get; } = new();
    public List<(string Method, string? Arg, IDictionary<string, object>? Options)> Calls { get; } = new();

    public Func<PortalResponse> NextResponse { get; set; } = () => new PortalResponse(PortalResponseCode.Success, null);
    public bool ThrowUnavailable { get; set; }
    public Dictionary<(string, string), object?> Settings { get; } = new();
    public Action<string, string, object>? SettingHandler { get; private set; }
    public Action<string, string>? ActionHandler { get; private set; }
    public byte[]? SecretToWrite { get; set; }
    public IReadOnlyDictionary<string, object>? Location { get; set; }
    public int OpenInhibitions { get; private set; }

    public Task<uint> GetVersionAsync(string portalInterface, CancellationToken cancellationToken = default)
        => Task.FromResult(Versions.TryGetValue(portalInterface, out var v) ? v : 0u);

    private Task<PortalResponse> Respond(string method, string? arg, IDictionary<string, object>? options)
    {
        Calls.Add((method, arg, options));
        if (ThrowUnavailable)
            return Task.FromException<PortalResponse>(new PortalUnavailableException("fake: unavailable"));
        return Task.FromResult(NextResponse());
    }

    public Task<PortalResponse> FileChooserOpenFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("OpenFile", title, options);
    public Task<PortalResponse> FileChooserSaveFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("SaveFile", title, options);
    public Task<PortalResponse> OpenUriAsync(string parentWindow, string uri, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("OpenURI", uri, options);
    public Task<PortalResponse> OpenFileAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken)
    {
        file.IsInvalid.Should().BeFalse("OpenFile must receive an open descriptor");
        return Respond("OpenURI.OpenFile", null, options);
    }
    public Task<PortalResponse> OpenDirectoryAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("OpenDirectory", null, options);
    public Task<PortalResponse> ScreenshotAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("Screenshot", null, options);

    public Task<PortalResponse> RetrieveSecretAsync(SafeHandle writeEnd, IDictionary<string, object> options, CancellationToken cancellationToken)
    {
        if (SecretToWrite != null && !ThrowUnavailable)
        {
            // What the backend does: write the secret into the passed descriptor.
            using var dup = new FileStream(new SafeFileHandle(writeEnd.DangerousGetHandle(), ownsHandle: false), FileAccess.Write, 1, false);
            dup.Write(SecretToWrite);
            dup.Flush();
        }
        return Respond("RetrieveSecret", null, options);
    }

    public Task<PortalResponse> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken) => Respond("RequestBackground", null, options);

    public Task<IAsyncDisposable> InhibitAsync(string parentWindow, PortalInhibitFlags flags, IDictionary<string, object> options, CancellationToken cancellationToken)
    {
        Calls.Add(("Inhibit", flags.ToString(), options));
        if (ThrowUnavailable)
            return Task.FromException<IAsyncDisposable>(new PortalUnavailableException("fake"));
        OpenInhibitions++;
        return Task.FromResult<IAsyncDisposable>(new Handle(this));
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly FakeDesktopPortal _owner;
        public Handle(FakeDesktopPortal owner) => _owner = owner;
        public ValueTask DisposeAsync()
        {
            _owner.OpenInhibitions--;
            _owner.Calls.Add(("Request.Close", null, null));
            return ValueTask.CompletedTask;
        }
    }

    public Task<object?> ReadSettingAsync(string @namespace, string key, CancellationToken cancellationToken)
    {
        if (ThrowUnavailable)
            return Task.FromException<object?>(new PortalUnavailableException("fake"));
        return Task.FromResult(Settings.TryGetValue((@namespace, key), out var v) ? v : null);
    }

    public Task<IDisposable> WatchSettingChangedAsync(Action<string, string, object> handler)
    {
        SettingHandler = handler;
        return Task.FromResult<IDisposable>(new Nothing());
    }

    public Task AddNotificationAsync(string id, IDictionary<string, object> notification, CancellationToken cancellationToken)
    {
        Calls.Add(("AddNotification", id, notification));
        return Task.CompletedTask;
    }

    public Task RemoveNotificationAsync(string id, CancellationToken cancellationToken)
    {
        Calls.Add(("RemoveNotification", id, null));
        return Task.CompletedTask;
    }

    public Task<IDisposable> WatchNotificationActionInvokedAsync(Action<string, string> handler)
    {
        ActionHandler = handler;
        return Task.FromResult<IDisposable>(new Nothing());
    }

    public Task<IReadOnlyDictionary<string, object>?> GetLocationAsync(IDictionary<string, object> sessionOptions, CancellationToken cancellationToken)
    {
        Calls.Add(("Location", null, sessionOptions));
        if (ThrowUnavailable)
            return Task.FromException<IReadOnlyDictionary<string, object>?>(new PortalUnavailableException("fake"));
        return Task.FromResult(Location);
    }

    /// <summary>The handler of the open location watch, to push updates; null when none is open.</summary>
    public Action<IReadOnlyDictionary<string, object>>? LocationWatcher { get; private set; }

    /// <summary>WatchLocationAsync answers as if the session were denied.</summary>
    public bool DenyLocationWatch { get; set; }

    public Task<IAsyncDisposable?> WatchLocationAsync(IDictionary<string, object> sessionOptions, Action<IReadOnlyDictionary<string, object>> onUpdate, CancellationToken cancellationToken)
    {
        Calls.Add(("WatchLocation", null, sessionOptions));
        if (ThrowUnavailable)
            return Task.FromException<IAsyncDisposable?>(new PortalUnavailableException("fake"));
        if (DenyLocationWatch)
            return Task.FromResult<IAsyncDisposable?>(null);
        LocationWatcher = onUpdate;
        return Task.FromResult<IAsyncDisposable?>(new StopWatch(this));
    }

    private sealed class StopWatch(FakeDesktopPortal owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.LocationWatcher = null;
            owner.Calls.Add(("StopLocation", null, null));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>Scriptable notification server.</summary>
internal sealed class FakeNotificationServer : INotificationServer
{
    public List<(string Summary, string Body, string[] Actions, IDictionary<string, object> Hints, int Expire, string Icon)> Notified { get; } = new();
    public List<uint> Closed { get; } = new();
    public uint NextId { get; set; } = 700;
    public bool Unavailable { get; set; }
    public Action<uint, string>? ActionInvoked { get; private set; }
    public Action<uint, uint>? NotificationClosed { get; private set; }

    public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout, CancellationToken cancellationToken)
    {
        if (Unavailable)
            return Task.FromException<uint>(new PortalUnavailableException("fake"));
        Notified.Add((summary, body, actions, hints, expireTimeout, appIcon));
        return Task.FromResult(NextId++);
    }

    public Task CloseNotificationAsync(uint id, CancellationToken cancellationToken)
    {
        Closed.Add(id);
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(!Unavailable);

    public Task<IDisposable> WatchAsync(Action<uint, string> actionInvoked, Action<uint, uint> closed)
    {
        ActionInvoked = actionInvoked;
        NotificationClosed = closed;
        return Task.FromResult<IDisposable>(new Nothing());
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose() { }
    }
}

public class PortalRequestTests
{
    [Fact]
    public void RequestPath_IsPredictedFromUniqueNameAndToken()
    {
        PortalRequestPath.SenderElement(":1.42").Should().Be("1_42");
        PortalRequestPath.ForRequest(":1.35362", "openmaui_1_7")
            .Should().Be("/org/freedesktop/portal/desktop/request/1_35362/openmaui_1_7");
        PortalRequestPath.ForSession(":1.2", "loc")
            .Should().Be("/org/freedesktop/portal/desktop/session/1_2/loc");
        FluentActions.Invoking(() => PortalRequestPath.SenderElement("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tokens_AreUniqueValidPathElements()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => PortalRequestPath.NewToken()).ToList();
        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().OnlyContain(t => PortalRequestPath.IsValidToken(t));
        PortalRequestPath.IsValidToken("a-b").Should().BeFalse();
        PortalRequestPath.IsValidToken("").Should().BeFalse();
    }

    [Fact]
    public void Response_MapsCodesAndReadsResults()
    {
        var ok = PortalResponse.FromSignal(0, new Dictionary<string, object>
        {
            ["uris"] = new[] { "file:///tmp/a%20b.txt" },
            ["background"] = true,
            ["count"] = 3u,
            ["uri"] = "file:///tmp/shot.png",
        });
        ok.Code.Should().Be(PortalResponseCode.Success);
        ok.GetStrings("uris").Should().Equal("file:///tmp/a%20b.txt");
        ok.GetBoolean("background").Should().BeTrue();
        ok.GetUInt32("count").Should().Be(3u);
        ok.GetString("uri").Should().Be("file:///tmp/shot.png");
        ok.GetStrings("missing").Should().BeEmpty();
        ok.GetBoolean("missing").Should().BeNull();

        PortalResponse.FromSignal(1, null).Code.Should().Be(PortalResponseCode.Cancelled);
        PortalResponse.FromSignal(2, null).Code.Should().Be(PortalResponseCode.Other);
        PortalResponse.FromSignal(99, null).Code.Should().Be(PortalResponseCode.Other);
        PortalResponse.FromSignal(1, null).Results.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0u, (int)PortalOutcome.Completed, false)]
    [InlineData(1u, (int)PortalOutcome.Cancelled, false)]
    [InlineData(2u, (int)PortalOutcome.Failed, true)]
    public void Outcome_DecidesFallback(uint code, int expectedOutcome, bool fallBack)
    {
        var expected = (PortalOutcome)expectedOutcome;
        var outcome = PortalResponse.FromSignal(code, null).ToOutcome();
        outcome.Should().Be(expected);
        outcome.ShouldFallBack().Should().Be(fallBack);
        PortalOutcome.Unavailable.ShouldFallBack().Should().BeTrue();
    }

    [Theory]
    [InlineData(null, (int)PortalMode.Auto)]
    [InlineData("", (int)PortalMode.Auto)]
    [InlineData("off", (int)PortalMode.Off)]
    [InlineData(" 0 ", (int)PortalMode.Off)]
    [InlineData("prefer", (int)PortalMode.Prefer)]
    [InlineData("TRUE", (int)PortalMode.Prefer)]
    [InlineData("something", (int)PortalMode.Auto)]
    public void Mode_ParsesEnvironmentValue(string? value, int expected)
        => DesktopPortal.ParseMode(value).Should().Be((PortalMode)expected);

    [Theory]
    [InlineData((int)PortalUse.Always, (int)PortalMode.Auto, false, true)]
    [InlineData((int)PortalUse.SandboxedOrPreferred, (int)PortalMode.Auto, false, false)]
    [InlineData((int)PortalUse.SandboxedOrPreferred, (int)PortalMode.Auto, true, true)]
    [InlineData((int)PortalUse.SandboxedOrPreferred, (int)PortalMode.Prefer, false, true)]
    [InlineData((int)PortalUse.Always, (int)PortalMode.Off, true, false)]
    public void Policy_DecidesWhenToTryThePortal(int use, int mode, bool sandboxed, bool expected)
        => DesktopPortal.ShouldTry((PortalUse)use, (PortalMode)mode, sandboxed).Should().Be(expected);

    [Fact]
    public void TestAssembly_RunsWithPortalsOff()
    {
        DesktopPortal.Mode.Should().Be(PortalMode.Off);
        DesktopPortal.Current.Should().BeSameAs(NullDesktopPortal.Instance);
        DesktopPortal.ShouldTry(PortalUse.Always).Should().BeFalse();
    }

    [Fact]
    public async Task NullPortal_IsUnavailableEverywhere()
    {
        var portal = NullDesktopPortal.Instance;
        foreach (var iface in PortalInterfaces.All)
            (await portal.GetVersionAsync(iface)).Should().Be(0u);
        await portal.Invoking(p => p.OpenUriAsync("", "https://example.com", new Dictionary<string, object>(), default))
            .Should().ThrowAsync<PortalUnavailableException>();
        await portal.Invoking(p => p.ReadSettingAsync("a", "b", default)).Should().ThrowAsync<PortalUnavailableException>();
    }

    [Fact]
    public void ParentWindow_UsesX11PrefixOrEmpty()
    {
        PortalParentWindow.ForX11(0x3a00001).Should().Be("x11:3a00001");
        PortalParentWindow.ForX11(0).Should().BeEmpty();
    }
}

public class PortalOptionsTests
{
    [Fact]
    public void HandleToken_IsAddedToACopy()
    {
        var original = new Dictionary<string, object> { ["modal"] = true };
        var withToken = PortalOptions.WithHandleToken(original, "t1");
        withToken["handle_token"].Should().Be("t1");
        withToken["modal"].Should().Be(true);
        original.Should().NotContainKey("handle_token");
        PortalOptions.WithHandleToken(null, "t2").Should().ContainKey("handle_token");
    }

    [Fact]
    public void FileChooserOpen_BuildsFiltersMultipleAndFolder()
    {
        var filter = PortalFileFilter.FromExtensions("Images", new[] { ".png", ".jpg" })!;
        var options = PortalOptions.FileChooserOpen(new PortalFileChooserRequest
        {
            Title = "Pick",
            Multiple = true,
            AcceptLabel = "Choose",
            Filters = new[] { filter },
            CurrentFolder = "/home/u/Pictures",
        });

        options["multiple"].Should().Be(true);
        options["modal"].Should().Be(true);
        options["accept_label"].Should().Be("Choose");
        options.Should().NotContainKey("directory");

        var filters = options["filters"].Should().BeOfType<(string, (uint, string)[])[]>().Subject;
        filters.Should().ContainSingle();
        filters[0].Item1.Should().Be("Images");
        filters[0].Item2.Should().Equal((0u, "*.png"), (0u, "*.jpg"));
        var current = options["current_filter"].Should().BeOfType<(string, (uint, string)[])>().Subject;
        current.Item1.Should().Be("Images");
        current.Item2.Should().Equal(filters[0].Item2);

        var folder = options["current_folder"].Should().BeOfType<byte[]>().Subject;
        folder[^1].Should().Be(0, "current_folder is a NUL-terminated byte string");
        System.Text.Encoding.UTF8.GetString(folder, 0, folder.Length - 1).Should().Be("/home/u/Pictures");
    }

    [Fact]
    public void FileChooserOpen_WithoutFiltersOmitsThem()
    {
        var options = PortalOptions.FileChooserOpen(new PortalFileChooserRequest { Directory = true });
        options.Should().NotContainKey("filters");
        options.Should().NotContainKey("current_filter");
        options.Should().NotContainKey("current_folder");
        options["directory"].Should().Be(true);
        options["multiple"].Should().Be(false);
    }

    [Fact]
    public void FileChooserSave_CarriesCurrentName()
    {
        var options = PortalOptions.FileChooserSave(new PortalFileChooserRequest { CurrentName = "report.pdf" });
        options["current_name"].Should().Be("report.pdf");
        options.Should().NotContainKey("multiple");
    }

    [Fact]
    public void Filter_WithMimeTypes_UsesTypeOne()
    {
        new PortalFileFilter("Text", new[] { "*.txt" }, new[] { "text/plain" }).ToVariant().Item2
            .Should().Equal((0u, "*.txt"), (1u, "text/plain"));
        PortalFileFilter.FromExtensions("None", Array.Empty<string>()).Should().BeNull();
    }

    [Fact]
    public void Uris_BecomeLocalPaths()
    {
        PortalOptions.UrisToLocalPaths(new[]
        {
            "file:///tmp/a%20b.txt",
            "file:///run/user/1000/doc/abc/r%C3%A9sum%C3%A9.pdf",
            "https://example.com/x",
            "",
            "/plain/path",
        }).Should().Equal("/tmp/a b.txt", "/run/user/1000/doc/abc/résumé.pdf", "/plain/path");
    }

    [Fact]
    public void OpenUri_OptionsOnlyCarryWhatIsSet()
    {
        PortalOptions.OpenUri().Should().BeEmpty();
        var options = PortalOptions.OpenUri(ask: true, writable: true, activationToken: "tok");
        options["ask"].Should().Be(true);
        options["writable"].Should().Be(true);
        options["activation_token"].Should().Be("tok");
    }

    [Fact]
    public void Inhibit_Background_Screenshot_Options()
    {
        PortalOptions.Inhibit("Playing video")["reason"].Should().Be("Playing video");
        PortalOptions.Inhibit(null).Should().BeEmpty();

        var bg = PortalOptions.Background("Sync", autostart: true, new[] { "/app/bin/x", "--tray" }, dbusActivatable: false);
        bg["reason"].Should().Be("Sync");
        bg["autostart"].Should().Be(true);
        bg["dbus-activatable"].Should().Be(false);
        bg["commandline"].Should().BeEquivalentTo(new[] { "/app/bin/x", "--tray" });
        PortalOptions.Background(null, false, null, false).Should().NotContainKeys("reason", "commandline");

        var shot = PortalOptions.Screenshot(interactive: false);
        shot["interactive"].Should().Be(false);
        shot["modal"].Should().Be(true);
        ((uint)PortalInhibitFlags.Idle).Should().Be(8u);
    }

    [Fact]
    public void PortalNotification_MapsPriorityIconAndButtons()
    {
        var dict = PortalOptions.Notification(
            new NotificationOptions { Title = "T", Message = "M", Urgency = NotificationUrgency.Critical, IconName = "dialog-information" },
            new Dictionary<string, string> { ["reply"] = "Reply" });

        dict["title"].Should().Be("T");
        dict["body"].Should().Be("M");
        dict["priority"].Should().Be("urgent");
        var icon = dict["icon"].Should().BeOfType<(string, object)>().Subject;
        icon.Item1.Should().Be("themed");
        icon.Item2.Should().BeEquivalentTo(new[] { "dialog-information" });
        var buttons = dict["buttons"].Should().BeAssignableTo<IDictionary<string, object>[]>().Subject;
        buttons.Should().ContainSingle();
        buttons[0]["label"].Should().Be("Reply");
        buttons[0]["action"].Should().Be("app.reply");
        PortalOptions.ActionKeyFromPortal("app.reply").Should().Be("reply");
        PortalOptions.ActionKeyFromPortal("other").Should().Be("other");

        PortalOptions.Notification(new NotificationOptions { Urgency = NotificationUrgency.Low }, null)["priority"].Should().Be("low");
    }

    [Fact]
    public void NotifyHintsAndActions_MatchNotifySendFlags()
    {
        var hints = PortalOptions.NotifyHints(new NotificationOptions { Urgency = NotificationUrgency.Critical, Category = "im.received", IsTransient = true });
        hints["urgency"].Should().Be((byte)2);
        hints["category"].Should().Be("im.received");
        hints["transient"].Should().Be(true);
        PortalOptions.NotifyHints(new NotificationOptions())["urgency"].Should().Be((byte)1);

        PortalOptions.NotifyActions(new Dictionary<string, string> { ["a"] = "Accept", ["d"] = "Decline" })
            .Should().Equal("a", "Accept", "d", "Decline");
        PortalOptions.NotifyActions(null).Should().BeEmpty();
    }

    [Fact]
    public void LocationSession_ClampsAccuracy()
    {
        var options = PortalOptions.LocationSession(9, "s1");
        options["accuracy"].Should().Be(5u);
        options["session_handle_token"].Should().Be("s1");
        PortalLocationFix.AccuracyFor(GeolocationAccuracy.Lowest).Should().Be(1u);
        PortalLocationFix.AccuracyFor(GeolocationAccuracy.Best).Should().Be(5u);
        PortalLocationFix.AccuracyFor(GeolocationAccuracy.Default).Should().Be(4u);
    }
}

public class PortalSettingsParsingTests
{
    [Theory]
    [InlineData(0u, (uint)PortalColorScheme.NoPreference)]
    [InlineData(1u, (uint)PortalColorScheme.PreferDark)]
    [InlineData(2u, (uint)PortalColorScheme.PreferLight)]
    [InlineData(7u, (uint)PortalColorScheme.NoPreference)]
    public void ColorScheme_Parses(uint raw, uint expected)
        => PortalAppearance.ParseColorScheme(raw).Should().Be((PortalColorScheme)expected);

    [Fact]
    public void ColorScheme_MissingOrWrongTypeIsNull()
    {
        PortalAppearance.ParseColorScheme(null).Should().BeNull();
        PortalAppearance.ParseColorScheme("dark").Should().BeNull();
        PortalAppearance.ParseColorScheme(1).Should().Be(PortalColorScheme.PreferDark);
    }

    [Fact]
    public void AccentColor_ParsesTupleAndArrayAndRejectsOutOfRange()
    {
        // Real value read from xdg-desktop-portal-kde (Breeze blue).
        PortalAppearance.ParseAccentColor((0.239215686917305, 0.6823529601097107, 0.9137254953384399))
            .Should().Be(((byte)61, (byte)174, (byte)233));
        PortalAppearance.ParseAccentColor(new object[] { 1.0, 0.0, 0.5 }).Should().Be(((byte)255, (byte)0, (byte)128));
        PortalAppearance.ParseAccentColor((-1.0, 0.5, 0.5)).Should().BeNull("out-of-range components mean unset");
        PortalAppearance.ParseAccentColor((0.1, 0.2)).Should().BeNull();
        PortalAppearance.ParseAccentColor(null).Should().BeNull();
    }

    [Fact]
    public void ThemeFromPortal_OnlyDecidesOnAPreference()
    {
        SystemThemeService.ThemeFromPortal(PortalColorScheme.PreferDark).Should().Be(SystemTheme.Dark);
        SystemThemeService.ThemeFromPortal(PortalColorScheme.PreferLight).Should().Be(SystemTheme.Light);
        SystemThemeService.ThemeFromPortal(PortalColorScheme.NoPreference).Should().BeNull();
        SystemThemeService.ThemeFromPortal(null).Should().BeNull();
    }

    [Fact]
    public async Task HiDpi_ReadsGnomeInterfaceScalingThroughSettings()
    {
        var portal = new FakeDesktopPortal();
        (await HiDpiService.ReadGnomeInterfaceAsync(portal, default)).Should().Be(default(HiDpiService.GnomeInterfaceScaling), "no Settings portal");

        portal.Versions[PortalInterfaces.Settings] = 2;
        portal.Settings[("org.gnome.desktop.interface", "scaling-factor")] = 2u;
        portal.Settings[("org.gnome.desktop.interface", "text-scaling-factor")] = 1.25;
        var values = await HiDpiService.ReadGnomeInterfaceAsync(portal, default);
        values.ScalingFactor.Should().Be(2);
        values.TextScalingFactor.Should().Be(1.25);

        portal.Settings[("org.gnome.desktop.interface", "scaling-factor")] = 0u;
        (await HiDpiService.ReadGnomeInterfaceAsync(portal, default)).ScalingFactor.Should().BeNull("0 means unset");

        portal.ThrowUnavailable = true;
        (await HiDpiService.ReadGnomeInterfaceAsync(portal, default)).Should().Be(default(HiDpiService.GnomeInterfaceScaling));
    }

    [Fact]
    public void HiDpi_GsettingsAndMutterParsing()
    {
        HiDpiService.ParseGsettingsScalingFactor("uint32 2\n").Should().Be(2);
        HiDpiService.ParseGsettingsScalingFactor("garbage").Should().BeNull();
        HiDpiService.ParseGsettingsDouble("1.5\n").Should().Be(1.5);
        HiDpiService.ParseGsettingsDouble(null).Should().BeNull();
        HiDpiService.PrimaryLogicalScale(new[] { (1.0, false), (1.75, true) }).Should().Be(1.75);
        HiDpiService.PrimaryLogicalScale(new[] { (1.5, false) }).Should().Be(1.5);
        HiDpiService.PrimaryLogicalScale(Array.Empty<(double, bool)>()).Should().BeNull();
    }

    [Fact]
    public void LocationFix_ParsesPortalDictionary()
    {
        var fix = PortalLocationFix.Parse(new Dictionary<string, object>
        {
            ["Latitude"] = 1.3521,
            ["Longitude"] = 103.8198,
            ["Accuracy"] = 25.0,
            ["Altitude"] = -1.7976931348623157E+308,
            ["Speed"] = 0.0,
            ["Timestamp"] = (1_700_000_000UL, 500_000UL),
        })!;
        fix.Latitude.Should().Be(1.3521);
        fix.Longitude.Should().Be(103.8198);
        fix.Accuracy.Should().Be(25.0);
        fix.Altitude.Should().BeNull("-G_MAXDOUBLE means unknown");
        fix.Speed.Should().Be(0.0);
        fix.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddMilliseconds(500));

        PortalLocationFix.Parse(new Dictionary<string, object> { ["Latitude"] = 91.0, ["Longitude"] = 0.0 }).Should().BeNull();
        PortalLocationFix.Parse(new Dictionary<string, object> { ["Longitude"] = 0.0 }).Should().BeNull();
        PortalLocationFix.Parse(null).Should().BeNull();

        var location = GeolocationService.ToLocation(fix)!;
        location.Latitude.Should().Be(1.3521);
        location.Accuracy.Should().Be(25.0);
        GeolocationService.ToLocation(null).Should().BeNull();
    }
}

public class PortalFeatureTests
{
    [Fact]
    public async Task FileChooser_Unavailable_WhenNoInterface()
    {
        var portal = new FakeDesktopPortal();
        var result = await new PortalFileChooser(portal).OpenAsync(new PortalFileChooserRequest(), "");
        result.Outcome.Should().Be(PortalOutcome.Unavailable);
        portal.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task FileChooser_ReturnsLocalPathsOnSuccess()
    {
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.FileChooser] = 4 } };
        portal.NextResponse = () => PortalResponse.FromSignal(0, new Dictionary<string, object> { ["uris"] = new[] { "file:///tmp/x%201.png", "file:///tmp/y.png" } });

        var request = PortalFilePickerService.BuildPortalRequest(
            new PickOptions { PickerTitle = "Choose images", FileTypes = new FilePickerFileType(new Dictionary<Microsoft.Maui.Devices.DevicePlatform, IEnumerable<string>> { [Microsoft.Maui.Devices.DevicePlatform.Create("Linux")] = new[] { "png" } }) },
            allowMultiple: true);
        var result = await new PortalFileChooser(portal).OpenAsync(request, "x11:1");

        result.Outcome.Should().Be(PortalOutcome.Completed);
        result.Paths.Should().Equal("/tmp/x 1.png", "/tmp/y.png");
        var call = portal.Calls.Single();
        call.Method.Should().Be("OpenFile");
        call.Arg.Should().Be("Choose images");
        call.Options!["multiple"].Should().Be(true);
        ((ValueTuple<string, (uint, string)[]>[])call.Options["filters"])[0].Item2.Should().Equal((0u, "*.png"));
    }

    [Fact]
    public async Task FileChooser_CancelIsFinal_OtherFallsBack()
    {
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.FileChooser] = 4 } };
        portal.NextResponse = () => PortalResponse.FromSignal(1, null);
        var cancelled = await new PortalFileChooser(portal).OpenAsync(new PortalFileChooserRequest(), "");
        cancelled.Outcome.Should().Be(PortalOutcome.Cancelled);
        cancelled.Outcome.ShouldFallBack().Should().BeFalse();
        cancelled.Paths.Should().BeEmpty();

        portal.NextResponse = () => PortalResponse.FromSignal(2, null);
        (await new PortalFileChooser(portal).SaveAsync(new PortalFileChooserRequest { CurrentName = "a.txt" }, "")).Outcome.ShouldFallBack().Should().BeTrue();
        portal.Calls.Last().Method.Should().Be("SaveFile");

        portal.ThrowUnavailable = true;
        (await new PortalFileChooser(portal).OpenAsync(new PortalFileChooserRequest(), "")).Outcome.Should().Be(PortalOutcome.Unavailable);
    }

    [Fact]
    public async Task FileChooser_DirectoryNeedsVersion3()
    {
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.FileChooser] = 2 } };
        (await new PortalFileChooser(portal).OpenAsync(new PortalFileChooserRequest { Directory = true }, "")).Outcome.Should().Be(PortalOutcome.Unavailable);
        portal.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Launcher_UsesOpenUriForUrisAndOpenFileForLocalFiles()
    {
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.OpenUri] = 5 } };
        var launcher = new PortalLauncher(portal);

        (await launcher.OpenUriAsync("https://example.com/a%20b")).Should().Be(PortalOutcome.Completed);
        portal.Calls.Last().Method.Should().Be("OpenURI");
        portal.Calls.Last().Arg.Should().Be("https://example.com/a%20b");

        var file = Path.Combine(Path.GetTempPath(), $"openmaui-portal-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, "x");
        try
        {
            (await launcher.OpenFileAsync(file, ask: true)).Should().Be(PortalOutcome.Completed);
            portal.Calls.Last().Method.Should().Be("OpenURI.OpenFile");
            portal.Calls.Last().Options!["ask"].Should().Be(true);

            // A file:// URI to an existing file also goes by descriptor.
            (await launcher.OpenUriAsync(new Uri(file).AbsoluteUri)).Should().Be(PortalOutcome.Completed);
            portal.Calls.Last().Method.Should().Be("OpenURI.OpenFile");

            (await launcher.OpenContainingFolderAsync(file)).Should().Be(PortalOutcome.Completed);
            portal.Calls.Last().Method.Should().Be("OpenDirectory");
        }
        finally
        {
            File.Delete(file);
        }

        (await launcher.OpenFileAsync("/nonexistent/openmaui/file.bin")).Should().Be(PortalOutcome.Unavailable);
    }

    [Fact]
    public async Task Launcher_UnavailablePortalFallsBackToXdgOpen()
    {
        var portal = new FakeDesktopPortal();
        (await new PortalLauncher(portal).OpenUriAsync("https://example.com")).Should().Be(PortalOutcome.Unavailable);

        LauncherService.ResolveLaunch(PortalOutcome.Unavailable, () => true).Should().BeTrue();
        LauncherService.ResolveLaunch(PortalOutcome.Failed, () => false).Should().BeFalse();
        LauncherService.ResolveLaunch(PortalOutcome.Completed, () => throw new InvalidOperationException("no fallback")).Should().BeTrue();
        LauncherService.ResolveLaunch(PortalOutcome.Cancelled, () => throw new InvalidOperationException("no fallback")).Should().BeFalse();
    }

    [Fact]
    public async Task Screenshot_ReturnsUriOnSuccessOnly()
    {
        var portal = new FakeDesktopPortal();
        (await new PortalScreenshot(portal).TakeAsync(interactive: false)).Should().BeNull();

        portal.Versions[PortalInterfaces.Screenshot] = 2;
        portal.NextResponse = () => PortalResponse.FromSignal(0, new Dictionary<string, object> { ["uri"] = "file:///home/u/Pictures/Screenshot.png" });
        (await new PortalScreenshot(portal).TakeAsync(interactive: false)).Should().Be("file:///home/u/Pictures/Screenshot.png");
        portal.Calls.Last().Options!["interactive"].Should().Be(false);

        portal.NextResponse = () => PortalResponse.FromSignal(1, null);
        (await new PortalScreenshot(portal).TakeAsync(interactive: true)).Should().BeNull();
    }

    [Fact]
    public async Task IdleInhibitor_HoldsOneHandleAndClosesIt()
    {
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.Inhibit] = 3 } };
        var inhibitor = new PortalIdleInhibitor(portal);

        (await inhibitor.SetAsync(true, "video")).Should().BeTrue();
        (await inhibitor.SetAsync(true, "video")).Should().BeTrue("a second enable reuses the handle");
        portal.OpenInhibitions.Should().Be(1);
        portal.Calls.Single(c => c.Method == "Inhibit").Arg.Should().Be("Idle");
        portal.Calls.Single(c => c.Method == "Inhibit").Options!["reason"].Should().Be("video");
        inhibitor.IsInhibiting.Should().BeTrue();

        (await inhibitor.SetAsync(false, "")).Should().BeTrue();
        portal.OpenInhibitions.Should().Be(0);
        portal.Calls.Last().Method.Should().Be("Request.Close");
        inhibitor.IsInhibiting.Should().BeFalse();
        (await inhibitor.SetAsync(false, "")).Should().BeFalse("nothing held: caller's fallback decides");
    }

    [Fact]
    public async Task IdleInhibitor_ReportsUnavailableForFallback()
    {
        var none = new PortalIdleInhibitor(new FakeDesktopPortal());
        (await none.SetAsync(true, "x")).Should().BeFalse();

        var failing = new FakeDesktopPortal { Versions = { [PortalInterfaces.Inhibit] = 3 }, ThrowUnavailable = true };
        (await new PortalIdleInhibitor(failing).SetAsync(true, "x")).Should().BeFalse();
    }

    [Fact]
    public async Task Secret_ReadsWhatTheBackendWritesAndDerivesAStableKey()
    {
        var secret = Enumerable.Range(1, 64).Select(i => (byte)i).ToArray();
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.Secret] = 1 }, SecretToWrite = secret };

        (await new PortalSecretKey(portal).RetrieveSecretAsync()).Should().Equal(secret);
        var key = await new PortalSecretKey(portal).DeriveStorageKeyAsync();
        key.Should().HaveCount(32);
        key.Should().Equal(PortalSecretKey.DeriveKey(secret));
        PortalSecretKey.DeriveKey(new byte[] { 9 }).Should().NotEqual(key);

        portal.NextResponse = () => PortalResponse.FromSignal(2, null);
        (await new PortalSecretKey(portal).RetrieveSecretAsync()).Should().BeNull();
        (await new PortalSecretKey(new FakeDesktopPortal()).RetrieveSecretAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Background_MapsResults()
    {
        var portal = new FakeDesktopPortal();
        var unavailable = LinuxBackgroundService.ToPublic(await new PortalBackground(portal).RequestAsync("r", true, null, false));
        unavailable.PortalAvailable.Should().BeFalse();

        portal.Versions[PortalInterfaces.Background] = 2;
        portal.NextResponse = () => PortalResponse.FromSignal(0, new Dictionary<string, object> { ["background"] = true, ["autostart"] = true });
        var granted = LinuxBackgroundService.ToPublic(await new PortalBackground(portal).RequestAsync("Keep syncing", true, new[] { "/app/bin/x" }, false));
        granted.PortalAvailable.Should().BeTrue();
        granted.Background.Should().BeTrue();
        granted.Autostart.Should().BeTrue();
        portal.Calls.Last().Options!["reason"].Should().Be("Keep syncing");

        portal.NextResponse = () => PortalResponse.FromSignal(1, new Dictionary<string, object> { ["background"] = true });
        var cancelled = LinuxBackgroundService.ToPublic(await new PortalBackground(portal).RequestAsync(null, false, null, false));
        cancelled.Cancelled.Should().BeTrue();
        cancelled.Background.Should().BeFalse("results only count on success");
    }

    [Fact]
    public async Task Geolocation_ReturnsFixOrNull()
    {
        var portal = new FakeDesktopPortal();
        (await new PortalGeolocation(portal).GetFixAsync(4, TimeSpan.FromSeconds(1))).Should().BeNull();

        portal.Versions[PortalInterfaces.Location] = 1;
        portal.Location = new Dictionary<string, object> { ["Latitude"] = 10.0, ["Longitude"] = 20.0 };
        var fix = await new PortalGeolocation(portal).GetFixAsync(4, TimeSpan.FromSeconds(1));
        fix!.Latitude.Should().Be(10.0);
        portal.Calls.Last().Options!["accuracy"].Should().Be(4u);

        portal.ThrowUnavailable = true;
        (await new PortalGeolocation(portal).GetFixAsync(4, TimeSpan.FromSeconds(1))).Should().BeNull();
    }
}

public class PortalServiceIntegrationTests
{
    [Fact]
    public async Task Geolocation_listens_raises_each_fix_and_stops()
    {
        var portal = new FakeDesktopPortal();
        var geo = new GeolocationService(portal, alwaysUsePortal: true);
        var seen = new List<Location>();
        geo.LocationChanged += (_, e) => seen.Add(e.Location);

        (await geo.StartListeningForegroundAsync(new GeolocationListeningRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5)))).Should().BeTrue();
        geo.IsListeningForeground.Should().BeTrue();
        var options = portal.Calls.Single(c => c.Method == "WatchLocation").Options!;
        options["time-threshold"].Should().Be(5u, "MinimumTime is the portal's time threshold");
        options["accuracy"].Should().Be(PortalLocationFix.AccuracyFor(GeolocationAccuracy.High));

        var act = () => geo.StartListeningForegroundAsync(new GeolocationListeningRequest());
        await act.Should().ThrowAsync<InvalidOperationException>("MAUI throws when already listening");

        portal.LocationWatcher!(new Dictionary<string, object> { ["Latitude"] = 1.5, ["Longitude"] = 2.5 });
        portal.LocationWatcher!(new Dictionary<string, object> { ["Latitude"] = 3.0, ["Longitude"] = 4.0 });
        await Task.Delay(50);
        seen.Select(l => (l.Latitude, l.Longitude)).Should().Equal((1.5, 2.5), (3.0, 4.0));
        (await geo.GetLastKnownLocationAsync())!.Latitude.Should().Be(3.0, "a fix while listening is the last known location");

        geo.StopListeningForeground();
        await Task.Delay(50);
        geo.IsListeningForeground.Should().BeFalse();
        portal.Calls.Should().Contain(c => c.Method == "StopLocation", "stopping closes the portal session");
        (await geo.StartListeningForegroundAsync(new GeolocationListeningRequest())).Should().BeTrue("it can listen again after stopping");
        geo.StopListeningForeground();
    }

    [Fact]
    public async Task Geolocation_listening_is_refused_when_denied_or_without_a_portal()
    {
        var denied = new GeolocationService(new FakeDesktopPortal { DenyLocationWatch = true }, alwaysUsePortal: true);
        (await denied.StartListeningForegroundAsync(new GeolocationListeningRequest())).Should().BeFalse();
        denied.IsListeningForeground.Should().BeFalse();

        var none = new GeolocationService(new FakeDesktopPortal { ThrowUnavailable = true }, alwaysUsePortal: true);
        (await none.StartListeningForegroundAsync(new GeolocationListeningRequest())).Should().BeFalse();
        none.IsListeningForeground.Should().BeFalse();
    }

    [Fact]
    public async Task Notification_UsesServerIdsAndDispatchesActionsByLocalId()
    {
        var server = new FakeNotificationServer { NextId = 900 };
        var service = new NotificationService("App", null, new FakeDesktopPortal(), server);
        var invoked = new List<string>();
        NotificationActionEventArgs? actionArgs = null;
        NotificationClosedEventArgs? closedArgs = null;
        service.ActionInvoked += (_, e) => actionArgs = e;
        service.NotificationClosed += (_, e) => closedArgs = e;

        var id = await service.ShowWithActionsAsync("Title", "Body", new[] { new NotificationAction("ok", "OK", () => invoked.Add("ok")) }, tag: "t1");

        var sent = server.Notified.Single();
        sent.Summary.Should().Be("Title");
        sent.Actions.Should().Equal("ok", "OK");
        sent.Expire.Should().Be(5000);
        server.ActionInvoked.Should().NotBeNull("action monitoring starts with the first actionable notification");

        // Signal for another app's notification: ignored.
        server.ActionInvoked!(12345, "ok");
        invoked.Should().BeEmpty();

        server.ActionInvoked!(900, "ok");
        invoked.Should().Equal("ok");
        actionArgs!.NotificationId.Should().Be(id);
        actionArgs.Tag.Should().Be("t1");

        server.NotificationClosed!(900, 2);
        closedArgs!.NotificationId.Should().Be(id);
        closedArgs.Reason.Should().Be((NotificationCloseReason)2);
        closedArgs.Tag.Should().Be("t1");
    }

    [Fact]
    public async Task Notification_CancelClosesTheServerId()
    {
        var server = new FakeNotificationServer { NextId = 41 };
        var service = new NotificationService("App", "/icons/app.png", new FakeDesktopPortal(), server);

        await service.ShowAsync(new NotificationOptions { Title = "a", Message = "b", ExpireTimeMs = 0, Urgency = NotificationUrgency.Low });
        server.Notified.Single().Expire.Should().Be(-1, "0 means the server default, as with notify-send");
        server.Notified.Single().Icon.Should().Be("/icons/app.png");
        server.Notified.Single().Hints["urgency"].Should().Be((byte)0);

        var id = await service.ShowWithActionsAsync("x", "y", Array.Empty<NotificationAction>());
        await service.CancelAsync(id);
        server.Closed.Should().Equal(42u);

        // An id the service does not know is passed through as a server id.
        await service.CancelAsync(7);
        server.Closed.Should().Equal(42u, 7u);
    }

    [Fact]
    public void Notification_TransportOrder()
    {
        NotificationService.TransportOrder(tryPortal: false, 1).Should().Equal(NotificationService.Transport.Server, NotificationService.Transport.NotifySend);
        NotificationService.TransportOrder(tryPortal: true, 0).Should().Equal(NotificationService.Transport.Server, NotificationService.Transport.NotifySend);
        NotificationService.TransportOrder(tryPortal: true, 1).Should().Equal(NotificationService.Transport.Portal, NotificationService.Transport.Server, NotificationService.Transport.NotifySend);
    }

    [Fact]
    public void Notification_PortalActionsMapBackToLocalIds()
    {
        var service = new NotificationService("App", null, new FakeDesktopPortal(), new FakeNotificationServer());
        // Nothing registered: ignored without throwing.
        service.Invoking(s => s.OnPortalActionInvoked("openmaui-1-1", "app.ok")).Should().NotThrow();
        service.PortalNotificationId(3).Should().EndWith("-3").And.StartWith("openmaui-");
    }

    [Fact]
    public async Task SecureStorage_PortalKeyWritesV2AndStillReadsLegacyV1()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"openmaui-securev2-{Guid.NewGuid():N}");
        try
        {
            // A value stored by the legacy (v1) format...
            var legacy = new SecureStorageService(dir, useSecretService: false);
            await legacy.SetAsync("token", "old-value");

            var secret = Enumerable.Range(0, 64).Select(i => (byte)(255 - i)).ToArray();
            var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.Secret] = 1 }, SecretToWrite = secret };
            var withPortal = new SecureStorageService(dir, useSecretService: false, portal);

            // ...stays readable once a portal key exists (backward compatible).
            (await withPortal.GetAsync("token")).Should().Be("old-value");

            // New writes use v2 under the portal-derived key.
            await withPortal.SetAsync("token", "new-value");
            var file = Directory.GetFiles(dir).Single();
            var bytes = File.ReadAllBytes(file);
            SecureStorageService.IsVersion2(bytes).Should().BeTrue();
            (await withPortal.GetAsync("token")).Should().Be("new-value");

            // Without the portal key a v2 record cannot be decrypted.
            (await new SecureStorageService(dir, useSecretService: false).GetAsync("token")).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SecureStorage_V2RoundTripAndTamperDetection()
    {
        var key = PortalSecretKey.DeriveKey(new byte[] { 1, 2, 3 });
        var record = SecureStorageService.EncryptVersion2(key, "hello wörld");
        SecureStorageService.IsVersion2(record).Should().BeTrue();
        SecureStorageService.TryDecryptVersion2(key, record, out var plain).Should().BeTrue();
        plain.Should().Be("hello wörld");

        SecureStorageService.TryDecryptVersion2(PortalSecretKey.DeriveKey(new byte[] { 4 }), record, out _).Should().BeFalse();
        record[^1] ^= 0xFF;
        SecureStorageService.TryDecryptVersion2(key, record, out _).Should().BeFalse();
        SecureStorageService.IsVersion2(new byte[] { 1, 2, 3 }).Should().BeFalse();
    }

    [Fact]
    public void FilePicker_BuildsPortalRequestFromPickOptions()
    {
        var request = PortalFilePickerService.BuildPortalRequest(new PickOptions(), allowMultiple: false, currentFolder: "/tmp");
        request.Title.Should().Be("Open File");
        request.Multiple.Should().BeFalse();
        request.Filters.Should().BeEmpty();
        request.CurrentFolder.Should().Be("/tmp");
    }

    [Fact]
    public async Task Services_WithPortalsOff_TakeTheLegacyPath()
    {
        // The module initializer turned portals off: the portal-aware services
        // must behave exactly as before (these would otherwise reach the fake).
        var portal = new FakeDesktopPortal { Versions = { [PortalInterfaces.OpenUri] = 5, [PortalInterfaces.Location] = 1, [PortalInterfaces.FileChooser] = 4 } };
        (await new GeolocationService(portal).GetLocationAsync(new GeolocationRequest())).Should().BeNull();
        (await new PortalFilePickerService(portal).PickSaveFileAsync("Save", "a.txt", null, null)).Should().BeNull();
        portal.Calls.Should().BeEmpty();
    }
}
