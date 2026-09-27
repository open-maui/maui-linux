// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Diagnostics;
using Xunit;
using Dependency = Microsoft.Maui.Platform.Linux.Diagnostics.OpenMauiDoctor.Dependency;
using DistroFamily = Microsoft.Maui.Platform.Linux.Diagnostics.OpenMauiDoctor.DistroFamily;

namespace Microsoft.Maui.Controls.Linux.Tests.Diagnostics;

/// <summary>
/// Pure-logic tests for openmaui doctor: trigger parsing, distro detection,
/// install-hint table, status aggregation / exit code, formatting, and the
/// dependency / font sections driven through injected probes (no dependence
/// on this machine). One smoke test runs the real probes.
/// </summary>
public class OpenMauiDoctorTests
{
    // ---------------- trigger ----------------

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" yes ", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRequested_EnvironmentValue(string? env, bool expected)
    {
        OpenMauiDoctor.IsRequested(env, Array.Empty<string>()).Should().Be(expected);
    }

    [Fact]
    public void IsRequested_CommandLineSwitch()
    {
        OpenMauiDoctor.IsRequested(null, new[] { "--title", "x", "--openmaui-doctor" }).Should().BeTrue();
        OpenMauiDoctor.IsRequested(null, new[] { "--OpenMaui-Doctor" }).Should().BeTrue();
        OpenMauiDoctor.IsRequested(null, new[] { "--openmaui-doctorx", "doctor" }).Should().BeFalse();
        OpenMauiDoctor.IsRequested(null, null).Should().BeFalse();
    }

    // ---------------- distro detection ----------------

