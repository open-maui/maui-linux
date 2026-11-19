// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.AtSpi;

namespace Microsoft.Maui.Platform;

// What each control is to assistive technology (the IAccessible model the AT-SPI bridge exports):
// its role, the name it has without SemanticProperties.Description, its states, actions, value
// and text, as GTK's widgets and WinUI's automation peers give them. A control's own changes
// (text, checked, value) are told to the bridge while it runs.

public partial class SkiaView
{
    /// <summary>Raised for every bindable property change; the accessible model hears about it (its notifications go out only while the bridge runs).</summary>
    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        OnAccessiblePropertyChanged(propertyName);
    }

    /// <summary>A property of this view changed. Overrides keep what they compare against current, and notify through the bridge when it runs.</summary>
    private protected virtual void OnAccessiblePropertyChanged(string? propertyName)
    {
    }

    /// <summary>The window finished handling a key, text or pointer event for this focused view.</summary>
    internal virtual void AfterAccessibleInput()
    {
    }

    /// <summary>A heading (SemanticProperties.HeadingLevel) is a heading whatever the control.</summary>
    private protected AccessibleRole RoleOrHeading(AccessibleRole role) =>
        SemanticHeadingLevel != SemanticHeadingLevel.None ? AccessibleRole.Heading : role;

    private static readonly AccessibleAction s_clickAction = new() { Name = "click", Description = "Activates the control" };
    private static readonly AccessibleAction s_toggleAction = new() { Name = "toggle", Description = "Toggles the control" };
    private static readonly AccessibleAction s_pressAction = new() { Name = "press", Description = "Opens the list" };

    private protected static IReadOnlyList<AccessibleAction> ClickAction { get; } = [s_clickAction];
    private protected static IReadOnlyList<AccessibleAction> ToggleActions { get; } = [s_clickAction, s_toggleAction];
    private protected static IReadOnlyList<AccessibleAction> PressAction { get; } = [s_pressAction];

    private protected void NotifyAccessibleName() => AtSpiBridge.Instance?.NameChanged(this);

    private protected void NotifyAccessibleValue() => AtSpiBridge.Instance?.ValueChanged(this);

    private protected void NotifyAccessibleState(AccessibleState state, bool value) =>
        AtSpiBridge.Instance?.StateChanged(this, AtSpi2AccessibilityService.StateName(state), value);
}

public partial class SkiaLabel : IAccessibleText
{
    /// <summary>The text shown: the formatted text's spans, else Text.</summary>
    private string DisplayedText => FormattedText is { Spans.Count: > 0 } formatted
        ? string.Concat(formatted.Spans.Select(s => s.Text))
        : Text ?? string.Empty;

    protected override AccessibleRole GetAccessibleRole() => RoleOrHeading(AccessibleRole.Label);

    protected override string GetDefaultAccessibleName() => DisplayedText;

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        // The controls it labels are renamed by SemanticMapper, which follows the MAUI label.
        if (propertyName is nameof(Text) or nameof(FormattedText))
            NotifyAccessibleName();
    }

    string IAccessibleText.Text => DisplayedText;
    int IAccessibleText.CaretOffset => -1;
    int IAccessibleText.SelectionCount => 0;
    (int Start, int End) IAccessibleText.GetSelection(int index) => (0, 0);
    bool IAccessibleText.SetSelection(int index, int start, int end) => false;
    char IAccessibleText.GetCharacterAtOffset(int offset) => offset >= 0 && offset < DisplayedText.Length ? DisplayedText[offset] : '\0';
    string IAccessibleText.GetTextInRange(int start, int end) => AccessibleTextRange(DisplayedText, start, end);
    AccessibleRect IAccessibleText.GetCharacterBounds(int offset) => ((IAccessible)this).Bounds;

    internal static string AccessibleTextRange(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = end < 0 ? text.Length : Math.Clamp(end, start, text.Length);
        return text[start..end];
    }
}

