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
public partial class EditorHandler : LinuxViewHandler<IEditor, SkiaEditor>
{
    public static IPropertyMapper<IEditor, EditorHandler> Mapper =
        new PropertyMapper<IEditor, EditorHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IEditor.Text)] = MapText,
            [nameof(Editor.TextTransform)] = MapText,
            [nameof(IEditor.Placeholder)] = MapPlaceholder,
            [nameof(IEditor.PlaceholderColor)] = MapPlaceholderColor,
            [nameof(IEditor.TextColor)] = MapTextColor,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IEditor.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IEditor.IsReadOnly)] = MapIsReadOnly,
            [nameof(IEditor.IsTextPredictionEnabled)] = MapIsTextPredictionEnabled,
            [nameof(IEditor.IsSpellCheckEnabled)] = MapIsSpellCheckEnabled,
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
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.Completed += OnCompleted;
        platformView.FocusGained += OnFocusGained;
        platformView.FocusLost += OnFocusLost;
    }

    protected override void DisconnectHandler(SkiaEditor platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.Completed -= OnCompleted;
        platformView.FocusGained -= OnFocusGained;
        platformView.FocusLost -= OnFocusLost;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        TextInputText.UpdateVirtualText(VirtualView, PlatformView.Text);
    }

    private void OnFocusGained(object? sender, EventArgs e) => UpdateIsFocused(true);

    private void OnFocusLost(object? sender, EventArgs e) => UpdateIsFocused(false);

    /// <summary>
    /// Reports platform focus to the MAUI view (IView.IsFocused), as MAUI's
    /// handlers do from the native focus events.
    /// </summary>
    private void UpdateIsFocused(bool isFocused)
    {
        if (VirtualView is { } view && view.IsFocused != isFocused)
            view.IsFocused = isFocused;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        // Editor doesn't typically have a completed event, but we could trigger it
    }

    public static void MapText(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        // The platform holds the text as shown (TextTransform applied, cut to
        // MaxLength); a difference flows back to the view through TextChanged.
        var text = TextInputText.GetDisplayText(editor);
        if (handler.PlatformView.Text != text)
        {
            handler.PlatformView.Text = text;
            handler.PlatformView.Invalidate();
        }

        // The text the platform holds (cut to MaxLength, transformed) is the
        // view's Text, as on MAUI's platforms.
        TextInputText.UpdateVirtualText(editor, handler.PlatformView.Text);
    }

    public static void MapPlaceholder(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        // No placeholder stays null, as on MAUI's Android and iOS editors.
        handler.PlatformView.Placeholder = editor.Placeholder;
    }

    public static void MapPlaceholderColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        if (editor.PlaceholderColor is not null)
        {
            handler.PlatformView.PlaceholderColor = editor.PlaceholderColor;
        }
    }

    public static void MapTextColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        if (editor.TextColor is not null)
        {
            handler.PlatformView.TextColor = editor.TextColor;
        }
    }

    public static void MapFont(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        var font = editor.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        // Convert Font weight/slant to FontAttributes
        FontAttributes attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        if (font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique)
            attrs |= FontAttributes.Italic;
        handler.PlatformView.FontAttributes = attrs;
    }

    public static void MapCharacterSpacing(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = editor.CharacterSpacing;
    }

    public static void MapIsReadOnly(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsReadOnly = editor.IsReadOnly;
    }

    public static void MapIsTextPredictionEnabled(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsTextPredictionEnabled = editor.IsTextPredictionEnabled;
    }

    public static void MapIsSpellCheckEnabled(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsSpellCheckEnabled = editor.IsSpellCheckEnabled;
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
        if (handler.PlatformView is null) return;
        handler.PlatformView.SelectionLength = editor.SelectionLength;
    }

    public static void MapKeyboard(EditorHandler handler, IEditor editor)
    {
        // On desktop the keyboard is the input method's content type.
        if (handler.PlatformView is null) return;
        handler.PlatformView.Keyboard = editor.Keyboard;
    }

    public static void MapHorizontalTextAlignment(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.HorizontalTextAlignment = editor.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    public static void MapVerticalTextAlignment(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalTextAlignment = editor.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    public static void MapBackground(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        if (editor.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.EditorBackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        if (editor is Editor ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.EditorBackgroundColor = ve.BackgroundColor;
            handler.PlatformView.Invalidate();
        }
    }
}
