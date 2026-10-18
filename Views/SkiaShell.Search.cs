// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shell.SearchHandler as MAUI's Windows implementation shows it (ShellItemHandler.Windows.cs,
/// UpdateSearchHandler): the presented page's effective SearchHandler puts an AutoSuggestBox, 300
/// wide, in the navigation view's top pane (WinUI's NavigationView places it on the pane's right,
/// before the pane footer) and forces the pane to show. The handler is looked up again as pages
/// change (Windows' OnShellNavigated). Typing sets SearchHandler.Query; Enter (or the query icon)
/// confirms it (QueryConfirmed); ItemsSource fills the suggestion list under the box while
/// ShowsResults is set; picking a suggestion clears the box as Windows does and calls
/// ISearchHandlerController.ItemSelected.
/// </summary>
public partial class SkiaShell
{
    // Windows' AutoSuggestBox: `new AutoSuggestBox() { Width = 300 }`, a 32px WinUI text box.
    private const float SearchBoxWidth = 300f;
    private const float SearchBoxHeight = 32f;
    private const float SearchBoxMargin = 12f;

    private SearchHandler? _currentSearchHandler;
    private ShellSearchBoxView? _searchBox;
    private bool _searchBoxWasShown;

    /// <summary>The search box of the top navigation pane (WinUI's NavigationView.AutoSuggestBox).</summary>
    internal ShellSearchBoxView SearchBox => _searchBox ??= new ShellSearchBoxView(this);

    /// <summary>The SearchHandler the search box shows (the presented page's effective one).</summary>
    internal SearchHandler? CurrentSearchHandler => _currentSearchHandler;

    /// <summary>
    /// The search box is shown: a SearchHandler applies to the presented page and its
    /// SearchBoxVisibility is not Hidden (Windows collapses the AutoSuggestBox only for Hidden).
    /// </summary>
    internal bool IsSearchBoxShown =>
        _currentSearchHandler is { SearchBoxVisibility: not SearchBoxVisibility.Hidden } && IsTopPaneShown;

    /// <summary>The width the search box takes at the pane's right end (the tabs get the rest).</summary>
    internal float SearchAreaWidth(float paneWidth) =>
        IsSearchBoxShown ? Math.Min(SearchBoxWidth + SearchBoxMargin * 2, Math.Max(0f, paneWidth / 2f)) : 0f;

    /// <summary>Shell.GetEffectiveValue(SearchHandlerProperty) from the presented page of <paramref name="item"/>.</summary>
    private SearchHandler? EffectiveSearchHandler(Microsoft.Maui.Controls.ShellItem item)
    {
        Element? element = DisplayedMauiPage(item.CurrentItem) ?? (Element?)item.CurrentItem?.CurrentItem;
        return EffectiveShellValue<SearchHandler?>(element, Shell.SearchHandlerProperty, null);
    }

    /// <summary>
    /// Windows' ShellItemHandler.UpdateSearchHandler: follows the presented page's SearchHandler
    /// (on navigation and as the shell lays out), moving the search box to the new one.
    /// </summary>
    internal void UpdateSearchHandler()
    {
        SearchHandler? handler = null;
        try
        {
            if (_mauiShell?.CurrentItem is { } item)
                handler = EffectiveSearchHandler(item);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Reading the page's SearchHandler failed", ex);
        }
        if (ReferenceEquals(handler, _currentSearchHandler))
            return;
        _currentSearchHandler = handler;
        if (handler != null || _searchBox != null)
            SearchBox.Attach(handler);
        InvalidateAccessibleChildren();
        InvalidateMeasure();
        Invalidate();
    }

    private void DetachSearchHandler()
    {
        _currentSearchHandler = null;
        _searchBox?.Attach(null);
    }

    /// <summary>Where the search box goes in the pane: its right end, vertically centred.</summary>
    private SKRect SearchBoxRect(Rect pane)
    {
        float area = SearchAreaWidth((float)pane.Width);
        if (area <= 0)
            return SKRect.Empty;
        float right = (float)pane.Right - SearchBoxMargin;
        float left = Math.Min(right, (float)pane.Right - area + SearchBoxMargin);
        float height = Math.Min(SearchBoxHeight, (float)pane.Height);
        float top = (float)pane.Top + ((float)pane.Height - height) / 2f;
        return new SKRect(left, top, right, top + height);
    }

    /// <summary>Lays the search box out in the pane (or closes its list), as part of <see cref="ArrangeTopPane"/>.</summary>
    private void ArrangeSearchBox()
    {
        UpdateSearchHandler();
        bool shown = IsSearchBoxShown;
        if (shown)
            SearchBox.Layout(SearchBoxRect(TabBarBounds));
        else
            _searchBox?.CloseList();
        if (shown != _searchBoxWasShown)
        {
            _searchBoxWasShown = shown;
            if (!shown)
                _searchBox?.ReleaseFocus();
            InvalidateAccessibleChildren();
        }
    }

