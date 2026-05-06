# Creating Custom Controls for OpenMaui Linux

This guide explains how to create custom controls that integrate with the OpenMaui Linux platform.

## Overview

OpenMaui Linux uses a layered architecture:

1. **MAUI Virtual Views** - Standard .NET MAUI controls (Button, Label, etc.)
2. **Handlers** - Bridge between MAUI and platform views
3. **Platform Views** - SkiaSharp-rendered controls

When creating custom controls, you can either:
- Create a MAUI control with a custom handler (recommended for reusable controls)
- Create a platform-specific SkiaView directly (for Linux-only functionality)

## Creating a MAUI Control with Handler

### Step 1: Define the MAUI Control

Create a standard MAUI control that inherits from `View`:

```csharp
// Controls/RatingControl.cs
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MyApp.Controls;

public class RatingControl : View
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(int),
            typeof(RatingControl),
            0,
            BindingMode.TwoWay);

    public static readonly BindableProperty MaxValueProperty =
        BindableProperty.Create(
            nameof(MaxValue),
            typeof(int),
            typeof(RatingControl),
            5);

    public static readonly BindableProperty StarColorProperty =
        BindableProperty.Create(
            nameof(StarColor),
            typeof(Color),
            typeof(RatingControl),
            Colors.Gold);

    public static readonly BindableProperty EmptyStarColorProperty =
        BindableProperty.Create(
            nameof(EmptyStarColor),
            typeof(Color),
            typeof(RatingControl),
            Colors.Gray);

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int MaxValue
    {
        get => (int)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public Color StarColor
    {
        get => (Color)GetValue(StarColorProperty);
        set => SetValue(StarColorProperty, value);
    }

    public Color EmptyStarColor
    {
        get => (Color)GetValue(EmptyStarColorProperty);
        set => SetValue(EmptyStarColorProperty, value);
    }

    public event EventHandler<int>? ValueChanged;

    internal void SendValueChanged(int value)
    {
        ValueChanged?.Invoke(this, value);
    }
}
```

### Step 2: Create the Platform View

Create a SkiaView that renders your control:

```csharp
// Platforms/Linux/SkiaRatingControl.cs
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace MyApp.Platforms.Linux;

public class SkiaRatingControl : SkiaView
{
    private int _value;
    private int _maxValue = 5;
    private Color _starColor = Colors.Gold;
    private Color _emptyStarColor = Colors.Gray;
    private const float StarSize = 24f;
    private const float StarSpacing = 4f;

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = Math.Clamp(value, 0, MaxValue);
                Invalidate();
            }
        }
    }

    public int MaxValue
    {
        get => _maxValue;
        set
        {
            if (_maxValue != value)
            {
                _maxValue = Math.Max(1, value);
                InvalidateMeasure();
            }
        }
    }

    public Color StarColor
    {
        get => _starColor;
        set
        {
            if (_starColor != value)
            {
                _starColor = value;
                Invalidate();
            }
        }
    }

    public Color EmptyStarColor
    {
        get => _emptyStarColor;
        set
        {
            if (_emptyStarColor != value)
            {
                _emptyStarColor = value;
                Invalidate();
            }
        }
    }

    public event EventHandler<int>? ValueChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = MaxValue * StarSize + (MaxValue - 1) * StarSpacing;
        return new Size(width, StarSize);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < MaxValue; i++)
        {
            var x = bounds.Left + i * (StarSize + StarSpacing);
            var y = bounds.Top;
            var color = i < Value ? StarColor : EmptyStarColor;

            DrawStar(canvas, x, y, StarSize, color);
        }
    }

    private void DrawStar(SKCanvas canvas, float x, float y, float size, Color color)
    {
        using var paint = new SKPaint
        {
            Color = color.ToSKColor(),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var path = CreateStarPath(x + size / 2, y + size / 2, size / 2, size / 4);
        canvas.DrawPath(path, paint);
    }

    private SKPath CreateStarPath(float cx, float cy, float outerRadius, float innerRadius)
    {
        var path = new SKPath();
        var angle = -Math.PI / 2;
        var step = Math.PI / 5;

        path.MoveTo(
            cx + (float)(outerRadius * Math.Cos(angle)),
            cy + (float)(outerRadius * Math.Sin(angle)));

        for (int i = 0; i < 10; i++)
        {
            angle += step;
            var radius = i % 2 == 0 ? innerRadius : outerRadius;
            path.LineTo(
                cx + (float)(radius * Math.Cos(angle)),
                cy + (float)(radius * Math.Sin(angle)));
        }

        path.Close();
        return path;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var starIndex = (int)((e.X - Bounds.Left) / (StarSize + StarSpacing));
        var newValue = Math.Clamp(starIndex + 1, 0, MaxValue);

        if (newValue != Value)
        {
            Value = newValue;
            ValueChanged?.Invoke(this, Value);
        }

        e.Handled = true;
    }
}
```

