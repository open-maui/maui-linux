# OpenMaui Linux - Recovery Merge Tracking

**Branch:** `final`
**Last Updated:** 2026-01-01
**Build Status:** SUCCEEDS

---

## HANDLERS

| File | Status | Notes |
|------|--------|-------|
| ActivityIndicatorHandler.cs | [x] | Verified - matches decompiled |
| ApplicationHandler.cs | [x] | Verified - matches decompiled |
| BorderHandler.cs | [ ] | BLOCKED - needs SkiaBorder.MauiView and Tapped |
| BoxViewHandler.cs | [x] | Verified |
| ButtonHandler.cs | [x] | Contains TextButtonHandler - Verified |
| CheckBoxHandler.cs | [x] | Verified |
| CollectionViewHandler.cs | [x] | FIXED - Added OnItemTapped gesture handling, MauiView assignment |
| DatePickerHandler.cs | [x] | Verified |
| EditorHandler.cs | [x] | Verified |
| EntryHandler.cs | [x] | Verified |
| FlexLayoutHandler.cs | [x] | Verified - matches decompiled |
| FlyoutPageHandler.cs | [x] | Verified - matches decompiled |
| FrameHandler.cs | [ ] | BLOCKED - needs SkiaFrame.MauiView and Tapped event |
| GestureManager.cs | [x] | FIXED - Added third fallback (TappedEvent fields), type info dump, swipe Right handling |
| GraphicsViewHandler.cs | [x] | Verified - matches decompiled |
| GtkWebViewHandler.cs | [x] | Added new file from decompiled |
| GtkWebViewManager.cs | [ ] | |
| GtkWebViewPlatformView.cs | [ ] | |
| GtkWebViewProxy.cs | [x] | Added new file from decompiled |
| ImageButtonHandler.cs | [x] | FIXED - added MapBackgroundColor |
| ImageHandler.cs | [x] | Verified |
| ItemsViewHandler.cs | [x] | Verified - matches decompiled |
| LabelHandler.cs | [x] | Verified |
| LayoutHandler.cs | [x] | Contains GridHandler, StackLayoutHandler, LayoutHandlerUpdate - Verified |
| NavigationPageHandler.cs | [x] | FIXED - Added LoadToolbarIcon, Icon loading, content handling, animated params |
| PageHandler.cs | [x] | Added MapBackgroundColor |
| PickerHandler.cs | [x] | Verified |
| ProgressBarHandler.cs | [x] | Verified |
| RadioButtonHandler.cs | [x] | Verified - matches decompiled |
| ScrollViewHandler.cs | [x] | Verified |
| SearchBarHandler.cs | [x] | Verified - matches decompiled |
| ShellHandler.cs | [x] | Verified - matches decompiled |
| SliderHandler.cs | [x] | Verified |
| StepperHandler.cs | [x] | FIXED - Added MapIncrement, MapIsEnabled, dark theme colors |
| SwitchHandler.cs | [x] | Verified |
| TabbedPageHandler.cs | [x] | Verified - matches decompiled |
| TimePickerHandler.cs | [x] | FIXED - Added dark theme colors |
| WebViewHandler.cs | [x] | Fixed namespace-qualified event args |
| WindowHandler.cs | [x] | Verified - Contains SkiaWindow, SizeChangedEventArgs |

---

## VIEWS

| File | Status | Notes |
|------|--------|-------|
| SkiaActivityIndicator.cs | [ ] | |
| SkiaAlertDialog.cs | [ ] | |
| SkiaBorder.cs | [ ] | Contains SkiaFrame |
| SkiaBoxView.cs | [ ] | |
| SkiaButton.cs | [ ] | |
| SkiaCarouselView.cs | [ ] | |
| SkiaCheckBox.cs | [ ] | |
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
| SkiaPicker.cs | [ ] | |
| SkiaProgressBar.cs | [ ] | |
| SkiaRadioButton.cs | [ ] | |
| SkiaRefreshView.cs | [ ] | |
| SkiaScrollView.cs | [ ] | |
| SkiaSearchBar.cs | [ ] | |
| SkiaShell.cs | [ ] | Contains ShellSection, ShellContent |
| SkiaSlider.cs | [ ] | |
| SkiaStepper.cs | [ ] | |
| SkiaSwipeView.cs | [ ] | |
| SkiaSwitch.cs | [ ] | |
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
