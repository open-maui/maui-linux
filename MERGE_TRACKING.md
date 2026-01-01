# OpenMaui Linux - Recovery Merge Tracking

**Branch:** `final`
**Last Updated:** 2026-01-01
**Build Status:** SUCCEEDS

---

## HANDLERS

| File | Status | Notes |
|------|--------|-------|
| ActivityIndicatorHandler.cs | [ ] | |
| ApplicationHandler.cs | [ ] | |
| BorderHandler.cs | [ ] | |
| BoxViewHandler.cs | [ ] | |
| ButtonHandler.cs | [ ] | Contains TextButtonHandler |
| CheckBoxHandler.cs | [ ] | |
| CollectionViewHandler.cs | [ ] | |
| DatePickerHandler.cs | [ ] | |
| EditorHandler.cs | [ ] | |
| EntryHandler.cs | [ ] | |
| FlexLayoutHandler.cs | [ ] | |
| FlyoutPageHandler.cs | [ ] | |
| FrameHandler.cs | [ ] | |
| GestureManager.cs | [ ] | |
| GraphicsViewHandler.cs | [ ] | |
| GtkWebViewHandler.cs | [x] | Added new file from decompiled |
| GtkWebViewManager.cs | [ ] | |
| GtkWebViewPlatformView.cs | [ ] | |
| GtkWebViewProxy.cs | [x] | Added new file from decompiled |
| ImageButtonHandler.cs | [ ] | |
| ImageHandler.cs | [ ] | |
| ItemsViewHandler.cs | [ ] | |
| LabelHandler.cs | [ ] | |
| LayoutHandler.cs | [ ] | Contains GridHandler, StackLayoutHandler, LayoutHandlerUpdate |
| NavigationPageHandler.cs | [ ] | Contains RelayCommand |
| PageHandler.cs | [x] | Added MapBackgroundColor |
| PickerHandler.cs | [ ] | |
| ProgressBarHandler.cs | [ ] | |
| RadioButtonHandler.cs | [ ] | |
| ScrollViewHandler.cs | [ ] | |
| SearchBarHandler.cs | [ ] | |
| ShellHandler.cs | [ ] | |
| SliderHandler.cs | [ ] | |
| StepperHandler.cs | [ ] | |
| SwitchHandler.cs | [ ] | |
| TabbedPageHandler.cs | [ ] | |
| TimePickerHandler.cs | [ ] | |
| WebViewHandler.cs | [x] | Fixed namespace-qualified event args |
| WindowHandler.cs | [ ] | Contains SkiaWindow, SizeChangedEventArgs, LinuxApplicationContext |

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
| SkiaItemsView.cs | [ ] | |
| SkiaLabel.cs | [ ] | |
| SkiaLayoutView.cs | [ ] | Contains SkiaGrid, SkiaStackLayout, SkiaAbsoluteLayout, GridLength, GridPosition |
| SkiaMenuBar.cs | [ ] | Contains MenuItem, MenuBarItem |
| SkiaNavigationPage.cs | [ ] | |
| SkiaPage.cs | [ ] | Contains SkiaContentPage, SkiaToolbarItem |
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
