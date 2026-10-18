// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;
using LinuxHandlers = Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Code written against MAUI's handler API compiles and works on OpenMaui's handlers: they
/// implement MAUI's I&lt;Control&gt;Handler interfaces, have MAUI's interface-typed static Map
/// methods (so a <c>PropertyMapper&lt;IEntry, IEntryHandler&gt;</c> customization binds to
/// them), <see cref="ILayoutHandler"/>'s operations and <see cref="IImageHandler.SourceLoader"/>
/// work, and MAUI's platform extensions (<c>UpdateText(platformLabel, label)</c>, ...) exist
/// for the Skia views.
/// </summary>
[Collection("LinuxApplication.Current")]
public class MauiHandlerApiTests
{
    private static THandler Connect<THandler>(THandler handler, IElement view) where THandler : IElementHandler
    {
        handler.SetMauiContext(HeadlessMauiContext.Instance);
        view.Handler = handler;
        handler.SetVirtualView(view);
        return handler;
    }

    public static IEnumerable<object[]> HandlerInterfaces() => new[]
    {
        new object[] { typeof(LinuxHandlers.ActivityIndicatorHandler), typeof(IActivityIndicatorHandler) },
        new object[] { typeof(LinuxHandlers.BorderHandler), typeof(IBorderHandler) },
        new object[] { typeof(LinuxHandlers.ButtonHandler), typeof(IButtonHandler) },
        new object[] { typeof(LinuxHandlers.CheckBoxHandler), typeof(ICheckBoxHandler) },
        new object[] { typeof(LinuxHandlers.ContentViewHandler), typeof(IContentViewHandler) },
        new object[] { typeof(LinuxHandlers.DatePickerHandler), typeof(IDatePickerHandler) },
        new object[] { typeof(LinuxHandlers.EditorHandler), typeof(IEditorHandler) },
        new object[] { typeof(LinuxHandlers.EntryHandler), typeof(IEntryHandler) },
        new object[] { typeof(LinuxHandlers.FlyoutPageHandler), typeof(IFlyoutViewHandler) },
        new object[] { typeof(LinuxHandlers.GraphicsViewHandler), typeof(IGraphicsViewHandler) },
        new object[] { typeof(LinuxHandlers.ImageHandler), typeof(IImageHandler) },
        new object[] { typeof(LinuxHandlers.ImageButtonHandler), typeof(IImageButtonHandler) },
        new object[] { typeof(LinuxHandlers.LabelHandler), typeof(ILabelHandler) },
        new object[] { typeof(LinuxHandlers.LayoutHandler), typeof(ILayoutHandler) },
        new object[] { typeof(LinuxHandlers.GridHandler), typeof(ILayoutHandler) },
        new object[] { typeof(LinuxHandlers.MenuBarHandler), typeof(IMenuBarHandler) },
        new object[] { typeof(LinuxHandlers.MenuFlyoutHandler), typeof(IMenuFlyoutHandler) },
        new object[] { typeof(LinuxHandlers.CoreNavigationViewHandler), typeof(INavigationViewHandler) },
        new object[] { typeof(LinuxHandlers.NavigationPageHandler), typeof(INavigationViewHandler) },
        new object[] { typeof(LinuxHandlers.CorePageHandler), typeof(IPageHandler) },
        new object[] { typeof(LinuxHandlers.ContentPageHandler), typeof(IPageHandler) },
        new object[] { typeof(LinuxHandlers.PickerHandler), typeof(IPickerHandler) },
        new object[] { typeof(LinuxHandlers.ProgressBarHandler), typeof(IProgressBarHandler) },
        new object[] { typeof(LinuxHandlers.RadioButtonHandler), typeof(IRadioButtonHandler) },
        new object[] { typeof(LinuxHandlers.ScrollViewHandler), typeof(IScrollViewHandler) },
        new object[] { typeof(LinuxHandlers.SearchBarHandler), typeof(ISearchBarHandler) },
        new object[] { typeof(LinuxHandlers.SliderHandler), typeof(ISliderHandler) },
        new object[] { typeof(LinuxHandlers.StepperHandler), typeof(IStepperHandler) },
        new object[] { typeof(LinuxHandlers.SwitchHandler), typeof(ISwitchHandler) },
        new object[] { typeof(LinuxHandlers.TabbedPageHandler), typeof(ITabbedViewHandler) },
        new object[] { typeof(LinuxHandlers.TimePickerHandler), typeof(ITimePickerHandler) },
        new object[] { typeof(LinuxHandlers.WebViewHandler), typeof(IWebViewHandler) },
    };

