// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit.Maui (15.x, generic net10.0 asset) on OpenMaui, registered
/// the documented way (<c>UseMauiCommunityToolkit()</c>). Pass = behaviors,
/// converters, Popup, Expander, AvatarView and DrawingView behave as on the
/// toolkit's own platforms when driven through OpenMaui's handlers, input and
/// renderer; Toast and Snackbar complete their API contract.
/// </summary>
[Collection(CompatHost.Collection)]
public class CommunityToolkitMauiCompatTests
{
    private static CompatHost Host(View content, int width = 800, int height = 600)
        => new(new ContentPage { Content = content, BackgroundColor = Colors.White },
               b => b.UseMauiCommunityToolkit(o => o.SetShouldEnableSnackbarOnWindows(false)), width, height);

    // ---- Behaviors -------------------------------------------------------

    [Fact]
    public void EventToCommandBehavior_forwards_a_real_click_to_the_command()
    {
        object? received = null;
        var button = new Button { Text = "Go", WidthRequest = 120, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        button.Behaviors.Add(new EventToCommandBehavior
        {
            EventName = nameof(Button.Clicked),
            Command = new Command<object?>(p => received = p),
            CommandParameter = "clicked",
        });
        using var host = Host(button);
        host.Render();

        host.Tap(button);

        received.Should().Be("clicked");
    }

    [Fact]
    public void EventToCommandBehavior_passes_converted_event_args_from_typed_text()
    {
        var seen = new List<string>();
        var entry = new Entry { WidthRequest = 200, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        entry.Behaviors.Add(new EventToCommandBehavior
        {
            EventName = nameof(Entry.TextChanged),
            Command = new Command<string>(s => seen.Add(s)),
            EventArgsConverter = new TextChangedArgsConverter(),
        });
        using var host = Host(entry);
        host.Render();

        host.Tap(entry);
        host.DisplayWindow.RaiseTextInput("4");
        host.DisplayWindow.RaiseTextInput("2");

        entry.Text.Should().Be("42", "keyboard input reaches the MAUI Entry through OpenMaui's text pipeline");
        seen.Should().Equal("4", "42");
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("10", true)]
    [InlineData("11", false)]
    [InlineData("abc", false)]
    [InlineData("0.5", false)] // below the minimum
    public async Task NumericValidationBehavior_validates_the_entry_text(string text, bool expectedValid)
    {
        var validStyle = new Style(typeof(Entry)) { Setters = { new Setter { Property = Entry.TextColorProperty, Value = Colors.Green } } };
        var invalidStyle = new Style(typeof(Entry)) { Setters = { new Setter { Property = Entry.TextColorProperty, Value = Colors.Red } } };
        var behavior = new NumericValidationBehavior
        {
            MinimumValue = 1,
            MaximumValue = 10,
            MaximumDecimalPlaces = 2,
            Flags = ValidationFlags.ValidateOnValueChanged,
            ValidStyle = validStyle,
            InvalidStyle = invalidStyle,
        };
        var entry = new Entry { WidthRequest = 200, HeightRequest = 40 };
        entry.Behaviors.Add(behavior);
        using var host = Host(entry);
        host.Render();

        entry.Text = text;
        await behavior.ForceValidate();

        behavior.IsValid.Should().Be(expectedValid);
        entry.TextColor.Should().Be(expectedValid ? Colors.Green : Colors.Red, "the Valid/Invalid style is applied");
        var platform = (Microsoft.Maui.Platform.SkiaEntry)CompatHost.PlatformOf(entry);
        platform.TextColor.Should().Be(expectedValid ? Colors.Green : Colors.Red, "the style change reaches the Skia entry");
    }

    // ---- Converters (through real bindings) --------------------------------

    [Fact]
    public void Converters_drive_rendered_label_properties_through_bindings()
    {
        var vm = new ConverterViewModel { IsBusy = true, Name = "", Count = 3 };
        var hidden = new Label { Text = "shown-when-idle" };
        hidden.SetBinding(VisualElement.IsVisibleProperty, new Binding(nameof(ConverterViewModel.IsBusy), converter: new InvertedBoolConverter()));
        var empty = new Label();
        empty.SetBinding(Label.TextProperty, new Binding(nameof(ConverterViewModel.Name), converter: new IsStringNullOrEmptyConverter()));
        var colored = new Label { Text = "status" };
        colored.SetBinding(Label.TextColorProperty, new Binding(nameof(ConverterViewModel.IsBusy),
            converter: new BoolToObjectConverter { TrueObject = Colors.Red, FalseObject = Colors.Blue }));
        var math = new Label();
        math.SetBinding(Label.TextProperty, new Binding(nameof(ConverterViewModel.Count),
            converter: new MathExpressionConverter(), converterParameter: "x * 2 + 1"));
        var layout = new VerticalStackLayout { Children = { hidden, empty, colored, math }, BindingContext = vm };
        using var host = Host(layout);
        host.Render();

        CompatHost.PlatformOf(hidden).IsVisible.Should().BeFalse();
        ((SkiaLabel)CompatHost.PlatformOf(empty)).Text.Should().Be("True");
        ((SkiaLabel)CompatHost.PlatformOf(colored)).TextColor.Should().Be(Colors.Red);
        ((SkiaLabel)CompatHost.PlatformOf(math)).Text.Should().Be("7");

        vm.IsBusy = false;
        vm.Name = "set";
        vm.Count = 10;

        CompatHost.PlatformOf(hidden).IsVisible.Should().BeTrue();
        ((SkiaLabel)CompatHost.PlatformOf(empty)).Text.Should().Be("False");
        ((SkiaLabel)CompatHost.PlatformOf(colored)).TextColor.Should().Be(Colors.Blue);
        ((SkiaLabel)CompatHost.PlatformOf(math)).Text.Should().Be("21");
    }

    [Theory]
    [InlineData(typeof(InvertedBoolConverter), true, false)]
    [InlineData(typeof(IsNullConverter), null, true)]
    [InlineData(typeof(IsNotNullConverter), null, false)]
    [InlineData(typeof(IntToBoolConverter), 0, false)]
    [InlineData(typeof(TextCaseConverter), "MiXed", "MiXed")]
    public void Converters_convert_without_platform_services(Type converterType, object? input, object? expected)
    {
        var converter = (IValueConverter)Activator.CreateInstance(converterType)!;
        var targetType = expected?.GetType() ?? typeof(object);
        converter.Convert(input, targetType, null, CultureInfo.InvariantCulture).Should().Be(expected);
    }

    // ---- Popup --------------------------------------------------------------

    [Fact]
    public async Task Popup_shows_as_a_modal_layer_and_returns_its_result()
    {
        var page = new ContentPage { Content = new Label { Text = "root" }, BackgroundColor = Colors.White };
        using var host = new CompatHost(page, b => b.UseMauiCommunityToolkit());
        host.Render();

        var message = new Label { Text = "Hello from the popup" };
        var popup = new Popup<string> { Content = message };
        bool opened = false;
        popup.Opened += (_, _) => opened = true;

        var resultTask = page.ShowPopupAsync<string>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false });

        host.Context.HasModal.Should().BeTrue("CT pushes a PopupPage onto the modal stack, which OpenMaui presents as a layer");
        host.Window.Navigation.ModalStack.Should().ContainSingle().Which.GetType().Name.Should().Be("PopupPage");
        host.Render();
        message.Handler.Should().NotBeNull("the popup content is realised by OpenMaui's handlers");
        CompatHost.PlatformOf(message).Should().BeOfType<SkiaLabel>();
        opened.Should().BeTrue();

        await popup.CloseAsync("done");
        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Result.Should().Be("done");
        result.WasDismissedByTappingOutsideOfPopup.Should().BeFalse();
        host.Context.HasModal.Should().BeFalse("closing the popup pops the modal layer");
    }

