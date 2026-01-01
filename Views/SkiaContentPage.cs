using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaContentPage : SkiaPage
{
	private readonly List<SkiaToolbarItem> _toolbarItems = new List<SkiaToolbarItem>();

	public IList<SkiaToolbarItem> ToolbarItems => _toolbarItems;

	protected override void DrawNavigationBar(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = base.TitleBarColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			if (!string.IsNullOrEmpty(base.Title))
			{
				SKFont val2 = new SKFont(SKTypeface.Default, 20f, 1f, 0f);
				try
				{
					SKPaint val3 = new SKPaint(val2)
					{
						Color = base.TitleTextColor,
						IsAntialias = true
					};
					try
					{
						SKRect val4 = default(SKRect);
						val3.MeasureText(base.Title, ref val4);
						float num = ((SKRect)(ref bounds)).Left + 56f;
						float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY;
						canvas.DrawText(base.Title, num, num2, val3);
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
			DrawToolbarItems(canvas, bounds);
			SKPaint val5 = new SKPaint
			{
				Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)30),
				Style = (SKPaintStyle)0,
				MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 2f)
			};
			try
			{
				canvas.DrawRect(new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom + 4f), val5);
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawToolbarItems(SKCanvas canvas, SKRect navBarBounds)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Expected O, but got Unknown
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		List<SkiaToolbarItem> list = _toolbarItems.Where((SkiaToolbarItem t) => t.Order == SkiaToolbarItemOrder.Primary).ToList();
		Console.WriteLine($"[SkiaContentPage] DrawToolbarItems: {list.Count} primary items, navBarBounds={navBarBounds}");
		if (list.Count == 0)
		{
			return;
		}
		SKFont val = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val)
			{
				Color = base.TitleTextColor,
				IsAntialias = true
			};
			try
			{
				float num = ((SKRect)(ref navBarBounds)).Right - 16f;
				SKRect val3 = default(SKRect);
				foreach (SkiaToolbarItem item in list.AsEnumerable().Reverse())
				{
					float num3;
					if (item.Icon != null)
					{
						float num2 = 40f;
						num3 = num - num2;
						item.HitBounds = new SKRect(num3, ((SKRect)(ref navBarBounds)).Top, num, ((SKRect)(ref navBarBounds)).Bottom);
						float num4 = num3 + (num2 - 24f) / 2f;
						float num5 = ((SKRect)(ref navBarBounds)).MidY - 12f;
						((SKRect)(ref val3))._002Ector(num4, num5, num4 + 24f, num5 + 24f);
						SKPaint val4 = new SKPaint
						{
							IsAntialias = true
						};
						try
						{
							canvas.DrawBitmap(item.Icon, val3, val4);
						}
						finally
						{
							((IDisposable)val4)?.Dispose();
						}
					}
					else
					{
						SKRect val5 = default(SKRect);
						val2.MeasureText(item.Text, ref val5);
						float num2 = ((SKRect)(ref val5)).Width + 24f;
						num3 = num - num2;
						item.HitBounds = new SKRect(num3, ((SKRect)(ref navBarBounds)).Top, num, ((SKRect)(ref navBarBounds)).Bottom);
						float num6 = num3 + 12f;
						float num7 = ((SKRect)(ref navBarBounds)).MidY - ((SKRect)(ref val5)).MidY;
						canvas.DrawText(item.Text, num6, num7, val2);
					}
					Console.WriteLine($"[SkiaContentPage] Toolbar item '{item.Text}' HitBounds set to {item.HitBounds}");
					num = num3 - 8f;
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine($"[SkiaContentPage] OnPointerPressed at ({e.X}, {e.Y}), ShowNavigationBar={base.ShowNavigationBar}, NavigationBarHeight={base.NavigationBarHeight}");
		Console.WriteLine($"[SkiaContentPage] ToolbarItems count: {_toolbarItems.Count}");
		if (base.ShowNavigationBar && e.Y < base.NavigationBarHeight)
		{
			Console.WriteLine("[SkiaContentPage] In navigation bar area, checking toolbar items");
			foreach (SkiaToolbarItem item in _toolbarItems.Where((SkiaToolbarItem t) => t.Order == SkiaToolbarItemOrder.Primary))
			{
				SKRect hitBounds = item.HitBounds;
				bool flag = ((SKRect)(ref hitBounds)).Contains(e.X, e.Y);
				Console.WriteLine($"[SkiaContentPage] Checking item '{item.Text}', HitBounds=({((SKRect)(ref hitBounds)).Left},{((SKRect)(ref hitBounds)).Top},{((SKRect)(ref hitBounds)).Right},{((SKRect)(ref hitBounds)).Bottom}), Click=({e.X},{e.Y}), Contains={flag}, Command={item.Command != null}");
				if (flag)
				{
					Console.WriteLine("[SkiaContentPage] Toolbar item clicked: " + item.Text);
					item.Command?.Execute(null);
					return;
				}
			}
			Console.WriteLine("[SkiaContentPage] No toolbar item hit");
		}
		base.OnPointerPressed(e);
	}
}
