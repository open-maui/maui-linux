// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Represents a property setter within a visual state.
/// Maps to MAUI Setter class.
/// </summary>
public class SkiaVisualStateSetter
{
    private object? _originalValue;
    private bool _hasOriginalValue;
    private SkiaView? _targetView;

    /// <summary>
    /// Gets or sets the property to set.
    /// </summary>
    public BindableProperty? Property { get; set; }

    /// <summary>
    /// Gets or sets the value to set.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the name of the target element within a template.
    /// If null, the setter applies to the root element.
    /// </summary>
    public string? TargetName { get; set; }

    /// <summary>
    /// Applies the setter value to the view.
    /// </summary>
    public void Apply(SkiaView view)
    {
        var target = ResolveTarget(view);
        if (target == null || Property == null)
            return;

        if (!_hasOriginalValue)
        {
            _originalValue = target.GetValue(Property);
            _hasOriginalValue = true;
            _targetView = target;
        }
        target.SetValue(Property, Value);
    }

    /// <summary>
    /// Restores the original value on the view.
    /// </summary>
    public void Unapply(SkiaView view)
    {
        var target = _targetView ?? ResolveTarget(view);
        if (target == null || Property == null || !_hasOriginalValue)
            return;

        target.SetValue(Property, _originalValue);
    }

    /// <summary>
    /// Resolves the target view based on TargetName.
    /// </summary>
    private SkiaView? ResolveTarget(SkiaView view)
    {
        if (string.IsNullOrEmpty(TargetName))
            return view;

        // Find named element in visual tree
        return FindNamedElement(view, TargetName);
    }

    /// <summary>
    /// Finds a named element in the visual tree.
    /// </summary>
    private static SkiaView? FindNamedElement(SkiaView root, string name)
    {
        // Check if root has the name (using Name property if available)
        if (root.Name == name)
            return root;

        // Search children if it's a layout
        if (root is SkiaLayoutView layout)
        {
            foreach (var child in layout.Children)
            {
                var found = FindNamedElement(child, name);
                if (found != null)
                    return found;
            }
        }

        return null;
    }
}
