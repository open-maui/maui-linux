using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaAbsoluteLayout : SkiaLayoutView
{
	private readonly Dictionary<SkiaView, AbsoluteLayoutBounds> _childBounds = new Dictionary<SkiaView, AbsoluteLayoutBounds>();

	public void AddChild(SkiaView child, SKRect bounds, AbsoluteLayoutFlags flags = AbsoluteLayoutFlags.None)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		base.AddChild(child);
		_childBounds[child] = new AbsoluteLayoutBounds(bounds, flags);
	}

	public override void RemoveChild(SkiaView child)
	{
		base.RemoveChild(child);
		_childBounds.Remove(child);
	}

	public AbsoluteLayoutBounds GetLayoutBounds(SkiaView child)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (!_childBounds.TryGetValue(child, out var value))
		{
			return new AbsoluteLayoutBounds(SKRect.Empty, AbsoluteLayoutFlags.None);
		}
		return value;
	}

	public void SetLayoutBounds(SkiaView child, SKRect bounds, AbsoluteLayoutFlags flags = AbsoluteLayoutFlags.None)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		_childBounds[child] = new AbsoluteLayoutBounds(bounds, flags);
		InvalidateMeasure();
		Invalidate();
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		float num = 0f;
		float num2 = 0f;
		foreach (SkiaView child in base.Children)
		{
			if (child.IsVisible)
			{
				SKRect bounds = GetLayoutBounds(child).Bounds;
				child.Measure(new SKSize(((SKRect)(ref bounds)).Width, ((SKRect)(ref bounds)).Height));
				num = Math.Max(num, ((SKRect)(ref bounds)).Right);
				num2 = Math.Max(num2, ((SKRect)(ref bounds)).Bottom);
			}
		}
		float num3 = num;
		SKRect padding = base.Padding;
		float num4 = num3 + ((SKRect)(ref padding)).Left;
		padding = base.Padding;
		float num5 = num4 + ((SKRect)(ref padding)).Right;
		float num6 = num2;
		padding = base.Padding;
		float num7 = num6 + ((SKRect)(ref padding)).Top;
		padding = base.Padding;
		return new SKSize(num5, num7 + ((SKRect)(ref padding)).Bottom);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		SKRect contentBounds = GetContentBounds(bounds);
		SKRect bounds3 = default(SKRect);
		foreach (SkiaView child in base.Children)
		{
			if (child.IsVisible)
			{
				AbsoluteLayoutBounds layoutBounds = GetLayoutBounds(child);
				SKRect bounds2 = layoutBounds.Bounds;
				AbsoluteLayoutFlags flags = layoutBounds.Flags;
				float num = ((!flags.HasFlag(AbsoluteLayoutFlags.XProportional)) ? (((SKRect)(ref contentBounds)).Left + ((SKRect)(ref bounds2)).Left) : (((SKRect)(ref contentBounds)).Left + ((SKRect)(ref bounds2)).Left * ((SKRect)(ref contentBounds)).Width));
				float num2 = ((!flags.HasFlag(AbsoluteLayoutFlags.YProportional)) ? (((SKRect)(ref contentBounds)).Top + ((SKRect)(ref bounds2)).Top) : (((SKRect)(ref contentBounds)).Top + ((SKRect)(ref bounds2)).Top * ((SKRect)(ref contentBounds)).Height));
				float num3;
				SKSize desiredSize;
				if (flags.HasFlag(AbsoluteLayoutFlags.WidthProportional))
				{
					num3 = ((SKRect)(ref bounds2)).Width * ((SKRect)(ref contentBounds)).Width;
				}
				else if (((SKRect)(ref bounds2)).Width < 0f)
				{
					desiredSize = child.DesiredSize;
					num3 = ((SKSize)(ref desiredSize)).Width;
				}
				else
				{
					num3 = ((SKRect)(ref bounds2)).Width;
				}
				float num4;
				if (flags.HasFlag(AbsoluteLayoutFlags.HeightProportional))
				{
					num4 = ((SKRect)(ref bounds2)).Height * ((SKRect)(ref contentBounds)).Height;
				}
				else if (((SKRect)(ref bounds2)).Height < 0f)
				{
					desiredSize = child.DesiredSize;
					num4 = ((SKSize)(ref desiredSize)).Height;
				}
				else
				{
					num4 = ((SKRect)(ref bounds2)).Height;
				}
				Thickness margin = child.Margin;
				((SKRect)(ref bounds3))._002Ector(num + (float)((Thickness)(ref margin)).Left, num2 + (float)((Thickness)(ref margin)).Top, num + num3 - (float)((Thickness)(ref margin)).Right, num2 + num4 - (float)((Thickness)(ref margin)).Bottom);
				child.Arrange(bounds3);
			}
		}
		return bounds;
	}
}
