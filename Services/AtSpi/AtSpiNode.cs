// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.AtSpi;

using Ref = ValueTuple<string, ObjectPath>;
using EventArgs = ValueTuple<string, int, int, object, IDictionary<string, object>>;

/// <summary>
/// One exported accessible: a view (or a window frame) as AT-SPI sees it. It holds the view
/// weakly; once the view is gone the object answers as defunct until the bridge drops it.
/// Every query reads the view's <see cref="IAccessible"/> model on the UI thread.
/// </summary>
internal sealed class AtSpiNode : IAtSpiAccessible, IAtSpiComponent, IAtSpiAction, IAtSpiValue, IAtSpiText,
    IAtSpiObjectEvents, IAtSpiFocusEvents, IAtSpiWindowEvents
{
    private readonly AtSpiBridge _bridge;
    private readonly WeakReference<IAccessible> _target;

    public AtSpiNode(AtSpiBridge bridge, IAccessible target, ObjectPath path)
    {
        _bridge = bridge;
        _target = new WeakReference<IAccessible>(target);
        ObjectPath = path;
    }

    public ObjectPath ObjectPath { get; }

    internal bool IsAlive => _target.TryGetTarget(out _);

    /// <summary>Reads the view on the UI thread, then registers any objects the answer refers to.</summary>
    private async Task<T> Read<T>(Func<IAccessible, T> read, T defunct)
    {
        if (!_target.TryGetTarget(out var target))
            return defunct;
        var result = await AtSpiBridge.OnUi(() => read(target)).ConfigureAwait(false);
        await _bridge.FlushAsync().ConfigureAwait(false);
        return result;
    }

    // ---- org.a11y.atspi.Accessible -------------------------------------------------------

    private static IReadOnlyList<IAccessible> ChildrenOf(IAccessible accessible) => accessible.Children;

    public Task<ValueTuple<Ref>> GetChildAtIndexAsync(int index) => Read(a =>
    {
        var children = ChildrenOf(a);
        return new ValueTuple<Ref>(index >= 0 && index < children.Count ? _bridge.Ref(children[index]) : _bridge.NullReference);
    }, new ValueTuple<Ref>(_bridge.NullReference));

    public Task<Ref[]> GetChildrenAsync() => Read(a => ChildrenOf(a).Select(_bridge.Ref).ToArray(), Array.Empty<Ref>());

    public Task<int> GetIndexInParentAsync() => Read(a =>
    {
        if (a is WindowAccessible)
            return _bridge.Windows().ToList().IndexOf(a);
        var siblings = a.Parent?.Children ?? (IReadOnlyList<IAccessible>?)(_bridge.WindowOfRoot(a) is { } window ? window.Children : null);
        if (siblings == null)
            return -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (ReferenceEquals(siblings[i], a))
                return i;
        }
        return -1;
    }, -1);

    public Task<(uint, Ref[])[]> GetRelationSetAsync() => Read(a =>
        a is IAccessibleRelations related
            ? related.Relations.Select(r => ((uint)r.Relation, r.Targets.Select(_bridge.Ref).ToArray())).ToArray()
            : Array.Empty<(uint, Ref[])>(),
        Array.Empty<(uint, Ref[])>());

    public Task<uint> GetRoleAsync() => Read(a => AtSpiBridge.AtSpiRole(a.Role, a), 0u);

    public Task<string> GetRoleNameAsync() => Read(a => AtSpiBridge.RoleName(AtSpiBridge.AtSpiRole(a.Role, a)), "invalid");

    public Task<string> GetLocalizedRoleNameAsync() => GetRoleNameAsync();

    public Task<uint[]> GetStateAsync() => Read(a => AtSpiBridge.StateSet(a.States, AtSpiBridge.IsShowing(a)), [1u << 6, 0u]);

    public Task<IDictionary<string, string>> GetAttributesAsync() => Read<IDictionary<string, string>>(a =>
    {
        var attributes = new Dictionary<string, string> { ["toolkit"] = "OpenMaui", ["class"] = a.GetType().Name };
        if (a is SkiaView { SemanticHeadingLevel: var level } && level != Microsoft.Maui.SemanticHeadingLevel.None)
            attributes["level"] = ((int)level).ToString(CultureInfo.InvariantCulture);
        if (a is SkiaView { MauiView.AutomationId: { Length: > 0 } automationId })
            attributes["id"] = automationId;
        return attributes;
    }, new Dictionary<string, string>());

    public Task<ValueTuple<Ref>> GetApplicationAsync() => Task.FromResult(new ValueTuple<Ref>(_bridge.RootReference));

    public Task<string[]> GetInterfacesAsync() => Read(a =>
    {
        var interfaces = new List<string> { "org.a11y.atspi.Accessible", "org.a11y.atspi.Component" };
        if (a.Actions.Count > 0)
            interfaces.Add("org.a11y.atspi.Action");
        if (a.Value != null)
            interfaces.Add("org.a11y.atspi.Value");
        if (a is IAccessibleText)
            interfaces.Add("org.a11y.atspi.Text");
        return interfaces.ToArray();
    }, ["org.a11y.atspi.Accessible"]);

    private AtSpiAccessibleProperties AccessibleProperties(IAccessible a) => new()
    {
        Name = a.AccessibleName ?? string.Empty,
        Description = a.AccessibleDescription ?? string.Empty,
        Parent = _bridge.ParentOf(a),
        ChildCount = ChildrenOf(a).Count,
        Locale = AtSpiBridge.Locale,
        AccessibleId = a is SkiaView { MauiView.AutomationId: { Length: > 0 } id } ? id : string.Empty,
        HelpText = a is SkiaView { SemanticHint: { Length: > 0 } hint } ? hint : string.Empty,
    };

    async Task<object> IAtSpiAccessible.GetAsync(string prop)
    {
        var all = await ((IAtSpiAccessible)this).GetAllAsync().ConfigureAwait(false);
        return prop switch
        {
            "Name" => all.Name,
            "Description" => all.Description,
            "Parent" => all.Parent,
            "ChildCount" => all.ChildCount,
            "Locale" => all.Locale,
            "AccessibleId" => all.AccessibleId,
            "HelpText" => all.HelpText,
            _ => throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop),
        };
    }

    Task<AtSpiAccessibleProperties> IAtSpiAccessible.GetAllAsync() =>
        Read(AccessibleProperties, new AtSpiAccessibleProperties { Parent = _bridge.NullReference });

    Task IAtSpiAccessible.SetAsync(string prop, object val) => Task.CompletedTask;

    // ---- org.a11y.atspi.Component --------------------------------------------------------

    /// <summary>
    /// An object's extents in the coordinates asked for (AtspiCoordType: 0 screen, 1 window,
    /// 2 parent; see <see cref="AtSpiBridge.CoordinateMap"/>). A frame's bounds are the window's own.
    /// </summary>
    private static (int X, int Y, int W, int H) Extents(IAccessible a, uint coordType)
    {
        var map = a is WindowAccessible window ? AtSpiBridge.FrameMap(window, coordType) : AtSpiBridge.CoordinateMap(a, coordType);
        return Map(a.Bounds, map);
    }

    private static (int X, int Y, int W, int H) Map(AccessibleRect r, (double X, double Y, double Scale) map) =>
        ((int)Math.Round(r.X * map.Scale + map.X), (int)Math.Round(r.Y * map.Scale + map.Y),
         (int)Math.Round(r.Width * map.Scale), (int)Math.Round(r.Height * map.Scale));

    /// <summary>A point in the coordinates asked for, in view-tree coordinates.</summary>
    private static (int X, int Y) ToTree(IAccessible a, int x, int y, uint coordType)
    {
        var map = AtSpiBridge.CoordinateMap(a, coordType);
        return ((int)Math.Floor((x - map.X) / map.Scale), (int)Math.Floor((y - map.Y) / map.Scale));
    }

    /// <summary>A view-tree rectangle (a character's) in the coordinates asked for.</summary>
    private static (int, int, int, int) FromTree(IAccessible a, AccessibleRect r, uint coordType) =>
        Map(r, AtSpiBridge.CoordinateMap(a, coordType));

    public Task<bool> ContainsAsync(int x, int y, uint coordType) => Read(a =>
    {
        var (ex, ey, w, h) = Extents(a, coordType);
        return x >= ex && y >= ey && x < ex + w && y < ey + h;
    }, false);

    public Task<ValueTuple<Ref>> GetAccessibleAtPointAsync(int x, int y, uint coordType) => Read(a =>
    {
        // The deepest showing child under the point, compared in view-tree coordinates.
        (x, y) = ToTree(a, x, y, coordType);
        IAccessible? hit = null;
        var current = a;
        while (true)
        {
            IAccessible? next = null;
            var children = ChildrenOf(current);
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                var cb = child.Bounds;
                if (x >= cb.X && y >= cb.Y && x < cb.X + cb.Width && y < cb.Y + cb.Height && child.States.HasFlag(AccessibleStates.Visible))
                {
                    next = child;
                    break;
                }
            }
            if (next == null)
                break;
            hit = next;
            current = next;
        }
        return new ValueTuple<Ref>(_bridge.Ref(hit));
    }, new ValueTuple<Ref>(_bridge.NullReference));

    public Task<ValueTuple<(int, int, int, int)>> GetExtentsAsync(uint coordType) =>
        Read(a => new ValueTuple<(int, int, int, int)>(Extents(a, coordType)), new ValueTuple<(int, int, int, int)>((0, 0, 0, 0)));

    public Task<(int, int)> GetPositionAsync(uint coordType) => Read(a => Extents(a, coordType) is var (x, y, _, _) ? (x, y) : default, (0, 0));

    public Task<(int, int)> GetSizeAsync() => Read(a => Extents(a, 1) is var (_, _, w, h) ? (w, h) : default, (0, 0));

    public Task<uint> GetLayerAsync() => Read(a => a is WindowAccessible ? 7u : 3u, 0u); // ATSPI_LAYER_WINDOW / ATSPI_LAYER_WIDGET

    public Task<short> GetMDIZOrderAsync() => Task.FromResult((short)-1);

    public Task<bool> GrabFocusAsync() => Read(a =>
    {
        if (a is not SkiaView { IsFocusable: true } view)
            return false;
        var context = LinuxApplication.Current?.WindowContexts.FirstOrDefault(c => IsInTree(c.RootView, view));
        if (context == null)
            return false;
        context.FocusedView = view;
        return true;
    }, false);

    private static bool IsInTree(SkiaView? root, SkiaView view)
    {
        for (var current = view; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, root))
                return true;
        }
        return false;
    }

    public Task<double> GetAlphaAsync() => Read(a => a is SkiaView view ? (double)view.Opacity : 1.0, 1.0);

    // ---- org.a11y.atspi.Action -----------------------------------------------------------

    private static AccessibleAction? ActionAt(IAccessible a, int index) =>
        index >= 0 && index < a.Actions.Count ? a.Actions[index] : null;

    public Task<string> GetDescriptionAsync(int index) => Read(a => ActionAt(a, index)?.Description ?? string.Empty, string.Empty);

    Task<string> IAtSpiAction.GetNameAsync(int index) => Read(a => ActionAt(a, index)?.Name ?? string.Empty, string.Empty);

    public Task<string> GetLocalizedNameAsync(int index) => Read(a => ActionAt(a, index)?.Name ?? string.Empty, string.Empty);

    public Task<string> GetKeyBindingAsync(int index) => Read(a => ActionAt(a, index)?.KeyBinding ?? string.Empty, string.Empty);

    public Task<(string, string, string)[]> GetActionsAsync() =>
        Read(a => a.Actions.Select(x => (x.Name, x.Description, x.KeyBinding)).ToArray(), Array.Empty<(string, string, string)>());

    public Task<bool> DoActionAsync(int index) => Read(a => ActionAt(a, index) is { } action && a.DoAction(action.Name), false);

    async Task<object> IAtSpiAction.GetAsync(string prop) =>
        prop == "NActions" ? (await ((IAtSpiAction)this).GetAllAsync().ConfigureAwait(false)).NActions
            : throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop);

    Task<AtSpiActionProperties> IAtSpiAction.GetAllAsync() => Read(a => new AtSpiActionProperties { NActions = a.Actions.Count }, new AtSpiActionProperties());

    Task IAtSpiAction.SetAsync(string prop, object val) => Task.CompletedTask;

    // ---- org.a11y.atspi.Value ------------------------------------------------------------

    async Task<object> IAtSpiValue.GetAsync(string prop)
    {
        var all = await ((IAtSpiValue)this).GetAllAsync().ConfigureAwait(false);
        return prop switch
        {
            "MinimumValue" => all.MinimumValue,
            "MaximumValue" => all.MaximumValue,
            "MinimumIncrement" => all.MinimumIncrement,
            "CurrentValue" => all.CurrentValue,
            "Text" => all.Text,
            _ => throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop),
        };
    }

    Task<AtSpiValueProperties> IAtSpiValue.GetAllAsync() => Read(a => new AtSpiValueProperties
    {
        MinimumValue = a.MinValue ?? 0,
        MaximumValue = a.MaxValue ?? 0,
        MinimumIncrement = 0,
        CurrentValue = a.Value ?? 0,
    }, new AtSpiValueProperties());

    async Task IAtSpiValue.SetAsync(string prop, object val)
    {
        if (prop != "CurrentValue" || val is not double value)
            return;
        await Read(a => a.SetValue(value), false).ConfigureAwait(false);
    }

    // ---- org.a11y.atspi.Text -------------------------------------------------------------

    private Task<T> ReadText<T>(Func<IAccessibleText, T> read, T otherwise) =>
        Read(a => a is IAccessibleText text ? read(text) : otherwise, otherwise);

    public Task<string> GetTextAsync(int startOffset, int endOffset) => ReadText(t =>
    {
        var s = t.Text ?? string.Empty;
        int start = Math.Clamp(startOffset, 0, s.Length);
        int end = endOffset < 0 ? s.Length : Math.Clamp(endOffset, start, s.Length);
        return s[start..end];
    }, string.Empty);

    public Task<bool> SetCaretOffsetAsync(int offset) => ReadText(t => t.SetSelection(0, offset, offset), false);

    public Task<(string, int, int)> GetTextBeforeOffsetAsync(int offset, uint type) =>
        ReadText(t => TextBoundaries.Before(t.Text ?? string.Empty, offset, type), (string.Empty, 0, 0));

    public Task<(string, int, int)> GetTextAtOffsetAsync(int offset, uint type) =>
        ReadText(t => TextBoundaries.At(t.Text ?? string.Empty, offset, type), (string.Empty, 0, 0));

    public Task<(string, int, int)> GetTextAfterOffsetAsync(int offset, uint type) =>
        ReadText(t => TextBoundaries.After(t.Text ?? string.Empty, offset, type), (string.Empty, 0, 0));

    public Task<(string, int, int)> GetStringAtOffsetAsync(int offset, uint granularity) =>
        ReadText(t => TextBoundaries.AtGranularity(t.Text ?? string.Empty, offset, granularity), (string.Empty, 0, 0));

    public Task<int> GetCharacterAtOffsetAsync(int offset) => ReadText(t =>
    {
        var s = t.Text ?? string.Empty;
        return offset >= 0 && offset < s.Length ? (int)s[offset] : 0;
    }, 0);

    public Task<string> GetAttributeValueAsync(int offset, string attributeName) => Task.FromResult(string.Empty);

    Task<(IDictionary<string, string>, int, int)> IAtSpiText.GetAttributesAsync(int offset) =>
        ReadText(t => ((IDictionary<string, string>)new Dictionary<string, string>(), 0, (t.Text ?? string.Empty).Length),
            ((IDictionary<string, string>)new Dictionary<string, string>(), 0, 0));

    public Task<IDictionary<string, string>> GetDefaultAttributesAsync() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

    public Task<(int, int, int, int)> GetCharacterExtentsAsync(int offset, uint coordType) =>
        ReadText(t => FromTree(t, t.GetCharacterBounds(offset), coordType), (0, 0, 0, 0));

    public Task<int> GetOffsetAtPointAsync(int x, int y, uint coordType) => ReadText(t =>
    {
        (x, y) = ToTree(t, x, y, coordType);
        var s = t.Text ?? string.Empty;
        for (int i = 0; i < s.Length; i++)
        {
            var r = t.GetCharacterBounds(i);
            if (x >= r.X && x < r.X + Math.Max(1, r.Width) && y >= r.Y && y < r.Y + Math.Max(1, r.Height))
                return i;
        }
        return -1;
    }, -1);

    public Task<int> GetNSelectionsAsync() => ReadText(t => t.SelectionCount, 0);

    public Task<(int, int)> GetSelectionAsync(int selectionNum) => ReadText(t => t.GetSelection(selectionNum), (0, 0));

    public Task<bool> AddSelectionAsync(int startOffset, int endOffset) => ReadText(t => t.SetSelection(0, startOffset, endOffset), false);

    public Task<bool> RemoveSelectionAsync(int selectionNum) => ReadText(t => t.SetSelection(selectionNum, t.CaretOffset, t.CaretOffset), false);

    public Task<bool> SetSelectionAsync(int selectionNum, int startOffset, int endOffset) => ReadText(t => t.SetSelection(selectionNum, startOffset, endOffset), false);

    public Task<(int, int, int, int)> GetRangeExtentsAsync(int startOffset, int endOffset, uint coordType) => ReadText(t =>
    {
        var s = t.Text ?? string.Empty;
        int start = Math.Clamp(startOffset, 0, s.Length);
        int end = Math.Clamp(endOffset < 0 ? s.Length : endOffset, start, s.Length);
        if (end <= start)
            return (0, 0, 0, 0);
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (int i = start; i < end; i++)
        {
            var r = t.GetCharacterBounds(i);
            left = Math.Min(left, r.X);
            top = Math.Min(top, r.Y);
            right = Math.Max(right, r.X + r.Width);
            bottom = Math.Max(bottom, r.Y + r.Height);
        }
        return FromTree(t, new AccessibleRect(left, top, right - left, bottom - top), coordType);
    }, (0, 0, 0, 0));

    public Task<(IDictionary<string, string>, int, int)> GetAttributeRunAsync(int offset, bool includeDefaults) =>
        ((IAtSpiText)this).GetAttributesAsync(offset);

    public Task<IDictionary<string, string>> GetDefaultAttributeSetAsync() => GetDefaultAttributesAsync();

    async Task<object> IAtSpiText.GetAsync(string prop)
    {
        var all = await ((IAtSpiText)this).GetAllAsync().ConfigureAwait(false);
        return prop switch
        {
            "CharacterCount" => all.CharacterCount,
            "CaretOffset" => all.CaretOffset,
            _ => throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop),
        };
    }

    Task<AtSpiTextProperties> IAtSpiText.GetAllAsync() =>
        ReadText(t => new AtSpiTextProperties { CharacterCount = (t.Text ?? string.Empty).Length, CaretOffset = t.CaretOffset }, new AtSpiTextProperties());

    Task IAtSpiText.SetAsync(string prop, object val) => Task.CompletedTask;

    // ---- Events ---------------------------------------------------------------------------

    public event Action<EventArgs>? StateChanged;
    public event Action<EventArgs>? PropertyChange;
    public event Action<EventArgs>? ChildrenChanged;
    public event Action<EventArgs>? TextChanged;
    public event Action<EventArgs>? TextCaretMoved;
    public event Action<EventArgs>? TextSelectionChanged;
    public event Action<EventArgs>? Announcement;
    public event Action<EventArgs>? Focus;
    public event Action<EventArgs>? Activate;
    public event Action<EventArgs>? Deactivate;
    public event Action<EventArgs>? Create;
    public event Action<EventArgs>? Destroy;

    private static readonly IDictionary<string, object> s_none = new Dictionary<string, object>();

    internal void RaiseStateChanged(string state, bool value) => StateChanged?.Invoke((state, value ? 1 : 0, 0, 0, s_none));

    internal void RaisePropertyChange(string property, object value) => PropertyChange?.Invoke((property, 0, 0, value, s_none));

    internal void RaiseChildrenChanged(string change, int index, Ref child) => ChildrenChanged?.Invoke((change, index, 0, child, s_none));

    internal void RaiseTextChanged(string change, int start, string text) => TextChanged?.Invoke((change, start, text.Length, text, s_none));

    internal void RaiseCaretMoved(int offset) => TextCaretMoved?.Invoke((string.Empty, offset, 0, 0, s_none));

    internal void RaiseAnnouncement(string text, int politeness) => Announcement?.Invoke((string.Empty, politeness, 0, text, s_none));

    internal void RaiseFocus() => Focus?.Invoke((string.Empty, 0, 0, 0, s_none));

    internal void RaiseActivate(string title) => Activate?.Invoke((string.Empty, 0, 0, title, s_none));

    internal void RaiseDeactivate(string title) => Deactivate?.Invoke((string.Empty, 0, 0, title, s_none));

    internal void RaiseCreate(string title) => Create?.Invoke((string.Empty, 0, 0, title, s_none));

    internal void RaiseDestroy(string title) => Destroy?.Invoke((string.Empty, 0, 0, title, s_none));

    public Task<IDisposable> WatchStateChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(StateChanged), handler);
    public Task<IDisposable> WatchPropertyChangeAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(PropertyChange), handler);
    public Task<IDisposable> WatchChildrenChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(ChildrenChanged), handler);
    public Task<IDisposable> WatchTextChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextChanged), handler);
    public Task<IDisposable> WatchTextCaretMovedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextCaretMoved), handler);
    public Task<IDisposable> WatchTextSelectionChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextSelectionChanged), handler);
    public Task<IDisposable> WatchAnnouncementAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Announcement), handler);
    public Task<IDisposable> WatchFocusAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Focus), handler);
    public Task<IDisposable> WatchActivateAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Activate), handler);
    public Task<IDisposable> WatchDeactivateAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Deactivate), handler);
    public Task<IDisposable> WatchCreateAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Create), handler);
    public Task<IDisposable> WatchDestroyAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Destroy), handler);
}

