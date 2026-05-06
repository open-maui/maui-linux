// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public static class VirtualizationExtensions
{
    public static (int first, int last) CalculateVisibleRange(float scrollOffset, float viewportHeight, float itemHeight, float itemSpacing, int totalItems)
    {
        if (totalItems == 0)
        {
            return (-1, -1);
        }

        var totalItemHeight = itemHeight + itemSpacing;
        var first = Math.Max(0, (int)(scrollOffset / totalItemHeight));
        var last = Math.Min(totalItems - 1, (int)((scrollOffset + viewportHeight) / totalItemHeight) + 1);

        return (first, last);
    }

    public static (int first, int last) CalculateVisibleRangeVariable(float scrollOffset, float viewportHeight, Func<int, float> getItemHeight, float itemSpacing, int totalItems)
    {
        if (totalItems == 0)
        {
            return (-1, -1);
        }

        var firstVisible = 0;
        var currentOffset = 0f;

        for (var i = 0; i < totalItems; i++)
        {
            var height = getItemHeight(i);
            if (currentOffset + height > scrollOffset)
            {
                firstVisible = i;
                break;
            }
            currentOffset += height + itemSpacing;
        }

        var lastVisible = firstVisible;
        var endOffset = scrollOffset + viewportHeight;

        for (var i = firstVisible; i < totalItems; i++)
        {
            var height = getItemHeight(i);
            if (currentOffset > endOffset)
            {
                break;
            }
            lastVisible = i;
            currentOffset += height + itemSpacing;
        }

        return (firstVisible, lastVisible);
    }

    public static (int firstRow, int lastRow) CalculateVisibleGridRange(float scrollOffset, float viewportHeight, float rowHeight, float rowSpacing, int totalRows)
    {
        if (totalRows == 0)
        {
            return (-1, -1);
        }

        var totalRowHeight = rowHeight + rowSpacing;
        var firstRow = Math.Max(0, (int)(scrollOffset / totalRowHeight));
        var lastRow = Math.Min(totalRows - 1, (int)((scrollOffset + viewportHeight) / totalRowHeight) + 1);

        return (firstRow, lastRow);
    }
}
