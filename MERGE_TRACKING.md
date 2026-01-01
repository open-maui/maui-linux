# OpenMaui Linux - Recovery Merge Tracking

## Executive Summary

| Category | In Main | In Decompiled | New to Add | To Compare | Completed |
|----------|---------|---------------|------------|------------|-----------|
| Handlers | 44 | 48 | 13 | 35 | 23 |
| Views/Types | 41 | 118 | 77 | 41 | 10 |
| Services | 33 | 103 | 70 | 33 | 7 |
| Hosting | 5 | 12 | 7 | 5 | 2 |
| Dispatching | 0 | 3 | 3 | 0 | 3 |
| Native | 0 | 5 | 5 | 0 | 5 |
| Window | 2 | 3 | 1 | 2 | 3 |
| Rendering | 1 | 2 | 1 | 1 | 1 |
| **TOTAL** | **123** | **289** | **175** | **114** | **54** |

**Branch:** `main`
**Base:** Clean main (builds with 0 errors)
**Status:** In progress - BUILD SUCCEEDS
**Last Updated:** 2026-01-01

---

## HANDLERS

### New Handlers (13 files) - TO ADD

- [ ] ContentPageHandler.cs - EXISTS IN PageHandler.cs, needs comparison
- [x] FlexLayoutHandler.cs - ADDED
- [x] GestureManager.cs - ADDED (tap, pan, swipe, pointer gesture processing)
- [ ] GridHandler.cs - EXISTS IN LayoutHandler.cs, needs comparison
- [ ] GtkWebViewHandler.cs
- [x] GtkWebViewManager.cs - ADDED
- [x] GtkWebViewPlatformView.cs - ADDED
- [ ] GtkWebViewProxy.cs
- [ ] LayoutHandlerUpdate.cs - EXISTS IN LayoutHandler.cs
- [ ] LinuxApplicationContext.cs
- [ ] RelayCommand.cs - EXISTS IN NavigationPageHandler.cs
- [ ] SizeChangedEventArgs.cs
- [ ] SkiaWindow.cs
- [ ] StackLayoutHandler.cs - EXISTS IN LayoutHandler.cs, needs comparison
- [ ] TextButtonHandler.cs - EXISTS IN ButtonHandler.cs

### Existing Handlers (35 files) - TO COMPARE

- [ ] ActivityIndicatorHandler.cs
- [ ] ActivityIndicatorHandler.Linux.cs
- [ ] ApplicationHandler.cs
- [x] BorderHandler.cs - Updated to use ToViewHandler
- [ ] BoxViewHandler.cs
- [ ] ButtonHandler.cs
- [ ] ButtonHandler.Linux.cs
- [x] CheckBoxHandler.cs - Updated with missing mappers
- [ ] CheckBoxHandler.Linux.cs
- [x] CollectionViewHandler.cs - Updated to use ToViewHandler
- [x] DatePickerHandler.cs - Updated with missing mappers
- [ ] EditorHandler.cs
- [x] EntryHandler.cs - Updated with missing mappers
- [ ] EntryHandler.Linux.cs
- [ ] FlyoutPageHandler.cs
- [x] FrameHandler.cs - Updated to use ToViewHandler
- [ ] GraphicsViewHandler.cs
- [ ] ImageButtonHandler.cs
- [x] ImageHandler.cs - Updated with LoadFromBitmap support
- [ ] ItemsViewHandler.cs
- [x] LabelHandler.cs - Added ConnectHandler, DisconnectHandler, OnPlatformViewTapped, MapFormattedText
- [ ] LabelHandler.Linux.cs
- [x] LayoutHandler.cs - Updated to use ToViewHandler
- [x] LayoutHandler.Linux.cs - Updated to use ToViewHandler
- [x] NavigationPageHandler.cs - Updated to use ToViewHandler
- [x] PageHandler.cs - Updated to use ToViewHandler
- [x] PickerHandler.cs - Updated with missing mappers
- [x] ProgressBarHandler.cs - Updated with missing mappers
- [ ] ProgressBarHandler.Linux.cs
- [ ] RadioButtonHandler.cs
- [x] ScrollViewHandler.cs - Updated to use ToViewHandler
- [ ] SearchBarHandler.cs
- [ ] SearchBarHandler.Linux.cs
- [ ] ShellHandler.cs
- [ ] SliderHandler.cs
- [ ] SliderHandler.Linux.cs
- [ ] StepperHandler.cs
- [x] SwitchHandler.cs - Updated with missing mappers
- [ ] SwitchHandler.Linux.cs
- [ ] TabbedPageHandler.cs
- [ ] TimePickerHandler.cs
- [ ] WebViewHandler.cs
- [ ] WebViewHandler.Linux.cs
- [ ] WindowHandler.cs

