// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// MAUI's Controls device tests are written without implicit usings (System.IO
// would make `Path` ambiguous with Microsoft.Maui.Controls.Shapes.Path); these
// are the namespaces the Linux files here rely on.
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
// MAUI's device-test build gets this from its runner package props.
global using Xunit;

// OpenMaui declares public types in Microsoft.Maui.Platform (a namespace MAUI
// code imports routinely) whose names are also Controls types; any file that
// imports both gets CS0104 / CS0176. An alias outranks namespace imports, so
// MAUI's meaning wins here. Recorded as API-surface findings in docs/CONFORMANCE.md.
global using LayoutAlignment = Microsoft.Maui.Controls.LayoutAlignment;
global using MenuBarItem = Microsoft.Maui.Controls.MenuBarItem;
global using FlyoutLayoutBehavior = Microsoft.Maui.Controls.FlyoutLayoutBehavior;
// The full list of such OpenMaui types (see the API-surface findings); PointerEventArgs
// is left out because the shared LinuxInput.cs means OpenMaui's.
global using CheckedChangedEventArgs = Microsoft.Maui.Controls.CheckedChangedEventArgs;
global using IndicatorShape = Microsoft.Maui.Controls.IndicatorShape;
global using ItemsLayoutOrientation = Microsoft.Maui.Controls.ItemsLayoutOrientation;
global using MenuItem = Microsoft.Maui.Controls.MenuItem;
global using NavigationEventArgs = Microsoft.Maui.Controls.NavigationEventArgs;
global using PositionChangedEventArgs = Microsoft.Maui.Controls.PositionChangedEventArgs;
global using ScrolledEventArgs = Microsoft.Maui.Controls.ScrolledEventArgs;
global using ScrollToPosition = Microsoft.Maui.Controls.ScrollToPosition;
global using ShellContent = Microsoft.Maui.Controls.ShellContent;
global using ShellSection = Microsoft.Maui.Controls.ShellSection;
global using StackOrientation = Microsoft.Maui.Controls.StackOrientation;
global using SwipeEndedEventArgs = Microsoft.Maui.Controls.SwipeEndedEventArgs;
global using SwipeItem = Microsoft.Maui.Controls.SwipeItem;
global using SwipeStartedEventArgs = Microsoft.Maui.Controls.SwipeStartedEventArgs;
global using TextChangedEventArgs = Microsoft.Maui.Controls.TextChangedEventArgs;
global using TimeChangedEventArgs = Microsoft.Maui.Controls.TimeChangedEventArgs;
global using ToggledEventArgs = Microsoft.Maui.Controls.ToggledEventArgs;
global using WebNavigatedEventArgs = Microsoft.Maui.Controls.WebNavigatedEventArgs;
global using WebNavigatingEventArgs = Microsoft.Maui.Controls.WebNavigatingEventArgs;
global using GridLength = Microsoft.Maui.GridLength;
global using GridUnitType = Microsoft.Maui.GridUnitType;
global using ScrollBarVisibility = Microsoft.Maui.ScrollBarVisibility;
global using ScrollOrientation = Microsoft.Maui.ScrollOrientation;
global using SwipeDirection = Microsoft.Maui.SwipeDirection;
global using SwipeMode = Microsoft.Maui.SwipeMode;

// The Skia image views hold an SKBitmap (MAUI names the native picture type per platform).
global using PlatformImageType = SkiaSharp.SKBitmap;
