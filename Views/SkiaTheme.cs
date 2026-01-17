// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Centralized theme colors for Skia views using MAUI Color types.
/// All colors are defined as MAUI Colors for API compliance and converted to SKColor for rendering.
/// </summary>
public static class SkiaTheme
{
    #region Primary Colors

    /// <summary>Material Blue - Primary accent color</summary>
    public static readonly Color Primary = Color.FromRgb(0x21, 0x96, 0xF3);

    /// <summary>Material Blue Dark - Darker primary variant</summary>
    public static readonly Color PrimaryDark = Color.FromRgb(0x19, 0x76, 0xD2);

    /// <summary>Material Blue Light with transparency</summary>
    public static readonly Color PrimaryLight = Color.FromRgba(0x21, 0x96, 0xF3, 0x60);

    /// <summary>Material Blue with 35% opacity for selection</summary>
    public static readonly Color PrimarySelection = Color.FromRgba(0x21, 0x96, 0xF3, 0x59);

    /// <summary>Material Blue with 50% opacity</summary>
    public static readonly Color PrimaryHalf = Color.FromRgba(0x21, 0x96, 0xF3, 0x80);

    #endregion

    #region Text Colors

    /// <summary>Primary text color - dark gray</summary>
    public static readonly Color TextPrimary = Color.FromRgb(0x21, 0x21, 0x21);

    /// <summary>Secondary text color - medium gray</summary>
    public static readonly Color TextSecondary = Color.FromRgb(0x61, 0x61, 0x61);

    /// <summary>Tertiary/hint text color</summary>
    public static readonly Color TextTertiary = Color.FromRgb(0x75, 0x75, 0x75);

    /// <summary>Disabled text color</summary>
    public static readonly Color TextDisabled = Color.FromRgb(0x9E, 0x9E, 0x9E);

    /// <summary>Placeholder text color</summary>
    public static readonly Color TextPlaceholder = Color.FromRgb(0x80, 0x80, 0x80);

    /// <summary>Link text color</summary>
    public static readonly Color TextLink = Color.FromRgb(0x21, 0x96, 0xF3);

    /// <summary>Visited link text color - Material Purple</summary>
    public static readonly Color TextLinkVisited = Color.FromRgb(0x9C, 0x27, 0xB0);

    #endregion

    #region Background Colors

    /// <summary>White background</summary>
    public static readonly Color BackgroundWhite = Colors.White;

    /// <summary>Semi-transparent white (59% opacity)</summary>
    public static readonly Color WhiteSemiTransparent = Color.FromRgba(255, 255, 255, 150);

    /// <summary>Light gray background</summary>
    public static readonly Color BackgroundLight = Color.FromRgb(0xF5, 0xF5, 0xF5);

    /// <summary>Slightly darker light background</summary>
    public static readonly Color BackgroundLightAlt = Color.FromRgb(0xFA, 0xFA, 0xFA);

    /// <summary>Surface background (cards, dialogs)</summary>
    public static readonly Color BackgroundSurface = Color.FromRgb(0xFF, 0xFF, 0xFF);

    /// <summary>Disabled background</summary>
    public static readonly Color BackgroundDisabled = Color.FromRgb(0xEE, 0xEE, 0xEE);

    #endregion

    #region Gray Scale

    /// <summary>Gray 50 - lightest</summary>
    public static readonly Color Gray50 = Color.FromRgb(0xFA, 0xFA, 0xFA);

    /// <summary>Gray 100</summary>
    public static readonly Color Gray100 = Color.FromRgb(0xF5, 0xF5, 0xF5);

    /// <summary>Gray 200</summary>
    public static readonly Color Gray200 = Color.FromRgb(0xEE, 0xEE, 0xEE);

    /// <summary>Gray 300</summary>
    public static readonly Color Gray300 = Color.FromRgb(0xE0, 0xE0, 0xE0);

    /// <summary>Gray 400</summary>
    public static readonly Color Gray400 = Color.FromRgb(0xBD, 0xBD, 0xBD);

    /// <summary>Gray 500</summary>
    public static readonly Color Gray500 = Color.FromRgb(0x9E, 0x9E, 0x9E);

    /// <summary>Gray 600</summary>
    public static readonly Color Gray600 = Color.FromRgb(0x75, 0x75, 0x75);

    /// <summary>Gray 700</summary>
    public static readonly Color Gray700 = Color.FromRgb(0x61, 0x61, 0x61);

    /// <summary>Gray 800</summary>
    public static readonly Color Gray800 = Color.FromRgb(0x42, 0x42, 0x42);

    /// <summary>Gray 900 - darkest</summary>
    public static readonly Color Gray900 = Color.FromRgb(0x21, 0x21, 0x21);

    #endregion

    #region Border Colors

    /// <summary>Light border color</summary>
    public static readonly Color BorderLight = Color.FromRgb(0xE0, 0xE0, 0xE0);