### Step 3: Create the Handler

Connect the MAUI control to the platform view:

```csharp
// Handlers/RatingControlHandler.cs
using Microsoft.Maui.Handlers;
using MyApp.Controls;
using MyApp.Platforms.Linux;

namespace MyApp.Handlers;

public partial class RatingControlHandler : ViewHandler<RatingControl, SkiaRatingControl>
{
    public static IPropertyMapper<RatingControl, RatingControlHandler> PropertyMapper =
        new PropertyMapper<RatingControl, RatingControlHandler>(ViewMapper)
        {
            [nameof(RatingControl.Value)] = MapValue,
            [nameof(RatingControl.MaxValue)] = MapMaxValue,
            [nameof(RatingControl.StarColor)] = MapStarColor,
            [nameof(RatingControl.EmptyStarColor)] = MapEmptyStarColor,
        };

    public static CommandMapper<RatingControl, RatingControlHandler> CommandMapper =
        new(ViewCommandMapper);

    public RatingControlHandler() : base(PropertyMapper, CommandMapper)
    {
    }

    protected override SkiaRatingControl CreatePlatformView()
    {
        return new SkiaRatingControl();
    }

    protected override void ConnectHandler(SkiaRatingControl platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ValueChanged += OnPlatformValueChanged;
    }

    protected override void DisconnectHandler(SkiaRatingControl platformView)
    {
        platformView.ValueChanged -= OnPlatformValueChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnPlatformValueChanged(object? sender, int value)
    {
        VirtualView.Value = value;
        VirtualView.SendValueChanged(value);
    }

    private static void MapValue(RatingControlHandler handler, RatingControl control)
    {
        handler.PlatformView.Value = control.Value;
    }

    private static void MapMaxValue(RatingControlHandler handler, RatingControl control)
    {
        handler.PlatformView.MaxValue = control.MaxValue;
    }

    private static void MapStarColor(RatingControlHandler handler, RatingControl control)
    {
        handler.PlatformView.StarColor = control.StarColor;
    }

    private static void MapEmptyStarColor(RatingControlHandler handler, RatingControl control)
    {
        handler.PlatformView.EmptyStarColor = control.EmptyStarColor;
    }
}
```

### Step 4: Register the Handler

Register your handler in `MauiProgram.cs`:

```csharp
using Microsoft.Maui.Hosting;
using MyApp.Controls;
using MyApp.Handlers;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseOpenMauiLinux()
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<RatingControl, RatingControlHandler>();
            });

        return builder.Build();
    }
}
```

### Step 5: Use in XAML

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:controls="clr-namespace:MyApp.Controls"
             x:Class="MyApp.MainPage">

    <VerticalStackLayout>
        <Label Text="Rate this product:" />
        <controls:RatingControl
            Value="{Binding Rating}"
            MaxValue="5"
            StarColor="Gold"
            EmptyStarColor="LightGray"
            ValueChanged="OnRatingChanged" />
    </VerticalStackLayout>
</ContentPage>
```

## Creating a Direct SkiaView

For Linux-only controls or simpler use cases, inherit directly from `SkiaView`:

```csharp
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;

public class CustomGauge : SkiaView
{
    private double _value;
    private double _minimum;
    private double _maximum = 100;

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            Invalidate();
        }
    }

    public double Minimum
    {
        get => _minimum;
        set { _minimum = value; Invalidate(); }
    }

    public double Maximum
    {
        get => _maximum;
        set { _maximum = value; Invalidate(); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(100, 100);  // Fixed size gauge
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var radius = Math.Min(bounds.Width, bounds.Height) / 2 - 10;

        // Draw background arc
        using var bgPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 10,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        canvas.DrawArc(
            new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius),
            135, 270, false, bgPaint);

        // Draw value arc
        var percentage = (Value - Minimum) / (Maximum - Minimum);
        var sweepAngle = (float)(270 * percentage);

        using var valuePaint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 10,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        canvas.DrawArc(
            new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius),
            135, sweepAngle, false, valuePaint);

        // Draw value text
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 24,
            TextAlign = SKTextAlign.Center,
            IsAntialias = true
        };

        canvas.DrawText(
            $"{Value:F0}",
            center.X, center.Y + 8,
            textPaint);
    }
}
```

## Best Practices

### 1. Use MAUI Types for Public APIs

Always use MAUI types (Color, Rect, Size, Thickness, double) in public APIs:

```csharp
// Good - MAUI types
public Color ForegroundColor { get; set; }
public double BorderWidth { get; set; }
public Thickness Padding { get; set; }

