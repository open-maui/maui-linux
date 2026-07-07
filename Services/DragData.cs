// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragData
{
    /// <summary>X11 source window of the drag (XDND backend only; 0 on Wayland).</summary>
    public nint SourceWindow { get; set; }

    /// <summary>X11 target atoms advertised by the source (XDND backend only; empty on Wayland).</summary>
    public nint[] SupportedTypes { get; set; } = [];

    /// <summary>MIME types advertised by the source (Wayland backend; empty on X11).</summary>
    public string[] SupportedMimeTypes { get; set; } = [];

    /// <summary>The wl_data_offer backing this drag (Wayland backend only; 0 on X11).</summary>
    internal nint WaylandOffer { get; set; }

    public string? Text { get; set; }
    public string[]? FilePaths { get; set; }
    public object? Data { get; set; }
}
