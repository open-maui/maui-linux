// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Services.AtSpi;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The accessibility service views report changes to: it forwards them, as AT-SPI2 events, to
/// the <see cref="AtSpiBridge"/>, which exports the app to the accessibility bus while assistive
/// technology is running. Without the bridge (no screen reader, no accessibility bus) every
/// call does nothing.
/// </summary>
public partial class AtSpi2AccessibilityService : IAccessibilityService, IDisposable
{
    private IAccessible? _focusedAccessible;
    private readonly string _applicationName;

    /// <summary>True while the app is exported to the accessibility bus.</summary>
    public bool IsEnabled => AtSpiBridge.Instance != null;

    public AtSpi2AccessibilityService(string applicationName = "MAUI Application")
    {
        _applicationName = applicationName;
    }

    /// <summary>The bridge is started by the application (<c>LinuxApplication.Run</c>); nothing to do here.</summary>
    public void Initialize()
    {
    }

    /// <summary>Objects are exported when a client reaches them; views do not register.</summary>
    public void Register(IAccessible accessible)
    {
    }

    public void Unregister(IAccessible accessible)
    {
    }

    public void NotifyFocusChanged(IAccessible? accessible)
    {
        var previous = _focusedAccessible;
        _focusedAccessible = accessible;
        AtSpiBridge.Instance?.FocusChanged(previous, accessible);
    }

    public void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property)
    {
        if (AtSpiBridge.Instance is not { } bridge || accessible == null)
            return;
        switch (property)
        {
            case AccessibleProperty.Name:
                bridge.NameChanged(accessible);
                break;
            case AccessibleProperty.Description:
                bridge.DescriptionChanged(accessible);
                break;
            case AccessibleProperty.Children:
                bridge.ChildrenChanged(accessible);
                break;
        }
    }

    public void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value)
    {
        if (accessible != null)
            AtSpiBridge.Instance?.StateChanged(accessible, StateName(state), value);
    }

    public void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite)
    {
        if (!string.IsNullOrEmpty(text))
            AtSpiBridge.Instance?.Announce(_focusedAccessible, text, priority == AnnouncementPriority.Assertive);
    }

    /// <summary>The AT-SPI name of a state (its AtspiStateType nick: "focused", "multi-line", "has-tooltip").</summary>
    internal static string StateName(AccessibleState state)
    {
        if (state == AccessibleState.HasToolTip)
            return "has-tooltip";
        var name = state.ToString();
        var nick = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                nick.Append('-');
            nick.Append(char.ToLowerInvariant(name[i]));
        }
        return nick.ToString();
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
    }
}
