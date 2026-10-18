// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The Shell's tabs as MAUI's Windows implementation shows them (ShellItemHandler.Windows.cs): the
/// current ShellItem is a navigation view whose pane is at the top (WinUI's
/// NavigationViewPaneDisplayMode.Top) when the item shows tabs or a SearchHandler is present, and
/// has no tab row otherwise (LeftMinimal). The pane lists the item's sections, a section MAUI made
/// around a bare ShellContent by that content; each tab shows its icon and title, the selected one
/// marked by the selection indicator. A section with more than one ShellContent lists its contents
/// as the tab's menu items: its tab has a chevron and opens a drop-down of them. Tabs that do not
/// fit move to the "More" overflow drop-down, as WinUI's top pane does. The pane stays while pages
/// are pushed on the section, as on Windows (the section's frame is the navigation view's content).
/// </summary>
public partial class SkiaShell
{
    private ShellTopNavigationView? _topNavigation;
    private bool _topPaneWasShown;
    private readonly HashSet<BaseShellItem> _paneTrackedItems = new();

    /// <summary>The top navigation pane (a view of its own so it takes keyboard focus and is read by assistive technology).</summary>
    internal ShellTopNavigationView TopNavigation => _topNavigation ??= new ShellTopNavigationView(this);

    /// <summary>
    /// Closes an open tab drop-down and the search box's suggestion list (the Shell's handler
    /// disconnects, so its window may be closing: their popup overlays are unregistered).
    /// </summary>
    internal void CloseTopNavigationDropDown()
    {
        _topNavigation?.CloseDropDown();
        _searchBox?.CloseList();
    }

    /// <summary>
    /// The pane is shown (WinUI's Top pane display mode): with a MAUI Shell, as Windows'
    /// ShellItemHandler.GetNavigationViewPaneDisplayMode decides it (ShowTabs, or a SearchHandler
    /// is present); without one, while <see cref="TabBarIsVisible"/> is set (the section's contents).
    /// </summary>
    private bool IsTopPaneShown
    {
        get
        {
            if (_mauiShell != null)
            {
                if (_mauiShell.CurrentItem is not { } item)
                    return false;
                try
                {
                    return WindowsShowTabs(item) || HasSearchHandler(item);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("SkiaShell", "Reading whether the ShellItem shows tabs failed", ex);
                    return false;
                }
            }
            return TabBarIsVisible && _selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count;
        }
    }

    private float TopPaneHeight => IsTopPaneShown ? TabBarHeight : 0f;

    /// <summary>
    /// IShellItemController.ShowTabs as MAUI computes it on Windows (ShellItem.cs, the WINDOWS
    /// branch): tabs are shown by default when the item has more than one section or its current
    /// section more than one content, and Shell.TabBarIsVisible (of the current content when set
    /// there, else the presented page or its ancestors) overrides that. MAUI's own ShowTabs is
    /// compiled for the other platforms here (sections only), so the Windows rule is applied here.
    /// </summary>
    private bool WindowsShowTabs(Microsoft.Maui.Controls.ShellItem item)
    {
        var controller = (IShellItemController)item;
        bool defaultShowTabs = controller.GetItems().Count > 1
            || ((item.CurrentItem as IShellSectionController)?.GetItems().Count ?? 0) > 1;

        var content = item.CurrentItem?.CurrentItem;
        Element? element = DisplayedMauiPage(item.CurrentItem);
        if (content != null && content.IsSet(Shell.TabBarIsVisibleProperty))
            element = content;
        return EffectiveShellValue(element ?? content, Shell.TabBarIsVisibleProperty, defaultShowTabs);
    }

    /// <summary>A SearchHandler applies to the presented page (Windows shows the pane for its search box).</summary>
    private bool HasSearchHandler(Microsoft.Maui.Controls.ShellItem item) => EffectiveSearchHandler(item) != null;

    /// <summary>The page the section presents: the top of its stack, else its content's page (Shell.GetCurrentShellPage).</summary>
    private static Microsoft.Maui.Controls.Page? DisplayedMauiPage(Microsoft.Maui.Controls.ShellSection? section)
    {
        if (section == null)
            return null;
        var stack = section.Navigation?.NavigationStack;
        if (stack != null && stack.Count > 0 && stack[stack.Count - 1] is { } top)
            return top;
        return ((IShellContentController?)section.CurrentItem)?.Page;
    }

    /// <summary>Shell.GetEffectiveValue: the value set on the element or its nearest ancestor below the Shell, else the default.</summary>
    private T EffectiveShellValue<T>(Element? element, BindableProperty property, T defaultValue)
    {
        while (element != null && !ReferenceEquals(element, _mauiShell))
        {
            if (element.IsSet(property))
                return (T)element.GetValue(property);
            element = element.Parent;
        }
        return defaultValue;
    }

