// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Editor control.
/// </summary>
public class EditorHandler : ViewHandler<IEditor, SkiaEditor>
{
    public static IPropertyMapper<IEditor, EditorHandler> Mapper = new PropertyMapper<IEditor, EditorHandler>(ViewHandler.ViewMapper)
    {
        ["Text"] = MapText,
        ["Placeholder"] = MapPlaceholder,
        ["PlaceholderColor"] = MapPlaceholderColor,
        ["TextColor"] = MapTextColor,
        ["CharacterSpacing"] = MapCharacterSpacing,
        ["IsReadOnly"] = MapIsReadOnly,
        ["IsTextPredictionEnabled"] = MapIsTextPredictionEnabled,
        ["MaxLength"] = MapMaxLength,
        ["CursorPosition"] = MapCursorPosition,
        ["SelectionLength"] = MapSelectionLength,
        ["Keyboard"] = MapKeyboard,
        ["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
        ["VerticalTextAlignment"] = MapVerticalTextAlignment,
        ["Background"] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor
    };

    public static CommandMapper<IEditor, EditorHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public EditorHandler() : base(Mapper, CommandMapper)
    {
    }

    public EditorHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaEditor CreatePlatformView()
    {
        return new SkiaEditor();
    }

    protected override void ConnectHandler(SkiaEditor platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.Completed += OnCompleted;
    }

    protected override void DisconnectHandler(SkiaEditor platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.Completed -= OnCompleted;
        base.DisconnectHandler(platformView);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (VirtualView != null && PlatformView != null)
        {
            VirtualView.Text = PlatformView.Text;
        }
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        // Editor completed - no specific action needed
    }

    public static void MapText(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Text = editor.Text ?? "";
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapPlaceholder(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Placeholder = editor.Placeholder ?? "";
        }
    }

    public static void MapPlaceholderColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null && editor.PlaceholderColor != null)
        {
            handler.PlatformView.PlaceholderColor = editor.PlaceholderColor;
        }
    }

    public static void MapTextColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null && editor.TextColor != null)
        {
            handler.PlatformView.TextColor = editor.TextColor;
        }
    }

    public static void MapCharacterSpacing(EditorHandler handler, IEditor editor)
    {
        // Character spacing not implemented for editor
    }

    public static void MapIsReadOnly(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsReadOnly = editor.IsReadOnly;
        }
    }

    public static void MapIsTextPredictionEnabled(EditorHandler handler, IEditor editor)
    {
        // Text prediction is a mobile feature
    }

    public static void MapMaxLength(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.MaxLength = editor.MaxLength;
        }
    }

    public static void MapCursorPosition(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.CursorPosition = editor.CursorPosition;
        }
    }

    public static void MapSelectionLength(EditorHandler handler, IEditor editor)
    {
        // Selection length not implemented
    }

    public static void MapKeyboard(EditorHandler handler, IEditor editor)
    {
        // Keyboard type is a mobile feature
    }

    public static void MapHorizontalTextAlignment(EditorHandler handler, IEditor editor)
    {
        // Horizontal text alignment not implemented
    }

    public static void MapVerticalTextAlignment(EditorHandler handler, IEditor editor)
    {
        // Vertical text alignment not implemented
    }

    public static void MapBackground(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            if (editor.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapBackgroundColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView != null)
        {
            if (editor is VisualElement ve && ve.BackgroundColor != null)
            {
                handler.PlatformView.BackgroundColor = ve.BackgroundColor;
                handler.PlatformView.Invalidate();
            }
        }
    }
}
