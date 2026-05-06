// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Interface for accessibility services using AT-SPI2.
/// Provides screen reader support on Linux.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>
    /// Gets whether accessibility is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Initializes the accessibility service.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Registers an accessible object.
    /// </summary>
    /// <param name="accessible">The accessible object to register.</param>
    void Register(IAccessible accessible);

    /// <summary>
    /// Unregisters an accessible object.
    /// </summary>
    /// <param name="accessible">The accessible object to unregister.</param>
    void Unregister(IAccessible accessible);

    /// <summary>
    /// Notifies that focus has changed.
    /// </summary>
    /// <param name="accessible">The newly focused accessible object.</param>
    void NotifyFocusChanged(IAccessible? accessible);

    /// <summary>
    /// Notifies that a property has changed.
    /// </summary>
    /// <param name="accessible">The accessible object.</param>
    /// <param name="property">The property that changed.</param>
    void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property);

    /// <summary>
    /// Notifies that an accessible's state has changed.
    /// </summary>
    /// <param name="accessible">The accessible object.</param>
    /// <param name="state">The state that changed.</param>
    /// <param name="value">The new value of the state.</param>
    void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value);

    /// <summary>
    /// Announces text to the screen reader.
    /// </summary>
    /// <param name="text">The text to announce.</param>
    /// <param name="priority">The announcement priority.</param>
    void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite);

    /// <summary>
    /// Shuts down the accessibility service.
    /// </summary>
    void Shutdown();
}
