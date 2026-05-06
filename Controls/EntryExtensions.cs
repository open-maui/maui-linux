// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Controls;

/// <summary>
/// Provides attached properties for Entry controls.
/// </summary>
public static class EntryExtensions
{
    /// <summary>
    /// Attached property for SelectAllOnDoubleClick behavior.
    /// When true, double-clicking the entry selects all text instead of just the word.
    /// </summary>
    public static readonly BindableProperty SelectAllOnDoubleClickProperty =
        BindableProperty.CreateAttached(
            "SelectAllOnDoubleClick",
            typeof(bool),
            typeof(EntryExtensions),
            false);

    /// <summary>
    /// Gets the SelectAllOnDoubleClick value for the specified entry.
    /// </summary>
    public static bool GetSelectAllOnDoubleClick(BindableObject view)
    {
        return (bool)view.GetValue(SelectAllOnDoubleClickProperty);
    }

    /// <summary>
    /// Sets the SelectAllOnDoubleClick value for the specified entry.
    /// </summary>
    public static void SetSelectAllOnDoubleClick(BindableObject view, bool value)
    {
        view.SetValue(SelectAllOnDoubleClickProperty, value);
    }
}