---

## VIEWS & TYPES

### New Types (77 files) - TO ADD

- [ ] AbsoluteLayoutBounds.cs - EXISTS IN SkiaLayoutView.cs
- [ ] AbsoluteLayoutFlags.cs - EXISTS IN SkiaLayoutView.cs
- [ ] CheckedChangedEventArgs.cs
- [ ] CollectionSelectionChangedEventArgs.cs
- [ ] ColorExtensions.cs
- [ ] ContextMenuItem.cs - EXISTS IN Types/
- [ ] FlexAlignContent.cs - EXISTS IN Types/
- [ ] FlexAlignItems.cs - EXISTS IN Types/
- [ ] FlexAlignSelf.cs - EXISTS IN Types/
- [ ] FlexBasis.cs - EXISTS IN Types/
- [ ] FlexDirection.cs - EXISTS IN Types/
- [ ] FlexJustify.cs - EXISTS IN Types/
- [ ] FlexWrap.cs - EXISTS IN Types/
- [ ] FlyoutLayoutBehavior.cs
- [ ] FontExtensions.cs
- [ ] GridLength.cs - EXISTS IN SkiaLayoutView.cs
- [ ] GridPosition.cs - EXISTS IN SkiaLayoutView.cs
- [ ] GridUnitType.cs - EXISTS IN SkiaLayoutView.cs
- [ ] ImageLoadingErrorEventArgs.cs
- [ ] IndicatorShape.cs
- [ ] ISkiaQueryAttributable.cs - EXISTS IN Types/
- [ ] ItemsLayoutOrientation.cs
- [ ] ItemsScrolledEventArgs.cs
- [ ] ItemsViewItemTappedEventArgs.cs
- [ ] Key.cs - EXISTS IN SkiaView.cs
- [ ] KeyEventArgs.cs - EXISTS IN SkiaView.cs
- [ ] KeyModifiers.cs - EXISTS IN SkiaView.cs
- [ ] LayoutAlignment.cs
- [ ] LineBreakMode.cs
- [ ] LinuxDialogService.cs
- [ ] MenuBarItem.cs - EXISTS IN SkiaMenuBar.cs
- [ ] MenuItem.cs - EXISTS IN SkiaMenuBar.cs
- [ ] MenuItemClickedEventArgs.cs - EXISTS IN SkiaMenuBar.cs
- [ ] NavigationEventArgs.cs - EXISTS IN SkiaNavigationPage.cs
- [ ] PointerButton.cs - EXISTS IN SkiaView.cs
- [ ] PointerEventArgs.cs - EXISTS IN SkiaView.cs
- [ ] PositionChangedEventArgs.cs
- [ ] ProgressChangedEventArgs.cs
- [ ] ScrollBarVisibility.cs - EXISTS IN SkiaScrollView.cs
- [ ] ScrolledEventArgs.cs - EXISTS IN SkiaScrollView.cs
- [ ] ScrollEventArgs.cs - EXISTS IN SkiaView.cs
- [ ] ScrollOrientation.cs - EXISTS IN SkiaScrollView.cs
- [ ] ShellContent.cs - EXISTS IN SkiaShell.cs
- [ ] ShellFlyoutBehavior.cs - EXISTS IN SkiaShell.cs
- [ ] ShellNavigationEventArgs.cs - EXISTS IN SkiaShell.cs
- [ ] ShellSection.cs - EXISTS IN SkiaShell.cs
- [ ] SkiaAbsoluteLayout.cs - EXISTS IN SkiaLayoutView.cs
- [ ] SkiaContentPage.cs - EXISTS IN SkiaPage.cs
- [ ] SkiaContextMenu.cs
- [x] SkiaFlexLayout.cs - ADDED
- [ ] SkiaFrame.cs - EXISTS IN SkiaBorder.cs
- [ ] SkiaGrid.cs - EXISTS IN SkiaLayoutView.cs
- [ ] SkiaMenuFlyout.cs
- [ ] SkiaSelectionMode.cs
- [ ] SkiaStackLayout.cs - EXISTS IN SkiaLayoutView.cs
- [ ] SkiaTextAlignment.cs
- [ ] SkiaTextSpan.cs - EXISTS IN Types/
- [ ] SkiaToolbarItem.cs - EXISTS IN SkiaPage.cs
- [ ] SkiaToolbarItemOrder.cs - EXISTS IN SkiaPage.cs
- [ ] SkiaVerticalAlignment.cs
- [ ] SkiaVisualState.cs
- [ ] SkiaVisualStateGroup.cs
- [ ] SkiaVisualStateGroupList.cs
- [ ] SkiaVisualStateSetter.cs
- [ ] SliderValueChangedEventArgs.cs
- [ ] StackOrientation.cs - EXISTS IN SkiaLayoutView.cs
- [ ] SwipeDirection.cs
- [ ] SwipeEndedEventArgs.cs
- [ ] SwipeItem.cs
- [ ] SwipeMode.cs
- [ ] SwipeStartedEventArgs.cs
- [ ] SystemClipboard.cs
- [ ] TabItem.cs
- [ ] TextAlignment.cs
- [ ] TextChangedEventArgs.cs
- [ ] TextInputEventArgs.cs - EXISTS IN SkiaView.cs
- [ ] ThicknessExtensions.cs
- [ ] ToggledEventArgs.cs
- [ ] WebNavigatedEventArgs.cs
- [ ] WebNavigatingEventArgs.cs