public partial class SkiaButton
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Button;

    protected override string GetDefaultAccessibleName() => Text ?? string.Empty;

    protected override AccessibleStates GetAccessibleStates()
    {
        var states = base.GetAccessibleStates();
        if (IsPressed)
            states |= AccessibleStates.Pressed;
        return states;
    }

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => ClickAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "click" || !IsEnabled)
            return false;
        OnClicked();
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Text))
            NotifyAccessibleName();
    }
}

public partial class SkiaImageButton
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Button;

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => ClickAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "click" || !IsEnabled)
            return false;
        Clicked?.Invoke(this, EventArgs.Empty);
        ExecuteCommand();
        return true;
    }
}

public partial class SkiaCheckBox
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.CheckBox;

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | (IsChecked ? AccessibleStates.Checked : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => ToggleActions;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName is not ("click" or "toggle") || !IsEnabled)
            return false;
        IsChecked = !IsChecked;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(IsChecked))
            NotifyAccessibleState(AccessibleState.Checked, IsChecked);
    }
}

public partial class SkiaSwitch
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Switch;

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | (IsOn ? AccessibleStates.Checked : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => ToggleActions;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName is not ("click" or "toggle") || !IsEnabled)
            return false;
        IsOn = !IsOn;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(IsOn))
            NotifyAccessibleState(AccessibleState.Checked, IsOn);
    }
}

public partial class SkiaRadioButton
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.RadioButton;

    protected override string GetDefaultAccessibleName() => Content ?? string.Empty;

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | (IsChecked ? AccessibleStates.Checked : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => ClickAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "click" || !IsEnabled)
            return false;
        IsChecked = true;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(IsChecked))
            NotifyAccessibleState(AccessibleState.Checked, IsChecked);
        else if (propertyName == nameof(Content))
            NotifyAccessibleName();
    }
}

public partial class SkiaSlider
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Slider;

    protected override AccessibleStates GetAccessibleStates() => base.GetAccessibleStates() | AccessibleStates.Horizontal;

    protected override double? GetAccessibleValue() => Value;
    protected override double? GetAccessibleMinValue() => Minimum;
    protected override double? GetAccessibleMaxValue() => Maximum;

    protected override bool SetAccessibleValue(double value)
    {
        if (!IsEnabled)
            return false;
        Value = value;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Value))
            NotifyAccessibleValue();
    }
}

public partial class SkiaStepper
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.SpinButton;

    protected override double? GetAccessibleValue() => Value;
    protected override double? GetAccessibleMinValue() => Minimum;
    protected override double? GetAccessibleMaxValue() => Maximum;

    protected override bool SetAccessibleValue(double value)
    {
        if (!IsEnabled)
            return false;
        Value = Math.Clamp(value, Minimum, Maximum);
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Value))
            NotifyAccessibleValue();
    }
}

public partial class SkiaProgressBar
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ProgressBar;

    protected override double? GetAccessibleValue() => Progress;
    protected override double? GetAccessibleMinValue() => 0;
    protected override double? GetAccessibleMaxValue() => 1;

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Progress))
            NotifyAccessibleValue();
    }
}

public partial class SkiaActivityIndicator
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ProgressBar;

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | (IsRunning ? AccessibleStates.Busy | AccessibleStates.Animated : AccessibleStates.None);

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(IsRunning))
            NotifyAccessibleState(AccessibleState.Busy, IsRunning);
    }
}

public partial class SkiaImage
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Image;
}

public partial class SkiaPicker
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ComboBox;

    protected override string GetDefaultAccessibleName() => SelectedItem ?? Title ?? string.Empty;

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | AccessibleStates.Expandable | (IsOpen ? AccessibleStates.Expanded : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => PressAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "press" || !IsEnabled)
            return false;
        IsOpen = !IsOpen;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName is nameof(SelectedIndex) or nameof(Title))
            NotifyAccessibleName();
    }
}

