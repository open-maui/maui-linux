// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class LinuxProgramHost
{
    public static void Run<TApp>(string[] args) where TApp : class, IApplication, new()
    {
        Run<TApp>(args, null);
    }

    public static void Run<TApp>(string[] args, Action<MauiAppBuilder>? configure) where TApp : class, IApplication, new()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseLinux();
        configure?.Invoke(builder);
        builder.UseMauiApp<TApp>();
        var mauiApp = builder.Build();

        var options = mauiApp.Services.GetService<LinuxApplicationOptions>()
                     ?? new LinuxApplicationOptions();
        ParseCommandLineOptions(args, options);

        using var linuxApp = new LinuxApplication();
        linuxApp.Initialize(options);

        // Create comprehensive demo UI with ALL controls
        var rootView = CreateComprehensiveDemo();
        linuxApp.RootView = rootView;

        linuxApp.Run();
    }

    private static void ParseCommandLineOptions(string[] args, LinuxApplicationOptions options)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--title" when i + 1 < args.Length:
                    options.Title = args[++i];
                    break;
                case "--width" when i + 1 < args.Length && int.TryParse(args[i + 1], out var w):
                    options.Width = w;
                    i++;
                    break;
                case "--height" when i + 1 < args.Length && int.TryParse(args[i + 1], out var h):
                    options.Height = h;
                    i++;
                    break;
            }
        }
    }

    private static SkiaView CreateComprehensiveDemo()
    {
        // Create scrollable container
        var scroll = new SkiaScrollView();
        
        var root = new SkiaStackLayout
        {
            Orientation = StackOrientation.Vertical,
            Spacing = 15,
            BackgroundColor = new SKColor(0xF5, 0xF5, 0xF5)
        };
        root.Padding = new SKRect(20, 20, 20, 20);

        // ========== TITLE ==========
        root.AddChild(new SkiaLabel 
        { 
            Text = "MAUI Linux Control Demo", 
            FontSize = 28, 
            TextColor = new SKColor(0x1A, 0x23, 0x7E),
            IsBold = true
        });
        root.AddChild(new SkiaLabel 
        { 
            Text = "All controls rendered using SkiaSharp on X11", 
            FontSize = 14, 
            TextColor = SKColors.Gray 
        });

        // ========== LABELS SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Labels"));
        var labelSection = new SkiaStackLayout { Orientation = StackOrientation.Vertical, Spacing = 5 };
        labelSection.AddChild(new SkiaLabel { Text = "Normal Label", FontSize = 16, TextColor = SKColors.Black });
        labelSection.AddChild(new SkiaLabel { Text = "Bold Label", FontSize = 16, TextColor = SKColors.Black, IsBold = true });
        labelSection.AddChild(new SkiaLabel { Text = "Italic Label", FontSize = 16, TextColor = SKColors.Gray, IsItalic = true });
        labelSection.AddChild(new SkiaLabel { Text = "Colored Label (Pink)", FontSize = 16, TextColor = new SKColor(0xE9, 0x1E, 0x63) });
        root.AddChild(labelSection);

        // ========== BUTTONS SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Buttons"));
        var buttonSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
        
        var btnPrimary = new SkiaButton { Text = "Primary", FontSize = 14 };
        btnPrimary.BackgroundColor = new SKColor(0x21, 0x96, 0xF3);
        btnPrimary.TextColor = SKColors.White;
        var clickCount = 0;
        btnPrimary.Clicked += (s, e) => { clickCount++; btnPrimary.Text = $"Clicked {clickCount}x"; };
        buttonSection.AddChild(btnPrimary);

        var btnSuccess = new SkiaButton { Text = "Success", FontSize = 14 };
        btnSuccess.BackgroundColor = new SKColor(0x4C, 0xAF, 0x50);
        btnSuccess.TextColor = SKColors.White;
        buttonSection.AddChild(btnSuccess);

        var btnDanger = new SkiaButton { Text = "Danger", FontSize = 14 };
        btnDanger.BackgroundColor = new SKColor(0xF4, 0x43, 0x36);
        btnDanger.TextColor = SKColors.White;
        buttonSection.AddChild(btnDanger);
        
        root.AddChild(buttonSection);

        // ========== ENTRY SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Text Entry"));
        var entry = new SkiaEntry { Placeholder = "Type here...", FontSize = 14 };
        root.AddChild(entry);

        // ========== SEARCHBAR SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("SearchBar"));
        var searchBar = new SkiaSearchBar { Placeholder = "Search for items..." };
        var searchResultLabel = new SkiaLabel { Text = "", FontSize = 12, TextColor = SKColors.Gray };
        searchBar.TextChanged += (s, e) => searchResultLabel.Text = $"Searching: {e.NewTextValue}";
        searchBar.SearchButtonPressed += (s, e) => searchResultLabel.Text = $"Search submitted: {searchBar.Text}";
        root.AddChild(searchBar);
        root.AddChild(searchResultLabel);

        // ========== EDITOR SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Editor (Multi-line)"));
        var editor = new SkiaEditor 
        { 
            Placeholder = "Enter multiple lines of text...", 
            FontSize = 14,
            BackgroundColor = SKColors.White
        };
        root.AddChild(editor);

        // ========== CHECKBOX SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("CheckBox"));
        var checkSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 20 };
        var cb1 = new SkiaCheckBox { IsChecked = true };
        checkSection.AddChild(cb1);
        checkSection.AddChild(new SkiaLabel { Text = "Checked", FontSize = 14 });
        var cb2 = new SkiaCheckBox { IsChecked = false };
        checkSection.AddChild(cb2);
        checkSection.AddChild(new SkiaLabel { Text = "Unchecked", FontSize = 14 });
        root.AddChild(checkSection);

        // ========== SWITCH SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Switch"));
        var switchSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 20 };
        var sw1 = new SkiaSwitch { IsOn = true };
        switchSection.AddChild(sw1);
        switchSection.AddChild(new SkiaLabel { Text = "On", FontSize = 14 });
        var sw2 = new SkiaSwitch { IsOn = false };
        switchSection.AddChild(sw2);
        switchSection.AddChild(new SkiaLabel { Text = "Off", FontSize = 14 });
        root.AddChild(switchSection);

        // ========== RADIOBUTTON SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("RadioButton"));
        var radioSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 15 };
        radioSection.AddChild(new SkiaRadioButton { Content = "Option A", IsChecked = true, GroupName = "demo" });
        radioSection.AddChild(new SkiaRadioButton { Content = "Option B", IsChecked = false, GroupName = "demo" });
        radioSection.AddChild(new SkiaRadioButton { Content = "Option C", IsChecked = false, GroupName = "demo" });
        root.AddChild(radioSection);

        // ========== SLIDER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Slider"));
        var sliderLabel = new SkiaLabel { Text = "Value: 50", FontSize = 14 };
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100, Value = 50 };
        slider.ValueChanged += (s, e) => sliderLabel.Text = $"Value: {(int)slider.Value}";
        root.AddChild(slider);
        root.AddChild(sliderLabel);

        // ========== STEPPER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Stepper"));
        var stepperSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
        var stepperLabel = new SkiaLabel { Text = "Value: 5", FontSize = 14 };
        var stepper = new SkiaStepper { Value = 5, Minimum = 0, Maximum = 10, Increment = 1 };
        stepper.ValueChanged += (s, e) => stepperLabel.Text = $"Value: {(int)stepper.Value}";
        stepperSection.AddChild(stepper);
        stepperSection.AddChild(stepperLabel);
        root.AddChild(stepperSection);

        // ========== PROGRESSBAR SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("ProgressBar"));
        var progress = new SkiaProgressBar { Progress = 0.7f };
        root.AddChild(progress);
        root.AddChild(new SkiaLabel { Text = "70% Complete", FontSize = 12, TextColor = SKColors.Gray });

        // ========== ACTIVITYINDICATOR SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("ActivityIndicator"));
        var activitySection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
        var activity = new SkiaActivityIndicator { IsRunning = true };
        activitySection.AddChild(activity);
        activitySection.AddChild(new SkiaLabel { Text = "Loading...", FontSize = 14, TextColor = SKColors.Gray });
        root.AddChild(activitySection);

        // ========== PICKER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Picker (Dropdown)"));
        var picker = new SkiaPicker { Title = "Select an item" };
        picker.SetItems(new[] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape" });
        var pickerLabel = new SkiaLabel { Text = "Selected: (none)", FontSize = 12, TextColor = SKColors.Gray };
        picker.SelectedIndexChanged += (s, e) => pickerLabel.Text = $"Selected: {picker.SelectedItem}";
        root.AddChild(picker);
        root.AddChild(pickerLabel);

        // ========== DATEPICKER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("DatePicker"));
        var datePicker = new SkiaDatePicker { Date = DateTime.Today };
        var dateLabel = new SkiaLabel { Text = $"Date: {DateTime.Today:d}", FontSize = 12, TextColor = SKColors.Gray };
        datePicker.DateSelected += (s, e) => dateLabel.Text = $"Date: {datePicker.Date:d}";
        root.AddChild(datePicker);
        root.AddChild(dateLabel);

        // ========== TIMEPICKER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("TimePicker"));
        var timePicker = new SkiaTimePicker();
        var timeLabel = new SkiaLabel { Text = $"Time: {DateTime.Now:t}", FontSize = 12, TextColor = SKColors.Gray };
        timePicker.TimeSelected += (s, e) => timeLabel.Text = $"Time: {DateTime.Today.Add(timePicker.Time):t}";
        root.AddChild(timePicker);
        root.AddChild(timeLabel);

        // ========== BORDER SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Border"));
        var border = new SkiaBorder
        {
            CornerRadius = 8,
            StrokeThickness = 2,
            Stroke = new SKColor(0x21, 0x96, 0xF3),
            BackgroundColor = new SKColor(0xE3, 0xF2, 0xFD)
        };
        border.SetPadding(15);
        border.AddChild(new SkiaLabel { Text = "Content inside a styled Border", FontSize = 14, TextColor = new SKColor(0x1A, 0x23, 0x7E) });
        root.AddChild(border);

        // ========== FRAME SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Frame (with shadow)"));
        var frame = new SkiaFrame();
        frame.BackgroundColor = SKColors.White;
        frame.AddChild(new SkiaLabel { Text = "Content inside a Frame with shadow effect", FontSize = 14 });
        root.AddChild(frame);

        // ========== COLLECTIONVIEW SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("CollectionView (List)"));
        var collectionView = new SkiaCollectionView
        {
            SelectionMode = SkiaSelectionMode.Single,
            Header = "Fruits",
            Footer = "End of list"
        };
        collectionView.ItemsSource =(new object[] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape", "Honeydew" });
        var collectionLabel = new SkiaLabel { Text = "Selected: (none)", FontSize = 12, TextColor = SKColors.Gray };
        collectionView.SelectionChanged += (s, e) => 
        {
            var selected = e.CurrentSelection.FirstOrDefault();
            collectionLabel.Text = $"Selected: {selected}";
        };
        root.AddChild(collectionView);
        root.AddChild(collectionLabel);

        // ========== IMAGEBUTTON SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("ImageButton"));
        var imageButtonSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
        
        // Create ImageButton with a generated icon (since we don't have image files)
        var imgBtn = new SkiaImageButton
        {
            CornerRadius = 8,
            StrokeColor = new SKColor(0x21, 0x96, 0xF3),
            StrokeThickness = 1,
            BackgroundColor = new SKColor(0xE3, 0xF2, 0xFD),
            PaddingLeft = 10,
            PaddingRight = 10,
            PaddingTop = 10,
            PaddingBottom = 10
        };
        // Generate a simple star icon bitmap
        var iconBitmap = CreateStarIcon(32, new SKColor(0x21, 0x96, 0xF3));
        imgBtn.Bitmap = iconBitmap;
        var imgBtnLabel = new SkiaLabel { Text = "Click the star!", FontSize = 12, TextColor = SKColors.Gray };
        imgBtn.Clicked += (s, e) => imgBtnLabel.Text = "Star clicked!";
        imageButtonSection.AddChild(imgBtn);
        imageButtonSection.AddChild(imgBtnLabel);
        root.AddChild(imageButtonSection);

        // ========== IMAGE SECTION ==========
        root.AddChild(CreateSeparator());
        root.AddChild(CreateSectionHeader("Image"));
        var imageSection = new SkiaStackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10 };
        
        // Create Image with a generated sample image
        var img = new SkiaImage();
        var sampleBitmap = CreateSampleImage(80, 60);
        img.Bitmap = sampleBitmap;
        imageSection.AddChild(img);
        imageSection.AddChild(new SkiaLabel { Text = "Sample generated image", FontSize = 12, TextColor = SKColors.Gray });
        root.AddChild(imageSection);

        // ========== FOOTER ==========
        root.AddChild(CreateSeparator());
        root.AddChild(new SkiaLabel 
        { 
            Text = "All 25+ controls are interactive - try them all!", 
            FontSize = 16, 
            TextColor = new SKColor(0x4C, 0xAF, 0x50),
            IsBold = true
        });
        root.AddChild(new SkiaLabel 
        { 
            Text = "Scroll down to see more controls", 
            FontSize = 12, 
            TextColor = SKColors.Gray
        });

        scroll.Content = root;
        return scroll;
    }

    private static SkiaLabel CreateSectionHeader(string text)
    {
        return new SkiaLabel
        {
            Text = text,
            FontSize = 18,
            TextColor = new SKColor(0x37, 0x47, 0x4F),
            IsBold = true
        };
    }

    private static SkiaView CreateSeparator()
    {
        var sep = new SkiaLabel { Text = "", BackgroundColor = new SKColor(0xE0, 0xE0, 0xE0), RequestedHeight = 1 };
        return sep;
    }

    private static SKBitmap CreateStarIcon(int size, SKColor color)
    {
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw a 5-point star
        using var path = new SKPath();
        var cx = size / 2f;
        var cy = size / 2f;
        var outerRadius = size / 2f - 2;
        var innerRadius = outerRadius * 0.4f;

        for (int i = 0; i < 5; i++)
        {
            var outerAngle = (i * 72 - 90) * Math.PI / 180;
            var innerAngle = ((i * 72) + 36 - 90) * Math.PI / 180;

            var ox = cx + outerRadius * (float)Math.Cos(outerAngle);
            var oy = cy + outerRadius * (float)Math.Sin(outerAngle);
            var ix = cx + innerRadius * (float)Math.Cos(innerAngle);
            var iy = cy + innerRadius * (float)Math.Sin(innerAngle);

            if (i == 0)
                path.MoveTo(ox, oy);
            else
                path.LineTo(ox, oy);

            path.LineTo(ix, iy);
        }
        path.Close();
        canvas.DrawPath(path, paint);

        return bitmap;
    }

    private static SKBitmap CreateSampleImage(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        // Draw gradient background
        using var bgPaint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, height),
            new SKColor[] { new SKColor(0x42, 0xA5, 0xF5), new SKColor(0x7E, 0x57, 0xC2) },
            new float[] { 0, 1 },
            SKShaderTileMode.Clamp);
        bgPaint.Shader = shader;
        canvas.DrawRect(0, 0, width, height, bgPaint);

        // Draw some shapes
        using var shapePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(width * 0.3f, height * 0.4f, 15, shapePaint);
        canvas.DrawRect(width * 0.5f, height * 0.3f, 20, 20, shapePaint);

        // Draw "IMG" text
        using var font = new SKFont(SKTypeface.Default, 12);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawText("IMG", 10, height - 8, textPaint);

        return bitmap;
    }
}
