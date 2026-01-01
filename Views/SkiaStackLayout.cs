using System;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaStackLayout : SkiaLayoutView
{
	public static readonly BindableProperty OrientationProperty = BindableProperty.Create("Orientation", typeof(StackOrientation), typeof(SkiaStackLayout), (object)StackOrientation.Vertical, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStackLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public StackOrientation Orientation
	{
		get
		{
			return (StackOrientation)((BindableObject)this).GetValue(OrientationProperty);
		}
		set
		{
			((BindableObject)this).SetValue(OrientationProperty, (object)value);
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		SKRect padding = base.Padding;
		float num;
		if (!float.IsNaN(((SKRect)(ref padding)).Left))
		{
			padding = base.Padding;
			num = ((SKRect)(ref padding)).Left;
		}
		else
		{
			num = 0f;
		}
		float num2 = num;
		padding = base.Padding;
		float num3;
		if (!float.IsNaN(((SKRect)(ref padding)).Right))
		{
			padding = base.Padding;
			num3 = ((SKRect)(ref padding)).Right;
		}
		else
		{
			num3 = 0f;
		}
		float num4 = num3;
		padding = base.Padding;
		float num5;
		if (!float.IsNaN(((SKRect)(ref padding)).Top))
		{
			padding = base.Padding;
			num5 = ((SKRect)(ref padding)).Top;
		}
		else
		{
			num5 = 0f;
		}
		float num6 = num5;
		padding = base.Padding;
		float num7;
		if (!float.IsNaN(((SKRect)(ref padding)).Bottom))
		{
			padding = base.Padding;
			num7 = ((SKRect)(ref padding)).Bottom;
		}
		else
		{
			num7 = 0f;
		}
		float num8 = num7;
		float num9 = ((SKSize)(ref availableSize)).Width - num2 - num4;
		float num10 = ((SKSize)(ref availableSize)).Height - num6 - num8;
		if (num9 < 0f || float.IsNaN(num9))
		{
			num9 = 0f;
		}
		if (num10 < 0f || float.IsNaN(num10))
		{
			num10 = 0f;
		}
		float num11 = 0f;
		float num12 = 0f;
		float num13 = 0f;
		float num14 = 0f;
		SKSize availableSize2 = default(SKSize);
		((SKSize)(ref availableSize2))._002Ector(num9, num10);
		foreach (SkiaView child in base.Children)
		{
			if (child.IsVisible)
			{
				SKSize val = child.Measure(availableSize2);
				float num15 = (float.IsNaN(((SKSize)(ref val)).Width) ? 0f : ((SKSize)(ref val)).Width);
				float num16 = (float.IsNaN(((SKSize)(ref val)).Height) ? 0f : ((SKSize)(ref val)).Height);
				if (Orientation == StackOrientation.Vertical)
				{
					num12 += num16;
					num13 = Math.Max(num13, num15);
				}
				else
				{
					num11 += num15;
					num14 = Math.Max(num14, num16);
				}
			}
		}
		int num17 = base.Children.Count((SkiaView c) => c.IsVisible);
		float num18 = (float)Math.Max(0, num17 - 1) * base.Spacing;
		if (Orientation == StackOrientation.Vertical)
		{
			num12 += num18;
			return new SKSize(num13 + num2 + num4, num12 + num6 + num8);
		}
		num11 += num18;
		return new SKSize(num11 + num2 + num4, num14 + num6 + num8);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected I4, but got Unknown
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		SKRect contentBounds = GetContentBounds(bounds);
		float num = ((float.IsInfinity(((SKRect)(ref contentBounds)).Width) || float.IsNaN(((SKRect)(ref contentBounds)).Width)) ? 800f : ((SKRect)(ref contentBounds)).Width);
		float num2 = ((float.IsInfinity(((SKRect)(ref contentBounds)).Height) || float.IsNaN(((SKRect)(ref contentBounds)).Height)) ? 600f : ((SKRect)(ref contentBounds)).Height);
		float num3 = 0f;
		SKRect val = default(SKRect);
		SKRect bounds2 = default(SKRect);
		foreach (SkiaView child in base.Children)
		{
			if (!child.IsVisible)
			{
				continue;
			}
			SKSize desiredSize = child.DesiredSize;
			float num4 = ((float.IsNaN(((SKSize)(ref desiredSize)).Width) || float.IsInfinity(((SKSize)(ref desiredSize)).Width)) ? num : ((SKSize)(ref desiredSize)).Width);
			float num5 = ((float.IsNaN(((SKSize)(ref desiredSize)).Height) || float.IsInfinity(((SKSize)(ref desiredSize)).Height)) ? num2 : ((SKSize)(ref desiredSize)).Height);
			if (Orientation == StackOrientation.Vertical)
			{
				float num6 = Math.Max(0f, num2 - num3);
				float num7 = ((child is SkiaScrollView) ? num6 : Math.Min(num5, (num6 > 0f) ? num6 : num5));
				((SKRect)(ref val))._002Ector(((SKRect)(ref contentBounds)).Left, ((SKRect)(ref contentBounds)).Top + num3, ((SKRect)(ref contentBounds)).Left + num, ((SKRect)(ref contentBounds)).Top + num3 + num7);
				num3 += num7 + base.Spacing;
			}
			else
			{
				float num8 = Math.Max(0f, num - num3);
				float num9 = ((child is SkiaScrollView) ? num8 : Math.Min(num4, (num8 > 0f) ? num8 : num4));
				float num10 = Math.Min(num5, num2);
				float num11 = ((SKRect)(ref contentBounds)).Top;
				float num12 = ((SKRect)(ref contentBounds)).Top + num10;
				LayoutOptions verticalOptions = child.VerticalOptions;
				switch ((int)((LayoutOptions)(ref verticalOptions)).Alignment)
				{
				case 1:
					num11 = ((SKRect)(ref contentBounds)).Top + (num2 - num10) / 2f;
					num12 = num11 + num10;
					break;
				case 2:
					num11 = ((SKRect)(ref contentBounds)).Top + num2 - num10;
					num12 = ((SKRect)(ref contentBounds)).Top + num2;
					break;
				case 3:
					num11 = ((SKRect)(ref contentBounds)).Top;
					num12 = ((SKRect)(ref contentBounds)).Top + num2;
					break;
				}
				((SKRect)(ref val))._002Ector(((SKRect)(ref contentBounds)).Left + num3, num11, ((SKRect)(ref contentBounds)).Left + num3 + num9, num12);
				num3 += num9 + base.Spacing;
			}
			Thickness margin = child.Margin;
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref val)).Left + (float)((Thickness)(ref margin)).Left, ((SKRect)(ref val)).Top + (float)((Thickness)(ref margin)).Top, ((SKRect)(ref val)).Right - (float)((Thickness)(ref margin)).Right, ((SKRect)(ref val)).Bottom - (float)((Thickness)(ref margin)).Bottom);
			child.Arrange(bounds2);
		}
		return bounds;
	}
}
