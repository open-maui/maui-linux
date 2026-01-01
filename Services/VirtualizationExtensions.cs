using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class VirtualizationExtensions
{
	public static (int first, int last) CalculateVisibleRange(float scrollOffset, float viewportHeight, float itemHeight, float itemSpacing, int totalItems)
	{
		if (totalItems == 0)
		{
			return (first: -1, last: -1);
		}
		float num = itemHeight + itemSpacing;
		int item = Math.Max(0, (int)(scrollOffset / num));
		int item2 = Math.Min(totalItems - 1, (int)((scrollOffset + viewportHeight) / num) + 1);
		return (first: item, last: item2);
	}

	public static (int first, int last) CalculateVisibleRangeVariable(float scrollOffset, float viewportHeight, Func<int, float> getItemHeight, float itemSpacing, int totalItems)
	{
		if (totalItems == 0)
		{
			return (first: -1, last: -1);
		}
		int num = 0;
		float num2 = 0f;
		for (int i = 0; i < totalItems; i++)
		{
			float num3 = getItemHeight(i);
			if (num2 + num3 > scrollOffset)
			{
				num = i;
				break;
			}
			num2 += num3 + itemSpacing;
		}
		int item = num;
		float num4 = scrollOffset + viewportHeight;
		for (int j = num; j < totalItems; j++)
		{
			float num5 = getItemHeight(j);
			if (num2 > num4)
			{
				break;
			}
			item = j;
			num2 += num5 + itemSpacing;
		}
		return (first: num, last: item);
	}

	public static (int firstRow, int lastRow) CalculateVisibleGridRange(float scrollOffset, float viewportHeight, float rowHeight, float rowSpacing, int totalRows)
	{
		if (totalRows == 0)
		{
			return (firstRow: -1, lastRow: -1);
		}
		float num = rowHeight + rowSpacing;
		int item = Math.Max(0, (int)(scrollOffset / num));
		int item2 = Math.Min(totalRows - 1, (int)((scrollOffset + viewportHeight) / num) + 1);
		return (firstRow: item, lastRow: item2);
	}
}