/// <summary>The application root: role application, its windows as children, the desktop as parent.</summary>
internal sealed class AtSpiRoot : IAtSpiAccessible, IAtSpiApplication, IAtSpiObjectEvents
{
    private readonly AtSpiBridge _bridge;
    private readonly string _name;
    private int _id;

    public AtSpiRoot(AtSpiBridge bridge, string name)
    {
        _bridge = bridge;
        _name = name;
    }

    public ObjectPath ObjectPath => new(AtSpiBridge.RootPath);

    private async Task<T> Read<T>(Func<T> read)
    {
        var result = await AtSpiBridge.OnUi(read).ConfigureAwait(false);
        await _bridge.FlushAsync().ConfigureAwait(false);
        return result;
    }

    public Task<ValueTuple<Ref>> GetChildAtIndexAsync(int index) => Read(() =>
    {
        var windows = _bridge.Windows();
        return new ValueTuple<Ref>(index >= 0 && index < windows.Count ? _bridge.Ref(windows[index]) : _bridge.NullReference);
    });

    public Task<Ref[]> GetChildrenAsync() => Read(() => _bridge.Windows().Select(_bridge.Ref).ToArray());
    public Task<int> GetIndexInParentAsync() => Task.FromResult(-1);
    public Task<(uint, Ref[])[]> GetRelationSetAsync() => Task.FromResult(Array.Empty<(uint, Ref[])>());
    public Task<uint> GetRoleAsync() => Task.FromResult(75u);
    public Task<string> GetRoleNameAsync() => Task.FromResult("application");
    public Task<string> GetLocalizedRoleNameAsync() => GetRoleNameAsync();
    public Task<uint[]> GetStateAsync() => Task.FromResult(new uint[] { 0, 0 });
    public Task<IDictionary<string, string>> GetAttributesAsync() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string> { ["toolkit"] = "OpenMaui" });
    public Task<ValueTuple<Ref>> GetApplicationAsync() => Task.FromResult(new ValueTuple<Ref>(_bridge.RootReference));
    public Task<string[]> GetInterfacesAsync() => Task.FromResult(new[] { "org.a11y.atspi.Accessible", "org.a11y.atspi.Application" });

    private Task<AtSpiAccessibleProperties> Properties() => Read(() => new AtSpiAccessibleProperties
    {
        Name = _name,
        Parent = _bridge.Desktop,
        ChildCount = _bridge.Windows().Count,
        Locale = AtSpiBridge.Locale,
    });

    async Task<object> IAtSpiAccessible.GetAsync(string prop)
    {
        var all = await Properties().ConfigureAwait(false);
        return prop switch
        {
            "Name" => all.Name,
            "Description" => all.Description,
            "Parent" => all.Parent,
            "ChildCount" => all.ChildCount,
            "Locale" => all.Locale,
            "AccessibleId" => all.AccessibleId,
            "HelpText" => all.HelpText,
            _ => throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop),
        };
    }

    Task<AtSpiAccessibleProperties> IAtSpiAccessible.GetAllAsync() => Properties();
    Task IAtSpiAccessible.SetAsync(string prop, object val) => Task.CompletedTask;

    public Task<string> GetLocaleAsync(uint lctype) => Task.FromResult(AtSpiBridge.Locale);

    private AtSpiApplicationProperties ApplicationProperties() => new()
    {
        Version = typeof(AtSpiRoot).Assembly.GetName().Version?.ToString() ?? string.Empty,
        Id = _id,
    };

    Task<object> IAtSpiApplication.GetAsync(string prop) => Task.FromResult<object>(prop switch
    {
        "ToolkitName" => ApplicationProperties().ToolkitName,
        "Version" => ApplicationProperties().Version,
        "AtspiVersion" => ApplicationProperties().AtspiVersion,
        "Id" => _id,
        _ => throw new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop),
    });

    Task<AtSpiApplicationProperties> IAtSpiApplication.GetAllAsync() => Task.FromResult(ApplicationProperties());

    Task IAtSpiApplication.SetAsync(string prop, object val)
    {
        // The registry gives the application its id.
        if (prop == "Id" && val is int id)
            _id = id;
        return Task.CompletedTask;
    }

    public event Action<EventArgs>? StateChanged;
    public event Action<EventArgs>? PropertyChange;
    public event Action<EventArgs>? ChildrenChanged;
    public event Action<EventArgs>? TextChanged;
    public event Action<EventArgs>? TextCaretMoved;
    public event Action<EventArgs>? TextSelectionChanged;
    public event Action<EventArgs>? Announcement;

    private static readonly IDictionary<string, object> s_none = new Dictionary<string, object>();

    internal void RaiseChildrenChanged(string change, int index, Ref child) => ChildrenChanged?.Invoke((change, index, 0, child, s_none));

    public Task<IDisposable> WatchStateChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(StateChanged), handler);
    public Task<IDisposable> WatchPropertyChangeAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(PropertyChange), handler);
    public Task<IDisposable> WatchChildrenChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(ChildrenChanged), handler);
    public Task<IDisposable> WatchTextChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextChanged), handler);
    public Task<IDisposable> WatchTextCaretMovedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextCaretMoved), handler);
    public Task<IDisposable> WatchTextSelectionChangedAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(TextSelectionChanged), handler);
    public Task<IDisposable> WatchAnnouncementAsync(Action<EventArgs> handler, Action<Exception>? onError = null) => SignalWatcher.AddAsync(this, nameof(Announcement), handler);
}