    [Theory]
    [InlineData("NAME=\"Fedora Linux\"\nID=fedora\nVERSION_ID=44\n", "Fedora")]
    [InlineData("ID=\"rhel\"\nID_LIKE=\"fedora\"\n", "Fedora")]
    [InlineData("ID=rocky\nID_LIKE=\"rhel centos fedora\"\n", "Fedora")]
    [InlineData("ID=ubuntu\nID_LIKE=debian\n", "Debian")]
    [InlineData("ID=debian\n", "Debian")]
    [InlineData("ID=linuxmint\nID_LIKE=\"ubuntu debian\"\n", "Debian")]
    [InlineData("ID=pop\nID_LIKE=\"ubuntu debian\"\n", "Debian")]
    [InlineData("ID=arch\n", "Arch")]
    [InlineData("ID=manjaro\nID_LIKE=arch\n", "Arch")]
    [InlineData("ID=endeavouros\nID_LIKE=arch\n", "Arch")]
    [InlineData("ID=\"opensuse-tumbleweed\"\nID_LIKE=\"opensuse suse\"\n", "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void DetectDistro_FromOsReleaseText(string? text, string expected)
    {
        OpenMauiDoctor.DetectDistro(text).ToString().Should().Be(expected);
    }

    [Fact]
    public void DetectDistro_IgnoresCommentsAndOtherKeys()
    {
        var text = "# ID=arch\nVERSION_ID=\"24.04\"\nID_LIKE=debian\nID=ubuntu\n";
        OpenMauiDoctor.DetectDistro(text).Should().Be(DistroFamily.Debian);
        OpenMauiDoctor.ParseOsReleaseValue(text, "VERSION_ID").Should().Be("24.04");
        OpenMauiDoctor.ParseOsReleaseValue(text, "PRETTY_NAME").Should().BeNull();
    }

    // ---------------- install hints ----------------

    public static IEnumerable<object[]> AllDependenciesAndDistros()
    {
        foreach (var dep in Enum.GetValues<Dependency>())
            foreach (var distro in new[] { DistroFamily.Debian, DistroFamily.Fedora, DistroFamily.Arch })
                yield return new object[] { dep.ToString(), distro.ToString() };
    }

    [Theory]
    [MemberData(nameof(AllDependenciesAndDistros))]
    public void InstallHint_UsesTheDistrosPackageManager(string depName, string distroName)
    {
        var dep = Enum.Parse<Dependency>(depName);
        var distro = Enum.Parse<DistroFamily>(distroName);
        var hint = OpenMauiDoctor.InstallHint(dep, distro);

        // Arch has no WPEPlatform-enabled package we can vouch for.
        if (dep == Dependency.WpeWebKit && distro == DistroFamily.Arch)
        {
            hint.Should().BeNull();
            return;
        }

        hint.Should().NotBeNullOrWhiteSpace();
        if (dep == Dependency.ProtocolShim)
        {
            hint.Should().Contain("libopenmaui_wl.so").And.Contain("native/build.sh");
            return;
        }

        var prefix = distro switch
        {
            DistroFamily.Debian => "sudo apt install ",
            DistroFamily.Fedora => "sudo dnf ",
            _ => "sudo pacman -S ",
        };
        hint.Should().StartWith(prefix);
    }

    [Theory]
    [InlineData("GStreamer", "Debian", "sudo apt install libgstreamer1.0-0 gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-libav")]
    [InlineData("GStreamer", "Fedora", "sudo dnf install gstreamer1 gstreamer1-plugins-base gstreamer1-plugins-good gstreamer1-plugins-bad-free gstreamer1-plugin-libav")]
    [InlineData("GStreamer", "Arch", "sudo pacman -S gstreamer gst-plugins-base gst-plugins-good gst-plugins-bad gst-libav")]
    [InlineData("Cups", "Debian", "sudo apt install libcups2")]
    [InlineData("Cups", "Fedora", "sudo dnf install cups-libs")]
    [InlineData("Cups", "Arch", "sudo pacman -S libcups")]
    [InlineData("AtSpi2", "Fedora", "sudo dnf install at-spi2-core")]
    [InlineData("AppIndicator", "Debian", "sudo apt install libayatana-appindicator3-1")]
    [InlineData("AppIndicator", "Fedora", "sudo dnf install libayatana-appindicator-gtk3")]
    [InlineData("Gtk3", "Fedora", "sudo dnf install gtk3")]
    [InlineData("WebKitGtk", "Debian", "sudo apt install libwebkit2gtk-4.1-0")]
    [InlineData("WebKitGtk", "Fedora", "sudo dnf install webkit2gtk4.1")]
    [InlineData("WpeWebKit", "Debian", "sudo apt install libwpewebkit-2.0-1 (Debian testing/sid; Debian 13 and Ubuntu ship no WPE 2.54: use the WebKitGTK WebView with UseGtk = true)")]
    [InlineData("WpeWebKit", "Fedora", "sudo dnf copr enable philn/wpewebkit && sudo dnf install wpewebkit")]
    [InlineData("NotoCjk", "Debian", "sudo apt install fonts-noto-cjk")]
    [InlineData("NotoCjk", "Fedora", "sudo dnf install google-noto-sans-cjk-fonts")]
    [InlineData("NotoCjk", "Arch", "sudo pacman -S noto-fonts-cjk")]
    [InlineData("NotoColorEmoji", "Debian", "sudo apt install fonts-noto-color-emoji")]
    [InlineData("NotoColorEmoji", "Fedora", "sudo dnf install google-noto-color-emoji-fonts")]
    [InlineData("NotoColorEmoji", "Arch", "sudo pacman -S noto-fonts-emoji")]
    public void InstallHint_ExactCommands(string dep, string distro, string expected)
    {
        OpenMauiDoctor.InstallHint(Enum.Parse<Dependency>(dep), Enum.Parse<DistroFamily>(distro)).Should().Be(expected);
    }

    [Fact]
    public void InstallHint_UnknownDistro_ListsEveryFamily()
    {
        OpenMauiDoctor.InstallHint(Dependency.Cups, DistroFamily.Unknown).Should().BeNull();

        var all = OpenMauiDoctor.InstallHintOrAll(Dependency.Cups, DistroFamily.Unknown);
        all.Should().Contain("sudo apt install libcups2")
            .And.Contain("sudo dnf install cups-libs")
            .And.Contain("sudo pacman -S libcups");

        // No Arch entry for WPE, so only two families are listed.
        var wpe = OpenMauiDoctor.InstallHintOrAll(Dependency.WpeWebKit, DistroFamily.Unknown);
        wpe.Should().Contain("philn/wpewebkit").And.NotContain("pacman");

        OpenMauiDoctor.InstallHintOrAll(Dependency.Cups, DistroFamily.Fedora).Should().Be("sudo dnf install cups-libs");
    }

    // ---------------- status aggregation / exit code ----------------

    [Fact]
    public void ExitCode_IsOneOnlyWhenARequiredItemIsMissing()
    {
        var report = new DoctorReport();
        var s = new DoctorSection("S");
        report.Sections.Add(s);
        s.Checks.Add(new DoctorCheck("a", DoctorStatus.Ok, "fine"));
        s.Checks.Add(new DoctorCheck("b", DoctorStatus.Missing, "optional gone"));
        s.Checks.Add(new DoctorCheck("c", DoctorStatus.Warn, "degraded", Required: true));
        report.HasMissingRequired.Should().BeFalse();
        report.ExitCode.Should().Be(0);

        s.Checks.Add(new DoctorCheck("d", DoctorStatus.Missing, "required gone", Required: true));
        report.HasMissingRequired.Should().BeTrue();
        report.ExitCode.Should().Be(1);
    }

    // ---------------- formatting ----------------

    [Fact]
    public void Format_AlignsColumnsShowsFixesAndSummary()
    {
        var report = new DoctorReport();
        var s = new DoctorSection("Native dependencies");
        report.Sections.Add(s);
        s.Checks.Add(new DoctorCheck("GTK 3", DoctorStatus.Ok, "libgtk-3.so.0"));
        s.Checks.Add(new DoctorCheck("libcups", DoctorStatus.Missing, "libcups.so.2 not found", "sudo dnf install cups-libs"));
        s.Checks.Add(new DoctorCheck("Display server", DoctorStatus.Missing, "no display", Required: true));
        s.Checks.Add(new DoctorCheck("Hint on ok", DoctorStatus.Ok, "present", "never shown"));
        report.Sections.Add(new DoctorSection("Empty section"));

        var text = OpenMauiDoctor.Format(report);
        var lines = text.Split('\n');

        text.Should().StartWith("OpenMaui doctor\n");
        text.Should().Contain("\nNative dependencies\n");
        text.Should().NotContain("Empty section");
        text.Should().NotContain("\u001b[");
        text.Should().NotContain("never shown");

        var gtk = lines.Single(l => l.Contains("GTK 3"));
        var cups = lines.Single(l => l.Contains("libcups.so.2"));
        gtk.Should().StartWith("  [ok]");
        cups.Should().StartWith("  [MISSING]");
        // Details start in the same column.
        gtk.IndexOf("libgtk-3.so.0", StringComparison.Ordinal)
            .Should().Be(cups.IndexOf("libcups.so.2", StringComparison.Ordinal));

        var fix = lines.Single(l => l.Contains("fix: sudo dnf install cups-libs"));
        fix.IndexOf("fix:", StringComparison.Ordinal).Should().Be(cups.IndexOf("libcups.so.2", StringComparison.Ordinal));

        text.Should().Contain("no display (required)");
        text.Should().Contain("Summary: 2 ok, 0 warning(s), 2 missing (1 required)");
    }

    [Fact]
    public void Format_WrapsLongDetailsUnderTheDetailColumn()
    {
        var report = new DoctorReport();
        var s = new DoctorSection("S");
        report.Sections.Add(s);
        var words = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"word{i}"));
        s.Checks.Add(new DoctorCheck("Long", DoctorStatus.Info, words));

        var lines = OpenMauiDoctor.Format(report).Split('\n').Where(l => l.Contains("word")).ToList();
        lines.Count.Should().BeGreaterThan(1);
        lines.Should().OnlyContain(l => l.Length <= 120);
        var column = lines[0].IndexOf("word0", StringComparison.Ordinal);
        lines.Skip(1).Should().OnlyContain(l => l.Length - l.TrimStart().Length == column);
    }

