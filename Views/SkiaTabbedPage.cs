using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaTabbedPage : SkiaLayoutView
{
	private readonly List<TabItem> _tabs = new List<TabItem>();

	private int _selectedIndex;

	private float _tabBarHeight = 48f;

	private bool _tabBarOnBottom;

	public float TabBarHeight
	{
		get
		{
			return _tabBarHeight;
		}
		set
		{
			if (_tabBarHeight != value)
			{
				_tabBarHeight = value;
				InvalidateMeasure();
				Invalidate();
			}
		}
	}

	public bool TabBarOnBottom
	{
		get
		{
			return _tabBarOnBottom;
		}
		set
		{
			if (_tabBarOnBottom != value)
			{
				_tabBarOnBottom = value;
				Invalidate();
			}
		}
	}

	public int SelectedIndex
	{
		get
		{
			return _selectedIndex;
		}
		set
		{
			if (value >= 0 && value < _tabs.Count && _selectedIndex != value)
			{
				_selectedIndex = value;
				this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
				Invalidate();
			}
		}
	}

	public TabItem? SelectedTab
	{
		get
		{
			if (_selectedIndex < 0 || _selectedIndex >= _tabs.Count)
			{
				return null;
			}
			return _tabs[_selectedIndex];
		}
	}

	public IReadOnlyList<TabItem> Tabs => _tabs;

	public SKColor TabBarBackgroundColor { get; set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public SKColor SelectedTabColor { get; set; } = SKColors.White;

	public SKColor UnselectedTabColor { get; set; } = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)180);

	public SKColor IndicatorColor { get; set; } = SKColors.White;

	public float IndicatorHeight { get; set; } = 3f;

	public event EventHandler? SelectedIndexChanged;

	public void AddTab(string title, SkiaView content, string? iconPath = null)
	{
		TabItem item = new TabItem
		{
			Title = title,
			Content = content,
			IconPath = iconPath
		};
		_tabs.Add(item);
		AddChild(content);
		if (_tabs.Count == 1)
		{
			_selectedIndex = 0;
		}
		InvalidateMeasure();
		Invalidate();
	}

	public void RemoveTab(int index)
	{
		if (index >= 0 && index < _tabs.Count)
		{
			TabItem tabItem = _tabs[index];
			_tabs.RemoveAt(index);
			RemoveChild(tabItem.Content);
			if (_selectedIndex >= _tabs.Count)
			{
				_selectedIndex = Math.Max(0, _tabs.Count - 1);
			}
			InvalidateMeasure();
			Invalidate();
		}
	}

	public void ClearTabs()
	{
		foreach (TabItem tab in _tabs)
		{
			RemoveChild(tab.Content);
		}
		_tabs.Clear();
		_selectedIndex = 0;
		InvalidateMeasure();
		Invalidate();
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKSize)(ref availableSize)).Height - TabBarHeight;
		SKSize availableSize2 = default(SKSize);
		((SKSize)(ref availableSize2))._002Ector(((SKSize)(ref availableSize)).Width, num);
		foreach (TabItem tab in _tabs)
		{
			tab.Content.Measure(availableSize2);
		}
		return availableSize;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds2 = default(SKRect);
		if (TabBarOnBottom)
		{
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom - TabBarHeight);
		}
		else
		{
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + TabBarHeight, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
		}
		foreach (TabItem tab in _tabs)
		{
			tab.Content.Arrange(bounds2);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		DrawTabBar(canvas);
		if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
		{
			_tabs[_selectedIndex].Content.Draw(canvas);
		}
		canvas.Restore();
	}

	private void DrawTabBar(SKCanvas canvas)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Expected O, but got Unknown
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds;
		SKRect val = default(SKRect);
		if (TabBarOnBottom)
		{
			bounds = base.Bounds;
			float left = ((SKRect)(ref bounds)).Left;
			bounds = base.Bounds;
			float num = ((SKRect)(ref bounds)).Bottom - TabBarHeight;
			bounds = base.Bounds;
			float right = ((SKRect)(ref bounds)).Right;
			bounds = base.Bounds;
			((SKRect)(ref val))._002Ector(left, num, right, ((SKRect)(ref bounds)).Bottom);
		}
		else
		{
			bounds = base.Bounds;
			float left2 = ((SKRect)(ref bounds)).Left;
			bounds = base.Bounds;
			float top = ((SKRect)(ref bounds)).Top;
			bounds = base.Bounds;
			float right2 = ((SKRect)(ref bounds)).Right;
			bounds = base.Bounds;
			((SKRect)(ref val))._002Ector(left2, top, right2, ((SKRect)(ref bounds)).Top + TabBarHeight);
		}
		SKPaint val2 = new SKPaint
		{
			Color = TabBarBackgroundColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRect(val, val2);
			if (_tabs.Count == 0)
			{
				return;
			}
			float num2 = ((SKRect)(ref val)).Width / (float)_tabs.Count;
			SKPaint val3 = new SKPaint
			{
				IsAntialias = true,
				TextSize = 14f,
				Typeface = SKTypeface.Default
			};
			try
			{
				SKRect val4 = default(SKRect);
				for (int i = 0; i < _tabs.Count; i++)
				{
					TabItem tabItem = _tabs[i];
					((SKRect)(ref val4))._002Ector(((SKRect)(ref val)).Left + (float)i * num2, ((SKRect)(ref val)).Top, ((SKRect)(ref val)).Left + (float)(i + 1) * num2, ((SKRect)(ref val)).Bottom);
					bool flag = i == _selectedIndex;
					val3.Color = (flag ? SelectedTabColor : UnselectedTabColor);
					val3.FakeBoldText = flag;
					SKRect val5 = default(SKRect);
					val3.MeasureText(tabItem.Title, ref val5);
					float num3 = ((SKRect)(ref val4)).MidX - ((SKRect)(ref val5)).MidX;
					float num4 = ((SKRect)(ref val4)).MidY - ((SKRect)(ref val5)).MidY;
					canvas.DrawText(tabItem.Title, num3, num4, val3);
				}
				if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
				{
					SKPaint val6 = new SKPaint
					{
						Color = IndicatorColor,
						Style = (SKPaintStyle)0,
						IsAntialias = true
					};
					try
					{
						float num5 = ((SKRect)(ref val)).Left + (float)_selectedIndex * num2;
						float num6 = (TabBarOnBottom ? ((SKRect)(ref val)).Top : (((SKRect)(ref val)).Bottom - IndicatorHeight));
						SKRect val7 = new SKRect(num5, num6, num5 + num2, num6 + IndicatorHeight);
						canvas.DrawRect(val7, val6);
						return;
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
					}
				}
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

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
				SKRect val = default(SKRect);
				if (TabBarOnBottom)
				{
					bounds = base.Bounds;
					float left = ((SKRect)(ref bounds)).Left;
					bounds = base.Bounds;
					float num = ((SKRect)(ref bounds)).Bottom - TabBarHeight;
					bounds = base.Bounds;
					float right = ((SKRect)(ref bounds)).Right;
					bounds = base.Bounds;
					((SKRect)(ref val))._002Ector(left, num, right, ((SKRect)(ref bounds)).Bottom);
				}
				else
				{
					bounds = base.Bounds;
					float left2 = ((SKRect)(ref bounds)).Left;
					bounds = base.Bounds;
					float top = ((SKRect)(ref bounds)).Top;
					bounds = base.Bounds;
					float right2 = ((SKRect)(ref bounds)).Right;
					bounds = base.Bounds;
					((SKRect)(ref val))._002Ector(left2, top, right2, ((SKRect)(ref bounds)).Top + TabBarHeight);
				}
				if (((SKRect)(ref val)).Contains(x, y))
				{
					return this;
				}
				if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
				{
					SkiaView skiaView = _tabs[_selectedIndex].Content.HitTest(x, y);
					if (skiaView != null)
					{
						return skiaView;
					}
				}
				return this;
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsEnabled)
		{
			SKRect bounds;
			SKRect val = default(SKRect);
			if (TabBarOnBottom)
			{
				bounds = base.Bounds;
				float left = ((SKRect)(ref bounds)).Left;
				bounds = base.Bounds;
				float num = ((SKRect)(ref bounds)).Bottom - TabBarHeight;
				bounds = base.Bounds;
				float right = ((SKRect)(ref bounds)).Right;
				bounds = base.Bounds;
				((SKRect)(ref val))._002Ector(left, num, right, ((SKRect)(ref bounds)).Bottom);
			}
			else
			{
				bounds = base.Bounds;
				float left2 = ((SKRect)(ref bounds)).Left;
				bounds = base.Bounds;
				float top = ((SKRect)(ref bounds)).Top;
				bounds = base.Bounds;
				float right2 = ((SKRect)(ref bounds)).Right;
				bounds = base.Bounds;
				((SKRect)(ref val))._002Ector(left2, top, right2, ((SKRect)(ref bounds)).Top + TabBarHeight);
			}
			if (((SKRect)(ref val)).Contains(e.X, e.Y) && _tabs.Count > 0)
			{
				float num2 = ((SKRect)(ref val)).Width / (float)_tabs.Count;
				int value = (int)((e.X - ((SKRect)(ref val)).Left) / num2);
				value = Math.Clamp(value, 0, _tabs.Count - 1);
				SelectedIndex = value;
				e.Handled = true;
			}
			base.OnPointerPressed(e);
		}
	}
}
