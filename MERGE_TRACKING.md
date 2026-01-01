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
| BorderHandler.cs | [x] | **FIXED 2026-01-01** - Added ConnectHandler/DisconnectHandler with MauiView and Tapped event, OnPlatformViewTapped calls GestureManager.ProcessTap |
| BoxViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, Color/CornerRadius/Background/BackgroundColor |
| ButtonHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Removed MapText/TextColor/Font (not in production), fixed namespace, added null checks |
| CheckBoxHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added VerticalLayoutAlignment/HorizontalLayoutAlignment, fixed namespace |
| CollectionViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| DatePickerHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production, dark theme support |
| EditorHandler.Linux.cs | [x] | **CREATED 2026-01-01** - Was missing, created from decompiled |
| EntryHandler.Linux.cs | [x] | **FIXED 2026-01-01** - Added CharacterSpacing/ClearButtonVisibility/VerticalTextAlignment, fixed namespace, null checks |
| FlexLayoutHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| FlyoutPageHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| FrameHandler.cs | [x] | **FIXED 2026-01-01** - Added ConnectHandler/DisconnectHandler with MauiView and Tapped event, OnPlatformViewTapped calls GestureManager.ProcessTap |
| GestureManager.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| GraphicsViewHandler.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| GtkWebViewHandler.cs | [x] | Added new file from decompiled |
| GtkWebViewManager.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
| GtkWebViewPlatformView.cs | [x] | **VERIFIED 2026-01-01** - Matches production |
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
| SkiaAlertDialog.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, modal dialog rendering |
| SkiaBorder.cs | [x] | **FIXED 2026-01-01** - Logic matches, removed embedded SkiaFrame (now separate file) |
| SkiaFrame.cs | [x] | **ADDED 2026-01-01** - Created as separate file matching decompiled pattern |
| SkiaBoxView.cs | [x] | Verified - all TwoWay, logic matches |
| SkiaButton.cs | [x] | Verified - all TwoWay, logic matches |
| SkiaCarouselView.cs | [x] | **FIXED 2026-01-01** - Logic matches, removed embedded PositionChangedEventArgs |
| PositionChangedEventArgs.cs | [x] | **ADDED 2026-01-01** - Created as separate file matching decompiled |
| SkiaCheckBox.cs | [x] | Verified - IsChecked=OneWay, rest TwoWay, logic matches |
| SkiaCollectionView.cs | [x] | **FIXED 2026-01-01** - Removed embedded SkiaSelectionMode, ItemsLayoutOrientation |
| SkiaSelectionMode.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| ItemsLayoutOrientation.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaContentPresenter.cs | [x] | **FIXED 2026-01-01** - Removed embedded LayoutAlignment (now separate file) |
| LayoutAlignment.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaContextMenu.cs | [x] | **VERIFIED 2026-01-01** - Logic matches decompiled |
| SkiaDatePicker.cs | [x] | **VERIFIED 2026-01-01** - Date=OneWay, all others=TwoWay |
| SkiaEditor.cs | [x] | **FIXED 2026-01-01** - All BindingModes corrected (Text=OneWay, others=TwoWay) |
| SkiaEntry.cs | [x] | **FIXED 2026-01-01** - TextProperty BindingMode.OneWay, others TwoWay |
| SkiaFlexLayout.cs | [x] | **VERIFIED 2026-01-01** - Logic matches decompiled |
| SkiaFlyoutPage.cs | [x] | **FIXED 2026-01-01** - Removed embedded FlyoutLayoutBehavior (now separate file) |
| FlyoutLayoutBehavior.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaGraphicsView.cs | [x] | **VERIFIED 2026-01-01** - Logic matches decompiled |
| SkiaImage.cs | [x] | **VERIFIED 2026-01-01** - No BindableProperties, logic matches |
| SkiaImageButton.cs | [x] | **FIXED 2026-01-01** - Added SVG support, multi-path search matching decompiled |
| SkiaIndicatorView.cs | [x] | **FIXED 2026-01-01** - Removed embedded IndicatorShape (now separate file) |
| IndicatorShape.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaItemsView.cs | [x] | Added GetItemView() method |
| SkiaLabel.cs | [x] | **FIXED 2026-01-01** - All BindingModes TwoWay |
| SkiaLayoutView.cs | [x] | **FIXED 2026-01-01** - All BindingModes TwoWay (Spacing, Padding, ClipToBounds, Orientation, RowSpacing, ColumnSpacing) |
| SkiaMenuBar.cs | [x] | **FIXED 2026-01-01** - Removed embedded MenuBarItem, MenuItem, SkiaMenuFlyout, MenuItemClickedEventArgs |
| MenuBarItem.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| MenuItem.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaMenuFlyout.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| MenuItemClickedEventArgs.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaNavigationPage.cs | [x] | **FIXED 2026-01-01** - Added LinuxApplication.IsGtkMode check, removed embedded NavigationEventArgs |
| NavigationEventArgs.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaPage.cs | [x] | Added SkiaToolbarItem.Icon property |
| SkiaPicker.cs | [x] | FIXED - SelectedIndex=OneWay, all others=TwoWay (was missing) |
| SkiaProgressBar.cs | [x] | Verified - Progress=OneWay, rest TwoWay, logic matches |
| SkiaRadioButton.cs | [x] | **FIXED 2026-01-01** - IsChecked=OneWay, all others=TwoWay |
| SkiaRefreshView.cs | [x] | **FIXED 2026-01-01** - Added ICommand support (Command, CommandParameter) matching decompiled |
| SkiaScrollView.cs | [x] | **FIXED 2026-01-01** - All BindingModes TwoWay |
| SkiaSearchBar.cs | [x] | **VERIFIED 2026-01-01** - No BindableProperties, logic matches |
| SkiaShell.cs | [x] | **FIXED 2026-01-01** - Added FlyoutTextColor, ContentBackgroundColor, route registration, query parameters, OnScroll |
| SkiaSlider.cs | [x] | FIXED - Value=OneWay, rest TwoWay (agent had inverted all) |
| SkiaStepper.cs | [x] | **FIXED 2026-01-01** - Value=OneWay, all others=TwoWay |
| SkiaSwipeView.cs | [x] | **FIXED 2026-01-01** - Removed embedded SwipeItem, SwipeDirection, SwipeMode, SwipeStartedEventArgs, SwipeEndedEventArgs |
| SwipeItem.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SwipeDirection.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SwipeMode.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SwipeStartedEventArgs.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SwipeEndedEventArgs.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaSwitch.cs | [x] | FIXED - IsOn=OneWay (agent had TwoWay) |
| SkiaTabbedPage.cs | [x] | **FIXED 2026-01-01** - Removed embedded TabItem (now separate file) |
| TabItem.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaTemplatedView.cs | [x] | **FIXED 2026-01-01** - Added missing using statements (Shapes, Graphics) |
| SkiaTimePicker.cs | [x] | **FIXED 2026-01-01** - Time=OneWay, all others=TwoWay |
| SkiaView.cs | [x] | Made Arrange() virtual |
| SkiaVisualStateManager.cs | [x] | **FIXED 2026-01-01** - Removed embedded SkiaVisualStateGroupList, SkiaVisualStateGroup, SkiaVisualState, SkiaVisualStateSetter |
| SkiaVisualStateGroupList.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaVisualStateGroup.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaVisualState.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaVisualStateSetter.cs | [x] | **ADDED 2026-01-01** - Created as separate file |
| SkiaWebView.cs | [x] | **FIXED 2026-01-01** - Full X11 embedding, position tracking, hardware accel, load callbacks |