    [Fact]
    public void Format_ColorOnlyWhenAsked()
    {
        var report = new DoctorReport();
        var s = new DoctorSection("S");
        report.Sections.Add(s);
        s.Checks.Add(new DoctorCheck("x", DoctorStatus.Missing, "y"));
        OpenMauiDoctor.Format(report, color: true).Should().Contain("\u001b[31m");
        OpenMauiDoctor.Format(report, color: false).Should().NotContain("\u001b[");
    }

    // ---------------- injected probes ----------------

    private static DoctorProbes Probes(ISet<string> libs, ISet<string>? fonts = null, string? tray = null) => new()
    {
        HasLibrary = libs.Contains,
        HasFont = f => fonts?.Contains(f) == true,
        HasGtkPrintDialog = () => true,
        TrayBackend = () => tray,
        WebViewBackend = () => null,
    };

    private static readonly string[] AllLibs =
    {
        "libgtk-3.so.0", "libglib-2.0.so.0", "libgstreamer-1.0.so.0", "libgstapp-1.0.so.0",
        "libcups.so.2", "libatspi.so.0", "libayatana-appindicator3.so.1", "libwebkit2gtk-4.1.so.0",
        "libWPEWebKit-2.0.so.1", "libfontconfig.so.1",
    };

    [Fact]
    public void DependencySection_AllPresent_IsAllOk()
    {
        var section = OpenMauiDoctor.BuildDependencySection(Probes(new HashSet<string>(AllLibs)), DistroFamily.Fedora);
        section.Checks.Should().OnlyContain(c => c.Status == DoctorStatus.Ok);
        section.Checks.Select(c => c.Name).Should().Contain(new[]
        {
            "GTK 3", "GStreamer", "libcups", "AT-SPI2", "Tray (AppIndicator)", "WebKitGTK", "WPE WebKit 2.54+", "fontconfig",
        });
    }