    /// <summary>
    /// The search box (Windows' AutoSuggestBox): a text box with the delete button and the query
    /// icon, and the suggestion list under it while it has suggestions. The text is edited by an
    /// OpenMaui entry (IME, selection, clipboard), which is the box's tab stop. In the box Down
    /// moves into the suggestions, Up back, Enter picks the highlighted suggestion or confirms the
    /// query, Escape closes the list. Assistive technology sees a combo box holding the entry and,
    /// while it is open, a list of the suggestions.
    /// </summary>
    internal sealed class ShellSearchBoxView : SkiaView
    {
        private const float ButtonWidth = 32f;
        private const float RowHeight = 40f;
        private const float ListPadding = 4f;
        private const float RowPadding = 12f;
        private const float FontSize = 14f;

        private readonly SkiaShell _shell;
        private readonly ShellSearchEntry _entry;
        private TabIconCache? _icons;
        private SearchHandler? _handler;
        private INotifyCollectionChanged? _observedList;
        private bool _suppressQuery;
        private bool _inEntryTextChanged;

        // The suggestions: the items with their text or templated view.
        private readonly List<SuggestionRow> _rows = new();
        private readonly List<SKRect> _rowRects = new();
        private bool _listOpen;
        private bool _dismissed;
        private int _highlight = -1;
        private int _hoverRow = -1;
        private float _listScroll;
        private SKRect _listRect;

        private sealed class SuggestionRow
        {
            public object? Item;
            public string Text = string.Empty;
            public DataTemplate? Template;
            public Microsoft.Maui.Controls.View? MauiView;
            public SkiaView? View;
            public float Height = RowHeight;
        }

        public ShellSearchBoxView(SkiaShell shell)
        {
            _shell = shell;
            Parent = shell; // invalidations reach the window
            _entry = new ShellSearchEntry(this)
            {
                EntryBackgroundColor = Colors.Transparent,
                BackgroundColor = Colors.Transparent,
                BorderColor = Colors.Transparent,
                FocusedBorderColor = Colors.Transparent,
                BorderWidth = 0,
                CornerRadius = 0,
                Padding = new Thickness(10, 0, 4, 0),
                VerticalTextAlignment = TextAlignment.Center,
                ClearButtonVisibility = ClearButtonVisibility.Never,
            };
            _entry.TextChanged += OnEntryTextChanged;
            AddChild(_entry);
        }

        private TabIconCache Icons => _icons ??= new TabIconCache(this);

        /// <summary>The entry that edits the query (the box's tab stop).</summary>
        internal SkiaEntry Entry => _entry;

        /// <summary>The suggestion list is open.</summary>
        internal bool IsListOpen => _listOpen;

        /// <summary>The suggestions' texts (a templated suggestion's is its item's text).</summary>
        internal IReadOnlyList<string> SuggestionTexts => _rows.Select(r => r.Text).ToList();

        /// <summary>The templated view of each suggestion (null for a text row).</summary>
        internal IReadOnlyList<SkiaView?> SuggestionViews => _rows.Select(r => r.View).ToList();

        /// <summary>The MAUI view each suggestion's ItemTemplate made (null for a text row).</summary>
        internal IReadOnlyList<Microsoft.Maui.Controls.View?> SuggestionTemplateViews => _rows.Select(r => r.MauiView).ToList();

        /// <summary>The highlighted suggestion (keyboard), -1 for none.</summary>
        internal int HighlightedIndex => _highlight;

        /// <summary>Where suggestion <paramref name="index"/> is drawn (in the shell's coordinates); empty when the list is closed.</summary>
        internal SKRect SuggestionBounds(int index)
        {
            LayoutList();
            return _listOpen && index >= 0 && index < _rowRects.Count ? _rowRects[index] : SKRect.Empty;
        }

        /// <summary>Where the query icon is.</summary>
        internal SKRect QueryIconBounds => SKRect.Create((float)Bounds.Right - ButtonWidth, (float)Bounds.Top, ButtonWidth, (float)Bounds.Height);

        /// <summary>Where the delete (clear) button and the clear-placeholder icon are.</summary>
        internal SKRect ClearButtonBounds => SKRect.Create((float)Bounds.Right - ButtonWidth * 2, (float)Bounds.Top, ButtonWidth, (float)Bounds.Height);

        // ----- The handler -----

        /// <summary>Shows <paramref name="handler"/> (null hides the box): Windows' UpdateSearchHandler.</summary>
        internal void Attach(SearchHandler? handler)
        {
            if (ReferenceEquals(handler, _handler))
                return;
            if (_handler != null)
            {
                _handler.PropertyChanged -= OnHandlerPropertyChanged;
                _handler.FocusChangeRequested -= OnFocusChangeRequested;
                ((ISearchHandlerController)_handler).ListProxyChanged -= OnListProxyChanged;
                if (_entry.IsFocused)
                    _handler.SetIsFocused(false);
            }
            CloseList();
            ObserveList(null);
            ClearRows();
            _handler = handler;
            if (handler == null)
            {
                ReleaseFocus();
                return;
            }
            handler.PropertyChanged += OnHandlerPropertyChanged;
            handler.FocusChangeRequested += OnFocusChangeRequested;
            ((ISearchHandlerController)handler).ListProxyChanged += OnListProxyChanged;
            _dismissed = false;
            ApplyAppearance();
            UpdateText();
            RefreshSuggestions();
            if (_entry.IsFocused)
                handler.SetIsFocused(true);
        }