---

## SERVICES

| File | Status | Notes |
|------|--------|-------|
| AccessibilityServiceFactory.cs | [x] | **FIXED 2026-01-01** - Fixed CreateService() to call Initialize(), added Reset() |
| AccessibleAction.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| AccessibleProperty.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (Name,Description,Role,Value,Parent,Children) |
| AccessibleRect.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| AccessibleRole.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (simplified list with Button,Tab,TabPanel) |
| AccessibleState.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| AccessibleStates.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (MultiSelectable capital S) |
| AnnouncementPriority.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (Polite,Assertive) |
| AppActionsService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean string interpolation |
| AppInfoService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean enum names |
| AtSpi2AccessibilityService.cs | [x] | **FIXED 2026-01-01** - Removed embedded AccessibilityServiceFactory, NullAccessibilityService |
| BrowserService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean nameof/interpolation/enums |
| ClipboardService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, xclip/xsel fallback |
| ColorDialogResult.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| ConnectivityService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean enum names |
| DesktopEnvironment.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (GNOME uppercase) |
| DeviceDisplayService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean enum names |
| DeviceInfoService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean enum names |
| DisplayServerFactory.cs | [x] | **FIXED 2026-01-01** - Removed embedded DisplayServerType, IDisplayWindow, X11DisplayWindow, WaylandDisplayWindow |
| DisplayServerType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| DragAction.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| DragData.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| DragDropService.cs | [x] | **FIXED 2026-01-01** - Removed embedded DragData, DragEventArgs, DropEventArgs, DragAction |
| DragEventArgs.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| DropEventArgs.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| EmailService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean nameof/interpolation |
| Fcitx5InputMethodService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, D-Bus interface |
| FileDialogResult.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| FilePickerService.cs | [x] | **FIXED 2026-01-01** - Removed embedded LinuxFileResult |
| FolderPickerOptions.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| FolderPickerResult.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| FolderPickerService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, zenity/kdialog fallback |
| FolderResult.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled |
| FontFallbackManager.cs | [x] | **FIXED 2026-01-01** - Removed embedded TextRun |
| GlobalHotkeyService.cs | [x] | **FIXED 2026-01-01** - Removed embedded HotkeyEventArgs, HotkeyModifiers, HotkeyKey |
| Gtk4InteropService.cs | [x] | **FIXED 2026-01-01** - Removed embedded GtkResponseType, GtkMessageType, GtkButtonsType, GtkFileChooserAction, FileDialogResult, ColorDialogResult |
| GtkButtonsType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| GtkContextMenuService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, GTK P/Invoke |
| GtkFileChooserAction.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| GtkHostService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, main has clean ??= syntax |
| GtkMenuItem.cs | [x] | **VERIFIED 2026-01-01** - Identical logic |
| GtkMessageType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| GtkResponseType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| HardwareVideoService.cs | [x] | **FIXED 2026-01-01** - Removed embedded VideoAccelerationApi, VideoProfile, VideoFrame |
| HiDpiService.cs | [x] | **FIXED 2026-01-01** - Removed embedded ScaleChangedEventArgs |
| HighContrastChangedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| HighContrastColors.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| HighContrastService.cs | [x] | **FIXED 2026-01-01** - Removed embedded HighContrastTheme, HighContrastColors, HighContrastChangedEventArgs |
| HighContrastTheme.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled (None,WhiteOnBlack,BlackOnWhite) |
| HotkeyEventArgs.cs | [x] | **FIXED 2026-01-01** - Fixed constructor order (int id, HotkeyKey key, HotkeyModifiers modifiers) |
| HotkeyKey.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| HotkeyModifiers.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| IAccessibilityService.cs | [x] | **FIXED 2026-01-01** - Removed many embedded types |
| IAccessible.cs | [x] | **FIXED 2026-01-01** - Fixed to match decompiled exactly |
| IAccessibleEditableText.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| IAccessibleText.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| IBusInputMethodService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, IBus D-Bus interface |
| IDisplayWindow.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| IInputContext.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| IInputMethodService.cs | [x] | **FIXED 2026-01-01** - Removed embedded IInputContext, TextCommittedEventArgs, PreEditChangedEventArgs, PreEditAttribute, PreEditAttributeType, KeyModifiers |
| InputMethodServiceFactory.cs | [x] | **FIXED 2026-01-01** - Removed embedded NullInputMethodService |
| KeyModifiers.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| LauncherService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, xdg-open |
| LinuxFileResult.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| LinuxResourcesProvider.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, system styles |
| MauiIconGenerator.cs | [x] | **FIXED 2026-01-01** - Added Svg.Skia, SVG foreground, Scale metadata |
| NotificationAction.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationActionEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationClosedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationCloseReason.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationContext.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationOptions.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NotificationService.cs | [x] | **FIXED 2026-01-01** - Removed embedded NotificationOptions, NotificationUrgency, NotificationCloseReason, NotificationContext, NotificationActionEventArgs, NotificationClosedEventArgs, NotificationAction |
| NotificationUrgency.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NullAccessibilityService.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| NullInputMethodService.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| PortalFilePickerService.cs | [x] | **FIXED 2026-01-01** - Removed embedded FolderResult, FolderPickerResult, FolderPickerOptions, PortalFolderPickerService |
| PortalFolderPickerService.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| PreEditAttribute.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| PreEditAttributeType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| PreEditChangedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| PreferencesService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, JSON file storage with XDG |
| ScaleChangedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SecureStorageService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, secret-tool with AES fallback |
| ShareService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, xdg-open with portal fallback |
| SystemColors.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SystemTheme.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SystemThemeService.cs | [x] | **FIXED 2026-01-01** - Removed embedded SystemTheme, DesktopEnvironment, ThemeChangedEventArgs, SystemColors |
| SystemTrayService.cs | [x] | **FIXED 2026-01-01** - Removed embedded TrayMenuItem |
| TextCommittedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| TextRun.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| ThemeChangedEventArgs.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| TrayMenuItem.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| VersionTrackingService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, JSON tracking file |
| VideoAccelerationApi.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| VideoFrame.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| VideoProfile.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| VirtualizationExtensions.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| VirtualizationManager.cs | [x] | **FIXED 2026-01-01** - Removed embedded VirtualizationExtensions |
| WaylandDisplayWindow.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| X11DisplayWindow.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| X11InputMethodService.cs | [x] | **VERIFIED 2026-01-01** - Logic matches, X11 XIM interface |

