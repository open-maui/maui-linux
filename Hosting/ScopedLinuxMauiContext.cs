// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Hosting;

public class ScopedLinuxMauiContext : IMauiContext
{
    private readonly LinuxMauiContext _parent;

    public IServiceProvider Services => _parent.Services;

    public IMauiHandlersFactory Handlers => _parent.Handlers;

    public ScopedLinuxMauiContext(LinuxMauiContext parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }
}