    [Fact]
    public void DependencySection_NothingPresent_GtkIsRequiredOthersOptionalWithDistroHints()
    {
        var section = OpenMauiDoctor.BuildDependencySection(Probes(new HashSet<string>()), DistroFamily.Debian);

        var gtk = section.Checks.Single(c => c.Name == "GTK 3");
        gtk.Status.Should().Be(DoctorStatus.Missing);
        gtk.Required.Should().BeTrue();
        gtk.Fix.Should().Be("sudo apt install libgtk-3-0");

        section.Checks.Where(c => c.Name != "GTK 3")
            .Should().OnlyContain(c => c.Status == DoctorStatus.Missing && !c.Required && c.Fix!.StartsWith("sudo apt install "));

        section.Checks.Single(c => c.Name == "WPE WebKit 2.54+").Fix.Should().Be("sudo apt install libwpewebkit-2.0-1 (Debian testing/sid; Debian 13 and Ubuntu ship no WPE 2.54: use the WebKitGTK WebView with UseGtk = true)");
        section.Checks.Single(c => c.Name == "GStreamer").Detail.Should().Contain("libgstreamer-1.0.so.0");

        var report = new DoctorReport();
        report.Sections.Add(section);
        report.ExitCode.Should().Be(1);
    }

    [Fact]
    public void DependencySection_PartialGStreamer_ReportsTheMissingLibrary()
    {
        var libs = new HashSet<string>(AllLibs);
        libs.Remove("libgstapp-1.0.so.0");
        var gst = OpenMauiDoctor.BuildDependencySection(Probes(libs), DistroFamily.Arch)
            .Checks.Single(c => c.Name == "GStreamer");
        gst.Status.Should().Be(DoctorStatus.Missing);
        gst.Detail.Should().StartWith("libgstapp-1.0.so.0 not found");
        gst.Fix.Should().StartWith("sudo pacman -S gstreamer");
    }

