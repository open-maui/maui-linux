namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Types of cursors supported on Linux.
/// </summary>
public enum CursorType
{
    Arrow,
    Hand,
    Text,

    /// <summary>Horizontal resize (left-right arrows), e.g. over a column border.</summary>
    SizeWestEast,

    /// <summary>Vertical resize (up-down arrows), e.g. over a row border.</summary>
    SizeNorthSouth,

    /// <summary>Move (four-way arrows), e.g. while dragging an item.</summary>
    SizeAll,
}