    [Theory]
    [MemberData(nameof(HandlerInterfaces))]
    public void Linux_handlers_implement_MAUIs_handler_interface(Type handler, Type mauiInterface)
    {
        handler.Should().BeAssignableTo(mauiInterface);
    }

    [Fact]
    public void A_MAUI_style_entry_mapper_binds_to_the_interface_typed_Map_methods()
    {
        var appended = new List<object?>();
        // As MAUI's own tests and apps write it: the Map methods take IEntryHandler.
        var mapper = new PropertyMapper<IEntry, IEntryHandler>(LinuxHandlers.EntryHandler.Mapper)
        {
            [nameof(IEntry.Keyboard)] = LinuxHandlers.EntryHandler.MapKeyboard,
            [nameof(IEntry.IsPassword)] = LinuxHandlers.EntryHandler.MapIsPassword,
            [nameof(IEntry.IsReadOnly)] = LinuxHandlers.EntryHandler.MapIsReadOnly,
        };
        mapper.AppendToMapping(nameof(IEntry.Placeholder), (handler, entry) => appended.Add(handler.PlatformView));

        var entry = new Entry { Keyboard = Keyboard.Numeric, IsPassword = true, IsReadOnly = true, Placeholder = "Name" };
        var handler = Connect(new LinuxHandlers.EntryHandler(mapper, LinuxHandlers.EntryHandler.CommandMapper), entry);
        var platform = handler.PlatformView;

        platform.Keyboard.Should().Be(Keyboard.Numeric);
        platform.IsPassword.Should().BeTrue();
        platform.IsReadOnly.Should().BeTrue();
        platform.Placeholder.Should().Be("Name");
        appended.Should().ContainSingle().Which.Should().BeSameAs(platform, "IEntryHandler.PlatformView is the Skia entry");
        ((IEntryHandler)handler).VirtualView.Should().BeSameAs(entry);

        entry.IsPassword = false;
        platform.IsPassword.Should().BeFalse();
    }

    [Fact]
    public void Map_methods_called_with_the_concrete_handler_run_the_Linux_mapping()
    {
        var label = new Label { Text = "Hello", CharacterSpacing = 2 };
        var handler = Connect(new LinuxHandlers.LabelHandler(), label);
        label.Text = "unchanged on the platform until mapped";
        handler.PlatformView.Text = "stale";

        // A concrete LabelHandler argument binds to MAUI's (ILabelHandler, ILabel) overload.
        LinuxHandlers.LabelHandler.MapText(handler, label);
        handler.PlatformView.Text.Should().Be(label.Text);

        // So does a delegate typed to the concrete handler, as OpenMaui's own mappers have.
        Action<LinuxHandlers.LabelHandler, ILabel> map = LinuxHandlers.LabelHandler.MapCharacterSpacing;
        label.CharacterSpacing = 5;
        map(handler, label);
        handler.PlatformView.CharacterSpacing.Should().Be(5);

        // An interface-typed handler reaches the same mapping.
        ILabelHandler asInterface = handler;
        label.Text = "through ILabelHandler";
        LinuxHandlers.LabelHandler.MapText(asInterface, label);
        handler.PlatformView.Text.Should().Be("through ILabelHandler");
        asInterface.PlatformView.Should().BeSameAs(handler.PlatformView);
    }

    /// <summary>A layout whose child changes do not notify its handler, so the test drives ILayoutHandler itself.</summary>
    private sealed class SilentLayout : VerticalStackLayout
    {
        protected override void OnAdd(int index, IView view) { }
        protected override void OnInsert(int index, IView view) { }
        protected override void OnRemove(int index, IView view) { }
        protected override void OnClear() { }
        protected override void OnUpdate(int index, IView view, IView oldView) { }
    }

    private static SkiaView PlatformOf(IView view) => (SkiaView)view.Handler!.PlatformView!;

