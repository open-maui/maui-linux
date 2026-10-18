// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Editor (multiline text) on Linux using Skia rendering.
/// </summary>
public partial class EditorHandler : LinuxViewHandler<IEditor, SkiaEditor>, IEditorHandler
{
    IEditor IEditorHandler.VirtualView => VirtualView;
    object IEditorHandler.PlatformView => PlatformView;

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
            // Controls' Editor.AutoSize (TextChanges: the editor grows with its text).
            [nameof(Microsoft.Maui.Controls.Editor.AutoSize)] = MapAutoSize,
        };

    public static void MapAutoSize(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.AutoSize = editor is Microsoft.Maui.Controls.Editor { AutoSize: Microsoft.Maui.Controls.EditorAutoSizeOption.TextChanges };
        handler.PlatformView.InvalidateMeasure();
    }

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

    /// <summary>MAUI's <c>MapText</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapText(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapTextCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapText(EditorHandler handler, IEditor editor) => MapTextCore(handler, editor);

    private static void MapTextCore(EditorHandler handler, IEditor editor)
    {
        if (handler.PlatformView is null) return;

        // The platform holds the text as shown (TextTransform applied, cut to
        // MaxLength); a difference flows back to the view through TextChanged.
        bool replaced = handler.PlatformView.SetDisplayText(editor);

        // The text the platform holds (cut to MaxLength, transformed) is the
        // view's Text, as on MAUI's platforms.
        TextInputText.UpdateVirtualText(editor, handler.PlatformView.Text);

        // Text replaced after the view was first shown puts the caret at its end
        // (MAUI's platforms), and the view's CursorPosition follows.
        if (replaced && !handler._initialMapping)
        {
            handler.PlatformView.SelectionLength = 0;
            handler.PlatformView.CursorPosition = handler.PlatformView.Text.Length;
            TextInputText.ReportSelection(editor, handler.PlatformView.CursorPosition, 0);
        }
    }

    // True while the view's initial values are mapped (SetVirtualView).
    private bool _initialMapping;

    public override void SetVirtualView(IView view)
    {
        _initialMapping = true;
        try
        {
            base.SetVirtualView(view);
        }
        finally
        {
            _initialMapping = false;
        }
    }

    /// <summary>MAUI's <c>MapPlaceholder</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPlaceholder(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapPlaceholderCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPlaceholder(EditorHandler handler, IEditor editor) => MapPlaceholderCore(handler, editor);

    private static void MapPlaceholderCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdatePlaceholder(editor);
    }

    /// <summary>MAUI's <c>MapPlaceholderColor</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPlaceholderColor(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapPlaceholderColorCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPlaceholderColor(EditorHandler handler, IEditor editor) => MapPlaceholderColorCore(handler, editor);

    private static void MapPlaceholderColorCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdatePlaceholderColor(editor);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapTextColor(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapTextColorCore(linuxHandler, editor);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapTextColor(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapTextColorCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapTextColor(EditorHandler handler, IEditor editor) => MapTextColorCore(handler, editor);

    private static void MapTextColorCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateTextColor(editor);
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapFont(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapFontCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapFont(EditorHandler handler, IEditor editor) => MapFontCore(handler, editor);

    private static void MapFontCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateFont(editor);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCharacterSpacing(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapCharacterSpacingCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCharacterSpacing(EditorHandler handler, IEditor editor) => MapCharacterSpacingCore(handler, editor);

    private static void MapCharacterSpacingCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateCharacterSpacing(editor);
    }

    /// <summary>MAUI's <c>MapIsReadOnly</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsReadOnly(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapIsReadOnlyCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsReadOnly(EditorHandler handler, IEditor editor) => MapIsReadOnlyCore(handler, editor);

    private static void MapIsReadOnlyCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateIsReadOnly(editor);
    }

    /// <summary>MAUI's <c>MapIsTextPredictionEnabled</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsTextPredictionEnabled(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapIsTextPredictionEnabledCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsTextPredictionEnabled(EditorHandler handler, IEditor editor) => MapIsTextPredictionEnabledCore(handler, editor);

    private static void MapIsTextPredictionEnabledCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateIsTextPredictionEnabled(editor);
    }

    /// <summary>MAUI's <c>MapIsSpellCheckEnabled</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsSpellCheckEnabled(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapIsSpellCheckEnabledCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsSpellCheckEnabled(EditorHandler handler, IEditor editor) => MapIsSpellCheckEnabledCore(handler, editor);

    private static void MapIsSpellCheckEnabledCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateIsSpellCheckEnabled(editor);
    }

    /// <summary>MAUI's <c>MapMaxLength</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMaxLength(IViewHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapMaxLengthCore(linuxHandler, editor);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMaxLength(EditorHandler handler, IEditor editor) => MapMaxLengthCore(handler, editor);

    private static void MapMaxLengthCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateMaxLength(editor);
    }

    /// <summary>MAUI's <c>MapCursorPosition</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCursorPosition(IEditorHandler handler, ITextInput editor)
    {
        if (handler is EditorHandler linuxHandler && editor is IEditor linuxView)
            MapCursorPositionCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCursorPosition(EditorHandler handler, IEditor editor) => MapCursorPositionCore(handler, editor);

    private static void MapCursorPositionCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateCursorPosition(editor);
    }

    /// <summary>MAUI's <c>MapSelectionLength</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapSelectionLength(IEditorHandler handler, ITextInput editor)
    {
        if (handler is EditorHandler linuxHandler && editor is IEditor linuxView)
            MapSelectionLengthCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapSelectionLength(EditorHandler handler, IEditor editor) => MapSelectionLengthCore(handler, editor);

    private static void MapSelectionLengthCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateSelectionLength(editor);
    }

    /// <summary>MAUI's <c>MapKeyboard</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapKeyboard(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapKeyboardCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapKeyboard(EditorHandler handler, IEditor editor) => MapKeyboardCore(handler, editor);

    private static void MapKeyboardCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateKeyboard(editor);
    }

    /// <summary>MAUI's <c>MapHorizontalTextAlignment</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapHorizontalTextAlignment(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapHorizontalTextAlignmentCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapHorizontalTextAlignment(EditorHandler handler, IEditor editor) => MapHorizontalTextAlignmentCore(handler, editor);

    private static void MapHorizontalTextAlignmentCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateHorizontalTextAlignment(editor);
    }

    /// <summary>MAUI's <c>MapVerticalTextAlignment</c> signature; maps the property on a Linux <see cref="EditorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapVerticalTextAlignment(IEditorHandler handler, IEditor editor)
    {
        if (handler is EditorHandler linuxHandler)
            MapVerticalTextAlignmentCore(linuxHandler, editor);
    }

    [Obsolete("Use the IEditorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapVerticalTextAlignment(EditorHandler handler, IEditor editor) => MapVerticalTextAlignmentCore(handler, editor);

    private static void MapVerticalTextAlignmentCore(EditorHandler handler, IEditor editor)
    {
        handler.PlatformView?.UpdateVerticalTextAlignment(editor);
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