### Existing Views (41 files) - TO COMPARE

- [ ] LinuxWebView.cs
- [ ] SkiaActivityIndicator.cs
- [ ] SkiaAlertDialog.cs
- [ ] SkiaBorder.cs
- [ ] SkiaBoxView.cs
- [ ] SkiaButton.cs
- [ ] SkiaCarouselView.cs
- [ ] SkiaCheckBox.cs
- [ ] SkiaCollectionView.cs
- [ ] SkiaContentPresenter.cs
- [ ] SkiaDatePicker.cs
- [ ] SkiaEditor.cs
- [x] SkiaEntry.cs - Added context menu support
- [ ] SkiaFlyoutPage.cs
- [ ] SkiaGraphicsView.cs
- [x] SkiaImage.cs - Added LoadFromBitmap method
- [ ] SkiaImageButton.cs
- [ ] SkiaIndicatorView.cs
- [ ] SkiaItemsView.cs
- [x] SkiaLabel.cs - Added FormattedSpans, Tapped event, formatted text rendering
- [ ] SkiaLayoutView.cs
- [ ] SkiaMenuBar.cs
- [ ] SkiaNavigationPage.cs
- [ ] SkiaPage.cs
- [ ] SkiaPicker.cs
- [ ] SkiaProgressBar.cs
- [ ] SkiaRadioButton.cs
- [ ] SkiaRefreshView.cs
- [ ] SkiaScrollView.cs
- [ ] SkiaSearchBar.cs
- [x] SkiaShell.cs - Added MauiShell, ContentRenderer, ColorRefresher, RefreshTheme()
- [ ] SkiaSlider.cs
- [ ] SkiaStepper.cs
- [ ] SkiaSwipeView.cs
- [ ] SkiaSwitch.cs
- [ ] SkiaTabbedPage.cs
- [ ] SkiaTemplatedView.cs
- [ ] SkiaTimePicker.cs
- [x] SkiaView.cs - Added MauiView, CursorType, transforms (Scale/Rotation/Translation/Anchor), GestureManager integration, enhanced Invalidate/Draw
- [ ] SkiaVisualStateManager.cs
- [x] SkiaWebView.cs - Added SetMainWindow, ProcessGtkEvents static methods

