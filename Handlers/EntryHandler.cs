// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Entry on Linux using Skia rendering.
/// Maps IEntry interface to SkiaEntry platform view.
/// </summary>
public partial class EntryHandler : LinuxViewHandler<IEntry, SkiaEntry>, IEntryHandler
{
    IEntry IEntryHandler.VirtualView => VirtualView;
    object IEntryHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IEntry, EntryHandler> Mapper = new PropertyMapper<IEntry, EntryHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ITextInput.Text)] = MapText,
        [nameof(Entry.TextTransform)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
        [nameof(IPlaceholder.Placeholder)] = MapPlaceholder,
        [nameof(IPlaceholder.PlaceholderColor)] = MapPlaceholderColor,
        [nameof(ITextInput.IsReadOnly)] = MapIsReadOnly,
        [nameof(ITextInput.MaxLength)] = MapMaxLength,
        [nameof(ITextInput.Keyboard)] = MapKeyboard,
        [nameof(ITextInput.CursorPosition)] = MapCursorPosition,
        [nameof(ITextInput.SelectionLength)] = MapSelectionLength,
        [nameof(IEntry.IsPassword)] = MapIsPassword,
        [nameof(IEntry.ReturnType)] = MapReturnType,
        [nameof(IEntry.ClearButtonVisibility)] = MapClearButtonVisibility,
        [nameof(IEntry.IsTextPredictionEnabled)] = MapIsTextPredictionEnabled,
        [nameof(IEntry.IsSpellCheckEnabled)] = MapIsSpellCheckEnabled,
        [nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
        ["SelectAllOnDoubleClick"] = MapSelectAllOnDoubleClick,
    };

    public static CommandMapper<IEntry, EntryHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public EntryHandler() : base(Mapper, CommandMapper)
    {
    }

    public EntryHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaEntry CreatePlatformView()
    {
        return new SkiaEntry();
    }

    protected override void ConnectHandler(SkiaEntry platformView)
    {
        base.ConnectHandler(platformView);
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.Completed += OnCompleted;
        platformView.FocusGained += OnFocusGained;
        platformView.FocusLost += OnFocusLost;
    }

    protected override void DisconnectHandler(SkiaEntry platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.Completed -= OnCompleted;
        platformView.FocusGained -= OnFocusGained;
        platformView.FocusLost -= OnFocusLost;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private bool _isUpdatingText;

    private void OnTextChanged(object? sender, SkiaTextChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _isUpdatingText) return;

        _isUpdatingText = true;
        try
        {
            TextInputText.UpdateVirtualText(VirtualView, e.NewTextValue);
        }
        finally
        {
            _isUpdatingText = false;
        }
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
        VirtualView?.Completed();
    }

    /// <summary>MAUI's <c>MapText</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapText(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapTextCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapText(EntryHandler handler, IEntry entry) => MapTextCore(handler, entry);

    private static void MapTextCore(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        // The platform holds the text as shown (TextTransform applied, cut to
        // MaxLength); a difference flows back to the view through TextChanged.
        bool replaced = handler.PlatformView.SetDisplayText(entry);

        // The text the platform holds (cut to MaxLength, transformed) is the
        // view's Text, as on MAUI's platforms.
        TextInputText.UpdateVirtualText(entry, handler.PlatformView.Text);

        // Text replaced after the view was first shown puts the caret at its end
        // (MAUI's platforms), and the view's CursorPosition follows.
        if (replaced && !handler._initialMapping)
        {
            handler.PlatformView.SelectionLength = 0;
            handler.PlatformView.CursorPosition = handler.PlatformView.Text.Length;
            TextInputText.ReportSelection(entry, handler.PlatformView.CursorPosition, 0);
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

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapTextColor(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapTextColorCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapTextColor(EntryHandler handler, IEntry entry) => MapTextColorCore(handler, entry);

    private static void MapTextColorCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateTextColor(entry);
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapFont(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapFontCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapFont(EntryHandler handler, IEntry entry) => MapFontCore(handler, entry);

    private static void MapFontCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateFont(entry);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCharacterSpacing(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapCharacterSpacingCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCharacterSpacing(EntryHandler handler, IEntry entry) => MapCharacterSpacingCore(handler, entry);

    private static void MapCharacterSpacingCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateCharacterSpacing(entry);
    }

    /// <summary>MAUI's <c>MapPlaceholder</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPlaceholder(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapPlaceholderCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPlaceholder(EntryHandler handler, IEntry entry) => MapPlaceholderCore(handler, entry);

    private static void MapPlaceholderCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdatePlaceholder(entry);
    }

    /// <summary>MAUI's <c>MapPlaceholderColor</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPlaceholderColor(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapPlaceholderColorCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPlaceholderColor(EntryHandler handler, IEntry entry) => MapPlaceholderColorCore(handler, entry);

    private static void MapPlaceholderColorCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdatePlaceholderColor(entry);
    }

    /// <summary>MAUI's <c>MapIsReadOnly</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsReadOnly(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapIsReadOnlyCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsReadOnly(EntryHandler handler, IEntry entry) => MapIsReadOnlyCore(handler, entry);

    private static void MapIsReadOnlyCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateIsReadOnly(entry);
    }

    /// <summary>MAUI's <c>MapMaxLength</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMaxLength(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapMaxLengthCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMaxLength(EntryHandler handler, IEntry entry) => MapMaxLengthCore(handler, entry);

    private static void MapMaxLengthCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateMaxLength(entry);
    }

    /// <summary>MAUI's <c>MapKeyboard</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapKeyboard(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapKeyboardCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapKeyboard(EntryHandler handler, IEntry entry) => MapKeyboardCore(handler, entry);

    private static void MapKeyboardCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateKeyboard(entry);
    }

    /// <summary>MAUI's <c>MapCursorPosition</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCursorPosition(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapCursorPositionCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCursorPosition(EntryHandler handler, IEntry entry) => MapCursorPositionCore(handler, entry);

    private static void MapCursorPositionCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateCursorPosition(entry);
    }

    /// <summary>MAUI's <c>MapSelectionLength</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapSelectionLength(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapSelectionLengthCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapSelectionLength(EntryHandler handler, IEntry entry) => MapSelectionLengthCore(handler, entry);

    private static void MapSelectionLengthCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateSelectionLength(entry);
    }

    /// <summary>MAUI's <c>MapIsPassword</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsPassword(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapIsPasswordCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsPassword(EntryHandler handler, IEntry entry) => MapIsPasswordCore(handler, entry);

    private static void MapIsPasswordCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateIsPassword(entry);
    }

    /// <summary>MAUI's <c>MapReturnType</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapReturnType(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapReturnTypeCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapReturnType(EntryHandler handler, IEntry entry) => MapReturnTypeCore(handler, entry);

    private static void MapReturnTypeCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateReturnType(entry);
    }

    /// <summary>MAUI's <c>MapClearButtonVisibility</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapClearButtonVisibility(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapClearButtonVisibilityCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapClearButtonVisibility(EntryHandler handler, IEntry entry) => MapClearButtonVisibilityCore(handler, entry);

    private static void MapClearButtonVisibilityCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateClearButtonVisibility(entry);
    }

    /// <summary>MAUI's <c>MapIsTextPredictionEnabled</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsTextPredictionEnabled(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapIsTextPredictionEnabledCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsTextPredictionEnabled(EntryHandler handler, IEntry entry) => MapIsTextPredictionEnabledCore(handler, entry);

    private static void MapIsTextPredictionEnabledCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateIsTextPredictionEnabled(entry);
    }

    /// <summary>MAUI's <c>MapIsSpellCheckEnabled</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsSpellCheckEnabled(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapIsSpellCheckEnabledCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsSpellCheckEnabled(EntryHandler handler, IEntry entry) => MapIsSpellCheckEnabledCore(handler, entry);

    private static void MapIsSpellCheckEnabledCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateIsSpellCheckEnabled(entry);
    }

    /// <summary>MAUI's <c>MapHorizontalTextAlignment</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapHorizontalTextAlignment(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapHorizontalTextAlignmentCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry) => MapHorizontalTextAlignmentCore(handler, entry);

    private static void MapHorizontalTextAlignmentCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateHorizontalTextAlignment(entry);
    }

    /// <summary>MAUI's <c>MapVerticalTextAlignment</c> signature; maps the property on a Linux <see cref="EntryHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapVerticalTextAlignment(IEntryHandler handler, IEntry entry)
    {
        if (handler is EntryHandler linuxHandler)
            MapVerticalTextAlignmentCore(linuxHandler, entry);
    }

    [Obsolete("Use the IEntryHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapVerticalTextAlignment(EntryHandler handler, IEntry entry) => MapVerticalTextAlignmentCore(handler, entry);

    private static void MapVerticalTextAlignmentCore(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView?.UpdateVerticalTextAlignment(entry);
    }

    public static void MapBackground(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry is Entry ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.EntryBackgroundColor = ve.BackgroundColor;
            // Also set base BackgroundColor so SkiaView.DrawBackground() respects transparency
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
        }
    }

    public static void MapSelectAllOnDoubleClick(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry is BindableObject bindable)
        {
            handler.PlatformView.SelectAllOnDoubleClick = EntryExtensions.GetSelectAllOnDoubleClick(bindable);
        }
    }
}
