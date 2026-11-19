// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>An AT-SPI relation between accessible objects (AtspiRelationType).</summary>
public enum AccessibleRelation
{
    /// <summary>This object is a label for the targets.</summary>
    LabelFor = 1,

    /// <summary>The targets label this object.</summary>
    LabelledBy = 2,
}

/// <summary>An accessible object that is related to others (a label and the control it names).</summary>
internal interface IAccessibleRelations
{
    IReadOnlyList<(AccessibleRelation Relation, IReadOnlyList<IAccessible> Targets)> Relations { get; }
}
