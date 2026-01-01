// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class PreEditAttribute
{
    public int Start { get; set; }

    public int Length { get; set; }

    public PreEditAttributeType Type { get; set; }
}
