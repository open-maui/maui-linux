// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Battery.EnergySaverStatus follows the desktop's power profile (power-profiles-daemon or
/// tuned-ppd): "power-saver" is on, any other profile off, no daemon unknown. It was always
/// Unknown.
/// </summary>
public class EnergySaverTests
{
    [Theory]
    [InlineData("power-saver", EnergySaverStatus.On)]
    [InlineData("balanced", EnergySaverStatus.Off)]
    [InlineData("performance", EnergySaverStatus.Off)]
    [InlineData(null, EnergySaverStatus.Unknown)]
    [InlineData("", EnergySaverStatus.Unknown)]
    public void A_profile_maps_to_the_energy_saver_status(string? profile, EnergySaverStatus expected)
        => PowerProfilesMonitor.FromProfile(profile).Should().Be(expected);

    [Fact]
    public async Task Battery_raises_EnergySaverStatusChanged_when_the_profile_switches()
    {
        var monitor = new PowerProfilesMonitor(useDaemon: false);
        var battery = new BatteryService(monitor);
        var seen = new System.Collections.Concurrent.ConcurrentQueue<EnergySaverStatus>();
        EventHandler<EnergySaverStatusChangedEventArgs> handler = (_, e) => seen.Enqueue(e.EnergySaverStatus);

        // Raised on the UI thread: posted there when another test has started the dispatcher.
        async Task WaitFor(int count)
        {
            for (int i = 0; i < 100 && seen.Count < count; i++)
                await Task.Delay(20);
        }
        battery.EnergySaverStatusChanged += handler;

        monitor.Report("power-saver");
        monitor.Report("power-saver");
        monitor.Report("balanced");
        await WaitFor(2);
        seen.Should().Equal(new[] { EnergySaverStatus.On, EnergySaverStatus.Off }, "once per change");

        battery.EnergySaverStatusChanged -= handler;
        monitor.Report("power-saver");
        await Task.Delay(100);
        seen.Should().HaveCount(2, "no events after the last handler is removed");
    }

    [Fact]
    public void The_live_daemon_is_read_when_there_is_one()
    {
        // Whatever this machine runs: a daemon gives On or Off, none gives Unknown; reading
        // never throws or hangs past the brief first wait.
        var started = DateTime.UtcNow;
        var status = new BatteryService(new PowerProfilesMonitor()).EnergySaverStatus;
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(3));
        status.Should().BeOneOf(EnergySaverStatus.On, EnergySaverStatus.Off, EnergySaverStatus.Unknown);
    }
}
