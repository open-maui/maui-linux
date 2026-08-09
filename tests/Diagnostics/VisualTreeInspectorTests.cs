// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Diagnostics;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Diagnostics;

/// <summary>
/// Unit tests for the visual-tree walk, snapshot model, and text dump. These build
/// a small SkiaView tree in-memory (no live window) and assert against the snapshot
/// and dump output. Overlay/pick behavior needs a real render pass and is not covered
/// here.
/// </summary>
public class VisualTreeInspectorTests
{
    /// <summary>Minimal concrete view (base MeasureOverride, no Text property).</summary>
    private sealed class PlainView : SkiaView
    {
        protected override void OnDraw(SKCanvas canvas, SKRect bounds) { }
    }

    /// <summary>View that exposes extra content roots outside its Children list.</summary>
    private sealed class ExtraRootView : SkiaView
    {
        private readonly List<SkiaView> _extra = new();
        public void AddExtra(SkiaView v) => _extra.Add(v);
        public override IEnumerable<SkiaView> ExtraContentRoots => _extra;
        protected override void OnDraw(SKCanvas canvas, SKRect bounds) { }
    }

    private static VisualTreeInspector Inspector => VisualTreeInspector.Instance;

    [Fact]
    public void Snapshot_CapturesTypeBoundsAndVisibility()
    {
        var view = new PlainView
        {
            Bounds = new Rect(10, 20, 100, 40),
            IsVisible = true,
        };

        var node = Inspector.Snapshot(view);

        node.TypeName.Should().Be("PlainView");
        node.Bounds.Should().Be(new Rect(10, 20, 100, 40));
        node.IsVisible.Should().BeTrue();
        node.ChildCount.Should().Be(0);
        node.Source.Should().BeSameAs(view);
    }

    [Fact]
    public void Snapshot_CapturesLabelText()
    {
        var label = new SkiaLabel { Text = "Hello" };

        var node = Inspector.Snapshot(label);

        node.Text.Should().Be("Hello");
    }

    [Fact]
    public void Snapshot_NonTextView_HasNullText()
    {
        var node = Inspector.Snapshot(new PlainView());

        node.Text.Should().BeNull();
    }

    [Fact]
    public void Snapshot_CapturesBackgroundColor()
    {
        var view = new PlainView { BackgroundColor = Colors.Purple };

        var node = Inspector.Snapshot(view);

        node.BackgroundColor.Should().Be(Colors.Purple);
    }

    [Fact]
    public void Snapshot_HonorsLayoutViewChildrenShadow()
    {
        // SkiaLayoutView shadows the base Children collection; children added via
        // its AddChild live in the shadowed list. The walk must find them.
        var stack = new SkiaStackLayout();
        stack.AddChild(new SkiaLabel { Text = "A" });
        stack.AddChild(new SkiaLabel { Text = "B" });

        var node = Inspector.Snapshot(stack);

        node.ChildCount.Should().Be(2);
        node.Children.Select(c => c.Text).Should().Equal("A", "B");
    }

    [Fact]
    public void Snapshot_IncludesExtraContentRoots()
    {
        var root = new ExtraRootView();
        root.AddChild(new PlainView());            // normal child
        root.AddExtra(new SkiaLabel { Text = "Extra" }); // extra content root

        var node = Inspector.Snapshot(root);

        node.ChildCount.Should().Be(2);
        node.Children.Should().Contain(c => c.Text == "Extra");
        node.Children.Should().Contain(c => c.TypeName == "PlainView");
    }

    [Fact]
    public void Snapshot_RecursesNestedTree()
    {
        var outer = new SkiaStackLayout();
        var inner = new SkiaStackLayout();
        inner.AddChild(new SkiaLabel { Text = "leaf" });
        outer.AddChild(inner);

        var node = Inspector.Snapshot(outer);

        node.ChildCount.Should().Be(1);
        node.Children[0].ChildCount.Should().Be(1);
        node.Children[0].Children[0].Text.Should().Be("leaf");
    }

    [Fact]
    public void DumpTree_ProducesIndentedOutput()
    {
        var stack = new SkiaStackLayout { Bounds = new Rect(0, 0, 200, 100) };
        stack.AddChild(new SkiaLabel { Text = "Hi", Bounds = new Rect(0, 0, 50, 20) });

        var dump = Inspector.DumpTree(stack);
        var lines = dump.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("SkiaStackLayout");
        lines[0].Should().Contain("(1 children)");
        // Child line is indented by two spaces and shows the captured text.
        lines[1].Should().StartWith("  SkiaLabel");
        lines[1].Should().Contain("Text=\"Hi\"");
    }

    [Fact]
    public void DumpTree_NoLiveRoot_ReturnsPlaceholder()
    {
        // With no LinuxApplication.Current there is no root view.
        Inspector.DumpTree().Should().Be("(no root view)");
    }
}
