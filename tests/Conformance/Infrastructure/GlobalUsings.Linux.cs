// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// MAUI's GlobalNamespaces.cs imports Microsoft.Maui.Platform, where OpenMaui
// declares its own public LayoutAlignment enum (Views/LayoutAlignment.cs, with
// different ordinals). Any MAUI source that also imports Microsoft.Maui.Primitives
// (StubBase.cs does) then fails with CS0104. An alias outranks namespace imports,
// so MAUI's meaning wins here. Recorded as an API-surface finding in docs/CONFORMANCE.md.
global using LayoutAlignment = Microsoft.Maui.Primitives.LayoutAlignment;

// MAUI's device-test build gets this from its runner package props.
global using Xunit;

// ImageHandlerTests.cs names the native picture type per platform; on Linux the
// Skia image views hold an SKBitmap.
global using PlatformImageType = SkiaSharp.SKBitmap;
