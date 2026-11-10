// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The desktop's animations setting is followed while the app runs, as MAUI's Windows ticker
/// follows UISettings.AnimationsEnabledChanged: the portal's reduced-motion, GNOME's
/// enable-animations and KDE's AnimationDurationFactor, read at start and updated by
/// SettingChanged, with OPENMAUI_REDUCE_MOTION winning.
/// </summary>
public class SystemAnimationsSettingTests
{
    private static FakeDesktopPortal Portal()
    {
        var portal = new FakeDesktopPortal();
        portal.Versions[PortalInterfaces.Settings] = 2;
        return portal;
    }

    [Fact]
    public async Task The_ticker_follows_gnome_enable_animations_while_the_app_runs()
    {
        var portal = Portal();
        portal.Settings[(PortalAppearance.GnomeInterfaceNamespace, "enable-animations")] = true;
        var setting = new SystemAnimationsSetting(() => null, probe: null);
        var changes = new List<bool>();
        setting.Changed += changes.Add;

        (await setting.StartAsync(portal)).Should().BeTrue();
        setting.IsLive.Should().BeTrue();
        var ticker = new LinuxTicker(() => 0, setting);
        ticker.SystemEnabled.Should().BeTrue();

        portal.SettingHandler!(PortalAppearance.GnomeInterfaceNamespace, "enable-animations", false);
        ticker.SystemEnabled.Should().BeFalse("turning animations off in the desktop applies at once");

        portal.SettingHandler!(PortalAppearance.GnomeInterfaceNamespace, "enable-animations", true);
        ticker.SystemEnabled.Should().BeTrue();
        changes.Should().Equal(false, true);
    }

    [Fact]
    public async Task The_start_value_is_read_from_the_portal()
    {
        var portal = Portal();
        portal.Settings[(PortalAppearance.GnomeInterfaceNamespace, "enable-animations")] = false;
        var setting = new SystemAnimationsSetting(() => null, probe: true);

        await setting.StartAsync(portal);

        setting.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Reduced_motion_wins_over_the_desktop_keys()
    {
        var portal = Portal();
        portal.Settings[(PortalAppearance.Namespace, "reduced-motion")] = 1u;
        portal.Settings[(PortalAppearance.GnomeInterfaceNamespace, "enable-animations")] = true;
        var setting = new SystemAnimationsSetting(() => null, probe: null);

        await setting.StartAsync(portal);
        setting.Enabled.Should().BeFalse("reduced-motion 1 asks for reduced motion");

        portal.SettingHandler!(PortalAppearance.Namespace, "reduced-motion", 0u);
        setting.Enabled.Should().BeTrue("0 is no preference");
    }

    [Fact]
    public async Task Kde_animation_speed_zero_turns_animations_off()
    {
        var portal = Portal();
        portal.Settings[(SystemAnimationsSetting.KdeGlobalsNamespace, "AnimationDurationFactor")] = "1";
        var setting = new SystemAnimationsSetting(() => null, probe: null);

        await setting.StartAsync(portal);
        setting.Enabled.Should().BeTrue();

        portal.SettingHandler!(SystemAnimationsSetting.KdeGlobalsNamespace, "AnimationDurationFactor", "0");
        setting.Enabled.Should().BeFalse("KDE's 'instant' animation speed");

        portal.SettingHandler!(SystemAnimationsSetting.KdeGlobalsNamespace, "AnimationDurationFactor", 0.5);
        setting.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Other_settings_are_ignored()
    {
        var portal = Portal();
        var setting = new SystemAnimationsSetting(() => null, probe: false);
        await setting.StartAsync(portal);

        portal.SettingHandler!(PortalAppearance.Namespace, PortalAppearance.ColorSchemeKey, 1u);
        setting.Enabled.Should().BeFalse("without portal values the gsettings value stays");
    }

    [Fact]
    public async Task The_environment_override_wins_and_is_read_each_time()
    {
        string? env = "1";
        var portal = Portal();
        portal.Settings[(PortalAppearance.GnomeInterfaceNamespace, "enable-animations")] = true;
        var setting = new SystemAnimationsSetting(() => env, probe: null);
        await setting.StartAsync(portal);
        var ticker = new LinuxTicker(() => 0, setting);

        ticker.SystemEnabled.Should().BeFalse("OPENMAUI_REDUCE_MOTION=1 turns animations off");
        env = "0";
        portal.SettingHandler!(PortalAppearance.GnomeInterfaceNamespace, "enable-animations", false);
        ticker.SystemEnabled.Should().BeTrue("OPENMAUI_REDUCE_MOTION=0 keeps them on whatever the desktop says");
        env = null;
        ticker.SystemEnabled.Should().BeFalse("without the override the desktop's value applies");
    }

    [Fact]
    public async Task Without_the_settings_portal_the_probed_value_stays()
    {
        var setting = new SystemAnimationsSetting(() => null, probe: false);
        (await setting.StartAsync(new FakeDesktopPortal())).Should().BeFalse();
        setting.IsLive.Should().BeFalse();
        setting.Enabled.Should().BeFalse();
    }

    [Fact]
    public void A_running_animation_finishes_when_the_desktop_turns_animations_off()
    {
        double now = 0;
        var setting = new SystemAnimationsSetting(() => null, probe: true);
        var ticker = new LinuxTicker(() => now, setting);
        var manager = new LinuxAnimationManager(ticker);
        double progress = -1;
        new Microsoft.Maui.Animations.Animation(p => progress = p, 0, 1.0, Easing.Linear).Commit(manager);

        now = 100;
        ticker.Pump();
        progress.Should().BeApproximately(0.1, 1e-6);

        setting.OnSettingChanged(PortalAppearance.GnomeInterfaceNamespace, "enable-animations", false);
        now = 200;
        ticker.Pump();
        progress.Should().Be(1.0, "the next frame completes the animation, as the manager does when SystemEnabled turns false");
        ticker.Stop();
    }
}
