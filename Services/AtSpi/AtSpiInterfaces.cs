// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.AtSpi;

// The AT-SPI2 D-Bus interfaces an application exports (at-spi2-core's xml/*.xml), as Tmds.DBus
// interfaces. Every exported accessible implements all of them; GetInterfaces tells a client
// which ones apply to it. Object references are (bus name, object path) structs. A method whose
// one out argument is a struct returns a one-element ValueTuple: Tmds.DBus sends a returned tuple's
// elements as separate out arguments.

/// <summary>org.a11y.atspi.Accessible: the tree, the role, the name and the states.</summary>
[DBusInterface("org.a11y.atspi.Accessible")]
internal interface IAtSpiAccessible : IDBusObject
{
    Task<ValueTuple<(string, ObjectPath)>> GetChildAtIndexAsync(int index);
    Task<(string, ObjectPath)[]> GetChildrenAsync();
    Task<int> GetIndexInParentAsync();
    Task<(uint, (string, ObjectPath)[])[]> GetRelationSetAsync();
    Task<uint> GetRoleAsync();
    Task<string> GetRoleNameAsync();
    Task<string> GetLocalizedRoleNameAsync();
    Task<uint[]> GetStateAsync();
    Task<IDictionary<string, string>> GetAttributesAsync();
    Task<ValueTuple<(string, ObjectPath)>> GetApplicationAsync();
    Task<string[]> GetInterfacesAsync();
    Task<object> GetAsync(string prop);
    Task<AtSpiAccessibleProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
}

[Dictionary]
internal sealed class AtSpiAccessibleProperties
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public (string, ObjectPath) Parent;
    public int ChildCount;
    public string Locale = string.Empty;
    public string AccessibleId = string.Empty;
    public string HelpText = string.Empty;
}

/// <summary>org.a11y.atspi.Application: the root object.</summary>
[DBusInterface("org.a11y.atspi.Application")]
internal interface IAtSpiApplication : IDBusObject
{
    Task<string> GetLocaleAsync(uint lctype);
    Task<object> GetAsync(string prop);
    Task<AtSpiApplicationProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
}

[Dictionary]
internal sealed class AtSpiApplicationProperties
{
    public string ToolkitName = "OpenMaui";
    public string Version = string.Empty;
    public string AtspiVersion = "2.1";
    public int Id;
}

/// <summary>org.a11y.atspi.Component: where an object is on screen.</summary>
[DBusInterface("org.a11y.atspi.Component")]
internal interface IAtSpiComponent : IDBusObject
{
    Task<bool> ContainsAsync(int x, int y, uint coordType);
    Task<ValueTuple<(string, ObjectPath)>> GetAccessibleAtPointAsync(int x, int y, uint coordType);
    Task<ValueTuple<(int, int, int, int)>> GetExtentsAsync(uint coordType);
    Task<(int, int)> GetPositionAsync(uint coordType);
    Task<(int, int)> GetSizeAsync();
    Task<uint> GetLayerAsync();
    Task<short> GetMDIZOrderAsync();
    Task<bool> GrabFocusAsync();
    Task<double> GetAlphaAsync();
}

/// <summary>org.a11y.atspi.Action: what can be done to an object.</summary>
[DBusInterface("org.a11y.atspi.Action")]
internal interface IAtSpiAction : IDBusObject
{
    Task<string> GetDescriptionAsync(int index);
    Task<string> GetNameAsync(int index);
    Task<string> GetLocalizedNameAsync(int index);
    Task<string> GetKeyBindingAsync(int index);
    Task<(string, string, string)[]> GetActionsAsync();
    Task<bool> DoActionAsync(int index);
    Task<object> GetAsync(string prop);
    Task<AtSpiActionProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
}

[Dictionary]
internal sealed class AtSpiActionProperties
{
    public int NActions;
}

/// <summary>org.a11y.atspi.Value: a range control's value.</summary>
[DBusInterface("org.a11y.atspi.Value")]
internal interface IAtSpiValue : IDBusObject
{
    Task<object> GetAsync(string prop);
    Task<AtSpiValueProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
}

[Dictionary]
internal sealed class AtSpiValueProperties
{
    public double MinimumValue;
    public double MaximumValue;
    public double MinimumIncrement;
    public double CurrentValue;
    public string Text = string.Empty;
}

