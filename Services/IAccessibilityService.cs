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

/// <summary>
/// Interface for accessible objects.
/// </summary>
public interface IAccessible
{
    /// <summary>
    /// Gets the unique identifier for this accessible.
    /// </summary>
    string AccessibleId { get; }

    /// <summary>
    /// Gets the accessible name (label for screen readers).
    /// </summary>
    string AccessibleName { get; }

    /// <summary>
    /// Gets the accessible description (additional context).
    /// </summary>
    string AccessibleDescription { get; }

    /// <summary>
    /// Gets the accessible role.
    /// </summary>
    AccessibleRole Role { get; }

    /// <summary>
    /// Gets the accessible states.
    /// </summary>
    AccessibleStates States { get; }

    /// <summary>
    /// Gets the parent accessible.
    /// </summary>
    IAccessible? Parent { get; }

    /// <summary>
    /// Gets the child accessibles.
    /// </summary>
    IReadOnlyList<IAccessible> Children { get; }

    /// <summary>
    /// Gets the bounding rectangle in screen coordinates.
    /// </summary>
    AccessibleRect Bounds { get; }

    /// <summary>
    /// Gets the available actions.
    /// </summary>
    IReadOnlyList<AccessibleAction> Actions { get; }

    /// <summary>
    /// Performs an action.
    /// </summary>
    /// <param name="actionName">The name of the action to perform.</param>
    /// <returns>True if the action was performed.</returns>
    bool DoAction(string actionName);

    /// <summary>
    /// Gets the accessible value (for sliders, progress bars, etc.).
    /// </summary>
    double? Value { get; }

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    double? MinValue { get; }

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    double? MaxValue { get; }

    /// <summary>
    /// Sets the accessible value.
    /// </summary>
    bool SetValue(double value);
}

/// <summary>
/// Interface for accessible text components.
/// </summary>
public interface IAccessibleText : IAccessible
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Gets the caret offset.
    /// </summary>
    int CaretOffset { get; }

    /// <summary>
    /// Gets the number of selections.
    /// </summary>
    int SelectionCount { get; }

    /// <summary>
    /// Gets the selection at the specified index.
    /// </summary>
    (int Start, int End) GetSelection(int index);

    /// <summary>
    /// Sets the selection.
    /// </summary>
    bool SetSelection(int index, int start, int end);

    /// <summary>
    /// Gets the character at the specified offset.
    /// </summary>
    char GetCharacterAtOffset(int offset);

    /// <summary>
    /// Gets the text in the specified range.
    /// </summary>
    string GetTextInRange(int start, int end);

    /// <summary>
    /// Gets the bounds of the character at the specified offset.
    /// </summary>
    AccessibleRect GetCharacterBounds(int offset);
}

/// <summary>
/// Interface for editable text components.
/// </summary>
public interface IAccessibleEditableText : IAccessibleText
{
    /// <summary>
    /// Sets the text content.
    /// </summary>
    bool SetText(string text);

    /// <summary>
    /// Inserts text at the specified position.
    /// </summary>
    bool InsertText(int position, string text);

    /// <summary>
    /// Deletes text in the specified range.
    /// </summary>
    bool DeleteText(int start, int end);

    /// <summary>
    /// Copies text to clipboard.
    /// </summary>
    bool CopyText(int start, int end);

    /// <summary>
    /// Cuts text to clipboard.
    /// </summary>
    bool CutText(int start, int end);

    /// <summary>
    /// Pastes text from clipboard.
    /// </summary>
    bool PasteText(int position);
}

/// <summary>
/// Accessible roles (based on AT-SPI2 roles).
/// </summary>
public enum AccessibleRole
{
    Unknown,
    Window,
    Application,
    Panel,
    Frame,
    Button,
    CheckBox,
    RadioButton,
    ComboBox,
    Entry,
    Label,
    List,
    ListItem,
    Menu,
    MenuBar,
    MenuItem,
    ScrollBar,
    Slider,
    SpinButton,
    StatusBar,
    Tab,
    TabPanel,
    Text,
    ToggleButton,
    ToolBar,
    ToolTip,
    Tree,
    TreeItem,
    Image,
    ProgressBar,
    Separator,
    Link,
    Table,
    TableCell,
    TableRow,
    TableColumnHeader,
    TableRowHeader,
    PageTab,
    PageTabList,
    Dialog,
    Alert,
    Filler,
    Icon,
    Canvas
}

/// <summary>
/// Accessible states.
/// </summary>
[Flags]
public enum AccessibleStates : long
{
    None = 0,
    Active = 1L << 0,
    Armed = 1L << 1,
    Busy = 1L << 2,
    Checked = 1L << 3,
    Collapsed = 1L << 4,
    Defunct = 1L << 5,
    Editable = 1L << 6,
    Enabled = 1L << 7,
    Expandable = 1L << 8,
    Expanded = 1L << 9,
    Focusable = 1L << 10,
    Focused = 1L << 11,
    HasToolTip = 1L << 12,
    Horizontal = 1L << 13,
    Iconified = 1L << 14,
    Modal = 1L << 15,
    MultiLine = 1L << 16,
    MultiSelectable = 1L << 17,
    Opaque = 1L << 18,
    Pressed = 1L << 19,
    Resizable = 1L << 20,
    Selectable = 1L << 21,
    Selected = 1L << 22,
    Sensitive = 1L << 23,
    Showing = 1L << 24,
    SingleLine = 1L << 25,
    Stale = 1L << 26,
    Transient = 1L << 27,
    Vertical = 1L << 28,
    Visible = 1L << 29,
    ManagesDescendants = 1L << 30,
    Indeterminate = 1L << 31,
    Required = 1L << 32,
    Truncated = 1L << 33,
    Animated = 1L << 34,
    InvalidEntry = 1L << 35,
    SupportsAutocompletion = 1L << 36,
    SelectableText = 1L << 37,
    IsDefault = 1L << 38,
    Visited = 1L << 39,
    ReadOnly = 1L << 40
}

/// <summary>
/// Accessible state enumeration for notifications.
/// </summary>
public enum AccessibleState
{
    Active,
    Armed,
    Busy,
    Checked,
    Collapsed,
    Defunct,
    Editable,
    Enabled,
    Expandable,
    Expanded,
    Focusable,
    Focused,
    Horizontal,
    Iconified,
    Modal,
    MultiLine,
    Opaque,
    Pressed,
    Resizable,
    Selectable,
    Selected,
    Sensitive,
    Showing,
    SingleLine,
    Stale,
    Transient,
    Vertical,
    Visible,
    ManagesDescendants,
    Indeterminate,
    Required,
    InvalidEntry,
    ReadOnly
}

/// <summary>
/// Accessible property for notifications.
/// </summary>
public enum AccessibleProperty
{
    Name,
    Description,
    Role,
    Value,
    Parent,
    Children
}

/// <summary>
/// Announcement priority.
/// </summary>
public enum AnnouncementPriority
{
    /// <summary>
    /// Low priority - can be interrupted.
    /// </summary>
    Polite,

    /// <summary>
    /// High priority - interrupts current speech.
    /// </summary>
    Assertive
}

/// <summary>
/// Represents an accessible action.
/// </summary>
public class AccessibleAction
{
    /// <summary>
    /// The action name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The action description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The keyboard shortcut for this action.
    /// </summary>
    public string? KeyBinding { get; set; }
}

/// <summary>
/// Represents a rectangle in accessible coordinates.
/// </summary>
public struct AccessibleRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public AccessibleRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