// Bad - SkiaSharp types (internal only)
// public SKColor ForegroundColor { get; set; }  // Don't expose
// public float BorderWidth { get; set; }         // Don't expose
```

### 2. Implement Visual States

Support visual state changes for interactive controls:

```csharp
public override void OnPointerEntered(PointerEventArgs e)
{
    SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.PointerOver);
    base.OnPointerEntered(e);
}

public override void OnPointerExited(PointerEventArgs e)
{
    SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Normal);
    base.OnPointerExited(e);
}

public override void OnPointerPressed(PointerEventArgs e)
{
    SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
    base.OnPointerPressed(e);
}
```

### 3. Support Accessibility

Override accessibility methods for screen reader support:

```csharp
protected override string GetDefaultAccessibleName()
{
    return $"Rating: {Value} of {MaxValue} stars";
}

protected override AccessibleRole GetAccessibleRole()
{
    return AccessibleRole.Slider;
}

protected override IReadOnlyList<AccessibleAction> GetAccessibleActions()
{
    return new[]
    {
        new AccessibleAction("Increment", "Increase rating"),
        new AccessibleAction("Decrement", "Decrease rating")
    };
}

protected override bool DoAccessibleAction(string actionName)
{
    switch (actionName)
    {
        case "Increment":
            Value = Math.Min(Value + 1, MaxValue);
            return true;
        case "Decrement":
            Value = Math.Max(Value - 1, 0);
            return true;
    }
    return false;
}
```

### 4. Implement Keyboard Navigation

Support keyboard input for accessibility:

```csharp
public CustomControl()
{
    IsFocusable = true;
}

public override void OnKeyDown(KeyEventArgs e)
{
    if (!IsEnabled) return;

    switch (e.Key)
    {
        case Key.Left:
        case Key.Down:
            Value--;
            e.Handled = true;
            break;
        case Key.Right:
        case Key.Up:
            Value++;
            e.Handled = true;
            break;
        case Key.Home:
            Value = Minimum;
            e.Handled = true;
            break;
        case Key.End:
            Value = Maximum;
            e.Handled = true;
            break;
    }
}
```

### 5. Handle Focus Visuals

Draw focus indicators when the control is focused:

```csharp
protected override void OnDraw(SKCanvas canvas, SKRect bounds)
{
    // Draw control content...

    // Draw focus ring
    if (IsFocused)
    {
        using var focusPaint = new SKPaint
        {
            Color = SkiaTheme.PrimarySK.WithAlpha(100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        canvas.DrawRoundRect(bounds.Inflate(2, 2), 4, 4, focusPaint);
    }
}
```

### 6. Optimize Rendering

Use `Invalidate()` sparingly and implement dirty region tracking:

```csharp
// Only invalidate what changed
public int Value
{
    get => _value;
    set
    {
        if (_value != value)
        {
            _value = value;
            Invalidate();  // Request redraw
        }
    }
}

// Use InvalidateMeasure() when size changes
public int MaxValue
{
    get => _maxValue;
    set
    {
        if (_maxValue != value)
        {
            _maxValue = value;
            InvalidateMeasure();  // Size changed, remeasure needed
        }
    }
}
```

## Testing Custom Controls

Create unit tests for your controls:

```csharp
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Xunit;

public class SkiaRatingControlTests
{
    [Fact]
    public void Value_ClampedToMaxValue()
    {
        var control = new SkiaRatingControl { MaxValue = 5 };

        control.Value = 10;

        control.Value.Should().Be(5);
    }

    [Fact]
    public void Value_ClampedToZero()
    {
        var control = new SkiaRatingControl();

        control.Value = -5;

        control.Value.Should().Be(0);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var control = new SkiaRatingControl { MaxValue = 5 };

        var size = control.Measure(new Size(500, 500));

        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().Be(24);  // StarSize
    }
}
```

## Summary

Creating custom controls for OpenMaui Linux follows these patterns:

1. **Define MAUI control** with BindableProperties and events
2. **Create platform view** inheriting from SkiaView
3. **Create handler** to connect MAUI and platform views
4. **Register handler** in MauiProgram.cs
5. **Follow best practices** for accessibility, visual states, and performance

For more examples, see the existing controls in the `Views/` directory.
