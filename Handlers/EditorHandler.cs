// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Editor (multiline text) on Linux using Skia rendering.
/// </summary>
public partial class EditorHandler : ViewHandler<IEditor, SkiaEditor>
{
    public static IPropertyMapper<IEditor, EditorHandler> Mapper =
        new PropertyMapper<IEditor, EditorHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IEditor.Text)] = MapText,
            [nameof(IEditor.Placeholder)] = MapPlaceholder,
            [nameof(IEditor.PlaceholderColor)] = MapPlaceholderColor,
            [nameof(IEditor.TextColor)] = MapTextColor,
            [nameof(IEditor.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IEditor.IsReadOnly)] = MapIsReadOnly,
            [nameof(IEditor.IsTextPredictionEnabled)] = MapIsTextPredictionEnabled,
            [nameof(IEditor.MaxLength)] = MapMaxLength,
            [nameof(IEditor.CursorPosition)] = MapCursorPosition,
            [nameof(IEditor.SelectionLength)] = MapSelectionLength,
            [nameof(IEditor.Keyboard)] = MapKeyboard,
            [nameof(IEditor.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
            [nameof(IEditor.VerticalTextAlignment)] = MapVerticalTextAlignment,
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
        };

    public static CommandMapper<IEditor, EditorHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

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
        if (VirtualView is null || PlatformView is null) return;

        VirtualView.Text = PlatformView.Text;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        // Editor doesn't typically have a completed event, but we could trigger it
    }

    public static void MapText(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Text = editor.Text ?? "";
        handler.PlatformView.Invalidate();
    }

    public static void MapPlaceholder(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Placeholder = editor.Placeholder ?? "";
    }

    public static void MapPlaceholderColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        if (editor.PlaceholderColor is not null)
        {
            handler.PlatformView.PlaceholderColor = editor.PlaceholderColor.ToSKColor();
        }
    }

    public static void MapTextColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        if (editor.TextColor is not null)
        {
            handler.PlatformView.TextColor = editor.TextColor.ToSKColor();
        }
    }

    public static void MapCharacterSpacing(EditorHandler handler, IEditor editor)
    {
        // Character spacing would require custom text rendering
    }

    public static void MapIsReadOnly(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsReadOnly = editor.IsReadOnly;
    }

    public static void MapIsTextPredictionEnabled(EditorHandler handler, IEditor editor)
    {
        // Text prediction not applicable to desktop
    }

    public static void MapMaxLength(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxLength = editor.MaxLength;
    }

    public static void MapCursorPosition(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CursorPosition = editor.CursorPosition;
    }

    public static void MapSelectionLength(EditorHandler handler, IEditor editor)
    {
        // Selection would need to be added to SkiaEditor
    }

    public static void MapKeyboard(EditorHandler handler, IEditor editor)
    {
        // Virtual keyboard type not applicable to desktop
    }

    public static void MapHorizontalTextAlignment(EditorHandler handler, IEditor editor)
    {
        // Text alignment would require changes to SkiaEditor drawing
    }

    public static void MapVerticalTextAlignment(EditorHandler handler, IEditor editor)
    {
        // Text alignment would require changes to SkiaEditor drawing
    }

    public static void MapBackground(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        if (editor.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapBackgroundColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        if (editor is VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
