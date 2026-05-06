// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class GpuStats
{
    public bool IsGpuAccelerated { get; init; }

    public int MaxTextureSize { get; init; }

    public long ResourceCacheUsedBytes { get; init; }

    public long ResourceCacheLimitBytes { get; init; }

    public double ResourceCacheUsedMB => ResourceCacheUsedBytes / 1048576.0;

    public double ResourceCacheLimitMB => ResourceCacheLimitBytes / 1048576.0;
}
