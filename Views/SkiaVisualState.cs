// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Represents a visual state with setters and optional triggers.
/// Maps to MAUI VisualState.
/// </summary>
public class SkiaVisualState
{
    /// <summary>
    /// Gets or sets the name of this visual state.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets the setters that define property changes for this state.
    /// </summary>
    public List<SkiaVisualStateSetter> Setters { get; } = new List<SkiaVisualStateSetter>();

    /// <summary>
    /// Gets the state triggers that can automatically activate this state.
    /// </summary>
    public List<SkiaStateTriggerBase> StateTriggers { get; } = new List<SkiaStateTriggerBase>();

    /// <summary>
    /// Gets or sets the target type this state applies to.
    /// </summary>
    public Type? TargetType { get; set; }

    /// <summary>
    /// Attaches triggers to the specified view.
    /// </summary>
    internal void AttachTriggers(SkiaView view)
    {
        foreach (var trigger in StateTriggers)
        {
            trigger.OwnerState = this;
            trigger.OwnerView = view;
        }
    }

    /// <summary>
    /// Detaches triggers from the view.
    /// </summary>
    internal void DetachTriggers()
    {
        foreach (var trigger in StateTriggers)
        {
            trigger.OwnerState = null;
            trigger.OwnerView = null;
        }
    }
}
