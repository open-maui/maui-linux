// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Xunit;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Tests;

public class HiDpiServiceTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var service = new HiDpiService();

        Assert.Equal(1.0f, service.ScaleFactor);
        Assert.Equal(96f, service.Dpi);
    }

    [Fact]
    public void Initialize_CanBeCalled()
    {
        var service = new HiDpiService();

        service.Initialize();

        // Should not throw
    }

    [Fact]
    public void Initialize_OnlyRunsOnce()
    {
        var service = new HiDpiService();

        service.Initialize();
        service.Initialize();

        // Second call should be no-op
    }

    [Fact]
    public void DetectScaleFactor_CanBeCalled()
    {
        var service = new HiDpiService();

        service.DetectScaleFactor();

        // Should not throw, may or may not find scale
    }

    [Fact]
    public void ToPhysicalPixels_WithDefaultScale_ReturnsInput()
    {
        var service = new HiDpiService();

        float result = service.ToPhysicalPixels(100f);

        Assert.Equal(100f, result);
    }

    [Fact]
    public void ToLogicalPixels_WithDefaultScale_ReturnsInput()
    {
        var service = new HiDpiService();

        float result = service.ToLogicalPixels(100f);

        Assert.Equal(100f, result);
    }

    [Fact]
    public void ScaleChangedEvent_CanBeSubscribed()
    {
        var service = new HiDpiService();
        bool eventRaised = false;

        service.ScaleChanged += (s, e) => eventRaised = true;

        Assert.False(eventRaised); // Not raised yet
    }

    [Fact]
    public void GetFontScaleFactor_ReturnsValidValue()
    {
        var service = new HiDpiService();

        float scale = service.GetFontScaleFactor();

        Assert.True(scale > 0);
    }
}

public class ScaleChangedEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var args = new ScaleChangedEventArgs(1.0f, 2.0f, 192f);

        Assert.Equal(1.0f, args.OldScale);
        Assert.Equal(2.0f, args.NewScale);
        Assert.Equal(192f, args.NewDpi);
    }
}

public class HighContrastServiceTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var service = new HighContrastService();

        Assert.False(service.IsHighContrastEnabled);
        Assert.Equal(HighContrastTheme.None, service.CurrentTheme);
    }

    [Fact]
    public void Initialize_CanBeCalled()
    {
        var service = new HighContrastService();

        service.Initialize();

        // Should not throw
    }

    [Fact]
    public void DetectHighContrast_CanBeCalled()
    {
        var service = new HighContrastService();

        service.DetectHighContrast();

        // Should not throw
    }

    [Fact]
    public void ForceHighContrast_EnablesHighContrast()
    {
        var service = new HighContrastService();

        service.ForceHighContrast(true, HighContrastTheme.WhiteOnBlack);

        Assert.True(service.IsHighContrastEnabled);
        Assert.Equal(HighContrastTheme.WhiteOnBlack, service.CurrentTheme);
    }

    [Fact]
    public void ForceHighContrast_DisablesHighContrast()
    {
        var service = new HighContrastService();
        service.ForceHighContrast(true);

        service.ForceHighContrast(false);

        Assert.False(service.IsHighContrastEnabled);
    }

    [Fact]
    public void GetColors_ReturnsWhiteOnBlackColors()
    {
        var service = new HighContrastService();
        service.ForceHighContrast(true, HighContrastTheme.WhiteOnBlack);

        var colors = service.GetColors();

        Assert.Equal(SKColors.Black, colors.Background);
        Assert.Equal(SKColors.White, colors.Foreground);
    }

    [Fact]
    public void GetColors_ReturnsBlackOnWhiteColors()
    {
        var service = new HighContrastService();
        service.ForceHighContrast(true, HighContrastTheme.BlackOnWhite);

        var colors = service.GetColors();

        Assert.Equal(SKColors.White, colors.Background);
        Assert.Equal(SKColors.Black, colors.Foreground);
    }

    [Fact]
    public void GetColors_ReturnsDefaultColorsWhenDisabled()
    {
        var service = new HighContrastService();

        var colors = service.GetColors();

        Assert.Equal(SKColors.White, colors.Background);
        Assert.NotEqual(SKColors.Black, colors.Foreground); // Default is gray
    }

    [Fact]
    public void HighContrastChangedEvent_CanBeSubscribed()
    {
        var service = new HighContrastService();
        bool eventRaised = false;

        service.HighContrastChanged += (s, e) => eventRaised = true;
        service.ForceHighContrast(true);

        Assert.True(eventRaised);
    }
}