    /// <summary>Medium border color</summary>
    public static readonly Color BorderMedium = Color.FromRgb(0xC8, 0xC8, 0xC8);

    /// <summary>Dark border color</summary>
    public static readonly Color BorderDark = Color.FromRgb(0xA0, 0xA0, 0xA0);

    #endregion

    #region Shadow Colors

    /// <summary>Shadow with 10% opacity</summary>
    public static readonly Color Shadow10 = Color.FromRgba(0, 0, 0, 0x1A);

    /// <summary>Shadow with 15% opacity</summary>
    public static readonly Color Shadow15 = Color.FromRgba(0, 0, 0, 0x26);

    /// <summary>Shadow with 20% opacity</summary>
    public static readonly Color Shadow20 = Color.FromRgba(0, 0, 0, 0x33);

    /// <summary>Shadow with 25% opacity</summary>
    public static readonly Color Shadow25 = Color.FromRgba(0, 0, 0, 0x40);

    /// <summary>Shadow with 40% opacity</summary>
    public static readonly Color Shadow40 = Color.FromRgba(0, 0, 0, 0x64);

    /// <summary>Shadow with 50% opacity</summary>
    public static readonly Color Shadow50 = Color.FromRgba(0, 0, 0, 0x80);

    #endregion

    #region Overlay Colors

    /// <summary>Scrim/overlay with 40% opacity</summary>
    public static readonly Color Overlay40 = Color.FromRgba(0, 0, 0, 0x64);

    /// <summary>Scrim/overlay with 50% opacity</summary>
    public static readonly Color Overlay50 = Color.FromRgba(0, 0, 0, 0x80);

    #endregion

    #region Status Colors

    /// <summary>Error/danger color - Material Red</summary>
    public static readonly Color Error = Color.FromRgb(0xF4, 0x43, 0x36);

    /// <summary>Success color - Material Green</summary>
    public static readonly Color Success = Color.FromRgb(0x4C, 0xAF, 0x50);

    /// <summary>Warning color - Material Orange</summary>
    public static readonly Color Warning = Color.FromRgb(0xFF, 0x98, 0x00);

    #endregion

    #region Button Colors

    /// <summary>Cancel button background</summary>
    public static readonly Color ButtonCancel = Color.FromRgb(0x9E, 0x9E, 0x9E);

    /// <summary>Cancel button hover</summary>
    public static readonly Color ButtonCancelHover = Color.FromRgb(0x75, 0x75, 0x75);

    #endregion

    #region Scrollbar Colors

    /// <summary>Scrollbar thumb color</summary>
    public static readonly Color ScrollbarThumb = Color.FromRgba(0x80, 0x80, 0x80, 0x80);

    /// <summary>Scrollbar track color</summary>
    public static readonly Color ScrollbarTrack = Color.FromRgba(0xC8, 0xC8, 0xC8, 0x40);

    #endregion

    #region Indicator Colors

    /// <summary>Unselected indicator color</summary>
    public static readonly Color IndicatorUnselected = Color.FromRgb(0xB4, 0xB4, 0xB4);

    /// <summary>Selected indicator color</summary>
    public static readonly Color IndicatorSelected = Color.FromRgb(0x21, 0x96, 0xF3);

    #endregion

    #region Menu Colors

    /// <summary>Menu background</summary>
    public static readonly Color MenuBackground = Color.FromRgb(0xF0, 0xF0, 0xF0);

    /// <summary>Menu hover background</summary>
    public static readonly Color MenuHover = Color.FromRgb(0xDC, 0xDC, 0xDC);

    /// <summary>Menu active/pressed background</summary>
    public static readonly Color MenuActive = Color.FromRgb(0xC8, 0xC8, 0xC8);

    /// <summary>Menu separator color</summary>
    public static readonly Color MenuSeparator = Color.FromRgb(0xDC, 0xDC, 0xDC);

    #endregion

    #region Dark Theme Colors

    /// <summary>Dark theme background</summary>
    public static readonly Color DarkBackground = Color.FromRgb(0x30, 0x30, 0x30);

    /// <summary>Dark theme surface</summary>
    public static readonly Color DarkSurface = Color.FromRgb(0x50, 0x50, 0x50);

    /// <summary>Dark theme text</summary>
    public static readonly Color DarkText = Color.FromRgb(0xE0, 0xE0, 0xE0);

    /// <summary>Dark theme hover</summary>
    public static readonly Color DarkHover = Color.FromRgb(0x50, 0x50, 0x50);

    #endregion

    #region SKColor Cached Conversions (for rendering performance)

    // Primary
    internal static readonly SKColor PrimarySK = Primary.ToSKColor();
    internal static readonly SKColor PrimaryDarkSK = PrimaryDark.ToSKColor();
    internal static readonly SKColor PrimaryLightSK = PrimaryLight.ToSKColor();
    internal static readonly SKColor PrimarySelectionSK = PrimarySelection.ToSKColor();
    internal static readonly SKColor PrimaryHalfSK = PrimaryHalf.ToSKColor();

