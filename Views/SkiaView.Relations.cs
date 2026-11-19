// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

/// <summary>
/// <c>AutomationProperties.LabeledBy</c>, as MAUI's Windows and Android platforms honour it: a
/// control labelled by another view (a Label beside an Entry) is "labelled by" it and takes its
/// name from it when it has no <c>SemanticProperties.Description</c>; the label is "label for"
/// the control. Screen readers read the relation (AT-SPI's labelled-by and label-for).
/// </summary>
public partial class SkiaView : IAccessibleRelations
{
#pragma warning disable CS0618 // AutomationProperties.LabeledBy is obsolete, but MAUI's platforms still honour it
    /// <summary>The view that labels this one (AutomationProperties.LabeledBy), as its Skia view.</summary>
    internal SkiaView? LabelledBy =>
        MauiView is { } view && AutomationProperties.GetLabeledBy(view)?.Handler?.PlatformView is SkiaView label && !ReferenceEquals(label, this)
            ? label
            : null;

    /// <summary>The views this one labels: those whose LabeledBy is this view.</summary>
    internal IReadOnlyList<SkiaView> LabelFor() =>
        MauiView is VisualElement self ? Linux.Handlers.SemanticMapper.LabelledBy(self) : Array.Empty<SkiaView>();
#pragma warning restore CS0618

    /// <summary>The name a control takes from its label, or null without one.</summary>
    private string? LabelledByName() =>
        LabelledBy is IAccessible label && label.AccessibleName is { Length: > 0 } name ? name : null;

    IReadOnlyList<(AccessibleRelation Relation, IReadOnlyList<IAccessible> Targets)> IAccessibleRelations.Relations
    {
        get
        {
            var relations = new List<(AccessibleRelation, IReadOnlyList<IAccessible>)>();
            if (LabelledBy is { } label)
                relations.Add((AccessibleRelation.LabelledBy, [label]));
            var labelled = LabelFor();
            if (labelled.Count > 0)
                relations.Add((AccessibleRelation.LabelFor, labelled.Cast<IAccessible>().ToList()));
            return relations;
        }
    }
}