public partial class SkiaDatePicker
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ComboBox;

    protected override string GetDefaultAccessibleName() =>
        Date.ToString(string.IsNullOrEmpty(Format) ? "d" : Format, CultureInfo.CurrentCulture);

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | AccessibleStates.Expandable | (IsOpen ? AccessibleStates.Expanded : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => PressAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "press" || !IsEnabled)
            return false;
        IsOpen = !IsOpen;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName is nameof(Date) or nameof(Format))
            NotifyAccessibleName();
    }
}

public partial class SkiaTimePicker
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ComboBox;

    protected override string GetDefaultAccessibleName()
    {
        var at = DateTime.Today.Add(Time);
        return at.ToString(string.IsNullOrEmpty(Format) ? "t" : Format, CultureInfo.CurrentCulture);
    }

    protected override AccessibleStates GetAccessibleStates() =>
        base.GetAccessibleStates() | AccessibleStates.Expandable | (IsOpen ? AccessibleStates.Expanded : AccessibleStates.None);

    protected override IReadOnlyList<AccessibleAction> GetAccessibleActions() => PressAction;

    protected override bool DoAccessibleAction(string actionName)
    {
        if (actionName != "press" || !IsEnabled)
            return false;
        IsOpen = !IsOpen;
        return true;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName is nameof(Time) or nameof(Format))
            NotifyAccessibleName();
    }
}

public partial class SkiaScrollView
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ScrollPane;
}

public partial class SkiaItemsView
{
    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.List;
}

public partial class SkiaBorder
{
    protected override AccessibleRole GetAccessibleRole() => RoleOrHeading(AccessibleRole.Panel);
}

public partial class SkiaFrame
{
    protected override AccessibleRole GetAccessibleRole() => RoleOrHeading(AccessibleRole.Panel);
}

/// <summary>The text an editable control's Text interface reads and edits, and the change events it sends.</summary>
internal static class AccessibleEditing
{
    /// <summary>Tells the bridge what changed between <paramref name="oldText"/> and <paramref name="newText"/> (one deletion, then one insertion).</summary>
    public static void NotifyTextChanged(IAccessible accessible, string oldText, string newText)
    {
        if (AtSpiBridge.Instance is not { } bridge || oldText == newText)
            return;
        int prefix = 0;
        int max = Math.Min(oldText.Length, newText.Length);
        while (prefix < max && oldText[prefix] == newText[prefix])
            prefix++;
        int suffix = 0;
        while (suffix < max - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            suffix++;
        var removed = oldText.Substring(prefix, oldText.Length - prefix - suffix);
        var added = newText.Substring(prefix, newText.Length - prefix - suffix);
        if (removed.Length > 0)
            bridge.TextChanged(accessible, inserted: false, prefix, removed);
        if (added.Length > 0)
            bridge.TextChanged(accessible, inserted: true, prefix, added);
    }

    public static (int Start, int End) Selection(int caret, int anchor, int length)
    {
        if (length == 0)
            return (caret, caret);
        int a = anchor, b = anchor + length;
        return (Math.Min(a, b), Math.Max(a, b));
    }
}

public partial class SkiaEntry : IAccessibleText
{
    private string _accessibleText = string.Empty;
    private int _accessibleCaret = -1;

    /// <summary>What a screen reader reads: the text, or a bullet per character for a password.</summary>
    private string AccessibleText => IsPassword ? new string('•', (Text ?? string.Empty).Length) : Text ?? string.Empty;

    protected override AccessibleRole GetAccessibleRole() => IsPassword ? AccessibleRole.PasswordText : AccessibleRole.Entry;

    protected override string GetDefaultAccessibleName() => Placeholder ?? string.Empty;