---

## HOSTING

| File | Status | Notes |
|------|--------|-------|
| GtkMauiContext.cs | [x] | **ADDED 2026-01-01** - Was missing, created from decompiled |
| HandlerMappingExtensions.cs | [x] | **ADDED 2026-01-01** - Was missing, created from decompiled |
| LinuxAnimationManager.cs | [x] | **ADDED 2026-01-01** - Was missing, created from decompiled |
| LinuxMauiAppBuilderExtensions.cs | [x] | **FIXED 2026-01-01** - Added IDispatcherProvider, IDeviceInfo, IDeviceDisplay, IAppInfo, IConnectivity, GtkHostService registrations; fixed WebView to use GtkWebViewHandler |
| LinuxMauiContext.cs | [x] | **FIXED 2026-01-01** - Removed embedded classes (now separate files), added LinuxDispatcher using |
| LinuxProgramHost.cs | [x] | **FIXED 2026-01-01** - Added GtkHostService.Initialize call for WebView support |
| LinuxTicker.cs | [x] | **ADDED 2026-01-01** - Was missing, created from decompiled |
| LinuxViewRenderer.cs | [x] | **FIXED 2026-01-01** - Added ApplyShellColors(), FlyoutHeader rendering, FlyoutFooterText, MauiShellContent, ContentRenderer/ColorRefresher delegates, page BackgroundColor handling |
| MauiAppBuilderExtensions.cs | [x] | **DELETED 2026-01-01** - Not in decompiled, was outdated duplicate with wrong namespace |
| MauiHandlerExtensions.cs | [x] | **FIXED 2026-01-01** - Fixed WebView to use GtkWebViewHandler, FlexLayout to use LayoutHandler |
| ScopedLinuxMauiContext.cs | [x] | **ADDED 2026-01-01** - Was missing, created from decompiled |