    [Fact]
    public async Task Popup_tapping_outside_dismisses_it()
    {
        var page = new ContentPage { Content = new Label { Text = "root" }, BackgroundColor = Colors.White };
        using var host = new CompatHost(page, b => b.UseMauiCommunityToolkit());
        host.Render();

        var popup = new Popup { Content = new Label { Text = "tap outside" }, WidthRequest = 200, HeightRequest = 100 };
        var resultTask = page.ShowPopupAsync(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        host.Render();

        host.DisplayWindow.RaisePointerPressed(5, 5);
        host.DisplayWindow.RaisePointerReleased(5, 5);
        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.WasDismissedByTappingOutsideOfPopup.Should().BeTrue();
        host.Context.HasModal.Should().BeFalse();
    }

    // ---- Alerts ---------------------------------------------------------------

    /// <summary>
    /// The toolkit's generic net10.0 build implements Toast and Snackbar as
    /// no-ops that complete successfully (its visual implementations are per
    /// platform: Android, iOS/Mac Catalyst, Windows). On OpenMaui they honour the
    /// API contract (awaitable, cancellable, Snackbar raises Shown/Dismissed)
    /// but draw nothing; that is the toolkit's behaviour on any platform it has
    /// no visual implementation for, not an OpenMaui gap.
    /// </summary>
    [Fact]
    public async Task Toast_and_Snackbar_complete_their_api_contract()
    {
        using var host = Host(new Label { Text = "x" });

        var toast = Toast.Make("Saved", CommunityToolkit.Maui.Core.ToastDuration.Short);
        await toast.Show();
        await toast.Dismiss();

        int shown = 0, dismissed = 0;
        EventHandler onShown = (_, _) => shown++, onDismissed = (_, _) => dismissed++;
        Snackbar.Shown += onShown;
        Snackbar.Dismissed += onDismissed;
        try
        {
            var snackbar = Snackbar.Make("Deleted", actionButtonText: "Undo", duration: TimeSpan.FromSeconds(1));
            await snackbar.Show();
            await snackbar.Dismiss();
        }
        finally
        {
            Snackbar.Shown -= onShown;
            Snackbar.Dismissed -= onDismissed;
        }
        shown.Should().Be(1);
        dismissed.Should().Be(1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = () => Toast.Make("x").Show(cts.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- Views ------------------------------------------------------------------

    [Fact]
    public void Expander_header_tap_expands_and_collapses_the_content()
    {
        var header = new Label { Text = "Details", HeightRequest = 40, BackgroundColor = Colors.LightGray };
        var body = new BoxView { Color = Colors.Red, HeightRequest = 100 };
        var expander = new Expander { Header = header, Content = body, VerticalOptions = LayoutOptions.Start };
        var changes = new List<bool>();
        expander.ExpandedChanged += (_, e) => changes.Add(e.IsExpanded);
        using var host = Host(expander);
        host.Render();

        expander.IsExpanded.Should().BeFalse();
        CompatHost.PlatformOf(body).IsVisible.Should().BeFalse();
        host.CountPixelsNear(new SKColor(255, 0, 0), host.WindowRect).Should().Be(0, "collapsed content is not drawn");

        host.Tap(header);
        host.Render();

        expander.IsExpanded.Should().BeTrue("a tap on the header toggles the expander through its TapGestureRecognizer");
        CompatHost.PlatformOf(body).IsVisible.Should().BeTrue();
        host.CountPixelsNear(new SKColor(255, 0, 0), host.WindowRect).Should().BeGreaterThan(800 * 90, "the expanded red body is painted below the header");
        CompatHost.PlatformOf(body).ScreenBounds.Top.Should().BeGreaterThanOrEqualTo(40);

        host.Tap(header);
        host.Render();

        expander.IsExpanded.Should().BeFalse();
        changes.Should().Equal(true, false);
    }

    [Fact]
    public void AvatarView_renders_its_initials_on_a_filled_circle()
    {
        var avatar = new AvatarView
        {
            Text = "AB",
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White,
            WidthRequest = 80,
            HeightRequest = 80,
            CornerRadius = 40,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        };
        using var host = Host(avatar);
        host.Render();

        avatar.Handler.Should().NotBeNull();
        var rect = CompatHost.RectOf(avatar);
        rect.Width.Should().Be(80);
        rect.Height.Should().Be(80);
        int blue = host.CountPixelsNear(new SKColor(0, 0, 255), rect);
        blue.Should().BeGreaterThan(3000, "the avatar background fills most of its circle");
        blue.Should().BeLessThan(6400, "the corners outside the circle are clipped");
        host.CountPixelsNear(new SKColor(255, 255, 255), new SKRectI(20, 20, 60, 60), tolerance: 60)
            .Should().BeGreaterThan(20, "the white initials are drawn in the middle");
        avatar.Content.Should().BeOfType<Label>().Which.Text.Should().Be("AB");
    }

    [Fact]
    public void DrawingView_resolves_to_a_skia_view_and_renders_its_lines()
    {
        var drawing = new DrawingView
        {
            LineColor = Colors.Red,
            LineWidth = 6,
            BackgroundColor = Colors.White,
            WidthRequest = 300,
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            IsMultiLineModeEnabled = true,
        };
        drawing.Lines.Add(new DrawingLine
        {
            LineColor = Colors.Blue,
            LineWidth = 8,
            Points = new System.Collections.ObjectModel.ObservableCollection<PointF> { new(10, 100), new(290, 100) },
        });
        using var host = Host(drawing);
        host.Render();

        drawing.Handler.Should().NotBeNull();
        CompatHost.PlatformOf(drawing).Should().NotBeNull("OpenMaui supplies a Skia platform view for the toolkit's DrawingView");
        host.CountPixelsNear(new SKColor(0, 0, 255), new SKRectI(0, 90, 300, 110)).Should().BeGreaterThan(1000, "the bound line is painted");
    }

    [Fact]
    public void DrawingView_pointer_strokes_become_lines_and_raise_events()
    {
        var completed = new List<IDrawingLine>();
        var drawing = new DrawingView
        {
            LineColor = Colors.Red,
            LineWidth = 6,
            BackgroundColor = Colors.White,
            WidthRequest = 300,
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        };
        drawing.DrawingLineCompleted += (_, e) => completed.Add(e.LastDrawingLine);
        using var host = Host(drawing);
        host.Render();

        host.DisplayWindow.RaisePointerPressed(20, 50);
        for (int x = 30; x <= 280; x += 10)
            host.DisplayWindow.RaisePointerMoved(x, 50);
        host.DisplayWindow.RaisePointerReleased(280, 50);
        host.Render();

        completed.Should().ContainSingle();
        drawing.Lines.Should().ContainSingle();
        drawing.Lines[0].Points.Count.Should().BeGreaterThan(10);
        drawing.Lines[0].LineColor.Should().Be(Colors.Red);
        host.CountPixelsNear(new SKColor(255, 0, 0), new SKRectI(0, 40, 300, 60)).Should().BeGreaterThan(800, "the stroke is painted");
    }

    private sealed class TextChangedArgsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Microsoft.Maui.Controls.TextChangedEventArgs e ? e.NewTextValue : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class ConverterViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isBusy;
        private string? _name;
        private int _count;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool IsBusy { get => _isBusy; set { _isBusy = value; Raise(nameof(IsBusy)); } }
        public string? Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
        public int Count { get => _count; set { _count = value; Raise(nameof(Count)); } }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new(name));
    }

    [Fact]
    public void FolderPicker_and_FileSaver_get_Linux_implementations()
    {
        // The toolkit's platform-neutral build implements neither; OpenMaui
        // installs portal-backed ones when the app is built.
        using var host = new CompatHost(new ContentPage(), b => b.UseMauiCommunityToolkit());

        CommunityToolkit.Maui.Storage.FolderPicker.Default.GetType().Name
            .Should().NotBe("FolderPickerImplementation", "the stub that always fails was replaced");
        CommunityToolkit.Maui.Storage.FileSaver.Default.GetType().Name
            .Should().NotBe("FileSaverImplementation");
    }

    [Fact]
    public void A_picked_folder_comes_back_as_a_successful_toolkit_result()
    {
        var result = (CommunityToolkit.Maui.Storage.FolderPickerResult)Microsoft.Maui.Platform.Linux.Services.ToolkitFolderPicker
            .ResultFor(typeof(CommunityToolkit.Maui.Storage.FolderPickerResult), "/home/user/Documents/Gitea");

        result.IsSuccessful.Should().BeTrue(result.Exception?.ToString());
        result.Folder!.Path.Should().Be("/home/user/Documents/Gitea");
        result.Folder.Name.Should().Be("Gitea");
    }
}