/// <summary>The (empty) cache: clients ask the objects themselves.</summary>
internal sealed class AtSpiCache : IAtSpiCache
{
    public ObjectPath ObjectPath => new("/org/a11y/atspi/cache");

    public Task<((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, uint[])[]> GetItemsAsync() =>
        Task.FromResult(Array.Empty<((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, uint[])>());
}

/// <summary>
/// A window as an accessible frame: named by its title, active while it has the keyboard, its
/// root view as its only child.
/// </summary>
internal sealed class WindowAccessible : IAccessible
{
    private readonly WeakReference<WindowContext> _context;
    private readonly string _id = Guid.NewGuid().ToString();

    public WindowAccessible(WindowContext context) => _context = new WeakReference<WindowContext>(context);

    internal bool IsActive { get; set; }

    internal WindowContext? Context => _context.TryGetTarget(out var c) ? c : null;

    internal bool IsShown => _context.TryGetTarget(out var c) && (c.DisplayWindow != null || c.GtkWindow != null);

    public string AccessibleId => _id;

    public string AccessibleName => _context.TryGetTarget(out var c) ? TitleOf(c) : string.Empty;

    private static string TitleOf(WindowContext context)
    {
        if (context.MauiWindow is Microsoft.Maui.Controls.Window { Title: { Length: > 0 } title })
            return title;
        if (context.MauiWindow is Microsoft.Maui.Controls.Window { Page: Microsoft.Maui.Controls.Page { Title: { Length: > 0 } pageTitle } })
            return pageTitle;
        return context.MauiWindow?.Title ?? context.GtkWindow?.Title ?? string.Empty;
    }

    public string AccessibleDescription => string.Empty;

    public AccessibleRole Role => AccessibleRole.Frame;

    public AccessibleStates States
    {
        get
        {
            var states = AccessibleStates.Enabled | AccessibleStates.Resizable;
            if (IsShown)
                states |= AccessibleStates.Visible;
            if (IsActive)
                states |= AccessibleStates.Active;
            return states;
        }
    }

    public IAccessible? Parent => null;

    public IReadOnlyList<IAccessible> Children =>
        _context.TryGetTarget(out var c) && c.RootView is IAccessible root ? [root] : Array.Empty<IAccessible>();

    /// <summary>The window's size in view-tree (logical) pixels: a native window's surface over its scale, a GTK window's own (GTK sizes in logical pixels).</summary>
    public AccessibleRect Bounds
    {
        get
        {
            if (!_context.TryGetTarget(out var c))
                return default;
            if (c.DisplayWindow is { } window)
                return new AccessibleRect(0, 0, (int)(window.Width / Math.Max(1f, c.Scale)), (int)(window.Height / Math.Max(1f, c.Scale)));
            if (c.GtkWindow is { } gtk)
                return new AccessibleRect(0, 0, gtk.Width, gtk.Height);
            return default;
        }
    }

    public IReadOnlyList<AccessibleAction> Actions => Array.Empty<AccessibleAction>();

    public double? Value => null;
    public double? MinValue => null;
    public double? MaxValue => null;

    public bool DoAction(string actionName) => false;

    public bool SetValue(double value) => false;
}