### New Views Added This Session

- [x] SkiaContextMenu.cs - ADDED (context menu with hover, keyboard, dark theme support)
- [x] LinuxDialogService.cs - ADDED (dialog and context menu management)

---

## SERVICES

### New Services (70 files) - TO ADD

- [ ] AccessibilityServiceFactory.cs
- [ ] AccessibleAction.cs
- [ ] AccessibleProperty.cs
- [ ] AccessibleRect.cs
- [ ] AccessibleRole.cs
- [ ] AccessibleState.cs
- [ ] AccessibleStates.cs
- [ ] AnnouncementPriority.cs
- [x] AppInfoService.cs - ADDED
- [ ] ColorDialogResult.cs
- [x] ConnectivityService.cs - ADDED
- [ ] DesktopEnvironment.cs - EXISTS IN SystemThemeService.cs
- [x] DeviceDisplayService.cs - ADDED
- [x] DeviceInfoService.cs - ADDED
- [ ] DisplayServerType.cs - EXISTS IN LinuxApplication.cs
- [ ] DragAction.cs
- [ ] DragData.cs
- [ ] DragEventArgs.cs
- [ ] DropEventArgs.cs
- [ ] FileDialogResult.cs
- [ ] FolderPickerOptions.cs
- [ ] FolderPickerResult.cs
- [ ] FolderResult.cs
- [ ] GtkButtonsType.cs
- [ ] GtkContextMenuService.cs
- [ ] GtkFileChooserAction.cs
- [x] GtkHostService.cs - ADDED
- [ ] GtkMenuItem.cs
- [ ] GtkMessageType.cs
- [ ] GtkResponseType.cs
- [ ] HighContrastChangedEventArgs.cs
- [ ] HighContrastColors.cs
- [ ] HighContrastTheme.cs
- [ ] HotkeyEventArgs.cs
- [ ] HotkeyKey.cs
- [ ] HotkeyModifiers.cs
- [ ] IAccessible.cs
- [ ] IAccessibleEditableText.cs
- [ ] IAccessibleText.cs
- [ ] IDisplayWindow.cs
- [ ] IInputContext.cs
- [ ] KeyModifiers.cs
- [ ] LinuxFileResult.cs
- [x] MauiIconGenerator.cs - ADDED (PNG icon generator, no Svg.Skia dependency)
- [ ] NotificationAction.cs
- [ ] NotificationActionEventArgs.cs
- [ ] NotificationClosedEventArgs.cs
- [ ] NotificationCloseReason.cs
- [ ] NotificationContext.cs
- [ ] NotificationOptions.cs
- [ ] NotificationUrgency.cs
- [ ] NullAccessibilityService.cs
- [ ] NullInputMethodService.cs
- [ ] PortalFolderPickerService.cs
- [ ] PreEditAttribute.cs
- [ ] PreEditAttributeType.cs
- [ ] PreEditChangedEventArgs.cs
- [ ] ScaleChangedEventArgs.cs
- [ ] SystemColors.cs
- [ ] SystemTheme.cs
- [ ] TextCommittedEventArgs.cs
- [ ] TextRun.cs
- [ ] ThemeChangedEventArgs.cs
- [ ] TrayMenuItem.cs
- [ ] VideoAccelerationApi.cs
- [ ] VideoFrame.cs
- [ ] VideoProfile.cs
- [ ] VirtualizationExtensions.cs
- [ ] WaylandDisplayWindow.cs
- [ ] X11DisplayWindow.cs

### Existing Services (33 files) - TO COMPARE