    protected override AccessibleStates GetAccessibleStates()
    {
        var states = base.GetAccessibleStates() | AccessibleStates.SingleLine | AccessibleStates.SelectableText;
        states |= IsReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.Editable;
        return states;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Text))
        {
            var text = AccessibleText;
            AccessibleEditing.NotifyTextChanged(this, _accessibleText, text);
            _accessibleText = text;
        }
        else if (propertyName == nameof(Placeholder))
        {
            NotifyAccessibleName();
        }
    }

    internal override void AfterAccessibleInput()
    {
        if (_cursorPosition == _accessibleCaret)
            return;
        _accessibleCaret = _cursorPosition;
        AtSpiBridge.Instance?.CaretMoved(this, _cursorPosition);
    }

    string IAccessibleText.Text => AccessibleText;
    int IAccessibleText.CaretOffset => _cursorPosition;
    int IAccessibleText.SelectionCount => _selectionLength != 0 ? 1 : 0;
    (int Start, int End) IAccessibleText.GetSelection(int index) =>
        index == 0 ? AccessibleEditing.Selection(_cursorPosition, _selectionStart, _selectionLength) : (0, 0);

    bool IAccessibleText.SetSelection(int index, int start, int end)
    {
        if (index != 0)
            return false;
        var length = (Text ?? string.Empty).Length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);
        CursorPosition = Math.Min(start, end);
        SelectionLength = Math.Abs(end - start);
        return true;
    }

    char IAccessibleText.GetCharacterAtOffset(int offset) => offset >= 0 && offset < AccessibleText.Length ? AccessibleText[offset] : '\0';
    string IAccessibleText.GetTextInRange(int start, int end) => SkiaLabel.AccessibleTextRange(AccessibleText, start, end);
    AccessibleRect IAccessibleText.GetCharacterBounds(int offset) => ((IAccessible)this).Bounds;
}

public partial class SkiaEditor : IAccessibleText
{
    private string _accessibleText = string.Empty;
    private int _accessibleCaret = -1;

    protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Entry;

    protected override string GetDefaultAccessibleName() => Placeholder ?? string.Empty;

    protected override AccessibleStates GetAccessibleStates()
    {
        var states = base.GetAccessibleStates() | AccessibleStates.MultiLine | AccessibleStates.SelectableText;
        states |= IsReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.Editable;
        return states;
    }

    private protected override void OnAccessiblePropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(Text))
        {
            var text = Text ?? string.Empty;
            AccessibleEditing.NotifyTextChanged(this, _accessibleText, text);
            _accessibleText = text;
        }
        else if (propertyName == nameof(Placeholder))
        {
            NotifyAccessibleName();
        }
    }

    internal override void AfterAccessibleInput()
    {
        if (_cursorPosition == _accessibleCaret)
            return;
        _accessibleCaret = _cursorPosition;
        AtSpiBridge.Instance?.CaretMoved(this, _cursorPosition);
    }

    string IAccessibleText.Text => Text ?? string.Empty;
    int IAccessibleText.CaretOffset => _cursorPosition;
    int IAccessibleText.SelectionCount => _selectionLength != 0 ? 1 : 0;
    (int Start, int End) IAccessibleText.GetSelection(int index) =>
        index == 0 ? AccessibleEditing.Selection(_cursorPosition, _cursorPosition, _selectionLength) : (0, 0);

    bool IAccessibleText.SetSelection(int index, int start, int end)
    {
        if (index != 0)
            return false;
        var length = (Text ?? string.Empty).Length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);
        CursorPosition = Math.Min(start, end);
        SelectionLength = Math.Abs(end - start);
        return true;
    }

    char IAccessibleText.GetCharacterAtOffset(int offset)
    {
        var text = Text ?? string.Empty;
        return offset >= 0 && offset < text.Length ? text[offset] : '\0';
    }

    string IAccessibleText.GetTextInRange(int start, int end) => SkiaLabel.AccessibleTextRange(Text ?? string.Empty, start, end);
    AccessibleRect IAccessibleText.GetCharacterBounds(int offset) => ((IAccessible)this).Bounds;
}