    // Text
    internal static readonly SKColor TextPrimarySK = TextPrimary.ToSKColor();
    internal static readonly SKColor TextSecondarySK = TextSecondary.ToSKColor();
    internal static readonly SKColor TextTertiarySK = TextTertiary.ToSKColor();
    internal static readonly SKColor TextDisabledSK = TextDisabled.ToSKColor();
    internal static readonly SKColor TextPlaceholderSK = TextPlaceholder.ToSKColor();
    internal static readonly SKColor TextLinkSK = TextLink.ToSKColor();
    internal static readonly SKColor TextLinkVisitedSK = TextLinkVisited.ToSKColor();

    // Backgrounds
    internal static readonly SKColor BackgroundWhiteSK = SKColors.White;
    internal static readonly SKColor WhiteSemiTransparentSK = WhiteSemiTransparent.ToSKColor();
    internal static readonly SKColor BackgroundLightSK = BackgroundLight.ToSKColor();
    internal static readonly SKColor BackgroundLightAltSK = BackgroundLightAlt.ToSKColor();
    internal static readonly SKColor BackgroundSurfaceSK = BackgroundSurface.ToSKColor();
    internal static readonly SKColor BackgroundDisabledSK = BackgroundDisabled.ToSKColor();

    // Gray scale
    internal static readonly SKColor Gray50SK = Gray50.ToSKColor();
    internal static readonly SKColor Gray100SK = Gray100.ToSKColor();
    internal static readonly SKColor Gray200SK = Gray200.ToSKColor();
    internal static readonly SKColor Gray300SK = Gray300.ToSKColor();
    internal static readonly SKColor Gray400SK = Gray400.ToSKColor();
    internal static readonly SKColor Gray500SK = Gray500.ToSKColor();
    internal static readonly SKColor Gray600SK = Gray600.ToSKColor();
    internal static readonly SKColor Gray700SK = Gray700.ToSKColor();
    internal static readonly SKColor Gray800SK = Gray800.ToSKColor();
    internal static readonly SKColor Gray900SK = Gray900.ToSKColor();

    // Borders
    internal static readonly SKColor BorderLightSK = BorderLight.ToSKColor();
    internal static readonly SKColor BorderMediumSK = BorderMedium.ToSKColor();
    internal static readonly SKColor BorderDarkSK = BorderDark.ToSKColor();

    // Shadows
    internal static readonly SKColor Shadow10SK = Shadow10.ToSKColor();
    internal static readonly SKColor Shadow15SK = Shadow15.ToSKColor();
    internal static readonly SKColor Shadow20SK = Shadow20.ToSKColor();
    internal static readonly SKColor Shadow25SK = Shadow25.ToSKColor();
    internal static readonly SKColor Shadow40SK = Shadow40.ToSKColor();
    internal static readonly SKColor Shadow50SK = Shadow50.ToSKColor();

    // Overlays
    internal static readonly SKColor Overlay40SK = Overlay40.ToSKColor();
    internal static readonly SKColor Overlay50SK = Overlay50.ToSKColor();

    // Status
    internal static readonly SKColor ErrorSK = Error.ToSKColor();
    internal static readonly SKColor SuccessSK = Success.ToSKColor();
    internal static readonly SKColor WarningSK = Warning.ToSKColor();

    // Buttons
    internal static readonly SKColor ButtonCancelSK = ButtonCancel.ToSKColor();
    internal static readonly SKColor ButtonCancelHoverSK = ButtonCancelHover.ToSKColor();

    // Scrollbars
    internal static readonly SKColor ScrollbarThumbSK = ScrollbarThumb.ToSKColor();
    internal static readonly SKColor ScrollbarTrackSK = ScrollbarTrack.ToSKColor();

    // Indicators
    internal static readonly SKColor IndicatorUnselectedSK = IndicatorUnselected.ToSKColor();
    internal static readonly SKColor IndicatorSelectedSK = IndicatorSelected.ToSKColor();

    // Menu
    internal static readonly SKColor MenuBackgroundSK = MenuBackground.ToSKColor();
    internal static readonly SKColor MenuHoverSK = MenuHover.ToSKColor();
    internal static readonly SKColor MenuActiveSK = MenuActive.ToSKColor();
    internal static readonly SKColor MenuSeparatorSK = MenuSeparator.ToSKColor();

    // Dark theme
    internal static readonly SKColor DarkBackgroundSK = DarkBackground.ToSKColor();
    internal static readonly SKColor DarkSurfaceSK = DarkSurface.ToSKColor();
    internal static readonly SKColor DarkTextSK = DarkText.ToSKColor();
    internal static readonly SKColor DarkHoverSK = DarkHover.ToSKColor();

    #endregion
}
