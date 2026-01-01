# OpenMaui Linux - Recovery Merge Tracking

**Branch:** `final`
**Last Updated:** 2026-01-01
**Build Status:** SUCCEEDS

---

## HANDLERS

**CRITICAL**: All handlers must use namespace `Microsoft.Maui.Platform.Linux.Handlers` and follow decompiled EXACTLY.

| File | Status | Notes |
|------|--------|-------|
| ActivityIndicatorHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Removed IsEnabled/BackgroundColor (not in production), fixed namespace |
| ApplicationHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| BorderHandler.cs | [ ] | BLOCKED - needs SkiaBorder.MauiView and Tapped |
| BoxViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, Color/CornerRadius/Background/BackgroundColor |
| ButtonHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Removed MapText/TextColor/Font (not in production), fixed namespace, added null checks |
| CheckBoxHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added VerticalLayoutAlignment/HorizontalLayoutAlignment, fixed namespace |
| CollectionViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| DatePickerHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, dark theme support |
| EditorHandler.Linux.cs | [x] | **CREATED 2026-01-01** - Was missing, created from decompiled |
| EntryHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added CharacterSpacing/ClearButtonVisibility/VerticalTextAlignment, fixed namespace, null checks |
| FlexLayoutHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| FlyoutPageHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| FrameHandler.cs | [ ] | BLOCKED - needs SkiaFrame.MauiView and Tapped event |
| GestureManager.cs | [ ] | NEEDS VERIFICATION |
| GraphicsViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| GtkWebViewHandler.cs | [x] | Added new file from decompiled |
| GtkWebViewManager.cs | [ ] | NEEDS VERIFICATION |
| GtkWebViewPlatformView.cs | [ ] | NEEDS VERIFICATION |
| GtkWebViewProxy.cs | [x] | Added new file from decompiled |
| ImageButtonHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, has ImageSourceServiceResultManager |
| ImageHandler.Linux.cs | [x] | **VERIFIED 2026-01-01** - Matches production, FontImageSource rendering |
| ItemsViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| LabelHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added CharacterSpacing/LayoutAlignment/FormattedText, ConnectHandler gesture logic, fixed namespace |
| LayoutHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, includes StackLayoutHandler/GridHandler |
| NavigationPageHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, toolbar items, SVG/PNG icons |
| PageHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, includes ContentPageHandler |
| PickerHandler.Linux.cs | [x] | **CREATED 2026-01-01** - Was missing, created from decompiled with collection changed tracking |
| ProgressBarHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added ConnectHandler/DisconnectHandler IsVisible tracking, fixed namespace |
| RadioButtonHandler.Linux.cs | [x] | **VERIFIED 2026-01-01** - Matches production, Content/GroupName/Value in ConnectHandler |
| ScrollViewHandler.Linux.cs | [x] | **VERIFIED 2026-01-01** - Matches production, CommandMapper with RequestScrollTo |
| SearchBarHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Fixed namespace, added CancelButtonColor, SolidPaint, null checks |
| ShellHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, navigation event handling |
| SliderHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Removed BackgroundColor (use base), fixed namespace, added ConnectHandler init calls |
| StepperHandler.Linux.cs | [x] | **VERIFIED 2026-01-01** - Matches production, dark theme support |
| SwitchHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added OffTrackColor logic, fixed namespace, removed extra BackgroundColor |
| TabbedPageHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, SelectedIndexChanged event |
| TimePickerHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, dark theme support |
| WebViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| WindowHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, includes SkiaWindow class |

---

## VIEWS

