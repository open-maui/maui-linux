// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class NullAccessibilityService : IAccessibilityService
{
    public bool IsEnabled => false;

    public void Initialize()
    {
    }

    public void Register(IAccessible accessible)
    {
    }

    public void Unregister(IAccessible accessible)
    {
    }

    public void NotifyFocusChanged(IAccessible? accessible)
    {
    }

    public void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property)
    {
    }

    public void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value)
    {
    }

    public void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite)
    {
    }

    public void Shutdown()
    {
    }
}
