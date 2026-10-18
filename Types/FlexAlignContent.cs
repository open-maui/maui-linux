namespace Microsoft.Maui.Platform;

/// <summary>
/// Specifies how flex lines are aligned within the flex container.
/// Matches Microsoft.Maui.Layouts.FlexAlignContent enum.
/// </summary>
[Obsolete("Use Microsoft.Maui.Layouts.FlexAlignContent; this type is removed in OpenMaui 11.0.")]
public enum FlexAlignContent
{
    Start,
    Center,
    End,
    Stretch,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly
}
