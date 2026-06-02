// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

// The project sets <GenerateAssemblyInfo>false</GenerateAssemblyInfo>, so the
// <InternalsVisibleTo> MSBuild item doesn't get materialized into the assembly
// — we declare it directly here. Tests need to see internal helpers (text
// encoding conversion, etc.) without having to widen the public surface.
[assembly: InternalsVisibleTo("OpenMaui.Controls.Linux.Tests")]