    [Fact]
    public void ILayoutHandler_adds_inserts_removes_updates_and_clears_the_platform_children()
    {
        var a = new Label { Text = "a" };
        var layout = new SilentLayout();
        layout.Add(a);
        var handler = Connect(new LinuxHandlers.LayoutHandler(), layout);
        ILayoutHandler layoutHandler = handler;
        var platform = handler.PlatformView;
        platform.Children.Should().Equal(PlatformOf(a));
        layoutHandler.VirtualView.Should().BeSameAs(layout);
        layoutHandler.PlatformView.Should().BeSameAs(platform);

        var b = new Label { Text = "b" };
        layout.Add(b);
        layoutHandler.Add(b);
        platform.Children.Should().Equal(PlatformOf(a), PlatformOf(b));

        var c = new Label { Text = "c" };
        layout.Insert(0, c);
        layoutHandler.Insert(0, c);
        platform.Children.Should().Equal(PlatformOf(c), PlatformOf(a), PlatformOf(b));

        layout.Remove(a);
        layoutHandler.Remove(a);
        platform.Children.Should().Equal(PlatformOf(c), PlatformOf(b));

        var d = new Label { Text = "d" };
        layout[1] = d;
        layoutHandler.Update(1, d);
        platform.Children.Should().Equal(PlatformOf(c), PlatformOf(d));

        d.ZIndex = 7;
        layoutHandler.UpdateZIndex(d);
        PlatformOf(d).ZIndex.Should().Be(7);

        layout.Clear();
        layoutHandler.Clear();
        platform.Children.Should().BeEmpty();
    }

    [Fact]
    public void MAUIs_layout_command_signatures_drive_the_Linux_layout()
    {
        var layout = new SilentLayout();
        var handler = Connect(new LinuxHandlers.LayoutHandler(), layout);
        var child = new Label { Text = "child" };
        layout.Add(child);

        LinuxHandlers.LayoutHandler.MapAdd(handler, layout, new LayoutHandlerUpdate(0, child));
        handler.PlatformView.Children.Should().Equal(PlatformOf(child));

        LinuxHandlers.LayoutHandler.MapClear(handler, layout, null);
        handler.PlatformView.Children.Should().BeEmpty();
    }

