// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Reflection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of CollectionViewTests.Windows.cs.
	public partial class CollectionViewTests
	{
		// Windows: the bounds of the item's container (ItemContentControl). SkiaItemsView
		// has no container view: an item's template root is its child, placed in the
		// row slot the items view lays out (GetItemOffset / GetItemHeight).
		Rect GetCollectionViewCellBounds(IView cellContent)
		{
			var view = LinuxPlatform.View(cellContent.Handler!);
			if (!view.IsLoaded())
				throw new Exception("The cell is not in the visual tree");

			var itemRoot = view;
			while (itemRoot.Parent is not null and not SkiaItemsView)
				itemRoot = itemRoot.Parent;
			if (itemRoot.Parent is not SkiaItemsView itemsView)
				throw new XunitException($"{view.GetType().Name} is not in a SkiaItemsView.");

			var index = -1;
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var cache = (System.Collections.IDictionary)typeof(SkiaItemsView).GetField("_itemViewCache", flags)!.GetValue(itemsView)!;
			foreach (System.Collections.DictionaryEntry entry in cache)
				if (ReferenceEquals(entry.Value, itemRoot))
					index = (int)entry.Key;
			if (index < 0)
				throw new XunitException("SkiaItemsView exposes no row for the item view.");
			var offset = (float)typeof(SkiaItemsView).GetMethod("GetItemOffset", flags)!.Invoke(itemsView, new object[] { index })!;
			var height = (float)typeof(SkiaItemsView).GetMethod("GetItemHeight", flags)!.Invoke(itemsView, new object[] { index })!;
			var scroll = (float)typeof(SkiaItemsView).GetField("_scrollOffset", flags)!.GetValue(itemsView)!;
			var b = itemsView.ScreenBounds;
			return new Rect(b.X, b.Y + offset - scroll, b.Width, height);
		}
	}
}
