// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls.Xaml;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Golden;

/// <summary>
/// Golden scenes for real sample pages (maui-linux-samples/ShellDemo), rendered
/// through the whole MAUI path apps take: runtime-inflated XAML with the
/// sample's own theme resources, handlers, layout and the rendering engine,
/// at every scale factor. The XAML lives in Golden/SamplePages; event-handler
/// attributes are stripped (and the files use .xml so the build does not try to compile them) because the pages' code-behind is not part of the
/// test assembly (they do not change what is drawn).
/// </summary>
[Collection("LinuxApplication.Current")]
public class SamplePageSceneTests
{
    public static IEnumerable<object[]> Scales => GoldenHarness.Scales.Select(s => new object[] { s });

    private static readonly Regex s_eventHandler = new(
        @"\s(Clicked|Toggled|ValueChanged|TextChanged|Tapped|SelectionChanged|CheckedChanged|Completed|Focused|Unfocused|SelectedIndexChanged|DateSelected|TimeSelected|Pressed|Released|DragStarting|Drop|DragOver|Invoked|PropertyChanged|PropertyChanging|Loaded|Unloaded|Appearing|Disappearing)=""[^""]*""",
        RegexOptions.Compiled);

    private static string Xaml(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SamplePages.{name}.xml")
            ?? throw new InvalidOperationException($"Sample page {name} is not embedded");
        using var reader = new StreamReader(stream);
        var xaml = reader.ReadToEnd();
        xaml = Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        return s_eventHandler.Replace(xaml, string.Empty);
    }

    private static void Verify(string name, float scale, int width = 480, int height = 900)
    {
        // Named font sizes (FontSize="Title") resolve through the service
        // UseLinux registers; the lightweight test host does not run UseLinux.
        DependencyService.Register<Microsoft.Maui.Controls.Internals.IFontNamedSizeService,
            Microsoft.Maui.Platform.Linux.Services.LinuxFontNamedSizeService>();

        // StaticResource resolves while the XAML loads, so the sample's
        // application resources are in place on the page first.
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // Deterministic across machines and days: US date formatting, and
            // date pickers left at their default (today) pinned to a fixed date.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            var page = new ContentPage { Resources = new ResourceDictionary().LoadFromXaml(Xaml("AppResources")) };
            page.LoadFromXaml(Xaml(name));
            foreach (var picker in page.GetVisualTreeDescendants().OfType<DatePicker>())
            {
                picker.MinimumDate = new DateTime(2026, 1, 1);
                picker.MaximumDate = new DateTime(2026, 12, 31);
                picker.Date = new DateTime(2026, 3, 14);
            }
            using var host = new HeadlessMauiHost(page, withEngine: true, width: width, height: height);
            host.Context.Render();
            GoldenHarness.Verify("sample-" + name.ToLowerInvariant(), host.RootView!, width, height, scale);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Typography_page(float scale) => Verify("TypographyPage", scale);

    [Theory]
    [MemberData(nameof(Scales))]
    public void Pickers_page(float scale) => Verify("PickersPage", scale);

    [Theory]
    [MemberData(nameof(Scales))]
    public void About_page(float scale) => Verify("AboutPage", scale);

    [Theory]
    [MemberData(nameof(Scales))]
    public void Controls_page(float scale) => Verify("ControlsPage", scale);
}
