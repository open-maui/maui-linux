namespace Microsoft.Maui.Platform.Linux.Rendering;

public class GpuStats
{
	public bool IsGpuAccelerated { get; init; }

	public int MaxTextureSize { get; init; }

	public long ResourceCacheUsedBytes { get; init; }

	public long ResourceCacheLimitBytes { get; init; }

	public double ResourceCacheUsedMB => (double)ResourceCacheUsedBytes / 1048576.0;

	public double ResourceCacheLimitMB => (double)ResourceCacheLimitBytes / 1048576.0;
}