---

## DISPATCHING

| File | Status | Notes |
|------|--------|-------|
| LinuxDispatcher.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, clean syntax |
| LinuxDispatcherProvider.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| LinuxDispatcherTimer.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, clean syntax |

---

## NATIVE

| File | Status | Notes |
|------|--------|-------|
| CairoNative.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| GdkNative.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| GLibNative.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| GtkNative.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| WebKitNative.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |

---

## WINDOW

| File | Status | Notes |
|------|--------|-------|
| CursorType.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| GtkHostWindow.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, main has clean comments |
| WaylandWindow.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, main has clean comments |
| X11Window.cs | [x] | **FIXED 2026-01-01** - Added SVG icon support, event counter logging from decompiled |

---

## RENDERING

| File | Status | Notes |
|------|--------|-------|
| GpuRenderingEngine.cs | [x] | **FIXED 2026-01-01** - Removed embedded GpuStats |
| GpuStats.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| GtkSkiaSurfaceWidget.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, same public API |
| LayeredRenderer.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| RenderCache.cs | [x] | **FIXED 2026-01-01** - Removed embedded LayeredRenderer, RenderLayer, TextRenderCache |
| RenderLayer.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| ResourceCache.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |
| SkiaRenderingEngine.cs | [x] | **FIXED 2026-01-01** - Removed embedded ResourceCache |
| TextRenderCache.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled |

---

## INTEROP

| File | Status | Notes |
|------|--------|-------|
| ClientMessageData.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| WebKitGtk.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, main has cleaner formatting with regions |
| X11.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled, X11Interop.cs DELETED (was duplicate) |
| XButtonEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XClientMessageEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XConfigureEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XCrossingEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XCursorShape.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XEventMask.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XEventType.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XExposeEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XFocusChangeEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XKeyEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XMotionEvent.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XSetWindowAttributes.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| XWindowClass.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |

---

## CONVERTERS

| File | Status | Notes |
|------|--------|-------|
| ColorExtensions.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SKColorTypeConverter.cs | [x] | **FIXED 2026-01-01** - Removed embedded ColorExtensions |
| SKPointTypeConverter.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SKRectTypeConverter.cs | [x] | **FIXED 2026-01-01** - Removed embedded SKSizeTypeConverter, SKPointTypeConverter, SKTypeExtensions |
| SKSizeTypeConverter.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |
| SKTypeExtensions.cs | [x] | **VERIFIED 2026-01-01** - Separate file, matches decompiled |

---

## CORE

| File | Status | Notes |
|------|--------|-------|
| LinuxApplication.cs | [x] | **FIXED 2026-01-01** - Removed embedded DisplayServerType, LinuxApplicationOptions |
| LinuxApplicationOptions.cs | [x] | **VERIFIED 2026-01-01** - Matches decompiled (separate file) |

---

## TYPES

| File | Status | Notes |
|------|--------|-------|
| ToggledEventArgs.cs | [x] | ADDED - was missing, required by SkiaSwitch |