    private static string CreatePng(SKColor color)
    {
        var dir = Path.Combine(Path.GetTempPath(), "openmaui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "image.png");
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }

    [Fact]
    public async Task IImageHandler_SourceLoader_loads_the_image_source()
    {
        // The Source mapping is replaced by a no-op, so only SourceLoader loads the picture.
        var mapper = new PropertyMapper<IImage, IImageHandler>(LinuxHandlers.ImageHandler.Mapper)
        {
            [nameof(IImage.Source)] = (_, _) => { },
        };
        var image = new Image { Source = ImageSource.FromFile(CreatePng(SKColors.Green)) };
        var handler = Connect(new LinuxHandlers.ImageHandler(mapper, null), image);
        handler.PlatformView.Bitmap.Should().BeNull("the source was not mapped");

        IImageHandler imageHandler = handler;
        imageHandler.SourceLoader.Should().BeSameAs(imageHandler.SourceLoader);
        await imageHandler.SourceLoader.UpdateImageSourceAsync();
        for (var i = 0; i < 100 && handler.PlatformView.Bitmap is null; i++)
            await Task.Delay(20);

        handler.PlatformView.Bitmap.Should().NotBeNull();
        handler.PlatformView.Bitmap!.GetPixel(0, 0).Should().Be(SKColors.Green);
        image.IsLoading.Should().BeFalse();

        image.Source = null;
        await imageHandler.SourceLoader.UpdateImageSourceAsync();
        handler.PlatformView.Bitmap.Should().BeNull("a null source clears the picture");
    }

    [Fact]
    public void Label_platform_extensions_apply_the_label_properties()
    {
        var label = new Label
        {
            Text = "Title",
            TextColor = Colors.Red,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.5,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Start,
            Padding = new Thickness(1, 2, 3, 4),
            MaxLines = 3,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        var platformLabel = new SkiaLabel();

        platformLabel.UpdateText(label);
        platformLabel.UpdateTextColor(label);
        platformLabel.UpdateFont(label);
        platformLabel.UpdateCharacterSpacing(label);
        platformLabel.UpdateHorizontalTextAlignment(label);
        platformLabel.UpdateVerticalTextAlignment(label);
        platformLabel.UpdatePadding(label);
        platformLabel.UpdateLineBreakMode(label);

        platformLabel.Text.Should().Be("Title");
        platformLabel.TextColor.Should().Be(Colors.Red);
        platformLabel.FontSize.Should().Be(22);
        platformLabel.FontAttributes.Should().Be(FontAttributes.Bold);
        platformLabel.CharacterSpacing.Should().Be(1.5);
        platformLabel.HorizontalTextAlignment.Should().Be(TextAlignment.End);
        platformLabel.VerticalTextAlignment.Should().Be(TextAlignment.Start);
        platformLabel.Padding.Should().Be(new Thickness(1, 2, 3, 4));
        platformLabel.MaxLines.Should().Be(3);

        // A truncating mode is single-line; back to word wrap restores MaxLines.
        label.LineBreakMode = LineBreakMode.HeadTruncation;
        platformLabel.UpdateLineBreakMode(label);
        platformLabel.MaxLines.Should().Be(1);
        label.LineBreakMode = LineBreakMode.WordWrap;
        platformLabel.UpdateLineBreakMode(label);
        platformLabel.MaxLines.Should().Be(3);

        label.MaxLines = 2;
        platformLabel.UpdateMaxLines(label);
        platformLabel.MaxLines.Should().Be(2);
    }

    [Fact]
    public void Text_input_platform_extensions_apply_the_entry_and_editor_properties()
    {
        var entry = new Entry
        {
            Text = "abcdef",
            MaxLength = 4,
            Placeholder = "Code",
            PlaceholderColor = Colors.Gray,
            IsReadOnly = true,
            IsPassword = true,
            Keyboard = Keyboard.Email,
            ReturnType = ReturnType.Search,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        var platformEntry = new SkiaEntry();

        platformEntry.UpdateMaxLength(entry);
        platformEntry.UpdateText(entry);
        platformEntry.UpdatePlaceholder(entry);
        platformEntry.UpdatePlaceholderColor(entry);
        platformEntry.UpdateIsReadOnly(entry);
        platformEntry.UpdateIsPassword(entry);
        platformEntry.UpdateKeyboard(entry);
        platformEntry.UpdateReturnType(entry);
        platformEntry.UpdateClearButtonVisibility(entry);
        platformEntry.UpdateHorizontalTextAlignment(entry);

        platformEntry.Text.Should().Be("abcd", "the text is cut to MaxLength");
        entry.Text.Should().Be("abcd", "the text the platform holds is the view's text, as on MAUI's platforms");
        platformEntry.Placeholder.Should().Be("Code");
        platformEntry.PlaceholderColor.Should().Be(Colors.Gray);
        platformEntry.IsReadOnly.Should().BeTrue();
        platformEntry.IsPassword.Should().BeTrue();
        platformEntry.Keyboard.Should().Be(Keyboard.Email);
        platformEntry.ReturnType.Should().Be(ReturnType.Search);
        platformEntry.ShowClearButton.Should().BeTrue();
        platformEntry.HorizontalTextAlignment.Should().Be(TextAlignment.Center);

        var editor = new Editor { Text = "notes", Placeholder = "Notes", IsReadOnly = true, Keyboard = Keyboard.Text, CharacterSpacing = 2 };
        var platformEditor = new SkiaEditor();
        platformEditor.UpdateText(editor);
        platformEditor.UpdatePlaceholder(editor);
        platformEditor.UpdateIsReadOnly(editor);
        platformEditor.UpdateKeyboard(editor);
        platformEditor.UpdateCharacterSpacing(editor);

        platformEditor.Text.Should().Be("notes");
        platformEditor.Placeholder.Should().Be("Notes");
        platformEditor.IsReadOnly.Should().BeTrue();
        platformEditor.Keyboard.Should().Be(Keyboard.Text);
        platformEditor.CharacterSpacing.Should().Be(2);
    }

    [Fact]
    public void Button_platform_extensions_apply_text_and_stroke()
    {
        var button = new Button { Text = "OK", BorderColor = Colors.Blue, BorderWidth = 2, CornerRadius = 6, Padding = new Thickness(4) };
        var platformButton = new SkiaButton();

        platformButton.UpdateText(button);
        platformButton.UpdateStrokeColor(button);
        platformButton.UpdateStrokeThickness(button);
        platformButton.UpdateCornerRadius(button);
        platformButton.UpdatePadding(button);

        platformButton.Text.Should().Be("OK");
        platformButton.BorderColor.Should().Be(Colors.Blue);
        platformButton.BorderWidth.Should().Be(2);
        platformButton.CornerRadius.Should().Be(6);
        platformButton.Padding.Should().Be(new Thickness(4));
    }
}
