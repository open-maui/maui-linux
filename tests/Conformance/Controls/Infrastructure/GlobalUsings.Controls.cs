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

// The Skia image views hold an SKBitmap (MAUI names the native picture type per platform).
global using PlatformImageType = SkiaSharp.SKBitmap;
