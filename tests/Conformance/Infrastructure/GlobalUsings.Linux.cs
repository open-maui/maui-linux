// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The project's implicit usings (from the MAUI package) import Microsoft.Maui.Controls, so any
// MAUI source that also imports Microsoft.Maui.Primitives (StubBase.cs does) sees two
// LayoutAlignment enums and fails with CS0104. An alias outranks namespace imports, so the
// Primitives one (IView's) wins here.
global using LayoutAlignment = Microsoft.Maui.Primitives.LayoutAlignment;

// MAUI's device-test build gets this from its runner package props.
global using Xunit;

// ImageHandlerTests.cs names the native picture type per platform; on Linux the
// Skia image views hold an SKBitmap.
global using PlatformImageType = SkiaSharp.SKBitmap;