    [Fact]
    public void DependencySection_TrayFallsBackToXEmbedAsWarning()
    {
        var libs = new HashSet<string>(AllLibs);
        libs.Remove("libayatana-appindicator3.so.1");
        var tray = OpenMauiDoctor.BuildDependencySection(Probes(libs, tray: "XEmbed"), DistroFamily.Fedora)
            .Checks.Single(c => c.Name == "Tray (AppIndicator)");
        tray.Status.Should().Be(DoctorStatus.Warn);
        tray.Fix.Should().Be("sudo dnf install libayatana-appindicator-gtk3");

        libs.Add("libappindicator3.so.1");
        OpenMauiDoctor.BuildDependencySection(Probes(libs, tray: "AppIndicator"), DistroFamily.Fedora)
            .Checks.Single(c => c.Name == "Tray (AppIndicator)").Status.Should().Be(DoctorStatus.Ok);
    }

    [Fact]
    public void DependencySection_WebKitGtk40IsAnAcceptedAlternative()
    {
        var libs = new HashSet<string>(AllLibs);
        libs.Remove("libwebkit2gtk-4.1.so.0");
        libs.Add("libwebkit2gtk-4.0.so.37");
        OpenMauiDoctor.BuildDependencySection(Probes(libs), DistroFamily.Debian)
            .Checks.Single(c => c.Name == "WebKitGTK").Status.Should().Be(DoctorStatus.Ok);
    }

    [Fact]
    public void DependencySection_ThrowingProbeIsTreatedAsMissing()
    {
        var probes = new DoctorProbes
        {
            HasLibrary = _ => throw new InvalidOperationException("boom"),
            TrayBackend = () => throw new InvalidOperationException("boom"),
            WebViewBackend = () => throw new InvalidOperationException("boom"),
            HasGtkPrintDialog = () => throw new InvalidOperationException("boom"),
        };
        var section = OpenMauiDoctor.BuildDependencySection(probes, DistroFamily.Fedora);
        section.Checks.Should().NotBeEmpty();
        section.Checks.Should().OnlyContain(c => c.Status == DoctorStatus.Missing);
    }

    [Fact]
    public void FontSection_DetectsCjkVariantsAndEmoji()
    {
        var none = OpenMauiDoctor.BuildFontSection(Probes(new HashSet<string>()), DistroFamily.Fedora);
        none.Checks.Should().HaveCount(2).And.OnlyContain(c => c.Status == DoctorStatus.Missing && !c.Required);
        none.Checks.Single(c => c.Name == "Noto CJK").Fix.Should().Be("sudo dnf install google-noto-sans-cjk-fonts");
        none.Checks.Single(c => c.Name == "Noto Color Emoji").Fix.Should().Be("sudo dnf install google-noto-color-emoji-fonts");

        var some = OpenMauiDoctor.BuildFontSection(
            Probes(new HashSet<string>(), new HashSet<string> { "Noto Sans CJK JP", "Noto Color Emoji" }), DistroFamily.Fedora);
        some.Checks.Should().OnlyContain(c => c.Status == DoctorStatus.Ok);
        some.Checks.Single(c => c.Name == "Noto CJK").Detail.Should().Be("Noto Sans CJK JP");
    }

    // ---------------- smoke ----------------

    [Fact]
    public void Run_OnThisMachine_DoesNotThrowAndReportsDotNet()
    {
        var report = OpenMauiDoctor.Run();
        report.Sections.Should().Contain(s => s.Title == "Runtime");
        report.Sections.Single(s => s.Title == "Runtime").Checks.Should().Contain(c => c.Name == ".NET" && c.Status == DoctorStatus.Ok);

        var text = OpenMauiDoctor.Format(report);
        text.Should().Contain(".NET").And.Contain("Summary:");
    }
}