/// <summary>org.a11y.atspi.Text: an object's text, caret and selection.</summary>
[DBusInterface("org.a11y.atspi.Text")]
internal interface IAtSpiText : IDBusObject
{
    Task<string> GetTextAsync(int startOffset, int endOffset);
    Task<bool> SetCaretOffsetAsync(int offset);
    Task<(string, int, int)> GetTextBeforeOffsetAsync(int offset, uint type);
    Task<(string, int, int)> GetTextAtOffsetAsync(int offset, uint type);
    Task<(string, int, int)> GetTextAfterOffsetAsync(int offset, uint type);
    Task<(string, int, int)> GetStringAtOffsetAsync(int offset, uint granularity);
    Task<int> GetCharacterAtOffsetAsync(int offset);
    Task<string> GetAttributeValueAsync(int offset, string attributeName);
    Task<(IDictionary<string, string>, int, int)> GetAttributesAsync(int offset);
    Task<IDictionary<string, string>> GetDefaultAttributesAsync();
    Task<(int, int, int, int)> GetCharacterExtentsAsync(int offset, uint coordType);
    Task<int> GetOffsetAtPointAsync(int x, int y, uint coordType);
    Task<int> GetNSelectionsAsync();
    Task<(int, int)> GetSelectionAsync(int selectionNum);
    Task<bool> AddSelectionAsync(int startOffset, int endOffset);
    Task<bool> RemoveSelectionAsync(int selectionNum);
    Task<bool> SetSelectionAsync(int selectionNum, int startOffset, int endOffset);
    Task<(int, int, int, int)> GetRangeExtentsAsync(int startOffset, int endOffset, uint coordType);
    Task<(IDictionary<string, string>, int, int)> GetAttributeRunAsync(int offset, bool includeDefaults);
    Task<IDictionary<string, string>> GetDefaultAttributeSetAsync();
    Task<object> GetAsync(string prop);
    Task<AtSpiTextProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
}

[Dictionary]
internal sealed class AtSpiTextProperties
{
    public int CharacterCount;
    public int CaretOffset;
}

/// <summary>org.a11y.atspi.Event.Object: state, property, children and text changes.</summary>
[DBusInterface("org.a11y.atspi.Event.Object")]
internal interface IAtSpiObjectEvents : IDBusObject
{
    Task<IDisposable> WatchStateChangedAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchPropertyChangeAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchChildrenChangedAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchTextChangedAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchTextCaretMovedAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchTextSelectionChangedAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchAnnouncementAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
}

/// <summary>org.a11y.atspi.Event.Focus: the legacy focus event.</summary>
[DBusInterface("org.a11y.atspi.Event.Focus")]
internal interface IAtSpiFocusEvents : IDBusObject
{
    Task<IDisposable> WatchFocusAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
}

/// <summary>org.a11y.atspi.Event.Window: a window activated, deactivated, created or destroyed.</summary>
[DBusInterface("org.a11y.atspi.Event.Window")]
internal interface IAtSpiWindowEvents : IDBusObject
{
    Task<IDisposable> WatchActivateAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchDeactivateAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchCreateAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchDestroyAsync(Action<(string, int, int, object, IDictionary<string, object>)> handler, Action<Exception>? onError = null);
}

/// <summary>org.a11y.atspi.Cache, exported at /org/a11y/atspi/cache (empty: clients ask the objects).</summary>
[DBusInterface("org.a11y.atspi.Cache")]
internal interface IAtSpiCache : IDBusObject
{
    Task<((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, uint[])[]> GetItemsAsync();
}

/// <summary>The registry's org.a11y.atspi.Socket, which embeds an application in the desktop.</summary>
[DBusInterface("org.a11y.atspi.Socket")]
internal interface IAtSpiSocket : IDBusObject
{
    Task<ValueTuple<(string, ObjectPath)>> EmbedAsync((string, ObjectPath) plug);
    Task UnembedAsync((string, ObjectPath) plug);
}

/// <summary>The session bus's org.a11y.Bus, which gives the accessibility bus's address.</summary>
[DBusInterface("org.a11y.Bus")]
internal interface IA11yBus : IDBusObject
{
    Task<string> GetAddressAsync();
}

/// <summary>org.a11y.Status on the session bus: whether assistive technology is running.</summary>
[DBusInterface("org.a11y.Status")]
internal interface IA11yStatus : IDBusObject
{
    Task<object> GetAsync(string prop);
    Task<A11yStatusProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
internal sealed class A11yStatusProperties
{
    public bool IsEnabled;
    public bool ScreenReaderEnabled;
}