| File | Status | Notes |
|------|--------|-------|
| SkiaActivityIndicator.cs | [x] | Verified - all TwoWay, logic matches |
| SkiaAlertDialog.cs | [ ] | |
| SkiaBorder.cs | [ ] | Contains SkiaFrame |
| SkiaBoxView.cs | [x] | Verified - all TwoWay, logic matches |
| SkiaButton.cs | [x] | Verified - all TwoWay, logic matches |
| SkiaCarouselView.cs | [ ] | |
| SkiaCheckBox.cs | [x] | Verified - IsChecked=OneWay, rest TwoWay, logic matches |
| SkiaCollectionView.cs | [ ] | |
| SkiaContentPresenter.cs | [ ] | |
| SkiaContextMenu.cs | [ ] | |
| SkiaDatePicker.cs | [ ] | |
| SkiaEditor.cs | [ ] | |
| SkiaEntry.cs | [ ] | |
| SkiaFlexLayout.cs | [ ] | |
| SkiaFlyoutPage.cs | [ ] | |
| SkiaGraphicsView.cs | [ ] | |
| SkiaImage.cs | [ ] | |
| SkiaImageButton.cs | [ ] | |
| SkiaIndicatorView.cs | [ ] | |
| SkiaItemsView.cs | [x] | Added GetItemView() method |
| SkiaLabel.cs | [ ] | |
| SkiaLayoutView.cs | [ ] | Contains SkiaGrid, SkiaStackLayout, SkiaAbsoluteLayout, GridLength, GridPosition |
| SkiaMenuBar.cs | [ ] | Contains MenuItem, MenuBarItem |
| SkiaNavigationPage.cs | [ ] | |
| SkiaPage.cs | [x] | Added SkiaToolbarItem.Icon property |
| SkiaPicker.cs | [x] | FIXED - SelectedIndex=OneWay, all others=TwoWay (was missing) |
| SkiaProgressBar.cs | [x] | Verified - Progress=OneWay, rest TwoWay, logic matches |
| SkiaRadioButton.cs | [ ] | |
| SkiaRefreshView.cs | [ ] | |
| SkiaScrollView.cs | [ ] | |
| SkiaSearchBar.cs | [ ] | |
| SkiaShell.cs | [ ] | Contains ShellSection, ShellContent |
| SkiaSlider.cs | [x] | FIXED - Value=OneWay, rest TwoWay (agent had inverted all) |
| SkiaStepper.cs | [ ] | |
| SkiaSwipeView.cs | [ ] | |
| SkiaSwitch.cs | [x] | FIXED - IsOn=OneWay (agent had TwoWay) |
| SkiaTabbedPage.cs | [ ] | |
| SkiaTemplatedView.cs | [ ] | |
| SkiaTimePicker.cs | [ ] | |
| SkiaView.cs | [x] | Made Arrange() virtual |
| SkiaVisualStateManager.cs | [ ] | |
| SkiaWebView.cs | [ ] | Contains WebNavigatingEventArgs, WebNavigatedEventArgs (TO REMOVE - use MAUI's) |

---

## SERVICES

| File | Status | Notes |
|------|--------|-------|
| AppActionsService.cs | [ ] | |
| AppInfoService.cs | [ ] | |
| AtSpi2AccessibilityService.cs | [ ] | |
| BrowserService.cs | [ ] | |
| ClipboardService.cs | [ ] | |
| ConnectivityService.cs | [ ] | |
| DeviceDisplayService.cs | [ ] | |
| DeviceInfoService.cs | [ ] | |
| DisplayServerFactory.cs | [ ] | |
| DragDropService.cs | [ ] | |
| EmailService.cs | [ ] | |
| Fcitx5InputMethodService.cs | [ ] | |
| FilePickerService.cs | [ ] | |
| FolderPickerService.cs | [ ] | |
| FontFallbackManager.cs | [ ] | |
| GlobalHotkeyService.cs | [ ] | |
| Gtk4InteropService.cs | [ ] | |
| GtkHostService.cs | [ ] | |
| HardwareVideoService.cs | [ ] | |
| HiDpiService.cs | [ ] | |
| HighContrastService.cs | [ ] | |
| IAccessibilityService.cs | [ ] | |
| IBusInputMethodService.cs | [ ] | |
| IInputMethodService.cs | [ ] | |
| InputMethodServiceFactory.cs | [ ] | |
| LauncherService.cs | [ ] | |
| LinuxResourcesProvider.cs | [ ] | |
| NotificationService.cs | [ ] | |
| PortalFilePickerService.cs | [ ] | |
| PreferencesService.cs | [ ] | |
| SecureStorageService.cs | [ ] | |
| ShareService.cs | [ ] | |
| SystemThemeService.cs | [ ] | |
| SystemTrayService.cs | [ ] | |
| VersionTrackingService.cs | [ ] | |
| VirtualizationManager.cs | [ ] | |
| X11InputMethodService.cs | [ ] | |

---

## HOSTING

| File | Status | Notes |
|------|--------|-------|
| LinuxMauiAppBuilderExtensions.cs | [ ] | |
| LinuxMauiContext.cs | [ ] | |
| LinuxProgramHost.cs | [ ] | |
| LinuxViewRenderer.cs | [ ] | |
| MauiAppBuilderExtensions.cs | [ ] | |
| MauiHandlerExtensions.cs | [ ] | |

---

## DISPATCHING

| File | Status | Notes |
|------|--------|-------|
| LinuxDispatcher.cs | [ ] | |
| LinuxDispatcherProvider.cs | [ ] | |
| LinuxDispatcherTimer.cs | [ ] | |

---

## NATIVE

| File | Status | Notes |
|------|--------|-------|
| CairoNative.cs | [ ] | |
| GdkNative.cs | [ ] | |
| GLibNative.cs | [ ] | |
| GtkNative.cs | [ ] | |
| WebKitNative.cs | [ ] | |

---

## WINDOW

| File | Status | Notes |
|------|--------|-------|
| CursorType.cs | [ ] | |
| GtkHostWindow.cs | [ ] | |
| X11Window.cs | [ ] | |

---

## RENDERING

| File | Status | Notes |
|------|--------|-------|
| GtkSkiaSurfaceWidget.cs | [ ] | |

---

## CORE

| File | Status | Notes |
|------|--------|-------|
| LinuxApplication.cs | [ ] | |
| LinuxApplicationOptions.cs | [ ] | |

---

## TYPES

| File | Status | Notes |
|------|--------|-------|
| ToggledEventArgs.cs | [x] | ADDED - was missing, required by SkiaSwitch |