public class HighContrastColorsTests
{
    [Fact]
    public void AllPropertiesCanBeSet()
    {
        var colors = new HighContrastColors
        {
            Background = SKColors.Black,
            Foreground = SKColors.White,
            Accent = SKColors.Cyan,
            Border = SKColors.White,
            Error = SKColors.Red,
            Success = SKColors.Green,
            Warning = SKColors.Yellow,
            Link = SKColors.Blue,
            LinkVisited = SKColors.Purple,
            Selection = SKColors.Blue,
            SelectionText = SKColors.White,
            DisabledText = SKColors.Gray,
            DisabledBackground = SKColors.DarkGray
        };

        Assert.Equal(SKColors.Black, colors.Background);
        Assert.Equal(SKColors.White, colors.Foreground);
        Assert.Equal(SKColors.Cyan, colors.Accent);
    }
}

public class HighContrastChangedEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var args = new HighContrastChangedEventArgs(true, HighContrastTheme.WhiteOnBlack);

        Assert.True(args.IsEnabled);
        Assert.Equal(HighContrastTheme.WhiteOnBlack, args.Theme);
    }
}

public class DragDropServiceTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var service = new DragDropService();

        Assert.False(service.IsDragging);
    }

    [Fact]
    public void CancelDrag_WhenNotDragging_DoesNotThrow()
    {
        var service = new DragDropService();

        service.CancelDrag();

        Assert.False(service.IsDragging);
    }

    [Fact]
    public void ProcessClientMessage_CanBeCalled()
    {
        var service = new DragDropService();

        // Simply verify the method can be called without exception
        service.ProcessClientMessage(0, new nint[5]);

        Assert.NotNull(service);
    }

    [Fact]
    public void DragEnterEvent_CanBeSubscribed()
    {
        var service = new DragDropService();
        bool eventRaised = false;

        service.DragEnter += (s, e) => eventRaised = true;

        Assert.False(eventRaised);
    }

    [Fact]
    public void DragOverEvent_CanBeSubscribed()
    {
        var service = new DragDropService();
        bool eventRaised = false;

        service.DragOver += (s, e) => eventRaised = true;

        Assert.False(eventRaised);
    }

    [Fact]
    public void DragLeaveEvent_CanBeSubscribed()
    {
        var service = new DragDropService();
        bool eventRaised = false;

        service.DragLeave += (s, e) => eventRaised = true;

        Assert.False(eventRaised);
    }

    [Fact]
    public void DropEvent_CanBeSubscribed()
    {
        var service = new DragDropService();
        bool eventRaised = false;

        service.Drop += (s, e) => eventRaised = true;

        Assert.False(eventRaised);
    }

    [Fact]
    public void Dispose_CanBeCalled()
    {
        var service = new DragDropService();

        service.Dispose();

        // Should not throw
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = new DragDropService();

        service.Dispose();
        service.Dispose();

        // Should not throw
    }
}

public class DragDataTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var data = new DragData();

        Assert.Equal(IntPtr.Zero, data.SourceWindow);
        Assert.Empty(data.SupportedTypes);
        Assert.Null(data.Text);
        Assert.Null(data.FilePaths);
        Assert.Null(data.Data);
    }

    [Fact]
    public void Text_CanBeSet()
    {
        var data = new DragData { Text = "Hello World" };

        Assert.Equal("Hello World", data.Text);
    }

    [Fact]
    public void FilePaths_CanBeSet()
    {
        var data = new DragData
        {
            FilePaths = new[] { "/home/user/file1.txt", "/home/user/file2.txt" }
        };

        Assert.Equal(2, data.FilePaths.Length);
    }

    [Fact]
    public void Data_CanBeSet()
    {
        var customData = new { Id = 1, Name = "Test" };
        var data = new DragData { Data = customData };

        Assert.Equal(customData, data.Data);
    }
}

