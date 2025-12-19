using System.Runtime.InteropServices;
using Microsoft.Maui.Platform;
using SkiaSharp;

var demo = new AllControlsDemo();
demo.Run();

class AllControlsDemo
{
    private IntPtr _display, _window, _gc;
    private int _screen, _width = 1024, _height = 768;
    private bool _running = true;
    private IntPtr _wmDeleteMessage, _pixelBuffer = IntPtr.Zero;
    private int _bufferSize = 0;

    private SkiaScrollView _scrollView = null!;
    private SkiaStackLayout _rootLayout = null!;
    private SkiaView? _pressedView = null;
    private SkiaView? _focusedView = null;
    private SkiaCollectionView _collectionView = null!;
    private SkiaDatePicker _datePicker = null!;
    private SkiaTimePicker _timePicker = null!;
    private SkiaPicker _picker = null!;
    private SkiaEntry _entry = null!;
    private SkiaSearchBar _searchBar = null!;
    private DateTime _lastMotionRender = DateTime.MinValue;

    public void Run()
    {
        try { InitializeX11(); CreateUI(); RunEventLoop(); }
        catch (Exception ex) { Console.WriteLine($"Error: {ex}"); }
        finally { Cleanup(); }
    }

    private void InitializeX11()
    {
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) throw new Exception("Cannot open X11 display");
        _screen = XDefaultScreen(_display);
        var root = XRootWindow(_display, _screen);
        _window = XCreateSimpleWindow(_display, root, 50, 50, (uint)_width, (uint)_height, 1,
            XBlackPixel(_display, _screen), XWhitePixel(_display, _screen));
        XStoreName(_display, _window, "MAUI Linux Demo - All Controls");
        XSelectInput(_display, _window, ExposureMask | KeyPressMask | KeyReleaseMask |
            ButtonPressMask | ButtonReleaseMask | PointerMotionMask | StructureNotifyMask);
        _gc = XCreateGC(_display, _window, 0, IntPtr.Zero);
        _wmDeleteMessage = XInternAtom(_display, "WM_DELETE_WINDOW", false);
        XSetWMProtocols(_display, _window, ref _wmDeleteMessage, 1);
        EnsurePixelBuffer(_width, _height);
        XMapWindow(_display, _window);
        XFlush(_display);
    }

    private void EnsurePixelBuffer(int w, int h)
    {
        int needed = w * h * 4;
        if (_pixelBuffer == IntPtr.Zero || _bufferSize < needed) {
            if (_pixelBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_pixelBuffer);
            _pixelBuffer = Marshal.AllocHGlobal(needed);
            _bufferSize = needed;
        }
    }

    private void CreateUI()
    {
        _scrollView = new SkiaScrollView { BackgroundColor = new SKColor(250, 250, 250) };
        _rootLayout = new SkiaStackLayout {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical,
            Spacing = 12, Padding = new SKRect(24, 24, 24, 24),
            BackgroundColor = new SKColor(250, 250, 250)
        };

        // Title
        _rootLayout.AddChild(new SkiaLabel { Text = "MAUI Linux Demo", FontSize = 28, IsBold = true,
            TextColor = new SKColor(25, 118, 210), RequestedHeight = 40 });

        // Basic Controls
        AddSection("Basic Controls");

        var button = new SkiaButton { Text = "Click Me!", RequestedHeight = 44 };
        button.Clicked += (s, e) => Console.WriteLine("Button clicked!");
        _rootLayout.AddChild(button);

        _rootLayout.AddChild(new SkiaLabel { Text = "This is a Label with some text", RequestedHeight = 24 });

        _entry = new SkiaEntry { Placeholder = "Type here...", RequestedHeight = 44 };
        _rootLayout.AddChild(_entry);

        // Toggle Controls
        AddSection("Toggle Controls");

        var checkbox = new SkiaCheckBox { IsChecked = true, RequestedHeight = 32 };
        _rootLayout.AddChild(checkbox);

        var switchCtrl = new SkiaSwitch { IsOn = true, RequestedHeight = 32 };
        _rootLayout.AddChild(switchCtrl);

        // Sliders
        AddSection("Sliders & Progress");

        var slider = new SkiaSlider { Value = 0.5, Minimum = 0, Maximum = 1, RequestedHeight = 40 };
        _rootLayout.AddChild(slider);

        var progress = new SkiaProgressBar { Progress = 0.7f, RequestedHeight = 16 };
        _rootLayout.AddChild(progress);

        // Pickers - These are the ones with popups
        AddSection("Pickers (click to open popups)");

        _datePicker = new SkiaDatePicker { Date = DateTime.Today, RequestedHeight = 44 };
        _rootLayout.AddChild(_datePicker);

        _timePicker = new SkiaTimePicker { Time = DateTime.Now.TimeOfDay, RequestedHeight = 44 };
        _rootLayout.AddChild(_timePicker);

        _picker = new SkiaPicker { Title = "Select a fruit...", RequestedHeight = 44 };
        _picker.SetItems(new[] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape" });
        _rootLayout.AddChild(_picker);

        // CollectionView
        AddSection("CollectionView (scroll with mouse wheel)");

        _collectionView = new SkiaCollectionView { RequestedHeight = 180, ItemHeight = 36 };
        var items = new List<string>();
        for (int i = 1; i <= 50; i++) items.Add($"Collection Item #{i}");
        _collectionView.ItemsSource = items;
        _rootLayout.AddChild(_collectionView);

        // Activity Indicator
        AddSection("Activity Indicator");
        var activity = new SkiaActivityIndicator { IsRunning = true, RequestedHeight = 50 };
        _rootLayout.AddChild(activity);

        // SearchBar
        AddSection("SearchBar");
        _searchBar = new SkiaSearchBar { Placeholder = "Search...", RequestedHeight = 44 };
        _rootLayout.AddChild(_searchBar);

        // Footer
        _rootLayout.AddChild(new SkiaLabel {
            Text = "Scroll this page to see all controls. ESC to exit.",
            FontSize = 12, TextColor = new SKColor(128, 128, 128), RequestedHeight = 30
        });

        _scrollView.Content = _rootLayout;
    }

    private void AddSection(string title)
    {
        _rootLayout.AddChild(new SkiaLabel {
            Text = title, FontSize = 16, IsBold = true,
            TextColor = new SKColor(55, 71, 79), RequestedHeight = 32
        });
    }

    private void RunEventLoop()
    {
        Console.WriteLine("MAUI Linux Demo running... ESC to quit");
        Console.WriteLine("- Click DatePicker/TimePicker/Picker to test popups");
        Console.WriteLine("- Use mouse wheel on CollectionView to scroll it");
        Console.WriteLine("- Use mouse wheel elsewhere to scroll the page");
        Render();
        var lastRender = DateTime.Now;
        while (_running) {
            while (XPending(_display) > 0) { XNextEvent(_display, out var ev); HandleEvent(ref ev); }

            // Continuous rendering for animations (ActivityIndicator, cursor blink, etc.)
            var now = DateTime.Now;
            if ((now - lastRender).TotalMilliseconds >= 50) // ~20 FPS for animations
            {
                lastRender = now;
                Render();
            }
            Thread.Sleep(8);
        }
    }

    private void HandleEvent(ref XEvent e)
    {
        switch (e.type)
        {
            case Expose: if (e.xexpose.count == 0) Render(); break;
            case ConfigureNotify:
                if (e.xconfigure.width != _width || e.xconfigure.height != _height) {
                    _width = e.xconfigure.width; _height = e.xconfigure.height;
                    EnsurePixelBuffer(_width, _height); Render();
                }
                break;
            case KeyPress:
                var keysym = XLookupKeysym(ref e.xkey, 0);
                if (keysym == 0xFF1B) { _running = false; break; } // ESC

                // Forward to focused view
                if (_focusedView != null)
                {
                    var key = KeysymToKey(keysym);
                    if (key != Key.Unknown)
                    {
                        _focusedView.OnKeyDown(new KeyEventArgs(key));
                        Render();
                    }

                    // Handle text input for printable characters
                    var ch = KeysymToChar(keysym, e.xkey.state);
                    if (ch != '\0')
                    {
                        _focusedView.OnTextInput(new TextInputEventArgs(ch.ToString()));
                        Render();
                    }
                }
                break;
            case ButtonPress:
                float sx = e.xbutton.x, sy = e.xbutton.y;
                if (e.xbutton.button == 4 || e.xbutton.button == 5) {
                    // Mouse wheel
                    var cvBounds = _collectionView.GetAbsoluteBounds();
                    bool overCV = sx >= cvBounds.Left && sx <= cvBounds.Right &&
                                  sy >= cvBounds.Top && sy <= cvBounds.Bottom;
                    float delta = (e.xbutton.button == 4) ? -1.5f : 1.5f;
                    if (overCV) {
                        _collectionView.OnScroll(new ScrollEventArgs(sx, sy, 0, delta));
                    } else {
                        _scrollView.ScrollY = Math.Max(0, _scrollView.ScrollY + (delta > 0 ? 40 : -40));
                    }
                    Render();
                } else {
                    // Check if clicking on popup areas first
                    bool handledPopup = HandlePopupClick(sx, sy);
                    if (!handledPopup) {
                        _pressedView = _scrollView.HitTest(sx, sy);
                        if (_pressedView != null && _pressedView != _scrollView) {
                            // Update focus
                            if (_pressedView != _focusedView && _pressedView.IsFocusable)
                            {
                                _focusedView?.OnFocusLost();
                                _focusedView = _pressedView;
                                _focusedView.OnFocusGained();
                            }
                            _pressedView.OnPointerPressed(new Microsoft.Maui.Platform.PointerEventArgs(sx, sy, Microsoft.Maui.Platform.PointerButton.Left));
                        }
                        else if (_pressedView == null || _pressedView == _scrollView)
                        {
                            // Clicked on empty area - clear focus
                            _focusedView?.OnFocusLost();
                            _focusedView = null;
                        }
                    }
                    Render();
                }
                break;
            case MotionNotify:
                // Forward drag events to pressed view (for sliders, etc.)
                if (_pressedView != null) {
                    // Close any open popups during drag to prevent glitches
                    if (_datePicker.IsOpen) _datePicker.IsOpen = false;
                    if (_timePicker.IsOpen) _timePicker.IsOpen = false;
                    if (_picker.IsOpen) _picker.IsOpen = false;

                    _pressedView.OnPointerMoved(new Microsoft.Maui.Platform.PointerEventArgs(e.xmotion.x, e.xmotion.y, Microsoft.Maui.Platform.PointerButton.Left));

                    // Throttle motion renders to prevent overwhelming the system
                    var now = DateTime.Now;
                    if ((now - _lastMotionRender).TotalMilliseconds >= 16) // ~60 FPS max for drag
                    {
                        _lastMotionRender = now;
                        Render();
                    }
                }
                break;
            case ButtonRelease:
                if (e.xbutton.button != 4 && e.xbutton.button != 5 && _pressedView != null) {
                    _pressedView.OnPointerReleased(new Microsoft.Maui.Platform.PointerEventArgs(e.xbutton.x, e.xbutton.y, Microsoft.Maui.Platform.PointerButton.Left));
                    _pressedView = null;
                    Render();
                }
                break;
            case ClientMessage:
                if (e.xclient.data_l0 == (long)_wmDeleteMessage) _running = false;
                break;
        }
    }

    private bool HandlePopupClick(float x, float y)
    {
        // Handle date picker popup clicks
        if (_datePicker.IsOpen)
        {
            var bounds = _datePicker.GetAbsoluteBounds();
            var popupRect = new SKRect(bounds.Left, bounds.Bottom + 4, bounds.Left + 280, bounds.Bottom + 324);
            if (x >= popupRect.Left && x <= popupRect.Right && y >= popupRect.Top && y <= popupRect.Bottom)
            {
                // Click inside popup - handle calendar navigation/selection
                HandleDatePickerPopupClick(x, y, bounds);
                return true;
            }
            else if (y >= bounds.Top && y <= bounds.Bottom && x >= bounds.Left && x <= bounds.Right)
            {
                // Click on picker button - toggle
                _datePicker.IsOpen = false;
                return true;
            }
            else
            {
                // Click outside - close
                _datePicker.IsOpen = false;
                return true;
            }
        }

        // Handle time picker popup clicks
        if (_timePicker.IsOpen)
        {
            var bounds = _timePicker.GetAbsoluteBounds();
            var popupRect = new SKRect(bounds.Left, bounds.Bottom + 4, bounds.Left + 280, bounds.Bottom + 364);
            if (y < popupRect.Top)
            {
                _timePicker.IsOpen = false;
                return true;
            }
        }

        // Handle dropdown picker popup clicks
        if (_picker.IsOpen)
        {
            var bounds = _picker.GetAbsoluteBounds();
            var dropdownRect = new SKRect(bounds.Left, bounds.Bottom + 4, bounds.Right, bounds.Bottom + 204);
            if (x >= dropdownRect.Left && x <= dropdownRect.Right && y >= dropdownRect.Top && y <= dropdownRect.Bottom)
            {
                // Click on item
                int itemIndex = (int)((y - dropdownRect.Top) / 40);
                if (itemIndex >= 0 && itemIndex < 7)
                {
                    _picker.SelectedIndex = itemIndex;
                }
                _picker.IsOpen = false;
                return true;
            }
            else if (y < dropdownRect.Top)
            {
                _picker.IsOpen = false;
                return true;
            }
        }

        return false;
    }

    private DateTime _displayMonth = DateTime.Today;

    private void HandleDatePickerPopupClick(float x, float y, SKRect pickerBounds)
    {
        var popupTop = pickerBounds.Bottom + 4;
        var headerHeight = 48f;
        var weekdayHeight = 30f;

        // Navigation arrows
        if (y >= popupTop && y < popupTop + headerHeight)
        {
            if (x < pickerBounds.Left + 40)
            {
                _displayMonth = _displayMonth.AddMonths(-1);
            }
            else if (x > pickerBounds.Left + 240)
            {
                _displayMonth = _displayMonth.AddMonths(1);
            }
            return;
        }

        // Day selection
        var daysTop = popupTop + headerHeight + weekdayHeight;
        if (y >= daysTop)
        {
            var cellWidth = 280f / 7;
            var cellHeight = 38f;
            var col = (int)((x - pickerBounds.Left) / cellWidth);
            var row = (int)((y - daysTop) / cellHeight);

            var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
            var startDayOfWeek = (int)firstDay.DayOfWeek;
            var dayIndex = row * 7 + col - startDayOfWeek + 1;
            var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

            if (dayIndex >= 1 && dayIndex <= daysInMonth)
            {
                _datePicker.Date = new DateTime(_displayMonth.Year, _displayMonth.Month, dayIndex);
                _datePicker.IsOpen = false;
            }
        }
    }

    private void Render()
    {
        _scrollView.Measure(new SKSize(_width, _height));
        _scrollView.Arrange(new SKRect(0, 0, _width, _height));

        var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info, _pixelBuffer, _width * 4);
        if (surface == null) return;
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(250, 250, 250));
        _scrollView.Draw(canvas);

        // Draw popups on top (outside of scrollview clipping)
        DrawPopups(canvas);

        canvas.Flush();

        var image = XCreateImage(_display, XDefaultVisual(_display, _screen),
            (uint)XDefaultDepth(_display, _screen), 2, 0, _pixelBuffer, (uint)_width, (uint)_height, 32, _width * 4);
        if (image != IntPtr.Zero) {
            XPutImage(_display, _window, _gc, image, 0, 0, 0, 0, (uint)_width, (uint)_height);
            XFree(image);
        }
        XFlush(_display);
    }

    private void DrawPopups(SKCanvas canvas)
    {
        // Draw DatePicker calendar popup
        if (_datePicker.IsOpen)
        {
            var bounds = _datePicker.GetAbsoluteBounds();
            DrawCalendarPopup(canvas, bounds);
        }

        // Draw TimePicker clock popup
        if (_timePicker.IsOpen)
        {
            var bounds = _timePicker.GetAbsoluteBounds();
            DrawTimePickerPopup(canvas, bounds);
        }

        // Draw Picker dropdown
        if (_picker.IsOpen)
        {
            var bounds = _picker.GetAbsoluteBounds();
            DrawPickerDropdown(canvas, bounds);
        }
    }

    private void DrawCalendarPopup(SKCanvas canvas, SKRect pickerBounds)
    {
        var popupRect = new SKRect(
            pickerBounds.Left, pickerBounds.Bottom + 4,
            pickerBounds.Left + 280, pickerBounds.Bottom + 324);

        // Shadow
        using var shadowPaint = new SKPaint {
            Color = new SKColor(0, 0, 0, 50),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
        };
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(popupRect.Left + 3, popupRect.Top + 3, popupRect.Right + 3, popupRect.Bottom + 3), 8), shadowPaint);

        // Background
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, 8), bgPaint);

        // Border
        using var borderPaint = new SKPaint { Color = new SKColor(200, 200, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, 8), borderPaint);

        // Header with month/year
        var headerRect = new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + 48);
        using var headerPaint = new SKPaint { Color = new SKColor(33, 150, 243), Style = SKPaintStyle.Fill };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(headerRect.Left, headerRect.Top, headerRect.Right, headerRect.Top + 16), 8));
        canvas.DrawRect(headerRect, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(headerRect.Left, headerRect.Top + 8, headerRect.Right, headerRect.Bottom), headerPaint);

        // Month/year text
        using var headerFont = new SKFont(SKTypeface.Default, 18);
        using var headerTextPaint = new SKPaint(headerFont) { Color = SKColors.White, IsAntialias = true };
        var monthYear = _displayMonth.ToString("MMMM yyyy");
        var textBounds = new SKRect();
        headerTextPaint.MeasureText(monthYear, ref textBounds);
        canvas.DrawText(monthYear, headerRect.MidX - textBounds.MidX, headerRect.MidY - textBounds.MidY, headerTextPaint);

        // Navigation arrows
        using var arrowPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        // Left arrow
        canvas.DrawLine(popupRect.Left + 24, headerRect.MidY, popupRect.Left + 18, headerRect.MidY, arrowPaint);
        canvas.DrawLine(popupRect.Left + 18, headerRect.MidY, popupRect.Left + 22, headerRect.MidY - 4, arrowPaint);
        canvas.DrawLine(popupRect.Left + 18, headerRect.MidY, popupRect.Left + 22, headerRect.MidY + 4, arrowPaint);
        // Right arrow
        canvas.DrawLine(popupRect.Right - 24, headerRect.MidY, popupRect.Right - 18, headerRect.MidY, arrowPaint);
        canvas.DrawLine(popupRect.Right - 18, headerRect.MidY, popupRect.Right - 22, headerRect.MidY - 4, arrowPaint);
        canvas.DrawLine(popupRect.Right - 18, headerRect.MidY, popupRect.Right - 22, headerRect.MidY + 4, arrowPaint);

        // Weekday headers
        var dayNames = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var cellWidth = 280f / 7;
        var weekdayTop = popupRect.Top + 48;
        using var weekdayFont = new SKFont(SKTypeface.Default, 12);
        using var weekdayPaint = new SKPaint(weekdayFont) { Color = new SKColor(128, 128, 128), IsAntialias = true };
        for (int i = 0; i < 7; i++)
        {
            var dayBounds = new SKRect();
            weekdayPaint.MeasureText(dayNames[i], ref dayBounds);
            var x = popupRect.Left + i * cellWidth + cellWidth / 2 - dayBounds.MidX;
            canvas.DrawText(dayNames[i], x, weekdayTop + 20, weekdayPaint);
        }

        // Days grid
        var daysTop = weekdayTop + 30;
        var cellHeight = 38f;
        var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
        var startDayOfWeek = (int)firstDay.DayOfWeek;
        var today = DateTime.Today;
        var selectedDate = _datePicker.Date;

        using var dayFont = new SKFont(SKTypeface.Default, 14);
        using var dayPaint = new SKPaint(dayFont) { IsAntialias = true };
        using var circlePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        for (int day = 1; day <= daysInMonth; day++)
        {
            var dayDate = new DateTime(_displayMonth.Year, _displayMonth.Month, day);
            var cellIndex = startDayOfWeek + day - 1;
            var row = cellIndex / 7;
            var col = cellIndex % 7;

            var cellX = popupRect.Left + col * cellWidth;
            var cellY = daysTop + row * cellHeight;
            var cellCenterX = cellX + cellWidth / 2;
            var cellCenterY = cellY + cellHeight / 2;

            var isSelected = dayDate.Date == selectedDate.Date;
            var isToday = dayDate.Date == today;

            // Draw selection/today circle
            if (isSelected)
            {
                circlePaint.Color = new SKColor(33, 150, 243);
                canvas.DrawCircle(cellCenterX, cellCenterY, 16, circlePaint);
            }
            else if (isToday)
            {
                circlePaint.Color = new SKColor(33, 150, 243, 60);
                canvas.DrawCircle(cellCenterX, cellCenterY, 16, circlePaint);
            }

            // Draw day number
            dayPaint.Color = isSelected ? SKColors.White : SKColors.Black;
            var dayText = day.ToString();
            var dayBounds = new SKRect();
            dayPaint.MeasureText(dayText, ref dayBounds);
            canvas.DrawText(dayText, cellCenterX - dayBounds.MidX, cellCenterY - dayBounds.MidY, dayPaint);
        }
    }

    private void DrawTimePickerPopup(SKCanvas canvas, SKRect pickerBounds)
    {
        var popupRect = new SKRect(
            pickerBounds.Left, pickerBounds.Bottom + 4,
            pickerBounds.Left + 280, pickerBounds.Bottom + 364);

        // Shadow
        using var shadowPaint = new SKPaint {
            Color = new SKColor(0, 0, 0, 50),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
        };
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(popupRect.Left + 3, popupRect.Top + 3, popupRect.Right + 3, popupRect.Bottom + 3), 8), shadowPaint);

        // Background
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, 8), bgPaint);

        // Header
        var headerRect = new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + 80);
        using var headerPaint = new SKPaint { Color = new SKColor(33, 150, 243), Style = SKPaintStyle.Fill };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(headerRect.Left, headerRect.Top, headerRect.Right, headerRect.Top + 16), 8));
        canvas.DrawRect(headerRect, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(headerRect.Left, headerRect.Top + 8, headerRect.Right, headerRect.Bottom), headerPaint);

        // Time display
        using var timeFont = new SKFont(SKTypeface.Default, 32);
        using var timePaint = new SKPaint(timeFont) { Color = SKColors.White, IsAntialias = true };
        var time = _timePicker.Time;
        var timeText = $"{time.Hours:D2}:{time.Minutes:D2}";
        var timeBounds = new SKRect();
        timePaint.MeasureText(timeText, ref timeBounds);
        canvas.DrawText(timeText, headerRect.MidX - timeBounds.MidX, headerRect.MidY - timeBounds.MidY, timePaint);

        // Clock face
        var clockCenterX = popupRect.MidX;
        var clockCenterY = popupRect.Top + 80 + 140;
        var clockRadius = 100f;

        using var clockBgPaint = new SKPaint { Color = new SKColor(245, 245, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(clockCenterX, clockCenterY, clockRadius + 20, clockBgPaint);

        // Hour numbers
        using var numFont = new SKFont(SKTypeface.Default, 14);
        using var numPaint = new SKPaint(numFont) { Color = SKColors.Black, IsAntialias = true };
        for (int i = 1; i <= 12; i++)
        {
            var angle = (i * 30 - 90) * Math.PI / 180;
            var x = clockCenterX + (float)(clockRadius * Math.Cos(angle));
            var y = clockCenterY + (float)(clockRadius * Math.Sin(angle));
            var numText = i.ToString();
            var numBounds = new SKRect();
            numPaint.MeasureText(numText, ref numBounds);
            canvas.DrawText(numText, x - numBounds.MidX, y - numBounds.MidY, numPaint);
        }

        // Clock hand
        var selectedHour = time.Hours % 12;
        if (selectedHour == 0) selectedHour = 12;
        var handAngle = (selectedHour * 30 - 90) * Math.PI / 180;
        var handEndX = clockCenterX + (float)((clockRadius - 20) * Math.Cos(handAngle));
        var handEndY = clockCenterY + (float)((clockRadius - 20) * Math.Sin(handAngle));

        using var handPaint = new SKPaint { Color = new SKColor(33, 150, 243), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawLine(clockCenterX, clockCenterY, handEndX, handEndY, handPaint);

        // Center dot
        handPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(clockCenterX, clockCenterY, 6, handPaint);

        // Selected hour highlight
        using var selPaint = new SKPaint { Color = new SKColor(33, 150, 243), Style = SKPaintStyle.Fill, IsAntialias = true };
        var selX = clockCenterX + (float)(clockRadius * Math.Cos(handAngle));
        var selY = clockCenterY + (float)(clockRadius * Math.Sin(handAngle));
        canvas.DrawCircle(selX, selY, 18, selPaint);
        numPaint.Color = SKColors.White;
        var selText = selectedHour.ToString();
        var selBounds = new SKRect();
        numPaint.MeasureText(selText, ref selBounds);
        canvas.DrawText(selText, selX - selBounds.MidX, selY - selBounds.MidY, numPaint);
    }

    private void DrawPickerDropdown(SKCanvas canvas, SKRect pickerBounds)
    {
        var items = new[] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape" };
        var itemHeight = 40f;
        var dropdownHeight = items.Length * itemHeight;

        var dropdownRect = new SKRect(
            pickerBounds.Left, pickerBounds.Bottom + 4,
            pickerBounds.Right, pickerBounds.Bottom + 4 + dropdownHeight);

        // Shadow
        using var shadowPaint = new SKPaint {
            Color = new SKColor(0, 0, 0, 50),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
        };
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(dropdownRect.Left + 3, dropdownRect.Top + 3, dropdownRect.Right + 3, dropdownRect.Bottom + 3), 4), shadowPaint);

        // Background
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), bgPaint);

        // Border
        using var borderPaint = new SKPaint { Color = new SKColor(200, 200, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), borderPaint);

        // Items
        using var itemFont = new SKFont(SKTypeface.Default, 14);
        using var itemPaint = new SKPaint(itemFont) { Color = SKColors.Black, IsAntialias = true };
        using var selBgPaint = new SKPaint { Color = new SKColor(33, 150, 243, 40), Style = SKPaintStyle.Fill };

        for (int i = 0; i < items.Length; i++)
        {
            var itemTop = dropdownRect.Top + i * itemHeight;
            var itemRect = new SKRect(dropdownRect.Left, itemTop, dropdownRect.Right, itemTop + itemHeight);

            if (i == _picker.SelectedIndex)
            {
                canvas.DrawRect(itemRect, selBgPaint);
            }

            var textBounds = new SKRect();
            itemPaint.MeasureText(items[i], ref textBounds);
            canvas.DrawText(items[i], itemRect.Left + 12, itemRect.MidY - textBounds.MidY, itemPaint);
        }
    }

    private Key KeysymToKey(ulong keysym)
    {
        return keysym switch
        {
            0xFF08 => Key.Backspace,
            0xFF09 => Key.Tab,
            0xFF0D => Key.Enter,
            0xFF1B => Key.Escape,
            0xFFFF => Key.Delete,
            0xFF50 => Key.Home,
            0xFF51 => Key.Left,
            0xFF52 => Key.Up,
            0xFF53 => Key.Right,
            0xFF54 => Key.Down,
            0xFF55 => Key.PageUp,
            0xFF56 => Key.PageDown,
            0xFF57 => Key.End,
            0x0020 => Key.Space,
            _ => Key.Unknown
        };
    }

    private char KeysymToChar(ulong keysym, uint state)
    {
        bool shift = (state & 1) != 0; // ShiftMask
        bool capsLock = (state & 2) != 0; // LockMask

        // Letters a-z / A-Z
        if (keysym >= 0x61 && keysym <= 0x7A) // a-z
        {
            char ch = (char)keysym;
            if (shift ^ capsLock) ch = char.ToUpper(ch);
            return ch;
        }

        // Numbers and symbols
        if (keysym >= 0x20 && keysym <= 0x7E)
        {
            if (shift)
            {
                return keysym switch
                {
                    0x31 => '!', 0x32 => '@', 0x33 => '#', 0x34 => '$', 0x35 => '%',
                    0x36 => '^', 0x37 => '&', 0x38 => '*', 0x39 => '(', 0x30 => ')',
                    0x2D => '_', 0x3D => '+', 0x5B => '{', 0x5D => '}', 0x5C => '|',
                    0x3B => ':', 0x27 => '"', 0x60 => '~', 0x2C => '<', 0x2E => '>',
                    0x2F => '?',
                    _ => (char)keysym
                };
            }
            return (char)keysym;
        }

        // Numpad
        if (keysym >= 0xFFB0 && keysym <= 0xFFB9)
            return (char)('0' + (keysym - 0xFFB0));

        return '\0';
    }

    private void Cleanup()
    {
        if (_pixelBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_pixelBuffer);
        if (_gc != IntPtr.Zero) XFreeGC(_display, _gc);
        if (_window != IntPtr.Zero) XDestroyWindow(_display, _window);
        if (_display != IntPtr.Zero) XCloseDisplay(_display);
    }

    const string LibX11 = "libX11.so.6";
    [DllImport(LibX11)] static extern IntPtr XOpenDisplay(IntPtr d);
    [DllImport(LibX11)] static extern int XCloseDisplay(IntPtr d);
    [DllImport(LibX11)] static extern int XDefaultScreen(IntPtr d);
    [DllImport(LibX11)] static extern IntPtr XRootWindow(IntPtr d, int s);
    [DllImport(LibX11)] static extern ulong XBlackPixel(IntPtr d, int s);
    [DllImport(LibX11)] static extern ulong XWhitePixel(IntPtr d, int s);
    [DllImport(LibX11)] static extern IntPtr XCreateSimpleWindow(IntPtr d, IntPtr p, int x, int y, uint w, uint h, uint bw, ulong b, ulong bg);
    [DllImport(LibX11)] static extern int XMapWindow(IntPtr d, IntPtr w);
    [DllImport(LibX11)] static extern int XStoreName(IntPtr d, IntPtr w, string n);
    [DllImport(LibX11)] static extern int XSelectInput(IntPtr d, IntPtr w, long m);
    [DllImport(LibX11)] static extern IntPtr XCreateGC(IntPtr d, IntPtr dr, ulong vm, IntPtr v);
    [DllImport(LibX11)] static extern int XFreeGC(IntPtr d, IntPtr gc);
    [DllImport(LibX11)] static extern int XFlush(IntPtr d);
    [DllImport(LibX11)] static extern int XPending(IntPtr d);
    [DllImport(LibX11)] static extern int XNextEvent(IntPtr d, out XEvent e);
    [DllImport(LibX11)] static extern ulong XLookupKeysym(ref XKeyEvent k, int i);
    [DllImport(LibX11)] static extern int XDestroyWindow(IntPtr d, IntPtr w);
    [DllImport(LibX11)] static extern IntPtr XDefaultVisual(IntPtr d, int s);
    [DllImport(LibX11)] static extern int XDefaultDepth(IntPtr d, int s);
    [DllImport(LibX11)] static extern IntPtr XCreateImage(IntPtr d, IntPtr v, uint dp, int f, int o, IntPtr data, uint w, uint h, int bp, int bpl);
    [DllImport(LibX11)] static extern int XPutImage(IntPtr d, IntPtr dr, IntPtr gc, IntPtr i, int sx, int sy, int dx, int dy, uint w, uint h);
    [DllImport(LibX11)] static extern int XFree(IntPtr data);
    [DllImport(LibX11)] static extern IntPtr XInternAtom(IntPtr d, string n, bool o);
    [DllImport(LibX11)] static extern int XSetWMProtocols(IntPtr d, IntPtr w, ref IntPtr p, int c);

    const long ExposureMask = 1L<<15, KeyPressMask = 1L<<0, KeyReleaseMask = 1L<<1;
    const long ButtonPressMask = 1L<<2, ButtonReleaseMask = 1L<<3, PointerMotionMask = 1L<<6, StructureNotifyMask = 1L<<17;
    const int KeyPress = 2, ButtonPress = 4, ButtonRelease = 5, MotionNotify = 6, Expose = 12, ConfigureNotify = 22, ClientMessage = 33;

    [StructLayout(LayoutKind.Explicit, Size = 192)] struct XEvent {
        [FieldOffset(0)] public int type; [FieldOffset(0)] public XExposeEvent xexpose;
        [FieldOffset(0)] public XConfigureEvent xconfigure; [FieldOffset(0)] public XKeyEvent xkey;
        [FieldOffset(0)] public XButtonEvent xbutton; [FieldOffset(0)] public XMotionEvent xmotion;
        [FieldOffset(0)] public XClientMessageEvent xclient;
    }
    [StructLayout(LayoutKind.Sequential)] struct XExposeEvent { public int type; public ulong serial; public int send_event; public IntPtr display, window; public int x, y, width, height, count; }
    [StructLayout(LayoutKind.Sequential)] struct XConfigureEvent { public int type; public ulong serial; public int send_event; public IntPtr display, evt, window; public int x, y, width, height, border_width; public IntPtr above; public int override_redirect; }
    [StructLayout(LayoutKind.Sequential)] struct XKeyEvent { public int type; public ulong serial; public int send_event; public IntPtr display, window, root, subwindow; public ulong time; public int x, y, x_root, y_root; public uint state, keycode; public int same_screen; }
    [StructLayout(LayoutKind.Sequential)] struct XButtonEvent { public int type; public ulong serial; public int send_event; public IntPtr display, window, root, subwindow; public ulong time; public int x, y, x_root, y_root; public uint state, button; public int same_screen; }
    [StructLayout(LayoutKind.Sequential)] struct XMotionEvent { public int type; public ulong serial; public int send_event; public IntPtr display, window, root, subwindow; public ulong time; public int x, y, x_root, y_root; public uint state; public byte is_hint; public int same_screen; }
    [StructLayout(LayoutKind.Sequential)] struct XClientMessageEvent { public int type; public ulong serial; public int send_event; public IntPtr display, window, message_type; public int format; public long data_l0, data_l1, data_l2, data_l3, data_l4; }
}