- [ ] AppActionsService.cs
- [ ] AtSpi2AccessibilityService.cs
- [ ] BrowserService.cs
- [ ] ClipboardService.cs
- [ ] DisplayServerFactory.cs
- [ ] DragDropService.cs
- [ ] EmailService.cs
- [ ] Fcitx5InputMethodService.cs
- [ ] FilePickerService.cs
- [ ] FolderPickerService.cs
- [ ] FontFallbackManager.cs
- [ ] GlobalHotkeyService.cs
- [ ] Gtk4InteropService.cs
- [ ] HardwareVideoService.cs
- [ ] HiDpiService.cs
- [ ] HighContrastService.cs
- [ ] IAccessibilityService.cs
- [ ] IBusInputMethodService.cs
- [ ] IInputMethodService.cs
- [ ] InputMethodServiceFactory.cs
- [ ] LauncherService.cs
- [ ] LinuxResourcesProvider.cs
- [ ] NotificationService.cs
- [ ] PortalFilePickerService.cs
- [ ] PreferencesService.cs
- [ ] SecureStorageService.cs
- [ ] ShareService.cs
- [ ] SystemClipboard.cs
- [ ] SystemThemeService.cs
- [ ] SystemTrayService.cs
- [ ] VersionTrackingService.cs
- [ ] VirtualizationManager.cs
- [ ] X11InputMethodService.cs

---

## HOSTING

### New Hosting (7 files) - TO ADD

- [ ] GtkMauiContext.cs
- [ ] HandlerMappingExtensions.cs
- [ ] LinuxAnimationManager.cs - EXISTS IN LinuxMauiContext.cs
- [ ] LinuxDispatcher.cs - EXISTS IN LinuxMauiContext.cs
- [ ] LinuxDispatcherTimer.cs - EXISTS IN LinuxMauiContext.cs
- [ ] LinuxTicker.cs - EXISTS IN LinuxMauiContext.cs
- [x] MauiHandlerExtensions.cs - ADDED (critical ToViewHandler fix)
- [ ] ScopedLinuxMauiContext.cs - EXISTS IN LinuxMauiContext.cs

### Existing Hosting (5 files) - TO COMPARE

- [ ] LinuxMauiAppBuilderExtensions.cs
- [ ] LinuxMauiContext.cs
- [ ] LinuxProgramHost.cs
- [ ] LinuxViewRenderer.cs
- [ ] MauiAppBuilderExtensions.cs

---

## NEW FOLDERS

### Dispatching (3 files) - TO ADD

- [ ] LinuxDispatcher.cs
- [ ] LinuxDispatcherProvider.cs
- [ ] LinuxDispatcherTimer.cs

### Native (5 files) - TO ADD

- [ ] CairoNative.cs
- [ ] GdkNative.cs
- [ ] GLibNative.cs
- [ ] GtkNative.cs
- [ ] WebKitNative.cs

---

## WINDOW

### Window Files - TO COMPARE/ADD

- [x] CursorType.cs - ADDED (Arrow, Hand, Text cursor types)
- [x] X11Window.cs - Added SetIcon, SetCursor methods, cursor initialization
- [x] GtkHostWindow.cs - ADDED (GTK-based host window with overlay support)

---

## RENDERING

### Rendering Files - TO COMPARE/ADD

- [x] GtkSkiaSurfaceWidget.cs - ADDED (GTK drawing area for Skia)

---

## CORE FILES

### Core (2 files) - TO COMPARE

- [x] LinuxApplication.cs - Massive update: GTK mode, Dispatcher init, theme handling, icon support, GTK events
- [ ] LinuxApplicationOptions.cs

---

## HOSTING

### Hosting Files - TO COMPARE/ADD

- [x] LinuxMauiContext.cs - Fixed duplicate LinuxDispatcher, uses Dispatching namespace
- [x] MauiHandlerExtensions.cs - ADDED (ToViewHandler extension)

---

## Process

1. **DECOMPILED = PRODUCTION (source of truth)**
2. **MAIN = OUTDATED (needs updates)**
3. **For EVERY file in decompiled**: Compare with main and apply differences
4. **Even "existing" files**: Must be compared - they likely have production fixes missing
5. **Update this document** after each file
6. **Build frequently** to catch errors early
