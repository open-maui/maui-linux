# OpenMaui Linux - Recovery Merge Tracking

## Executive Summary

| Category | In Main | In Decompiled | New to Add | To Compare | Completed |
|----------|---------|---------------|------------|------------|-----------|
| Handlers | 44 | 48 | 13 | 35 | 0 |
| Views/Types | 41 | 118 | 77 | 41 | 0 |
| Services | 33 | 103 | 70 | 33 | 0 |
| Hosting | 5 | 12 | 7 | 5 | 0 |
| Dispatching | 0 | 3 | 3 | 0 | 0 |
| Native | 0 | 5 | 5 | 0 | 0 |
| **TOTAL** | **123** | **289** | **175** | **114** | **0** |

**Branch:** `final`
**Base:** Clean main (builds with 0 errors)
**Status:** Ready to begin

---

## HANDLERS

### New Handlers (13 files) - TO ADD

- [ ] ContentPageHandler.cs
- [ ] FlexLayoutHandler.cs
- [ ] GestureManager.cs
- [ ] GridHandler.cs
- [ ] GtkWebViewHandler.cs
- [ ] GtkWebViewManager.cs
- [ ] GtkWebViewPlatformView.cs
- [ ] GtkWebViewProxy.cs
- [ ] LayoutHandlerUpdate.cs
- [ ] LinuxApplicationContext.cs
- [ ] RelayCommand.cs
- [ ] SizeChangedEventArgs.cs
- [ ] SkiaWindow.cs
- [ ] StackLayoutHandler.cs
- [ ] TextButtonHandler.cs

### Existing Handlers (35 files) - TO COMPARE

- [ ] ActivityIndicatorHandler.cs
- [ ] ActivityIndicatorHandler.Linux.cs
- [ ] ApplicationHandler.cs
- [ ] BorderHandler.cs
- [ ] BoxViewHandler.cs
- [ ] ButtonHandler.cs
- [ ] ButtonHandler.Linux.cs
- [ ] CheckBoxHandler.cs
- [ ] CheckBoxHandler.Linux.cs
- [ ] CollectionViewHandler.cs
- [ ] DatePickerHandler.cs
- [ ] EditorHandler.cs
- [ ] EntryHandler.cs
- [ ] EntryHandler.Linux.cs
- [ ] FlyoutPageHandler.cs
- [ ] FrameHandler.cs
- [ ] GraphicsViewHandler.cs
- [ ] ImageButtonHandler.cs
- [ ] ImageHandler.cs
- [ ] ItemsViewHandler.cs
- [ ] LabelHandler.cs
- [ ] LabelHandler.Linux.cs
- [ ] LayoutHandler.cs
- [ ] LayoutHandler.Linux.cs
- [ ] NavigationPageHandler.cs
- [ ] PageHandler.cs
- [ ] PickerHandler.cs
- [ ] ProgressBarHandler.cs
- [ ] ProgressBarHandler.Linux.cs
- [ ] RadioButtonHandler.cs
- [ ] ScrollViewHandler.cs
- [ ] SearchBarHandler.cs
- [ ] SearchBarHandler.Linux.cs
- [ ] ShellHandler.cs
- [ ] SliderHandler.cs
- [ ] SliderHandler.Linux.cs
- [ ] StepperHandler.cs
- [ ] SwitchHandler.cs
- [ ] SwitchHandler.Linux.cs
- [ ] TabbedPageHandler.cs
- [ ] TimePickerHandler.cs
- [ ] WebViewHandler.cs
- [ ] WebViewHandler.Linux.cs
- [ ] WindowHandler.cs

---

## VIEWS & TYPES

### New Types (77 files) - TO ADD

