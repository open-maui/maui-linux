// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A BindableLayout list with an EmptyView (GitCleaner's workspace list):
/// removing the last item shows the empty view, adding one shows the item.
/// </summary>
[Collection("LinuxApplication.Current")]
public class BindableLayoutUpdateTests
{
    private static List<string> ShownTexts(SkiaView view)
    {
        var texts = new List<string>();
        void Walk(SkiaView v)
        {
            if (!v.IsVisible) return;
            if (v is SkiaLabel { Text: { Length: > 0 } t }) texts.Add(t);
            var children = v is SkiaLayoutView l ? l.Children : v.Children;
            foreach (var c in children) Walk(c);
        }
        Walk(view);
        return texts;
    }

    [Fact]
    public void Items_added_after_the_empty_view_are_shown()
    {
        var items = new ObservableCollection<string> { "/home/one" };
        var list = new VerticalStackLayout();
        BindableLayout.SetEmptyView(list, new Label { Text = "No workspaces" });
        BindableLayout.SetItemTemplate(list, new DataTemplate(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            return label;
        }));
        BindableLayout.SetItemsSource(list, items);

        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaView)list.Handler!.PlatformView!;
        ShownTexts(skia).Should().Equal("/home/one");

        items.RemoveAt(0);
        host.Context.Render();
        ShownTexts(skia).Should().Equal("No workspaces");

        items.Add("/home/two");
        host.Context.Render();
        ShownTexts(skia).Should().Equal("/home/two");
    }
}
