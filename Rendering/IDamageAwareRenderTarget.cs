// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// A render target that does not keep the previous frame in the buffer it
/// hands out (a GPU swapchain), but can say how old that buffer's contents
/// are and accept the damaged region with the submission. With both, the
/// engine repaints only what changed since that buffer was last shown and the
/// compositor only recomposes the damage.
/// </summary>
public interface IDamageAwareRenderTarget
{
    /// <summary>
    /// Age of the buffer the next frame will draw into: 0 = contents
    /// undefined (repaint everything), 1 = the previous frame, n = n frames
    /// old. Must be queried before <see cref="IRenderTarget.BeginFrame"/>.
    /// </summary>
    int QueryBufferAge();

    /// <summary>
    /// Damage of the frame about to be submitted by the next
    /// <see cref="IRenderTarget.EndFrame"/>, in physical pixels with a
    /// top-left origin; null for the whole surface.
    /// </summary>
    void SetFrameDamage(IReadOnlyList<SKRectI>? damage);
}