- [ ] AbsoluteLayoutBounds.cs
- [ ] AbsoluteLayoutFlags.cs
- [ ] CheckedChangedEventArgs.cs
- [ ] CollectionSelectionChangedEventArgs.cs
- [ ] ColorExtensions.cs
- [ ] ContextMenuItem.cs
- [ ] FlexAlignContent.cs
- [ ] FlexAlignItems.cs
- [ ] FlexAlignSelf.cs
- [ ] FlexBasis.cs
- [ ] FlexDirection.cs
- [ ] FlexJustify.cs
- [ ] FlexWrap.cs
- [ ] FlyoutLayoutBehavior.cs
- [ ] FontExtensions.cs
- [ ] GridLength.cs
- [ ] GridPosition.cs
- [ ] GridUnitType.cs
- [ ] ImageLoadingErrorEventArgs.cs
- [ ] IndicatorShape.cs
- [ ] ISkiaQueryAttributable.cs
- [ ] ItemsLayoutOrientation.cs
- [ ] ItemsScrolledEventArgs.cs
- [ ] ItemsViewItemTappedEventArgs.cs
- [ ] Key.cs
- [ ] KeyEventArgs.cs
- [ ] KeyModifiers.cs
- [ ] LayoutAlignment.cs
- [ ] LineBreakMode.cs
- [ ] LinuxDialogService.cs
- [ ] MenuBarItem.cs
- [ ] MenuItem.cs
- [ ] MenuItemClickedEventArgs.cs
- [ ] NavigationEventArgs.cs
- [ ] PointerButton.cs
- [ ] PointerEventArgs.cs
- [ ] PositionChangedEventArgs.cs
- [ ] ProgressChangedEventArgs.cs
- [ ] ScrollBarVisibility.cs
- [ ] ScrolledEventArgs.cs
- [ ] ScrollEventArgs.cs
- [ ] ScrollOrientation.cs
- [ ] ShellContent.cs
- [ ] ShellFlyoutBehavior.cs
- [ ] ShellNavigationEventArgs.cs
- [ ] ShellSection.cs
- [ ] SkiaAbsoluteLayout.cs
- [ ] SkiaContentPage.cs
- [ ] SkiaContextMenu.cs
- [ ] SkiaFlexLayout.cs
- [ ] SkiaFrame.cs
- [ ] SkiaGrid.cs
- [ ] SkiaMenuFlyout.cs
- [ ] SkiaSelectionMode.cs
- [ ] SkiaStackLayout.cs
- [ ] SkiaTextAlignment.cs
- [ ] SkiaTextSpan.cs
- [ ] SkiaToolbarItem.cs
- [ ] SkiaToolbarItemOrder.cs
- [ ] SkiaVerticalAlignment.cs
- [ ] SkiaVisualState.cs
- [ ] SkiaVisualStateGroup.cs
- [ ] SkiaVisualStateGroupList.cs
- [ ] SkiaVisualStateSetter.cs
- [ ] SliderValueChangedEventArgs.cs
- [ ] StackOrientation.cs
- [ ] SwipeDirection.cs
- [ ] SwipeEndedEventArgs.cs
- [ ] SwipeItem.cs
- [ ] SwipeMode.cs
- [ ] SwipeStartedEventArgs.cs
- [ ] SystemClipboard.cs
- [ ] TabItem.cs
- [ ] TextAlignment.cs
- [ ] TextChangedEventArgs.cs
- [ ] TextInputEventArgs.cs
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
- [ ] SkiaEntry.cs
- [ ] SkiaFlyoutPage.cs
- [ ] SkiaGraphicsView.cs
- [ ] SkiaImage.cs
- [ ] SkiaImageButton.cs
- [ ] SkiaIndicatorView.cs
- [ ] SkiaItemsView.cs
- [ ] SkiaLabel.cs
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
- [ ] SkiaShell.cs
- [ ] SkiaSlider.cs
- [ ] SkiaStepper.cs
- [ ] SkiaSwipeView.cs
- [ ] SkiaSwitch.cs
- [ ] SkiaTabbedPage.cs
- [ ] SkiaTemplatedView.cs
- [ ] SkiaTimePicker.cs
- [ ] SkiaView.cs
- [ ] SkiaVisualStateManager.cs
- [ ] SkiaWebView.cs

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
- [ ] AppInfoService.cs
- [ ] ColorDialogResult.cs
- [ ] ConnectivityService.cs
- [ ] DesktopEnvironment.cs
- [ ] DeviceDisplayService.cs
- [ ] DeviceInfoService.cs
- [ ] DisplayServerType.cs
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
- [ ] GtkHostService.cs
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
- [ ] MauiIconGenerator.cs
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
- [ ] LinuxAnimationManager.cs
- [ ] LinuxDispatcher.cs
- [ ] LinuxDispatcherTimer.cs
- [ ] LinuxTicker.cs
- [ ] MauiHandlerExtensions.cs
- [ ] ScopedLinuxMauiContext.cs

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

## CORE FILES

### Core (2 files) - TO COMPARE

- [ ] LinuxApplication.cs
- [ ] LinuxApplicationOptions.cs

---

## Progress Log

| Date | Files Completed | Notes |
|------|-----------------|-------|
| - | 0 | Awaiting approval to begin |

---

## Process

1. **For NEW files:** Read decompiled → Write clean version → Add to project → Commit
2. **For EXISTING files:** Compare main vs decompiled → Apply only NEW changes cleanly → Commit
3. **Update this document** after each file
4. **Build frequently** to catch errors early
