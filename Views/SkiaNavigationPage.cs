using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Platform.Linux;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaNavigationPage : SkiaView
{
	private readonly Stack<SkiaPage> _navigationStack = new Stack<SkiaPage>();

	private SkiaPage? _currentPage;

	private bool _isAnimating;

	private float _animationProgress;

	private SkiaPage? _incomingPage;

	private bool _isPushAnimation;

	private SKColor _barBackgroundColor = new SKColor((byte)33, (byte)150, (byte)243);

	private SKColor _barTextColor = SKColors.White;

	private float _navigationBarHeight = 56f;

	private bool _showBackButton = true;

	public SKColor BarBackgroundColor
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _barBackgroundColor;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_barBackgroundColor = value;
			UpdatePageNavigationBar();
			Invalidate();
		}
	}

	public SKColor BarTextColor
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _barTextColor;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_barTextColor = value;
			UpdatePageNavigationBar();
			Invalidate();
		}
	}

	public float NavigationBarHeight
	{
		get
		{
			return _navigationBarHeight;
		}
		set
		{
			_navigationBarHeight = value;
			UpdatePageNavigationBar();
			Invalidate();
		}
	}

	public SkiaPage? CurrentPage => _currentPage;

	public SkiaPage? RootPage
	{
		get
		{
			if (_navigationStack.Count <= 0)
			{
				return _currentPage;
			}
			return _navigationStack.Last();
		}
	}

	public int StackDepth => _navigationStack.Count + ((_currentPage != null) ? 1 : 0);

	public event EventHandler<NavigationEventArgs>? Pushed;

	public event EventHandler<NavigationEventArgs>? Popped;

	public event EventHandler<NavigationEventArgs>? PoppedToRoot;

	public SkiaNavigationPage()
	{
	}//IL_0018: Unknown result type (might be due to invalid IL or missing references)
	//IL_001d: Unknown result type (might be due to invalid IL or missing references)
	//IL_0023: Unknown result type (might be due to invalid IL or missing references)
	//IL_0028: Unknown result type (might be due to invalid IL or missing references)


	public SkiaNavigationPage(SkiaPage rootPage)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		SetRootPage(rootPage);
	}

	public void SetRootPage(SkiaPage page)
	{
		_navigationStack.Clear();
		_currentPage?.OnDisappearing();
		_currentPage = page;
		_currentPage.Parent = this;
		ConfigurePage(_currentPage, showBackButton: false);
		_currentPage.OnAppearing();
		Invalidate();
	}

	public void Push(SkiaPage page, bool animated = true)
	{
		if (!_isAnimating)
		{
			if (LinuxApplication.IsGtkMode)
			{
				animated = false;
			}
			if (_currentPage != null)
			{
				_currentPage.OnDisappearing();
				_navigationStack.Push(_currentPage);
			}
			ConfigurePage(page, showBackButton: true);
			page.Parent = this;
			if (animated)
			{
				_incomingPage = page;
				_isPushAnimation = true;
				_animationProgress = 0f;
				_isAnimating = true;
				AnimatePush();
			}
			else
			{
				Console.WriteLine("[SkiaNavigationPage] Push (no animation): setting _currentPage to " + page.Title);
				_currentPage = page;
				_currentPage.OnAppearing();
				Console.WriteLine("[SkiaNavigationPage] Push: calling Invalidate");
				Invalidate();
				Console.WriteLine("[SkiaNavigationPage] Push: Invalidate called, _currentPage is now " + _currentPage?.Title);
			}
			this.Pushed?.Invoke(this, new NavigationEventArgs(page));
		}
	}

	public SkiaPage? Pop(bool animated = true)
	{
		if (_isAnimating || _navigationStack.Count == 0)
		{
			return null;
		}
		if (LinuxApplication.IsGtkMode)
		{
			animated = false;
		}
		SkiaPage currentPage = _currentPage;
		currentPage?.OnDisappearing();
		SkiaPage skiaPage = _navigationStack.Pop();
		if (animated && currentPage != null)
		{
			_incomingPage = skiaPage;
			_isPushAnimation = false;
			_animationProgress = 0f;
			_isAnimating = true;
			AnimatePop(currentPage);
		}
		else
		{
			_currentPage = skiaPage;
			_currentPage?.OnAppearing();
			Invalidate();
		}
		if (currentPage != null)
		{
			this.Popped?.Invoke(this, new NavigationEventArgs(currentPage));
		}
		return currentPage;
	}

	public void PopToRoot(bool animated = true)
	{
		if (!_isAnimating && _navigationStack.Count != 0)
		{
			_currentPage?.OnDisappearing();
			SkiaPage skiaPage = null;
			while (_navigationStack.Count > 0)
			{
				skiaPage = _navigationStack.Pop();
			}
			if (skiaPage != null)
			{
				_currentPage = skiaPage;
				ConfigurePage(_currentPage, showBackButton: false);
				_currentPage.OnAppearing();
				Invalidate();
			}
			this.PoppedToRoot?.Invoke(this, new NavigationEventArgs(_currentPage));
		}
	}

	private void ConfigurePage(SkiaPage page, bool showBackButton)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		page.ShowNavigationBar = true;
		page.TitleBarColor = _barBackgroundColor;
		page.TitleTextColor = _barTextColor;
		page.NavigationBarHeight = _navigationBarHeight;
		_showBackButton = showBackButton && _navigationStack.Count > 0;
	}

	private void UpdatePageNavigationBar()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (_currentPage != null)
		{
			_currentPage.TitleBarColor = _barBackgroundColor;
			_currentPage.TitleTextColor = _barTextColor;
			_currentPage.NavigationBarHeight = _navigationBarHeight;
		}
	}

	private async void AnimatePush()
	{
		DateTime startTime = DateTime.Now;
		while (_animationProgress < 1f)
		{
			await Task.Delay(16);
			double totalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
			_animationProgress = Math.Min(1f, (float)(totalMilliseconds / 250.0));
			Invalidate();
		}
		_currentPage = _incomingPage;
		_incomingPage = null;
		_isAnimating = false;
		_currentPage?.OnAppearing();
		Invalidate();
	}

	private async void AnimatePop(SkiaPage outgoingPage)
	{
		DateTime startTime = DateTime.Now;
		while (_animationProgress < 1f)
		{
			await Task.Delay(16);
			double totalMilliseconds = (DateTime.Now - startTime).TotalMilliseconds;
			_animationProgress = Math.Min(1f, (float)(totalMilliseconds / 250.0));
			Invalidate();
		}
		_currentPage = _incomingPage;
		_incomingPage = null;
		_isAnimating = false;
		_currentPage?.OnAppearing();
		outgoingPage.Parent = null;
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		if (base.BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = base.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (_isAnimating && _incomingPage != null)
		{
			float num = EaseOutCubic(_animationProgress);
			if (_isPushAnimation)
			{
				float num2 = (0f - ((SKRect)(ref bounds)).Width) * num;
				float num3 = ((SKRect)(ref bounds)).Width * (1f - num);
				if (_currentPage != null)
				{
					canvas.Save();
					canvas.Translate(num2, 0f);
					_currentPage.Bounds = bounds;
					_currentPage.Draw(canvas);
					canvas.Restore();
				}
				canvas.Save();
				canvas.Translate(num3, 0f);
				_incomingPage.Bounds = bounds;
				_incomingPage.Draw(canvas);
				canvas.Restore();
			}
			else
			{
				float num4 = (0f - ((SKRect)(ref bounds)).Width) * (1f - num);
				float num5 = ((SKRect)(ref bounds)).Width * num;
				canvas.Save();
				canvas.Translate(num4, 0f);
				_incomingPage.Bounds = bounds;
				_incomingPage.Draw(canvas);
				canvas.Restore();
				if (_currentPage != null)
				{
					canvas.Save();
					canvas.Translate(num5, 0f);
					_currentPage.Bounds = bounds;
					_currentPage.Draw(canvas);
					canvas.Restore();
				}
			}
		}
		else if (_currentPage != null)
		{
			Console.WriteLine("[SkiaNavigationPage] OnDraw: drawing _currentPage=" + _currentPage.Title);
			_currentPage.Bounds = bounds;
			_currentPage.Draw(canvas);
			if (_showBackButton && _navigationStack.Count > 0)
			{
				DrawBackButton(canvas, bounds);
			}
		}
	}

	private void DrawBackButton(SKCanvas canvas, SKRect bounds)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left + 8f, ((SKRect)(ref bounds)).Top + 12f, ((SKRect)(ref bounds)).Left + 48f, ((SKRect)(ref bounds)).Top + _navigationBarHeight - 12f);
		SKPaint val2 = new SKPaint
		{
			Color = _barTextColor,
			Style = (SKPaintStyle)1,
			StrokeWidth = 2.5f,
			IsAntialias = true,
			StrokeCap = (SKStrokeCap)1
		};
		try
		{
			float midY = ((SKRect)(ref val)).MidY;
			float num = 10f;
			float num2 = ((SKRect)(ref val)).Left + 8f;
			SKPath val3 = new SKPath();
			try
			{
				val3.MoveTo(num2 + num, midY - num);
				val3.LineTo(num2, midY);
				val3.LineTo(num2 + num, midY + num);
				canvas.DrawPath(val3, val2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private static float EaseOutCubic(float t)
	{
		return 1f - (float)Math.Pow(1f - t, 3.0);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return availableSize;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		Console.WriteLine($"[SkiaNavigationPage] OnPointerPressed at ({e.X}, {e.Y}), _isAnimating={_isAnimating}");
		if (!_isAnimating)
		{
			if (_showBackButton && _navigationStack.Count > 0 && e.X < 56f && e.Y < _navigationBarHeight)
			{
				Console.WriteLine("[SkiaNavigationPage] Back button clicked");
				Pop();
			}
			else
			{
				Console.WriteLine("[SkiaNavigationPage] Forwarding to _currentPage: " + ((object)_currentPage)?.GetType().Name);
				_currentPage?.OnPointerPressed(e);
			}
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (!_isAnimating)
		{
			_currentPage?.OnPointerMoved(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (!_isAnimating)
		{
			_currentPage?.OnPointerReleased(e);
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (!_isAnimating)
		{
			if ((e.Key == Key.Escape || e.Key == Key.Backspace) && _navigationStack.Count > 0)
			{
				Pop();
				e.Handled = true;
			}
			else
			{
				_currentPage?.OnKeyDown(e);
			}
		}
	}

	public override void OnKeyUp(KeyEventArgs e)
	{
		if (!_isAnimating)
		{
			_currentPage?.OnKeyUp(e);
		}
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		if (!_isAnimating)
		{
			_currentPage?.OnScroll(e);
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		if (!base.IsVisible)
		{
			return null;
		}
		if (_showBackButton && _navigationStack.Count > 0 && x < 56f && y < _navigationBarHeight)
		{
			return this;
		}
		if (_currentPage != null)
		{
			try
			{
				SkiaView skiaView = _currentPage.HitTest(x, y);
				if (skiaView != null)
				{
					return skiaView;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[SkiaNavigationPage] HitTest error: " + ex.Message);
			}
		}
		return this;
	}
}
