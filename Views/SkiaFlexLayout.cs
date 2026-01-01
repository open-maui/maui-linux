using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaFlexLayout : SkiaLayoutView
{
	public static readonly BindableProperty DirectionProperty = BindableProperty.Create("Direction", typeof(FlexDirection), typeof(SkiaFlexLayout), (object)FlexDirection.Row, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaFlexLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty WrapProperty = BindableProperty.Create("Wrap", typeof(FlexWrap), typeof(SkiaFlexLayout), (object)FlexWrap.NoWrap, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaFlexLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty JustifyContentProperty = BindableProperty.Create("JustifyContent", typeof(FlexJustify), typeof(SkiaFlexLayout), (object)FlexJustify.Start, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaFlexLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AlignItemsProperty = BindableProperty.Create("AlignItems", typeof(FlexAlignItems), typeof(SkiaFlexLayout), (object)FlexAlignItems.Stretch, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaFlexLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AlignContentProperty = BindableProperty.Create("AlignContent", typeof(FlexAlignContent), typeof(SkiaFlexLayout), (object)FlexAlignContent.Stretch, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaFlexLayout)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty OrderProperty = BindableProperty.CreateAttached("Order", typeof(int), typeof(SkiaFlexLayout), (object)0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty GrowProperty = BindableProperty.CreateAttached("Grow", typeof(float), typeof(SkiaFlexLayout), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShrinkProperty = BindableProperty.CreateAttached("Shrink", typeof(float), typeof(SkiaFlexLayout), (object)1f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BasisProperty = BindableProperty.CreateAttached("Basis", typeof(FlexBasis), typeof(SkiaFlexLayout), (object)FlexBasis.Auto, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AlignSelfProperty = BindableProperty.CreateAttached("AlignSelf", typeof(FlexAlignSelf), typeof(SkiaFlexLayout), (object)FlexAlignSelf.Auto, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public FlexDirection Direction
	{
		get
		{
			return (FlexDirection)((BindableObject)this).GetValue(DirectionProperty);
		}
		set
		{
			((BindableObject)this).SetValue(DirectionProperty, (object)value);
		}
	}

	public FlexWrap Wrap
	{
		get
		{
			return (FlexWrap)((BindableObject)this).GetValue(WrapProperty);
		}
		set
		{
			((BindableObject)this).SetValue(WrapProperty, (object)value);
		}
	}

	public FlexJustify JustifyContent
	{
		get
		{
			return (FlexJustify)((BindableObject)this).GetValue(JustifyContentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(JustifyContentProperty, (object)value);
		}
	}

	public FlexAlignItems AlignItems
	{
		get
		{
			return (FlexAlignItems)((BindableObject)this).GetValue(AlignItemsProperty);
		}
		set
		{
			((BindableObject)this).SetValue(AlignItemsProperty, (object)value);
		}
	}

	public FlexAlignContent AlignContent
	{
		get
		{
			return (FlexAlignContent)((BindableObject)this).GetValue(AlignContentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(AlignContentProperty, (object)value);
		}
	}

	public static int GetOrder(SkiaView view)
	{
		return (int)((BindableObject)view).GetValue(OrderProperty);
	}

	public static void SetOrder(SkiaView view, int value)
	{
		((BindableObject)view).SetValue(OrderProperty, (object)value);
	}

	public static float GetGrow(SkiaView view)
	{
		return (float)((BindableObject)view).GetValue(GrowProperty);
	}

	public static void SetGrow(SkiaView view, float value)
	{
		((BindableObject)view).SetValue(GrowProperty, (object)value);
	}

	public static float GetShrink(SkiaView view)
	{
		return (float)((BindableObject)view).GetValue(ShrinkProperty);
	}

	public static void SetShrink(SkiaView view, float value)
	{
		((BindableObject)view).SetValue(ShrinkProperty, (object)value);
	}

	public static FlexBasis GetBasis(SkiaView view)
	{
		return (FlexBasis)((BindableObject)view).GetValue(BasisProperty);
	}

	public static void SetBasis(SkiaView view, FlexBasis value)
	{
		((BindableObject)view).SetValue(BasisProperty, (object)value);
	}

	public static FlexAlignSelf GetAlignSelf(SkiaView view)
	{
		return (FlexAlignSelf)((BindableObject)view).GetValue(AlignSelfProperty);
	}

	public static void SetAlignSelf(SkiaView view, FlexAlignSelf value)
	{
		((BindableObject)view).SetValue(AlignSelfProperty, (object)value);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		bool flag = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
		float num = 0f;
		float num2 = 0f;
		foreach (SkiaView child in base.Children)
		{
			if (child.IsVisible)
			{
				SKSize val = child.Measure(availableSize);
				if (flag)
				{
					num += ((SKSize)(ref val)).Width;
					num2 = Math.Max(num2, ((SKSize)(ref val)).Height);
				}
				else
				{
					num += ((SKSize)(ref val)).Height;
					num2 = Math.Max(num2, ((SKSize)(ref val)).Width);
				}
			}
		}
		if (!flag)
		{
			return new SKSize(num2, num);
		}
		return new SKSize(num, num2);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		if (base.Children.Count == 0)
		{
			return bounds;
		}
		bool flag = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
		bool flag2 = Direction == FlexDirection.RowReverse || Direction == FlexDirection.ColumnReverse;
		List<SkiaView> list = (from c in base.Children
			where c.IsVisible
			orderby GetOrder(c)
			select c).ToList();
		if (list.Count == 0)
		{
			return bounds;
		}
		float num = (flag ? ((SKRect)(ref bounds)).Width : ((SKRect)(ref bounds)).Height);
		float num2 = (flag ? ((SKRect)(ref bounds)).Height : ((SKRect)(ref bounds)).Width);
		List<(SkiaView, SKSize, float, float)> list2 = new List<(SkiaView, SKSize, float, float)>();
		float num3 = 0f;
		float num4 = 0f;
		float num5 = 0f;
		foreach (SkiaView item10 in list)
		{
			FlexBasis basis = GetBasis(item10);
			float grow = GetGrow(item10);
			float shrink = GetShrink(item10);
			SKSize item;
			if (basis.IsAuto)
			{
				item = item10.Measure(new SKSize(((SKRect)(ref bounds)).Width, ((SKRect)(ref bounds)).Height));
			}
			else
			{
				float length = basis.Length;
				item = (flag ? item10.Measure(new SKSize(length, ((SKRect)(ref bounds)).Height)) : item10.Measure(new SKSize(((SKRect)(ref bounds)).Width, length)));
			}
			list2.Add((item10, item, grow, shrink));
			num3 += (flag ? ((SKSize)(ref item)).Width : ((SKSize)(ref item)).Height);
			num4 += grow;
			num5 += shrink;
		}
		float num6 = num - num3;
		List<(SkiaView, float, float)> list3 = new List<(SkiaView, float, float)>();
		foreach (var item11 in list2)
		{
			SkiaView item2 = item11.Item1;
			SKSize item3 = item11.Item2;
			float item4 = item11.Item3;
			float item5 = item11.Item4;
			float num7 = (flag ? ((SKSize)(ref item3)).Width : ((SKSize)(ref item3)).Height);
			float item6 = (flag ? ((SKSize)(ref item3)).Height : ((SKSize)(ref item3)).Width);
			if (num6 > 0f && num4 > 0f)
			{
				num7 += num6 * (item4 / num4);
			}
			else if (num6 < 0f && num5 > 0f)
			{
				num7 += num6 * (item5 / num5);
			}
			list3.Add((item2, Math.Max(0f, num7), item6));
		}
		float num8 = list3.Sum<(SkiaView, float, float)>(((SkiaView child, float mainSize, float crossSize) s) => s.mainSize);
		float num9 = Math.Max(0f, num - num8);
		float num10 = (flag ? ((SKRect)(ref bounds)).Left : ((SKRect)(ref bounds)).Top);
		float num11 = 0f;
		switch (JustifyContent)
		{
		case FlexJustify.Center:
			num10 += num9 / 2f;
			break;
		case FlexJustify.End:
			num10 += num9;
			break;
		case FlexJustify.SpaceBetween:
			if (list3.Count > 1)
			{
				num11 = num9 / (float)(list3.Count - 1);
			}
			break;
		case FlexJustify.SpaceAround:
			if (list3.Count > 0)
			{
				num11 = num9 / (float)list3.Count;
				num10 += num11 / 2f;
			}
			break;
		case FlexJustify.SpaceEvenly:
			if (list3.Count > 0)
			{
				num11 = num9 / (float)(list3.Count + 1);
				num10 += num11;
			}
			break;
		}
		float num12 = num10;
		IEnumerable<(SkiaView, float, float)> enumerable2;
		if (!flag2)
		{
			IEnumerable<(SkiaView, float, float)> enumerable = list3;
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = list3.AsEnumerable().Reverse();
		}
		SKRect bounds2 = default(SKRect);
		foreach (var item12 in enumerable2)
		{
			SkiaView item7 = item12.Item1;
			float item8 = item12.Item2;
			float item9 = item12.Item3;
			FlexAlignSelf alignSelf = GetAlignSelf(item7);
			FlexAlignItems flexAlignItems = ((alignSelf == FlexAlignSelf.Auto) ? AlignItems : ((FlexAlignItems)alignSelf));
			float num13 = (flag ? ((SKRect)(ref bounds)).Top : ((SKRect)(ref bounds)).Left);
			float num14 = item9;
			switch (flexAlignItems)
			{
			case FlexAlignItems.End:
				num13 = (flag ? ((SKRect)(ref bounds)).Bottom : ((SKRect)(ref bounds)).Right) - num14;
				break;
			case FlexAlignItems.Center:
				num13 += (num2 - num14) / 2f;
				break;
			case FlexAlignItems.Stretch:
				num14 = num2;
				break;
			}
			if (flag)
			{
				((SKRect)(ref bounds2))._002Ector(num12, num13, num12 + item8, num13 + num14);
			}
			else
			{
				((SKRect)(ref bounds2))._002Ector(num13, num12, num13 + num14, num12 + item8);
			}
			item7.Arrange(bounds2);
			num12 += item8 + num11;
		}
		return bounds;
	}
}
