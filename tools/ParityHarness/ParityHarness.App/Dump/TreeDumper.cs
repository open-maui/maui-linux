namespace ParityHarness.Dump;

/// <summary>
/// Walks the MAUI visual tree (IVisualTreeElement) from a root page and records every
/// VisualElement. Platform-neutral apart from <see cref="NativeFrames"/>.
/// </summary>
public static class TreeDumper
{
    public static List<ElementRecord> Walk(Page root)
    {
        var records = new List<ElementRecord>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        var rootNative = NativeFrames.RootOf(root);
        Visit(root, root, null, 0, Point.Zero, isRoot: true, records, usedKeys, rootNative);

        // ContainsText: any descendant with Text.
        var byKey = records.ToDictionary(r => r.Key);
        foreach (var r in records.Where(r => r.Text))
        {
            var p = r.ParentKey;
            while (p != null && byKey.TryGetValue(p, out var parent))
            {
                parent.ContainsText = true;
                p = parent.ParentKey;
            }
        }
        return records;
    }

    private static void Visit(
        IVisualTreeElement node, Page root, ElementRecord? parent, int depth, Point origin, bool isRoot,
        List<ElementRecord> records, HashSet<string> usedKeys, object? rootNative)
    {
        ElementRecord? self = parent;
        Point childOrigin = origin;

        // A Border's StrokeShape is a logical child with no layout of its own.
        if (node is Microsoft.Maui.Controls.Shapes.Shape shape
            && shape.Parent is Microsoft.Maui.Controls.Border border
            && ReferenceEquals(border.StrokeShape, shape))
            return;

        if (node is VisualElement ve)
        {
            string type = ve.GetType().Name;
            string? named = DumpKey.GetKey(ve) ?? NullIfEmpty(ve.AutomationId);
            // Unnamed: parentKey/Type, made unique below as parentKey/Type[2], [3], ...
            string key = named ?? $"{parent?.Key ?? "?"}/{type}";
            key = Unique(key, usedKeys);

            var frame = ve.Frame;
            Rect pageFrame = isRoot
                ? new Rect(0, 0, frame.Width, frame.Height)
                : new Rect(origin.X + frame.X, origin.Y + frame.Y, frame.Width, frame.Height);

            self = new ElementRecord
            {
                Key = key,
                AutomationId = NullIfEmpty(ve.AutomationId),
                Type = type,
                ParentKey = parent?.Key,
                Depth = depth,
                IsVisible = ve.IsVisible,
                Text = ve is Microsoft.Maui.IText || ve is Microsoft.Maui.ITextInput,
                TextDependent = DumpKey.GetTextDependent(ve),
                Frame = RectDto.From(pageFrame),
                Native = RectDto.From(NativeFrames.Get(ve, rootNative)),
            };
            records.Add(self);
            childOrigin = pageFrame.Location;
            depth++;
        }

        foreach (var child in node.GetVisualChildren())
        {
            if (child is null) continue;
            Visit(child, root, self, depth, childOrigin, isRoot: false, records, usedKeys, rootNative);
        }
    }

    private static string Unique(string key, HashSet<string> used)
    {
        if (used.Add(key)) return key;
        for (int n = 2; ; n++)
        {
            var k = $"{key}[{n}]";
            if (used.Add(k)) return k;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>A cheap fingerprint of the tree's geometry, for settle detection.</summary>
    public static string Fingerprint(List<ElementRecord> records)
    {
        var sb = new System.Text.StringBuilder(records.Count * 48);
        foreach (var r in records)
        {
            sb.Append(r.Key).Append(r.IsVisible ? '1' : '0');
            Append(sb, r.Frame);
            Append(sb, r.Native);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void Append(System.Text.StringBuilder sb, RectDto? r)
    {
        if (r is null) { sb.Append("|-"); return; }
        sb.Append('|').Append(r.X).Append(',').Append(r.Y).Append(',').Append(r.W).Append(',').Append(r.H);
    }
}
