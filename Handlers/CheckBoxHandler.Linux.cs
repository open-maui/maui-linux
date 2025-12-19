// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for CheckBox control.
/// </summary>
public partial class CheckBoxHandler : ViewHandler<ICheckBox, SkiaCheckBox>
{
    /// <summary>
    /// Maps the property mapper for the handler.
    /// </summary>
    public static IPropertyMapper<ICheckBox, CheckBoxHandler> Mapper = new PropertyMapper<ICheckBox, CheckBoxHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ICheckBox.IsChecked)] = MapIsChecked,
        [nameof(ICheckBox.Foreground)] = MapForeground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    /// <summary>
    /// Maps the command mapper for the handler.
    /// </summary>
    public static CommandMapper<ICheckBox, CheckBoxHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CheckBoxHandler() : base(Mapper, CommandMapper)
    {
    }

    public CheckBoxHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CheckBoxHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaCheckBox CreatePlatformView()
    {
        return new SkiaCheckBox();
    }

    protected override void ConnectHandler(SkiaCheckBox platformView)
    {
        base.ConnectHandler(platformView);
        platformView.CheckedChanged += OnCheckedChanged;
    }

    protected override void DisconnectHandler(SkiaCheckBox platformView)
    {
        platformView.CheckedChanged -= OnCheckedChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (VirtualView != null && VirtualView.IsChecked != e.IsChecked)
        {
            VirtualView.IsChecked = e.IsChecked;
        }
    }

    public static void MapIsChecked(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView.IsChecked != checkBox.IsChecked)
        {
            handler.PlatformView.IsChecked = checkBox.IsChecked;
        }
    }

    public static void MapForeground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        var foreground = checkBox.Foreground;
        if (foreground is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BoxColor = solidBrush.Color.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(CheckBoxHandler handler, ICheckBox checkBox)
    {
        handler.PlatformView.IsEnabled = checkBox.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