    /// <summary>
    /// The pane's items, as Windows' ShellItemHandler.MapMenuItems builds them: per visible section
    /// of the current item the section (its visible contents as menu items when it has more than
    /// one), or for a section MAUI made implicitly its current content. The selected item is the
    /// current section's current content when it is a menu item, else the current section's item.
    /// Without a MAUI Shell: the current section's contents.
    /// </summary>
    internal List<ShellTopNavItem> BuildTopNavItems()
    {
        var result = new List<ShellTopNavItem>();
        if (_mauiShell == null)
        {
            if (_selectedSectionIndex < 0 || _selectedSectionIndex >= _sections.Count)
                return result;
            var legacy = _sections[_selectedSectionIndex];
            for (int i = 0; i < legacy.Items.Count; i++)
            {
                result.Add(new ShellTopNavItem(legacy.Items[i], legacy.Items[i].Title, null, true)
                {
                    LegacyIndex = i,
                    IsSelected = i == _selectedItemIndex,
                });
            }
            return result;
        }

        if (_mauiShell.CurrentItem is not { } item)
        {
            TrackPaneItems(Array.Empty<BaseShellItem>());
            return result;
        }

        var tracked = new List<BaseShellItem>();
        try
        {
            var current = item.CurrentItem;
            foreach (var section in ((IShellItemController)item).GetItems())
            {
                tracked.Add(section);
                if (IsImplicitShellElement(section) && section.CurrentItem is { } implicitContent)
                {
                    tracked.Add(implicitContent);
                    result.Add(new ShellTopNavItem(implicitContent, implicitContent.Title, implicitContent.Icon, implicitContent.IsEnabled)
                    {
                        IsSelected = ReferenceEquals(implicitContent.Parent, current),
                    });
                    continue;
                }

                var navItem = new ShellTopNavItem(section, section.Title, section.Icon, section.IsEnabled)
                {
                    IsSelected = ReferenceEquals(section, current),
                };
                var contents = ((IShellSectionController)section).GetItems();
                if (contents.Count > 1)
                {
                    navItem.Children = new List<ShellTopNavItem>(contents.Count);
                    foreach (var content in contents)
                    {
                        tracked.Add(content);
                        bool selected = ReferenceEquals(section, current) && ReferenceEquals(content, current?.CurrentItem);
                        navItem.Children.Add(new ShellTopNavItem(content, content.Title, content.Icon, content.IsEnabled) { IsSelected = selected });
                        if (selected)
                        {
                            // The content's menu item is the selection; its section's tab shows it.
                            navItem.IsSelected = false;
                            navItem.IsChildSelected = true;
                        }
                    }
                }
                result.Add(navItem);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Reading the ShellItem's sections failed", ex);
        }
        TrackPaneItems(tracked);
        return result;
    }

    /// <summary>
    /// Follows the title, icon and enabled state of the listed sections and contents (Windows'
    /// OnShellItemPropertyChanged and ShellSectionHandler's content tracking).
    /// </summary>
    private void TrackPaneItems(IReadOnlyCollection<BaseShellItem> items)
    {
        if (_paneTrackedItems.Count == items.Count && items.All(_paneTrackedItems.Contains))
            return;
        foreach (var gone in _paneTrackedItems.Where(i => !items.Contains(i)).ToList())
        {
            gone.PropertyChanged -= OnPaneItemPropertyChanged;
            _paneTrackedItems.Remove(gone);
        }
        foreach (var added in items)
        {
            if (_paneTrackedItems.Add(added))
                added.PropertyChanged += OnPaneItemPropertyChanged;
        }
    }

    private void UntrackPaneItems()
    {
        foreach (var item in _paneTrackedItems)
            item.PropertyChanged -= OnPaneItemPropertyChanged;
        _paneTrackedItems.Clear();
    }

    private void OnPaneItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BaseShellItem.Title) or nameof(BaseShellItem.Icon) or nameof(BaseShellItem.IsEnabled))
        {
            _topNavigation?.InvalidateAccessibleChildren();
            Invalidate();
        }
    }

    /// <summary>Lays the pane out at <see cref="TabBarBounds"/> (or closes it) and tells assistive technology when it comes or goes.</summary>
    private void ArrangeTopPane()
    {
        bool shown = IsTopPaneShown;
        var pane = TopNavigation;
        if (shown)
        {
            var rect = TabBarBounds;
            pane.Measure(new Size(rect.Width, rect.Height));
            pane.Arrange(rect);
        }
        else
        {
            pane.CloseDropDown();
        }
        ArrangeSearchBox();
        if (shown != _topPaneWasShown)
        {
            _topPaneWasShown = shown;
            InvalidateAccessibleChildren();
        }
    }

    /// <summary>The tab stops of the shell's own chrome, before the page's: the top navigation pane when shown.</summary>
    internal IEnumerable<SkiaView> ChromeTabStops()
    {
        if (IsTopPaneShown && _topNavigation is { } pane && pane.Bounds.Width > 0 && pane.Bounds.Height > 0)
            yield return pane;
        // The search box follows the tabs, as WinUI's AutoSuggestBox follows the top pane's items.
        if (IsSearchBoxShown && _searchBox is { IsEnabled: true } box && box.Bounds.Width > 0 && box.Bounds.Height > 0)
            yield return box.Entry;
    }

    protected override List<IAccessible> GetAccessibleChildren()
    {
        var children = base.GetAccessibleChildren();
        if (IsTopPaneShown)
        {
            children.Insert(0, TopNavigation);
            if (IsSearchBoxShown)
                children.Insert(1, SearchBox);
        }
        return children;
    }

    /// <summary>
    /// Selects the item of the pane as Windows' ShellItemHandler.OnNavigationTabChanged does: a
    /// section through IShellItemController.ProposeSection; a content of the current section through
    /// ProposeNavigation(ShellContentChanged) and the section's CurrentItem set from the platform; a
    /// content of another section by proposing that section with the content as its target, then
    /// making both current. A section with menu items is not selected itself (its drop-down opens).
    /// </summary>
    internal void SelectTopNavItem(ShellTopNavItem navItem)
    {
        if (!navItem.IsEnabled)
            return;
        if (navItem.LegacyIndex >= 0)
        {
            if (navItem.LegacyIndex != _selectedItemIndex)
                SelectSection(_selectedSectionIndex, navItem.LegacyIndex);
            return;
        }
        if (_mauiShell is not IShellController controller || _mauiShell.CurrentItem is not { } item)
            return;
        try
        {
            switch (navItem.Data)
            {
                case Microsoft.Maui.Controls.ShellSection section when navItem.Children == null:
                    if (!ReferenceEquals(item.CurrentItem, section))
                        ((IShellItemController)item).ProposeSection(section);
                    break;

                case Microsoft.Maui.Controls.ShellContent content when content.Parent is Microsoft.Maui.Controls.ShellSection owner:
                    if (ReferenceEquals(item.CurrentItem, owner))
                    {
                        if (ReferenceEquals(owner.CurrentItem, content))
                            return;
                        controller.ProposeNavigation(ShellNavigationSource.ShellContentChanged, item, owner, content, owner.Stack, false);
                        owner.SetValueFromRenderer(Microsoft.Maui.Controls.ShellSection.CurrentItemProperty, content);
                    }
                    else if (controller.ProposeNavigation(ShellNavigationSource.ShellSectionChanged, item, owner, content, owner.Stack, true))
                    {
                        if (!ReferenceEquals(owner.CurrentItem, content))
                            owner.SetValueFromRenderer(Microsoft.Maui.Controls.ShellSection.CurrentItemProperty, content);
                        item.SetValueFromRenderer(Microsoft.Maui.Controls.ShellItem.CurrentItemProperty, owner);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", $"Selecting the tab '{navItem.Title}' failed", ex);
        }
    }

    /// <summary>One item of the top navigation pane (Windows' NavigationViewItemViewModel).</summary>
    internal sealed class ShellTopNavItem
    {
        public ShellTopNavItem(object data, string? title, IImageSource? icon, bool isEnabled)
        {
            Data = data;
            Title = title ?? string.Empty;
            Icon = icon;
            IsEnabled = isEnabled;
        }

        /// <summary>The MAUI ShellSection or ShellContent (or the platform's own content without a MAUI Shell).</summary>
        public object Data { get; }
        public string Title { get; }
        public IImageSource? Icon { get; }
        public bool IsEnabled { get; }
        /// <summary>The menu items (MenuItemsSource): a section's contents when it has more than one.</summary>
        public List<ShellTopNavItem>? Children { get; set; }
        /// <summary>The navigation view's SelectedItem is this item.</summary>
        public bool IsSelected { get; set; }
        /// <summary>One of the menu items is selected (the selection indicator is on this tab).</summary>
        public bool IsChildSelected { get; set; }
        /// <summary>The content's index in the platform's own section (no MAUI Shell); -1 otherwise.</summary>
        public int LegacyIndex { get; set; } = -1;

        public bool HasIcon => Icon is { IsEmpty: false };
    }

    /// <summary>
    /// The top navigation pane: WinUI's NavigationView top pane, drawn with Skia. One keyboard tab
    /// stop; Left/Right/Home/End move between tabs (and the More button), Enter or Space selects a
    /// tab or opens its drop-down; in a drop-down Up/Down/Home/End move, Enter or Space picks,
    /// Escape closes. Assistive technology sees a page tab list of page tabs, the More button, and
    /// an open drop-down as a menu of menu items.
    /// </summary>
    internal sealed class ShellTopNavigationView : SkiaView
    {
        // WinUI's top pane metrics (NavigationView generic.xaml): 16px icons, 14px text, 48px minimum item width.
        private const float ItemPadding = 12f;
        private const float ItemMinWidth = 48f;
        private const float PaneIconSize = 16f;
        private const float IconGap = 12f;
        private const float ChevronSize = 12f;
        private const float ChevronGap = 8f;
        private const float MoreButtonWidth = 48f;
        private const float IndicatorWidth = 16f;
        private const float IndicatorThickness = 3f;
        private const float MenuRowHeight = 40f;
        private const float MenuPadding = 4f;
        private const float MenuMinWidth = 160f;
        private const float MenuIndent = 24f;
        private const float FontSize = 14f;

        private static readonly object MoreMarker = new();

        private readonly SkiaShell _shell;
        private TabIconCache? _icons;
        private List<ShellTopNavItem> _items = new();
        private readonly List<int> _primary = new();
        private readonly List<int> _overflow = new();
        private readonly List<SKRect> _slotRects = new();
        private readonly Dictionary<ShellTopNavItem, (SKRect Icon, SKRect Title)> _parts = new();
        private int _focusSlot = -1;
        private int _hoverSlot = -1;
        private bool _keyboardFocus;

        // The open drop-down: the Data of the tab it belongs to, or MoreMarker for the overflow.
        private object? _openFor;
        private readonly List<(ShellTopNavItem Item, int Depth, SKRect Bounds)> _menuRows = new();
        private SKRect _menuRect;
        private int _menuIndex = -1;
        private readonly HashSet<object> _expanded = new();
        private string _accessibleSignature = string.Empty;

        public ShellTopNavigationView(SkiaShell shell)
        {
            _shell = shell;
            IsFocusable = true;
            Parent = shell; // invalidations reach the window
        }

        private TabIconCache Icons => _icons ??= new TabIconCache(_shell);

        /// <summary>The items in the pane, in order (tests).</summary>
        internal IReadOnlyList<ShellTopNavItem> Items
        {
            get { LayoutItems(); return _items; }
        }

        /// <summary>The items shown as tabs, in order (the others are in the More drop-down).</summary>
        internal IReadOnlyList<ShellTopNavItem> PrimaryItems
        {
            get { LayoutItems(); return _primary.Select(i => _items[i]).ToList(); }
        }

        /// <summary>The items moved to the More drop-down, in order.</summary>
        internal IReadOnlyList<ShellTopNavItem> OverflowItems
        {
            get { LayoutItems(); return _overflow.Select(i => _items[i]).ToList(); }
        }

        /// <summary>Where the More button is; empty when every tab fits.</summary>
        internal SKRect MoreButtonBounds
        {
            get { LayoutItems(); return _overflow.Count > 0 ? _slotRects[_primary.Count] : SKRect.Empty; }
        }

        /// <summary>The drop-down is open.</summary>
        internal bool IsDropDownOpen => _openFor != null;

        /// <summary>The rows of the open drop-down and where they are (in the pane's coordinates).</summary>
        internal IReadOnlyList<(ShellTopNavItem Item, SKRect Bounds)> DropDownRows
        {
            get { LayoutItems(); return _menuRows.Select(r => (r.Item, r.Bounds)).ToList(); }
        }

        internal int FocusSlot => _focusSlot;

        /// <summary>Where the tab of the item whose Data is <paramref name="data"/> is; empty when it has none in the pane.</summary>
        internal SKRect ItemBounds(object data)
        {
            LayoutItems();
            for (int s = 0; s < _primary.Count; s++)
                if (ReferenceEquals(_items[_primary[s]].Data, data))
                    return _slotRects[s];
            return SKRect.Empty;
        }

        /// <summary>The icon and title of the tab standing for <paramref name="section"/> (its own, or its implicit content's).</summary>
        internal (SKRect Icon, SKRect Title) ItemPartBounds(Microsoft.Maui.Controls.ShellSection section)
        {
            LayoutItems();
            foreach (var index in _primary)
            {
                var item = _items[index];
                if (ReferenceEquals(item.Data, section)
                    || (item.Data is Microsoft.Maui.Controls.ShellContent c && ReferenceEquals(c.Parent, section)))
                    return _parts.TryGetValue(item, out var parts) ? parts : (SKRect.Empty, SKRect.Empty);
            }
            return (SKRect.Empty, SKRect.Empty);
        }

        private int SlotCount => _primary.Count + (_overflow.Count > 0 ? 1 : 0);

        private bool IsMoreSlot(int slot) => _overflow.Count > 0 && slot == _primary.Count;

        private ShellTopNavItem? SlotItem(int slot) =>
            slot >= 0 && slot < _primary.Count ? _items[_primary[slot]] : null;

        private object? SlotKey(int slot) => IsMoreSlot(slot) ? MoreMarker : SlotItem(slot)?.Data;

        private int SlotOf(object? key)
        {
            if (key == null)
                return -1;
            for (int s = 0; s < SlotCount; s++)
                if (ReferenceEquals(SlotKey(s), key))
                    return s;
            return -1;
        }

        /// <summary>The slot showing the selection: the selected tab, or the More button when the selection is in it.</summary>
        private int SelectedSlot()
        {
            for (int s = 0; s < _primary.Count; s++)
            {
                var item = _items[_primary[s]];
                if (item.IsSelected || item.IsChildSelected)
                    return s;
            }
            return SlotCount > 0 ? 0 : -1;
        }

        private float ItemWidth(SKFont font, ShellTopNavItem item)
        {
            float w = ItemPadding * 2 + font.MeasureText(item.Title);
            if (item.HasIcon)
                w += PaneIconSize + (item.Title.Length > 0 ? IconGap : 0f);
            if (item.Children != null)
                w += ChevronGap + ChevronSize;
            return Math.Max(ItemMinWidth, w);
        }

        /// <summary>
        /// Reads the items and lays them out left to right; when they do not all fit, the ones that
        /// do not (the selected one always stays) move to the More drop-down, as WinUI's top pane does.
        /// </summary>
        internal void LayoutItems()
        {
            _items = _shell.BuildTopNavItems();
            _primary.Clear();
            _overflow.Clear();
            _slotRects.Clear();
            _parts.Clear();

            // The search box takes the pane's right end (WinUI's AutoSuggestBox area); the tabs get the rest.
            float searchArea = _shell.SearchAreaWidth((float)Bounds.Width);
            var pane = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right - searchArea, (float)Bounds.Bottom);
            using var font = SkiaFontFactory.Create(FontSize);
            var widths = _items.Select(i => ItemWidth(font, i)).ToArray();

            if (widths.Sum() <= pane.Width)
            {
                for (int i = 0; i < _items.Count; i++)
                    _primary.Add(i);
            }
            else
            {
                float available = pane.Width - MoreButtonWidth;
                int selected = _items.FindIndex(i => i.IsSelected || i.IsChildSelected);
                float used = selected >= 0 ? widths[selected] : 0f;
                var keep = new HashSet<int>();
                if (selected >= 0)
                    keep.Add(selected);
                for (int i = 0; i < _items.Count; i++)
                {
                    if (i == selected)
                        continue;
                    if (used + widths[i] > available)
                        break;
                    used += widths[i];
                    keep.Add(i);
                }
                for (int i = 0; i < _items.Count; i++)
                    (keep.Contains(i) ? _primary : _overflow).Add(i);
            }

            float x = pane.Left;
            foreach (var index in _primary)
            {
                var rect = new SKRect(x, pane.Top, x + widths[index], pane.Bottom);
                _slotRects.Add(rect);
                _parts[_items[index]] = PartRects(font, _items[index], rect);
                x += widths[index];
            }
            if (_overflow.Count > 0)
                _slotRects.Add(new SKRect(x, pane.Top, x + MoreButtonWidth, pane.Bottom));

            if (_focusSlot >= SlotCount)
                _focusSlot = SlotCount - 1;
            if (_hoverSlot >= SlotCount)
                _hoverSlot = -1;

            LayoutDropDown();

            var signature = string.Join("|", _primary.Select(i => SignatureOf(_items[i])))
                + "#" + string.Join("|", _overflow.Select(i => SignatureOf(_items[i])))
                + "#" + (_openFor != null ? string.Join("|", _menuRows.Select(r => SignatureOf(r.Item))) : "-");
            if (signature != _accessibleSignature)
            {
                _accessibleSignature = signature;
                InvalidateAccessibleChildren();
            }
        }

        private static string SignatureOf(ShellTopNavItem item) =>
            $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.Data)}:{item.Title}:{item.IsEnabled}:{item.IsSelected}:{item.IsChildSelected}";

        /// <summary>Where the icon and title of a tab go: icon, title, then the chevron, centred vertically from the left padding.</summary>
        private (SKRect Icon, SKRect Title) PartRects(SKFont font, ShellTopNavItem item, SKRect tab)
        {
            float left = tab.Left + ItemPadding;
            SKRect icon = SKRect.Empty;
            if (item.HasIcon)
            {
                // The icon's place is reported once it has loaded, as it is drawn.
                if (Icons.Get(item.Icon) != null)
                    icon = SKRect.Create(left, tab.MidY - PaneIconSize / 2f, PaneIconSize, PaneIconSize);
                left += PaneIconSize + (item.Title.Length > 0 ? IconGap : 0f);
            }
            float width = font.MeasureText(item.Title, out var textBounds);
            float baseline = TextRenderingHelper.BaselineForVerticalCenter(font, tab.MidY);
            var title = item.Title.Length > 0
                ? new SKRect(left, baseline + textBounds.Top, left + width, baseline + textBounds.Bottom)
                : SKRect.Empty;
            return (icon, title);
        }

        // ----- Drop-down -----

        private void LayoutDropDown()
        {
            _menuRows.Clear();
            if (_openFor == null)
                return;
            int slot = SlotOf(_openFor);
            var rows = new List<(ShellTopNavItem Item, int Depth)>();
            if (slot >= 0 && IsMoreSlot(slot))
            {
                foreach (var index in _overflow)
                {
                    var item = _items[index];
                    rows.Add((item, 0));
                    if (item.Children != null && _expanded.Contains(item.Data))
                        rows.AddRange(item.Children.Select(c => (c, 1)));
                }
            }
            else if (SlotItem(slot) is { Children: { } children })
            {
                rows.AddRange(children.Select(c => (c, 0)));
            }

            if (rows.Count == 0)
            {
                CloseDropDown();
                return;
            }

            using var font = SkiaFontFactory.Create(FontSize);
            float width = MenuMinWidth;
            foreach (var (item, depth) in rows)
            {
                float w = MenuPadding * 2 + ItemPadding * 2 + depth * MenuIndent + font.MeasureText(item.Title);
                if (item.HasIcon)
                    w += PaneIconSize + IconGap;
                if (item.Children != null)
                    w += ChevronGap + ChevronSize;
                width = Math.Max(width, w);
            }
            var anchor = _slotRects[slot];
            float height = rows.Count * MenuRowHeight + MenuPadding * 2;
            var shellBounds = _shell.Bounds;
            width = Math.Min(width, (float)shellBounds.Width);
            // WinUI: a tab's flyout is aligned to the tab's left edge, the More flyout to the button's right edge.
            float left = IsMoreSlot(slot) ? anchor.Right - width : anchor.Left;
            left = Math.Clamp(left, (float)shellBounds.Left, Math.Max((float)shellBounds.Left, (float)shellBounds.Right - width));
            float top = (float)Bounds.Bottom + 2f;
            _menuRect = new SKRect(left, top, left + width, top + height);

            float y = top + MenuPadding;
            foreach (var (item, depth) in rows)
            {
                _menuRows.Add((item, depth, new SKRect(left + MenuPadding, y, left + width - MenuPadding, y + MenuRowHeight)));
                y += MenuRowHeight;
            }
            if (_menuIndex >= _menuRows.Count)
                _menuIndex = _menuRows.Count - 1;
        }

        private void OpenDropDown(int slot, bool fromKeyboard)
        {
            _openFor = SlotKey(slot);
            _menuIndex = -1;
            LayoutItems();
            if (_openFor == null)
                return;
            if (fromKeyboard)
            {
                _menuIndex = _menuRows.FindIndex(r => r.Item.IsSelected || r.Item.IsChildSelected);
                if (_menuIndex < 0)
                    _menuIndex = 0;
            }
            RegisterPopupOverlay(this, DrawDropDownOverlay);
            InvalidateAccessibleChildren();
            Invalidate();
        }

        internal void CloseDropDown()
        {
            if (_openFor == null)
                return;
            _openFor = null;
            _menuRows.Clear();
            _menuIndex = -1;
            UnregisterPopupOverlay(this);
            InvalidateAccessibleChildren();
            Invalidate();
        }

        /// <summary>Activates a tab (or the More button): a tab with menu items, and More, open their drop-down; any other tab is selected.</summary>
        private void ActivateSlot(int slot, bool fromKeyboard)
        {
            if (slot < 0 || slot >= SlotCount)
                return;
            if (IsMoreSlot(slot))
            {
                OpenDropDown(slot, fromKeyboard);
                return;
            }
            var item = SlotItem(slot)!;
            if (!item.IsEnabled)
                return;
            if (item.Children != null)
                OpenDropDown(slot, fromKeyboard);
            else
                _shell.SelectTopNavItem(item);
        }

        /// <summary>Activates a drop-down row: an overflowed section with menu items expands or collapses, any other row is selected.</summary>
        private void ActivateRow(int row)
        {
            if (row < 0 || row >= _menuRows.Count)
                return;
            var item = _menuRows[row].Item;
            if (!item.IsEnabled)
                return;
            if (item.Children != null)
            {
                if (!_expanded.Remove(item.Data))
                    _expanded.Add(item.Data);
                _menuIndex = row;
                LayoutItems();
                InvalidateAccessibleChildren();
                Invalidate();
                return;
            }
            CloseDropDown();
            _shell.SelectTopNavItem(item);
        }

        private int RowAt(float x, float y)
        {
            for (int i = 0; i < _menuRows.Count; i++)
                if (_menuRows[i].Bounds.Contains(x, y))
                    return i;
            return -1;
        }

        private int SlotAt(float x, float y)
        {
            for (int s = 0; s < _slotRects.Count; s++)
                if (_slotRects[s].Contains(x, y))
                    return s;
            return -1;
        }

        // The pane's coordinates (Bounds) to the window's, where overlays are drawn and hit-tested.
        private SKPoint WindowOffset
        {
            get
            {
                var screen = ScreenBounds;
                return new SKPoint((float)(screen.Left - Bounds.Left), (float)(screen.Top - Bounds.Top));
            }
        }

        /// <summary>
        /// While the drop-down is open it takes every press in the window: one outside it closes it
        /// (WinUI's light dismiss), as long as the pane is still shown.
        /// </summary>
        protected override bool HitTestPopupArea(float x, float y)
        {
            if (IsDropDownLive())
                return true;
            // A drop-down whose pane went away is unregistered, so it does not count as an open popup.
            CloseDropDown();
            return false;
        }

        /// <summary>
        /// The drop-down is open and its pane is still shown in an open window (popup overlays are
        /// global: one left registered by a pane that went away must neither draw nor take input).
        /// </summary>
        private bool IsDropDownLive()
        {
            if (_openFor == null || !_shell.IsTopPaneShown || !ReferenceEquals(_shell._topNavigation, this))
                return false;
            var app = Microsoft.Maui.Platform.Linux.LinuxApplication.Current;
            if (app == null)
                return true;
            SkiaView top = this;
            while (top.Parent != null)
                top = top.Parent;
            foreach (var context in app.WindowContexts)
                if (ReferenceEquals(context.RootView, top))
                    return true;
            return false;
        }

        // ----- Drawing -----

        private readonly record struct Palette(SKColor Background, SKColor SelectedIcon, SKColor SelectedText, SKColor Indicator, SKColor Unselected, SKColor Disabled);

        /// <summary>
        /// Windows' ShellItemHandler.UpdateAppearance: the pane's background is TabBarBackgroundColor;
        /// the selected tab's text TabBarTitleColor (else TabBarForegroundColor), its icon and the
        /// selection indicator TabBarForegroundColor (else TabBarTitleColor); the other tabs'
        /// icons and text TabBarUnselectedColor, which also colours disabled tabs; a
        /// TabBarDisabledColor, when set, colours disabled tabs.
        /// </summary>
        private Palette CurrentPalette()
        {
            var appearance = _shell._appearance as IShellAppearanceElement;
            var foreground = appearance?.EffectiveTabBarForegroundColor?.ToSKColor();
            var title = appearance?.EffectiveTabBarTitleColor?.ToSKColor();
            var unselected = appearance?.EffectiveTabBarUnselectedColor?.ToSKColor();
            var disabled = appearance?.EffectiveTabBarDisabledColor?.ToSKColor();
            var text = SkiaTheme.CurrentTextSK;
            return new Palette(
                appearance?.EffectiveTabBarBackgroundColor?.ToSKColor() ?? SkiaTheme.CurrentSurfaceSK,
                foreground ?? title ?? text,
                title ?? foreground ?? text,
                foreground ?? title ?? SkiaTheme.PrimarySK,
                unselected ?? text.WithAlpha(170),
                disabled ?? unselected ?? text.WithAlpha(92));
        }

        protected override void OnDraw(SKCanvas canvas, SKRect bounds)
        {
            LayoutItems();
            var palette = CurrentPalette();
            using (var bg = new SKPaint { Color = palette.Background, Style = SKPaintStyle.Fill })
                canvas.DrawRect(bounds, bg);
            // The content's top border under the pane (WinUI's content grid in Top mode).
            using (var border = new SKPaint { Color = SkiaTheme.CurrentBorderSK, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                canvas.DrawLine(bounds.Left, bounds.Bottom - 0.5f, bounds.Right, bounds.Bottom - 0.5f, border);

            Icons.Retain(_items.SelectMany(i => i.Children == null ? new[] { i } : i.Children.Prepend(i)).Select(i => i.Icon));

            canvas.Save();
            canvas.ClipRect(bounds);
            using var font = SkiaFontFactory.Create(FontSize);
            using var textPaint = new SKPaint { IsAntialias = true };
            for (int s = 0; s < _primary.Count; s++)
            {
                var item = _items[_primary[s]];
                var tab = _slotRects[s];
                DrawHoverAndFocus(canvas, s, tab);

                var color = !item.IsEnabled ? palette.Disabled : item.IsSelected ? palette.SelectedText : palette.Unselected;
                var iconColor = !item.IsEnabled ? palette.Disabled : item.IsSelected ? palette.SelectedIcon : palette.Unselected;
                var (iconRect, titleRect) = _parts[item];
                if (!iconRect.IsEmpty && Icons.Get(item.Icon) is { } bitmap)
                    TabIconCache.DrawTinted(canvas, bitmap, iconRect, iconColor);
                if (item.Title.Length > 0)
                {
                    textPaint.Color = color;
                    font.MeasureText(item.Title, out var textBounds);
                    float baseline = TextRenderingHelper.BaselineForVerticalCenter(font, tab.MidY);
                    canvas.DrawText(item.Title, titleRect.Left - textBounds.Left, baseline, SKTextAlign.Left, font, textPaint);
                }
                if (item.Children != null)
                    DrawChevron(canvas, new SKPoint(tab.Right - ItemPadding - ChevronSize / 2f, tab.MidY), color, up: ReferenceEquals(_openFor, item.Data));

                if (item.IsSelected || item.IsChildSelected)
                {
                    using var indicatorPaint = new SKPaint { Color = palette.Indicator, Style = SKPaintStyle.Fill, IsAntialias = true };
                    var bar = SKRect.Create(tab.MidX - IndicatorWidth / 2f, tab.Bottom - IndicatorThickness - 2f, IndicatorWidth, IndicatorThickness);
                    canvas.DrawRoundRect(bar, IndicatorThickness / 2f, IndicatorThickness / 2f, indicatorPaint);
                }
            }

            if (_overflow.Count > 0)
            {
                var more = _slotRects[_primary.Count];
                DrawHoverAndFocus(canvas, _primary.Count, more);
                using var dots = new SKPaint { Color = palette.Unselected, Style = SKPaintStyle.Fill, IsAntialias = true };
                for (int d = -1; d <= 1; d++)
                    canvas.DrawCircle(more.MidX + d * 6f, more.MidY, 1.6f, dots);
            }
            canvas.Restore();
        }

        private void DrawHoverAndFocus(SKCanvas canvas, int slot, SKRect tab)
        {
            var inner = tab;
            inner.Inflate(-3f, -6f);
            if (slot == _hoverSlot || ReferenceEquals(_openFor, SlotKey(slot)))
            {
                using var hover = new SKPaint { Color = SkiaTheme.CurrentTextSK.WithAlpha(15), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(inner, 4f, 4f, hover);
            }
            if (IsFocused && _keyboardFocus && slot == _focusSlot)
            {
                using var focusPaint = new SKPaint { Color = SkiaTheme.CurrentTextSK, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                canvas.DrawRoundRect(inner, 4f, 4f, focusPaint);
            }
        }

        private static void DrawChevron(SKCanvas canvas, SKPoint center, SKColor color, bool up)
        {
            using var paint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
            };
            float w = 4f, h = up ? -2.5f : 2.5f;
            using var path = new SKPath();
            path.MoveTo(center.X - w, center.Y - h);
            path.LineTo(center.X, center.Y + h);
            path.LineTo(center.X + w, center.Y - h);
            canvas.DrawPath(path, paint);
        }

        private void DrawDropDownOverlay(SKCanvas canvas)
        {
            if (_menuRows.Count == 0 || !IsDropDownLive())
                return;
            var offset = WindowOffset;
            canvas.Save();
            canvas.Translate(offset.X, offset.Y);

            // A WinUI flyout: the surface with a hairline border and a soft shadow, rows inset with rounded highlights.
            using (var shadow = new SKPaint
            {
                Color = SkiaTheme.Shadow25SK,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateBlur(6f, 6f),
            })
                canvas.DrawRoundRect(SKRect.Create(_menuRect.Left, _menuRect.Top + 2f, _menuRect.Width, _menuRect.Height), 8f, 8f, shadow);
            using (var surface = new SKPaint { Color = SkiaTheme.CurrentSurfaceSK, Style = SKPaintStyle.Fill, IsAntialias = true })
                canvas.DrawRoundRect(_menuRect, 8f, 8f, surface);
            using (var border = new SKPaint { Color = SkiaTheme.CurrentBorderSK, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
                canvas.DrawRoundRect(_menuRect, 8f, 8f, border);

            var text = SkiaTheme.CurrentTextSK;
            using var font = SkiaFontFactory.Create(FontSize);
            using var textPaint = new SKPaint { IsAntialias = true };
            for (int i = 0; i < _menuRows.Count; i++)
            {
                var (item, depth, row) = _menuRows[i];
                var highlight = row;
                highlight.Inflate(0f, -2f);
                if (i == _menuIndex || item.IsSelected)
                {
                    using var hover = new SKPaint { Color = text.WithAlpha((byte)(i == _menuIndex ? 20 : 12)), Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRoundRect(highlight, 4f, 4f, hover);
                }
                if (item.IsSelected || item.IsChildSelected)
                {
                    // The left-mode selection indicator of a menu item.
                    using var indicator = new SKPaint { Color = SkiaTheme.PrimarySK, Style = SKPaintStyle.Fill, IsAntialias = true };
                    var pill = SKRect.Create(row.Left + depth * MenuIndent, row.MidY - IndicatorWidth / 2f, IndicatorThickness, IndicatorWidth);
                    canvas.DrawRoundRect(pill, IndicatorThickness / 2f, IndicatorThickness / 2f, indicator);
                }
                if (IsFocused && _keyboardFocus && i == _menuIndex)
                {
                    using var focusPaint = new SKPaint { Color = text, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    canvas.DrawRoundRect(highlight, 4f, 4f, focusPaint);
                }

                var color = item.IsEnabled ? text : text.WithAlpha(92);
                float left = row.Left + ItemPadding + depth * MenuIndent;
                if (item.HasIcon)
                {
                    if (Icons.Get(item.Icon) is { } bitmap)
                        TabIconCache.DrawTinted(canvas, bitmap, SKRect.Create(left, row.MidY - PaneIconSize / 2f, PaneIconSize, PaneIconSize), color);
                    left += PaneIconSize + IconGap;
                }
                textPaint.Color = color;
                font.MeasureText(item.Title, out var textBounds);
                canvas.DrawText(item.Title, left - textBounds.Left, TextRenderingHelper.BaselineForVerticalCenter(font, row.MidY), SKTextAlign.Left, font, textPaint);
                if (item.Children != null)
                    DrawChevron(canvas, new SKPoint(row.Right - ItemPadding - ChevronSize / 2f, row.MidY), color, up: _expanded.Contains(item.Data));
            }
            canvas.Restore();
        }

        // ----- Input -----

        public override SkiaView? HitTest(float x, float y) =>
            IsVisible && Bounds.Contains(x, y) ? this : null;

        public override void OnPointerPressed(PointerEventArgs e)
        {
            _keyboardFocus = false;
            e.Handled = true;
            LayoutItems();
            if (_openFor != null)
            {
                if (_menuRect.Contains(e.X, e.Y))
                {
                    ActivateRow(RowAt(e.X, e.Y));
                    Invalidate();
                    return;
                }
                var openSlot = SlotOf(_openFor);
                int pressed = SlotAt(e.X, e.Y);
                CloseDropDown();
                // A press on the tab whose drop-down was open just closes it; on another tab it acts on that tab.
                if (pressed >= 0 && pressed != openSlot)
                {
                    _focusSlot = pressed;
                    ActivateSlot(pressed, fromKeyboard: false);
                }
                Invalidate();
                return;
            }
            int slot = SlotAt(e.X, e.Y);
            if (slot >= 0)
            {
                _focusSlot = slot;
                ActivateSlot(slot, fromKeyboard: false);
            }
            Invalidate();
        }

        public override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            int slot = SlotAt(e.X, e.Y);
            int row = _openFor != null ? RowAt(e.X, e.Y) : -1;
            if (slot != _hoverSlot || (row >= 0 && row != _menuIndex))
            {
                _hoverSlot = slot;
                if (row >= 0)
                    _menuIndex = row;
                Invalidate();
            }
        }

        public override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (_hoverSlot != -1)
            {
                _hoverSlot = -1;
                Invalidate();
            }
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled)
                return;
            LayoutItems();
            if (SlotCount == 0)
                return;
            if (_focusSlot < 0 || _focusSlot >= SlotCount)
                _focusSlot = SelectedSlot();

            if (_openFor != null)
            {
                switch (e.Key)
                {
                    case Key.Up:
                        _menuIndex = Math.Max(0, _menuIndex - 1);
                        break;
                    case Key.Down:
                        _menuIndex = Math.Min(_menuRows.Count - 1, _menuIndex + 1);
                        break;
                    case Key.Home:
                        _menuIndex = 0;
                        break;
                    case Key.End:
                        _menuIndex = _menuRows.Count - 1;
                        break;
                    case Key.Enter:
                    case Key.Space:
                        ActivateRow(_menuIndex);
                        break;
                    case Key.Escape:
                        CloseDropDown();
                        break;
                    case Key.Left:
                    case Key.Right:
                        CloseDropDown();
                        MoveFocus(e.Key == Key.Left ? _focusSlot - 1 : _focusSlot + 1);
                        break;
                    default:
                        return;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        MoveFocus(_focusSlot - 1);
                        break;
                    case Key.Right:
                        MoveFocus(_focusSlot + 1);
                        break;
                    case Key.Home:
                        MoveFocus(0);
                        break;
                    case Key.End:
                        MoveFocus(SlotCount - 1);
                        break;
                    case Key.Enter:
                    case Key.Space:
                        ActivateSlot(_focusSlot, fromKeyboard: true);
                        break;
                    case Key.Down:
                        if (!IsMoreSlot(_focusSlot) && SlotItem(_focusSlot)?.Children == null)
                            return;
                        ActivateSlot(_focusSlot, fromKeyboard: true);
                        break;
                    default:
                        return;
                }
            }
            _keyboardFocus = true;
            InvalidateAccessibleChildren();
            e.Handled = true;
            Invalidate();
        }

        private void MoveFocus(int slot) => _focusSlot = Math.Clamp(slot, 0, Math.Max(0, SlotCount - 1));

        public override void OnFocusGained()
        {
            LayoutItems();
            _focusSlot = SelectedSlot();
            // Focus that a press brings is reset to pointer focus by the press that follows.
            _keyboardFocus = true;
            base.OnFocusGained();
        }

        public override void OnFocusLost()
        {
            _keyboardFocus = false;
            CloseDropDown();
            base.OnFocusLost();
        }

        // ----- Accessibility -----

        protected override AccessibleRole GetAccessibleRole() => AccessibleRole.PageTabList;

        protected override string GetDefaultAccessibleName() => _shell._mauiShell?.CurrentItem?.Title ?? string.Empty;

        protected override List<IAccessible> GetAccessibleChildren()
        {
            LayoutItems();
            var children = new List<IAccessible>(SlotCount + 1);
            for (int s = 0; s < SlotCount; s++)
                children.Add(new SlotAccessible(this, s, SlotKey(s)!));
            if (_openFor != null && _menuRows.Count > 0)
                children.Add(new MenuAccessible(this));
            return children;
        }

        private AccessibleRect ToScreen(SKRect rect)
        {
            var offset = WindowOffset;
            return new AccessibleRect((int)(rect.Left + offset.X), (int)(rect.Top + offset.Y), (int)rect.Width, (int)rect.Height);
        }

        private abstract class PartAccessible : IAccessible
        {
            private readonly string _id = Guid.NewGuid().ToString();

            protected PartAccessible(IAccessible parent) => Parent = parent;

            public string AccessibleId => _id;
            public abstract string AccessibleName { get; }
            public string AccessibleDescription => string.Empty;
            public abstract AccessibleRole Role { get; }
            public abstract AccessibleStates States { get; }
            public IAccessible? Parent { get; }
            public virtual IReadOnlyList<IAccessible> Children => Array.Empty<IAccessible>();
            public abstract AccessibleRect Bounds { get; }
            public virtual IReadOnlyList<AccessibleAction> Actions => Array.Empty<AccessibleAction>();
            public double? Value => null;
            public double? MinValue => null;
            public double? MaxValue => null;
            public virtual bool DoAction(string actionName) => false;
            public bool SetValue(double value) => false;

            protected static AccessibleStates Base(bool enabled)
            {
                var states = AccessibleStates.Visible | AccessibleStates.Showing;
                if (enabled)
                    states |= AccessibleStates.Enabled | AccessibleStates.Sensitive;
                return states;
            }
        }

        /// <summary>A tab (a page tab, expandable when it has a drop-down) or the More button.</summary>
        private sealed class SlotAccessible : PartAccessible
        {
            private readonly ShellTopNavigationView _pane;
            private readonly object _key;

            public SlotAccessible(ShellTopNavigationView pane, int slot, object key) : base(pane)
            {
                _pane = pane;
                _key = key;
            }

            private int Slot => _pane.SlotOf(_key);
            private bool IsMore => ReferenceEquals(_key, MoreMarker);
            private ShellTopNavItem? Item => _pane.SlotItem(Slot);

            public override string AccessibleName => IsMore ? "More" : Item?.Title ?? string.Empty;
            public override AccessibleRole Role => IsMore ? AccessibleRole.Button : AccessibleRole.PageTab;

            public override AccessibleStates States
            {
                get
                {
                    var item = Item;
                    var states = Base(IsMore || item?.IsEnabled == true) | AccessibleStates.Focusable;
                    if (!IsMore)
                        states |= AccessibleStates.Selectable;
                    if (item != null && (item.IsSelected || item.IsChildSelected))
                        states |= AccessibleStates.Selected;
                    if (IsMore || item?.Children != null)
                    {
                        states |= AccessibleStates.Expandable;
                        if (ReferenceEquals(_pane._openFor, _key))
                            states |= AccessibleStates.Expanded;
                    }
                    if (_pane.IsFocused && Slot >= 0 && Slot == _pane._focusSlot && _pane._openFor == null)
                        states |= AccessibleStates.Focused;
                    return states;
                }
            }

            public override AccessibleRect Bounds
            {
                get
                {
                    int slot = Slot;
                    return slot >= 0 && slot < _pane._slotRects.Count ? _pane.ToScreen(_pane._slotRects[slot]) : default;
                }
            }

            public override IReadOnlyList<AccessibleAction> Actions =>
                [new AccessibleAction { Name = "click", Description = IsMore || Item?.Children != null ? "Open the drop-down" : "Select the tab" }];

            public override bool DoAction(string actionName)
            {
                if (actionName != "click")
                    return false;
                _pane.LayoutItems();
                int slot = Slot;
                if (slot < 0)
                    return false;
                _pane._focusSlot = slot;
                _pane.ActivateSlot(slot, fromKeyboard: true);
                return true;
            }
        }

        /// <summary>The open drop-down: a menu of menu items.</summary>
        private sealed class MenuAccessible : PartAccessible
        {
            private readonly ShellTopNavigationView _pane;

            public MenuAccessible(ShellTopNavigationView pane) : base(pane) => _pane = pane;

            public override string AccessibleName =>
                ReferenceEquals(_pane._openFor, MoreMarker) ? "More" : _pane.SlotItem(_pane.SlotOf(_pane._openFor))?.Title ?? string.Empty;

            public override AccessibleRole Role => AccessibleRole.Menu;
            public override AccessibleStates States => Base(true);
            public override AccessibleRect Bounds => _pane.ToScreen(_pane._menuRect);

            public override IReadOnlyList<IAccessible> Children =>
                _pane._menuRows.Select((r, i) => (IAccessible)new RowAccessible(_pane, this, r.Item)).ToList();
        }

        private sealed class RowAccessible : PartAccessible
        {
            private readonly ShellTopNavigationView _pane;
            private readonly ShellTopNavItem _item;

            public RowAccessible(ShellTopNavigationView pane, IAccessible menu, ShellTopNavItem item) : base(menu)
            {
                _pane = pane;
                _item = item;
            }

            private int Row => _pane._menuRows.FindIndex(r => ReferenceEquals(r.Item.Data, _item.Data));

            public override string AccessibleName => _item.Title;
            public override AccessibleRole Role => AccessibleRole.MenuItem;

            public override AccessibleStates States
            {
                get
                {
                    var states = Base(_item.IsEnabled) | AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (_item.IsSelected || _item.IsChildSelected)
                        states |= AccessibleStates.Selected;
                    if (_item.Children != null)
                    {
                        states |= AccessibleStates.Expandable;
                        if (_pane._expanded.Contains(_item.Data))
                            states |= AccessibleStates.Expanded;
                    }
                    if (_pane.IsFocused && Row >= 0 && Row == _pane._menuIndex)
                        states |= AccessibleStates.Focused;
                    return states;
                }
            }

            public override AccessibleRect Bounds
            {
                get
                {
                    int row = Row;
                    return row >= 0 ? _pane.ToScreen(_pane._menuRows[row].Bounds) : default;
                }
            }

            public override IReadOnlyList<AccessibleAction> Actions =>
                [new AccessibleAction { Name = "click", Description = _item.Children != null ? "Expand or collapse" : "Select" }];

            public override bool DoAction(string actionName)
            {
                if (actionName != "click")
                    return false;
                int row = Row;
                if (row < 0)
                    return false;
                _pane.ActivateRow(row);
                return true;
            }
        }
    }
}