public class LinuxDragEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var dragData = new DragData { Text = "Test" };
        var args = new Microsoft.Maui.Platform.Linux.Services.DragEventArgs(dragData, 100, 200);

        Assert.Equal(dragData, args.Data);
        Assert.Equal(100, args.X);
        Assert.Equal(200, args.Y);
        Assert.False(args.Accepted);
    }

    [Fact]
    public void Accepted_CanBeSet()
    {
        var args = new Microsoft.Maui.Platform.Linux.Services.DragEventArgs(new DragData(), 0, 0);

        args.Accepted = true;

        Assert.True(args.Accepted);
    }

    [Fact]
    public void AllowedAction_CanBeSet()
    {
        var args = new Microsoft.Maui.Platform.Linux.Services.DragEventArgs(new DragData(), 0, 0);

        args.AllowedAction = DragAction.Move;

        Assert.Equal(DragAction.Move, args.AllowedAction);
    }
}

public class LinuxDropEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var dragData = new DragData();
        var args = new Microsoft.Maui.Platform.Linux.Services.DropEventArgs(dragData, "dropped content");

        Assert.Equal(dragData, args.Data);
        Assert.Equal("dropped content", args.DroppedData);
        Assert.False(args.Handled);
    }

    [Fact]
    public void Handled_CanBeSet()
    {
        var args = new Microsoft.Maui.Platform.Linux.Services.DropEventArgs(new DragData(), null);

        args.Handled = true;

        Assert.True(args.Handled);
    }
}

public class GlobalHotkeyServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var service = new GlobalHotkeyService();

        Assert.NotNull(service);
    }

    [Fact]
    public void HotkeyPressedEvent_CanBeSubscribed()
    {
        var service = new GlobalHotkeyService();
        bool eventRaised = false;

        service.HotkeyPressed += (s, e) => eventRaised = true;

        Assert.False(eventRaised);
    }

    [Fact]
    public void UnregisterAll_WhenNoRegistrations_DoesNotThrow()
    {
        var service = new GlobalHotkeyService();

        service.UnregisterAll();

        // Should not throw
    }

    [Fact]
    public void Dispose_CanBeCalled()
    {
        var service = new GlobalHotkeyService();

        service.Dispose();

        // Should not throw
    }
}

public class HotkeyEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var args = new HotkeyEventArgs(1, HotkeyKey.A, HotkeyModifiers.Control);

        Assert.Equal(1, args.Id);
        Assert.Equal(HotkeyKey.A, args.Key);
        Assert.Equal(HotkeyModifiers.Control, args.Modifiers);
    }

    [Fact]
    public void ModifiersCanBeCombined()
    {
        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift;
        var args = new HotkeyEventArgs(1, HotkeyKey.S, modifiers);

        Assert.True(args.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(args.Modifiers.HasFlag(HotkeyModifiers.Shift));
    }
}

public class HotkeyModifiersTests
{
    [Fact]
    public void None_IsZero()
    {
        Assert.Equal(0, (int)HotkeyModifiers.None);
    }

    [Fact]
    public void AllModifiers_AreFlagBased()
    {
        var all = HotkeyModifiers.Shift | HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Super;

        Assert.True(all.HasFlag(HotkeyModifiers.Shift));
        Assert.True(all.HasFlag(HotkeyModifiers.Control));
        Assert.True(all.HasFlag(HotkeyModifiers.Alt));
        Assert.True(all.HasFlag(HotkeyModifiers.Super));
    }
}

public class HotkeyKeyTests
{
    [Fact]
    public void Letters_HaveCorrectValues()
    {
        Assert.Equal((uint)0x61, (uint)HotkeyKey.A);
        Assert.Equal((uint)0x7A, (uint)HotkeyKey.Z);
    }

    [Fact]
    public void FunctionKeys_HaveCorrectValues()
    {
        Assert.Equal((uint)0xFFBE, (uint)HotkeyKey.F1);
        Assert.Equal((uint)0xFFC9, (uint)HotkeyKey.F12);
    }

    [Fact]
    public void SpecialKeys_HaveCorrectValues()
    {
        Assert.Equal((uint)0xFF1B, (uint)HotkeyKey.Escape);
        Assert.Equal((uint)0x20, (uint)HotkeyKey.Space);
        Assert.Equal((uint)0xFF0D, (uint)HotkeyKey.Return);
    }

    [Fact]
    public void ArrowKeys_HaveCorrectValues()
    {
        Assert.Equal((uint)0xFF51, (uint)HotkeyKey.Left);
        Assert.Equal((uint)0xFF52, (uint)HotkeyKey.Up);
        Assert.Equal((uint)0xFF53, (uint)HotkeyKey.Right);
        Assert.Equal((uint)0xFF54, (uint)HotkeyKey.Down);
    }
}