        private void OnHandlerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_handler == null)
                return;
            switch (e.PropertyName)
            {
                case nameof(SearchHandler.Query):
                case nameof(SearchHandler.TextTransform):
                    UpdateText();
                    break;
                case nameof(SearchHandler.ItemsSource):
                case nameof(SearchHandler.ItemTemplate):
                case nameof(SearchHandler.DisplayMemberName):
                case nameof(SearchHandler.ShowsResults):
                    RefreshSuggestions();
                    break;
                case nameof(SearchHandler.IsFocused):
                case nameof(SearchHandler.SelectedItem):
                    break;
                case nameof(SearchHandler.SearchBoxVisibility):
                    ApplyAppearance();
                    _shell.InvalidateAccessibleChildren();
                    _shell.InvalidateMeasure();
                    _shell.Invalidate();
                    break;
                default:
                    ApplyAppearance();
                    break;
            }
        }

        /// <summary>
        /// The handler's colours, font, alignment, keyboard, placeholder and enabled state on the
        /// box (Windows' AutoSuggestBoxExtensions.UpdateSearchHandler*).
        /// </summary>
        private void ApplyAppearance()
        {
            var h = _handler;
            if (h == null)
                return;
            _entry.Placeholder = h.Placeholder ?? string.Empty;
            _entry.PlaceholderColor = h.PlaceholderColor;
            _entry.TextColor = h.TextColor;
            _entry.FontFamily = h.FontFamily ?? string.Empty;
            _entry.FontSize = h.FontSize > 0 ? h.FontSize : FontSize;
            _entry.FontAttributes = h.FontAttributes;
            _entry.CharacterSpacing = h.CharacterSpacing;
            _entry.HorizontalTextAlignment = h.HorizontalTextAlignment;
            _entry.VerticalTextAlignment = h.VerticalTextAlignment;
            _entry.Keyboard = h.Keyboard ?? Keyboard.Default;
            bool enabled = h.IsSearchEnabled;
            IsEnabled = enabled;
            _entry.IsEnabled = enabled;
            if (!enabled || h.SearchBoxVisibility == SearchBoxVisibility.Hidden)
            {
                CloseList();
                ReleaseFocus();
            }
            InvalidateAccessibleChildren();
            Invalidate();
        }

        private static string Transform(string? text, TextTransform transform) => transform switch
        {
            TextTransform.Uppercase => (text ?? string.Empty).ToUpperInvariant(),
            TextTransform.Lowercase => (text ?? string.Empty).ToLowerInvariant(),
            _ => text ?? string.Empty,
        };

        /// <summary>The box shows the query (Windows' UpdateSearchHandlerText); a programmatic change, which does not set the query again.</summary>
        private void UpdateText()
        {
            if (_handler == null)
                return;
            var text = Transform(_handler.Query, _handler.TextTransform);
            if (_entry.Text == text)
                return;
            if (_inEntryTextChanged)
            {
                // The query was set from the box's own change: apply the transform once that is done.
                Dispatcher?.Dispatch(UpdateText);
                return;
            }
            SetTextProgrammatically(text);
        }

        private void SetTextProgrammatically(string text)
        {
            _suppressQuery = true;
            try
            {
                _entry.Text = text;
                _entry.CursorPosition = text.Length;
            }
            finally
            {
                _suppressQuery = false;
            }
        }

        /// <summary>Windows' OnSearchBoxTextChanged: a change the user made sets the query.</summary>
        private void OnEntryTextChanged(object? sender, EventArgs e)
        {
            if (_suppressQuery || _handler == null)
                return;
            _inEntryTextChanged = true;
            try
            {
                _handler.Query = _entry.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "Setting SearchHandler.Query failed", ex);
            }
            finally
            {
                _inEntryTextChanged = false;
            }
            // Typing opens the suggestions again (WinUI opens the list on user input).
            _dismissed = false;
            UpdateListOpen();
        }

        /// <summary>SearchHandler.Focus() / Unfocus(): the entry takes or gives up the window's focus.</summary>
        private void OnFocusChangeRequested(object? sender, VisualElement.FocusRequestArgs e)
        {
            var context = Microsoft.Maui.Platform.Linux.Handlers.FocusCommands.ContextOf(_entry);
            if (e.Focus)
            {
                if (context != null && _shell.IsSearchBoxShown && _entry.IsEnabled)
                    context.FocusedView = _entry;
                e.Result = context != null && ReferenceEquals(context.FocusedView, _entry);
            }
            else
            {
                if (context != null && ReferenceEquals(context.FocusedView, _entry))
                    context.FocusedView = null;
                e.Result = true;
            }
        }

        /// <summary>The entry gives up the window's focus (the box went away or was disabled).</summary>
        internal void ReleaseFocus()
        {
            if (!_entry.IsFocused)
                return;
            var context = Microsoft.Maui.Platform.Linux.Handlers.FocusCommands.ContextOf(_entry);
            if (context != null && ReferenceEquals(context.FocusedView, _entry))
                context.FocusedView = null;
            else
                _entry.OnFocusLost();
        }

        internal void OnEntryFocusChanged(bool focused)
        {
            try
            {
                _handler?.SetIsFocused(focused);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "Reporting the search box's focus failed", ex);
            }
            if (!focused)
                CloseList();
            InvalidateAccessibleChildren();
            Invalidate();
        }

        // ----- Suggestions -----

        private void OnListProxyChanged(object? sender, ListProxyChangedEventArgs e) => RefreshSuggestions();

        private void OnListCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshSuggestions();

        private void ObserveList(INotifyCollectionChanged? list)
        {
            if (ReferenceEquals(list, _observedList))
                return;
            if (_observedList != null)
                _observedList.CollectionChanged -= OnListCollectionChanged;
            _observedList = list;
            if (list != null)
                list.CollectionChanged += OnListCollectionChanged;
        }

        /// <summary>
        /// Reads the suggestions (Windows' UpdateShowsResults: ItemsSource while ShowsResults, none
        /// otherwise), each realised from ItemTemplate with the item as its BindingContext, else its
        /// DisplayMemberName value or its text.
        /// </summary>
        private void RefreshSuggestions()
        {
            var h = _handler;
            var old = _rows.ToList();
            _rows.Clear();
            if (h != null && h.ShowsResults)
            {
                var proxy = ((ISearchHandlerController)h).ListProxy;
                ObserveList(proxy as INotifyCollectionChanged);
                if (proxy != null)
                {
                    try
                    {
                        foreach (var item in proxy)
                            _rows.Add(RowFor(h, item, old));
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.Error("SkiaShell", "Reading the SearchHandler's ItemsSource failed", ex);
                    }
                }
            }
            else
            {
                ObserveList(null);
            }
            foreach (var gone in old.Where(r => !_rows.Contains(r)))
                ReleaseRow(gone);
            if (_highlight >= _rows.Count)
                _highlight = _rows.Count - 1;
            _hoverRow = -1;
            UpdateListOpen();
            InvalidateAccessibleChildren();
            Invalidate();
        }

        private SuggestionRow RowFor(SearchHandler h, object? item, List<SuggestionRow> old)
        {
            var template = h.ItemTemplate;
            if (template is DataTemplateSelector selector)
                template = selector.SelectTemplate(item, h);
            var reused = old.FirstOrDefault(r => ReferenceEquals(r.Item, item) && ReferenceEquals(r.Template, template) && !_rows.Contains(r));
            if (reused != null && (template == null || reused.View != null))
            {
                reused.Text = TextOf(h, item);
                return reused;
            }

            var row = new SuggestionRow { Item = item, Text = TextOf(h, item), Template = template };
            if (template != null && _shell.ViewRenderer is { } renderer)
            {
                try
                {
                    if (template.CreateContent() is Microsoft.Maui.Controls.View view)
                    {
                        // As Windows' ItemContentControl: the item is the view's BindingContext.
                        view.BindingContext = item;
                        row.MauiView = view;
                        row.View = view.Handler?.PlatformView as SkiaView ?? renderer(view);
                        if (row.View is { Parent: null } platform)
                            platform.Parent = this;
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("SkiaShell", "Realising a SearchHandler.ItemTemplate failed", ex);
                }
            }
            return row;
        }

        private void ReleaseRow(SuggestionRow row)
        {
            if (row.View is { } view && ReferenceEquals(view.Parent, this))
                view.Parent = null;
            try
            {
                row.MauiView?.Handler?.DisconnectHandler();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("SkiaShell", "Disconnecting a suggestion view failed", ex);
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
                ReleaseRow(row);
            _rows.Clear();
            _highlight = -1;
            _hoverRow = -1;
        }

        /// <summary>The text of a suggestion: its DisplayMemberName value (a property path), else its own text.</summary>
        private static string TextOf(SearchHandler h, object? item)
        {
            if (item == null)
                return string.Empty;
            var path = h.DisplayMemberName;
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    object? value = item;
                    foreach (var part in path.Split('.'))
                    {
                        if (value == null)
                            break;
                        value = value.GetType().GetProperty(part)?.GetValue(value);
                    }
                    return value?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Debug("SkiaShell", $"Reading DisplayMemberName '{path}' failed", ex);
                }
            }
            return item.ToString() ?? string.Empty;
        }

        /// <summary>The list is open while the focused box has suggestions and was not dismissed.</summary>
        private void UpdateListOpen()
        {
            bool open = _handler is { ShowsResults: true } && _rows.Count > 0 && _entry.IsFocused
                && !_dismissed && IsEnabled && _shell.IsSearchBoxShown;
            if (open == _listOpen)
            {
                if (open)
                    LayoutList();
                return;
            }
            if (open)
            {
                _listOpen = true;
                _listScroll = 0;
                _highlight = -1;
                LayoutList();
                RegisterPopupOverlay(this, DrawListOverlay);
            }
            else
            {
                CloseList();
                return;
            }
            InvalidateAccessibleChildren();
            Invalidate();
        }

        /// <summary>Closes the suggestion list (and unregisters its overlay).</summary>
        internal void CloseList()
        {
            if (!_listOpen)
                return;
            _listOpen = false;
            _highlight = -1;
            _hoverRow = -1;
            _rowRects.Clear();
            UnregisterPopupOverlay(this);
            InvalidateAccessibleChildren();
            Invalidate();
        }

        private void Dismiss()
        {
            _dismissed = true;
            CloseList();
        }

        /// <summary>
        /// Windows' OnSearchBoxSuggestionChosen: the box is cleared (AutoSuggestBox has no
        /// TextMemberPath, so a programmatic change that leaves the query as it was) and the item is
        /// reported through ISearchHandlerController.ItemSelected, which also confirms the query on
        /// this platform (WinUI raises QuerySubmitted after SuggestionChosen).
        /// </summary>
        private void ChooseSuggestion(int index)
        {
            if (_handler == null || index < 0 || index >= _rows.Count)
                return;
            var item = _rows[index].Item;
            Dismiss();
            SetTextProgrammatically(string.Empty);
            try
            {
                ((ISearchHandlerController)_handler).ItemSelected(item!);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "SearchHandler.OnItemSelected failed", ex);
            }
        }

        /// <summary>Windows' OnSearchBoxQuerySubmitted: ISearchHandlerController.QueryConfirmed (OnQueryConfirmed, Command).</summary>
        private void ConfirmQuery()
        {
            if (_handler == null)
                return;
            Dismiss();
            try
            {
                ((ISearchHandlerController)_handler).QueryConfirmed();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "SearchHandler.OnQueryConfirmed failed", ex);
            }
        }

        // ----- Layout -----

        internal void Layout(SKRect rect)
        {
            Arrange(new Rect(rect.Left, rect.Top, rect.Width, rect.Height));
            var entryRect = new Rect(rect.Left, rect.Top, Math.Max(0, rect.Width - ButtonWidth * 2), rect.Height);
            _entry.Measure(new Size(entryRect.Width, entryRect.Height));
            _entry.Arrange(entryRect);
            if (_listOpen)
                LayoutList();
        }

        private void LayoutList()
        {
            _rowRects.Clear();
            if (!_listOpen)
                return;
            float width = Math.Max((float)Bounds.Width, 160f);
            float left = (float)Bounds.Left;
            var shellBounds = _shell.Bounds;
            left = Math.Clamp(left, (float)shellBounds.Left, Math.Max((float)shellBounds.Left, (float)shellBounds.Right - width));
            float top = (float)Bounds.Bottom + 2f;
            float rowWidth = width - ListPadding * 2;

            float total = 0;
            foreach (var row in _rows)
            {
                row.Height = RowHeight;
                if (row.View is { } view)
                {
                    var desired = view.Measure(new Size(rowWidth, double.PositiveInfinity));
                    if (desired.Height > 0 && !double.IsInfinity(desired.Height))
                        row.Height = (float)desired.Height;
                }
                total += row.Height;
            }
            float max = Math.Max(RowHeight + ListPadding * 2, (float)shellBounds.Bottom - top - 8f);
            float height = Math.Min(total + ListPadding * 2, max);
            _listRect = new SKRect(left, top, left + width, top + height);
            _listScroll = Math.Clamp(_listScroll, 0f, Math.Max(0f, total + ListPadding * 2 - height));

            float y = top + ListPadding - _listScroll;
            foreach (var row in _rows)
            {
                _rowRects.Add(new SKRect(left + ListPadding, y, left + width - ListPadding, y + row.Height));
                y += row.Height;
            }
        }

        private void ScrollIntoView(int index)
        {
            LayoutList();
            if (index < 0 || index >= _rowRects.Count)
                return;
            var row = _rowRects[index];
            float clipTop = _listRect.Top + ListPadding, clipBottom = _listRect.Bottom - ListPadding;
            if (row.Top < clipTop)
                _listScroll -= clipTop - row.Top;
            else if (row.Bottom > clipBottom)
                _listScroll += row.Bottom - clipBottom;
            LayoutList();
        }

        // ----- Keyboard (the entry's keys come here first) -----

        /// <summary>Down/Up move through the suggestions, Enter picks or confirms, Escape closes the list.</summary>
        internal bool HandleKey(KeyEventArgs e)
        {
            if (_handler == null)
                return false;
            switch (e.Key)
            {
                case Key.Down:
                    if (_rows.Count == 0 || !_handler.ShowsResults)
                        return false;
                    if (!_listOpen)
                    {
                        _dismissed = false;
                        UpdateListOpen();
                        if (!_listOpen)
                            return false;
                    }
                    _highlight = Math.Min(_rows.Count - 1, _highlight + 1);
                    ScrollIntoView(_highlight);
                    break;
                case Key.Up:
                    if (!_listOpen)
                        return false;
                    _highlight = Math.Max(-1, _highlight - 1);
                    ScrollIntoView(_highlight);
                    break;
                case Key.Enter:
                    if (_listOpen && _highlight >= 0)
                        ChooseSuggestion(_highlight);
                    else
                        ConfirmQuery();
                    break;
                case Key.Escape:
                    if (!_listOpen)
                        return false;
                    Dismiss();
                    break;
                default:
                    return false;
            }
            e.Handled = true;
            InvalidateAccessibleChildren();
            Invalidate();
            return true;
        }

        // ----- Pointer -----

        public override SkiaView? HitTest(float x, float y)
        {
            if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y))
                return null;
            if (_entry.Bounds.Contains(x, y))
                return _entry.HitTestAt(x, y) ?? this;
            return this;
        }

        /// <summary>
        /// While the list is open it takes the presses on it and outside the box (one outside closes
        /// it); the box itself keeps its own input. A list whose box went away is closed.
        /// </summary>
        protected override bool HitTestPopupArea(float x, float y)
        {
            if (!IsListLive())
            {
                CloseList();
                return false;
            }
            if (_listRect.Contains(x, y))
                return true;
            return !Bounds.Contains(x, y);
        }

        private bool IsListLive()
        {
            if (!_listOpen || !_shell.IsSearchBoxShown || !ReferenceEquals(_shell._searchBox, this))
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

        private int RowAt(float x, float y)
        {
            if (!_listRect.Contains(x, y))
                return -1;
            for (int i = 0; i < _rowRects.Count; i++)
                if (_rowRects[i].Contains(x, y))
                    return i;
            return -1;
        }

        private bool ShowsClearButton => _entry.IsFocused && !string.IsNullOrEmpty(_entry.Text);

        private bool ShowsClearPlaceholder =>
            _handler is { ClearPlaceholderEnabled: true, ClearPlaceholderIcon: { IsEmpty: false } } && string.IsNullOrEmpty(_entry.Text);

        public override void OnPointerPressed(PointerEventArgs e)
        {
            e.Handled = true;
            if (_listOpen)
            {
                LayoutList();
                if (_listRect.Contains(e.X, e.Y))
                {
                    int row = RowAt(e.X, e.Y);
                    if (row >= 0)
                        ChooseSuggestion(row);
                    return;
                }
                if (!Bounds.Contains(e.X, e.Y))
                {
                    // WinUI's light dismiss.
                    Dismiss();
                    return;
                }
            }
            if (!IsEnabled || _handler == null)
                return;
            if (ClearButtonBounds.Contains(e.X, e.Y) && ShowsClearButton)
            {
                // The text box's delete button: a change the user made, so the query follows.
                _entry.Text = string.Empty;
                return;
            }
            if (ClearButtonBounds.Contains(e.X, e.Y) && ShowsClearPlaceholder)
            {
                try
                {
                    ((ISearchHandlerController)_handler).ClearPlaceholderClicked();
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("SkiaShell", "SearchHandler.OnClearPlaceholderClicked failed", ex);
                }
                return;
            }
            if (QueryIconBounds.Contains(e.X, e.Y))
            {
                // WinUI: the query icon submits the query.
                ConfirmQuery();
                return;
            }
            // Elsewhere in the box: the text box takes the focus.
            if (Microsoft.Maui.Platform.Linux.Handlers.FocusCommands.ContextOf(_entry) is { } context)
                context.FocusedView = _entry;
        }

        public override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_listOpen)
                return;
            int row = RowAt(e.X, e.Y);
            if (row != _hoverRow)
            {
                _hoverRow = row;
                Invalidate();
            }
        }

        public override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (_hoverRow != -1)
            {
                _hoverRow = -1;
                Invalidate();
            }
        }

        public override void OnScroll(ScrollEventArgs e)
        {
            if (_listOpen && _listRect.Contains(e.X, e.Y))
            {
                _listScroll += e.DeltaY * 30f;
                LayoutList();
                e.Handled = true;
                Invalidate();
                return;
            }
            base.OnScroll(e);
        }

        // ----- Drawing -----

        private SKPoint WindowOffset
        {
            get
            {
                var screen = ScreenBounds;
                return new SKPoint((float)(screen.Left - Bounds.Left), (float)(screen.Top - Bounds.Top));
            }
        }

        protected override void OnDraw(SKCanvas canvas, SKRect bounds)
        {
            var h = _handler;
            var text = SkiaTheme.CurrentTextSK;
            var background = h?.BackgroundColor?.ToSKColor() ?? SkiaTheme.InputBackgroundSK;
            if (!IsEnabled)
                background = background.WithAlpha((byte)(background.Alpha * 0.6f));

            // A WinUI text box: 4px corners, a hairline border, the accent underline while focused.
            using (var fill = new SKPaint { Color = background, Style = SKPaintStyle.Fill, IsAntialias = true })
                canvas.DrawRoundRect(bounds, 4f, 4f, fill);
            using (var border = new SKPaint { Color = SkiaTheme.CurrentBorderSK, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
                canvas.DrawRoundRect(bounds, 4f, 4f, border);
            if (_entry.IsFocused)
            {
                using var accent = new SKPaint { Color = SkiaTheme.PrimarySK, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(bounds, 4f, 4f), antialias: true);
                canvas.DrawRect(SKRect.Create(bounds.Left, bounds.Bottom - 2f, bounds.Width, 2f), accent);
                canvas.Restore();
            }

            var iconColor = IsEnabled ? text.WithAlpha(200) : text.WithAlpha(92);
            Icons.Retain(new[] { h?.QueryIcon, h?.ClearPlaceholderIcon });

            var clear = ClearButtonBounds;
            if (ShowsClearButton)
            {
                // The delete button, in CancelButtonColor (Windows' UpdateSearchHandlerCancelButtonColor).
                var color = h?.CancelButtonColor?.ToSKColor() ?? iconColor;
                using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
                float s = 4.5f;
                canvas.DrawLine(clear.MidX - s, clear.MidY - s, clear.MidX + s, clear.MidY + s, paint);
                canvas.DrawLine(clear.MidX + s, clear.MidY - s, clear.MidX - s, clear.MidY + s, paint);
            }
            else if (ShowsClearPlaceholder && Icons.Get(h!.ClearPlaceholderIcon) is { } placeholderIcon)
            {
                TabIconCache.DrawTinted(canvas, placeholderIcon, SKRect.Create(clear.MidX - 8f, clear.MidY - 8f, 16f, 16f), iconColor);
            }

            // The query icon: QueryIcon, else WinUI's Find symbol.
            var query = QueryIconBounds;
            if (h?.QueryIcon is { IsEmpty: false } && Icons.Get(h.QueryIcon) is { } queryIcon)
            {
                TabIconCache.DrawTinted(canvas, queryIcon, SKRect.Create(query.MidX - 8f, query.MidY - 8f, 16f, 16f), iconColor);
            }
            else
            {
                using var paint = new SKPaint { Color = iconColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
                float r = 5f;
                var center = new SKPoint(query.MidX - 1.5f, query.MidY - 1.5f);
                canvas.DrawCircle(center, r, paint);
                canvas.DrawLine(center.X + r * 0.7f, center.Y + r * 0.7f, center.X + r * 1.6f, center.Y + r * 1.6f, paint);
            }
        }

        private void DrawListOverlay(SKCanvas canvas)
        {
            if (!IsListLive() || _rows.Count == 0)
                return;
            LayoutList();
            var offset = WindowOffset;
            canvas.Save();
            canvas.Translate(offset.X, offset.Y);

            using (var shadow = new SKPaint
            {
                Color = SkiaTheme.Shadow25SK,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateBlur(6f, 6f),
            })
                canvas.DrawRoundRect(SKRect.Create(_listRect.Left, _listRect.Top + 2f, _listRect.Width, _listRect.Height), 8f, 8f, shadow);
            using (var surface = new SKPaint { Color = SkiaTheme.CurrentSurfaceSK, Style = SKPaintStyle.Fill, IsAntialias = true })
                canvas.DrawRoundRect(_listRect, 8f, 8f, surface);
            using (var border = new SKPaint { Color = SkiaTheme.CurrentBorderSK, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
                canvas.DrawRoundRect(_listRect, 8f, 8f, border);

            canvas.Save();
            canvas.ClipRect(SKRect.Create(_listRect.Left, _listRect.Top + ListPadding, _listRect.Width, Math.Max(0f, _listRect.Height - ListPadding * 2)));
            var text = SkiaTheme.CurrentTextSK;
            using var font = SkiaFontFactory.Create(FontSize);
            using var textPaint = new SKPaint { IsAntialias = true, Color = _handler?.TextColor?.ToSKColor() ?? text };
            for (int i = 0; i < _rows.Count && i < _rowRects.Count; i++)
            {
                var rect = _rowRects[i];
                if (rect.Bottom < _listRect.Top || rect.Top > _listRect.Bottom)
                    continue;
                var highlight = rect;
                highlight.Inflate(0f, -2f);
                if (i == _highlight || i == _hoverRow)
                {
                    using var hover = new SKPaint { Color = text.WithAlpha((byte)(i == _highlight ? 20 : 12)), Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRoundRect(highlight, 4f, 4f, hover);
                }
                if (i == _highlight)
                {
                    using var focus = new SKPaint { Color = text, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    canvas.DrawRoundRect(highlight, 4f, 4f, focus);
                }

                var row = _rows[i];
                if (row.View is { } view)
                {
                    view.Arrange(new Rect(rect.Left, rect.Top, rect.Width, rect.Height));
                    view.Draw(canvas);
                }
                else
                {
                    canvas.Save();
                    canvas.ClipRect(rect);
                    canvas.DrawText(row.Text, rect.Left + RowPadding, TextRenderingHelper.BaselineForVerticalCenter(font, rect.MidY), SKTextAlign.Left, font, textPaint);
                    canvas.Restore();
                }
            }
            canvas.Restore();
            canvas.Restore();
        }

        // ----- Accessibility -----

        protected override AccessibleRole GetAccessibleRole() => AccessibleRole.ComboBox;

        protected override string GetDefaultAccessibleName() => _handler?.Placeholder ?? string.Empty;

        protected override AccessibleStates GetAccessibleStates()
        {
            var states = base.GetAccessibleStates() | AccessibleStates.Showing | AccessibleStates.Expandable;
            if (IsEnabled)
                states |= AccessibleStates.Sensitive;
            if (_listOpen)
                states |= AccessibleStates.Expanded;
            return states;
        }

        protected override List<IAccessible> GetAccessibleChildren()
        {
            var children = new List<IAccessible> { _entry };
            if (_listOpen && _rows.Count > 0)
                children.Add(new ListAccessible(this));
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
        }

        /// <summary>The open suggestion list: a list of list items.</summary>
        private sealed class ListAccessible : PartAccessible
        {
            private readonly ShellSearchBoxView _box;

            public ListAccessible(ShellSearchBoxView box) : base(box) => _box = box;

            public override string AccessibleName => _box._handler?.Placeholder ?? string.Empty;
            public override AccessibleRole Role => AccessibleRole.List;
            public override AccessibleStates States => AccessibleStates.Visible | AccessibleStates.Showing | AccessibleStates.Enabled | AccessibleStates.Sensitive;
            public override AccessibleRect Bounds => _box.ToScreen(_box._listRect);

            public override IReadOnlyList<IAccessible> Children =>
                _box._rows.Select((r, i) => (IAccessible)new RowAccessible(_box, this, r)).ToList();
        }

        private sealed class RowAccessible : PartAccessible
        {
            private readonly ShellSearchBoxView _box;
            private readonly SuggestionRow _row;

            public RowAccessible(ShellSearchBoxView box, IAccessible list, SuggestionRow row) : base(list)
            {
                _box = box;
                _row = row;
            }

            private int Index => _box._rows.IndexOf(_row);

            public override string AccessibleName => _row.Text;
            public override AccessibleRole Role => AccessibleRole.ListItem;

            public override AccessibleStates States
            {
                get
                {
                    var states = AccessibleStates.Visible | AccessibleStates.Showing | AccessibleStates.Enabled
                        | AccessibleStates.Sensitive | AccessibleStates.Selectable;
                    if (Index >= 0 && Index == _box._highlight)
                        states |= AccessibleStates.Selected | AccessibleStates.Focused;
                    return states;
                }
            }

            public override AccessibleRect Bounds
            {
                get
                {
                    int index = Index;
                    return index >= 0 && index < _box._rowRects.Count ? _box.ToScreen(_box._rowRects[index]) : default;
                }
            }

            public override IReadOnlyList<AccessibleAction> Actions =>
                [new AccessibleAction { Name = "click", Description = "Choose the suggestion" }];

            public override bool DoAction(string actionName)
            {
                if (actionName != "click")
                    return false;
                int index = Index;
                if (index < 0)
                    return false;
                _box.ChooseSuggestion(index);
                return true;
            }
        }

        /// <summary>The search box's text box: an OpenMaui entry whose keys reach the box first.</summary>
        private sealed class ShellSearchEntry : SkiaEntry
        {
            private readonly ShellSearchBoxView _box;

            public ShellSearchEntry(ShellSearchBoxView box) => _box = box;

            public override void OnKeyDown(KeyEventArgs e)
            {
                if (!e.Handled && _box.HandleKey(e))
                    return;
                base.OnKeyDown(e);
            }

            public override void OnFocusGained()
            {
                base.OnFocusGained();
                _box.OnEntryFocusChanged(true);
            }

            public override void OnFocusLost()
            {
                base.OnFocusLost();
                _box.OnEntryFocusChanged(false);
            }

            protected override AccessibleRole GetAccessibleRole() => AccessibleRole.Entry;

            protected override string GetDefaultAccessibleName() => Placeholder ?? string.Empty;

            protected override AccessibleStates GetAccessibleStates() =>
                base.GetAccessibleStates() | AccessibleStates.Editable | AccessibleStates.SingleLine | AccessibleStates.SupportsAutocompletion;
        }
    }
}
