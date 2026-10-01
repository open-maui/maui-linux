// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// CommunityToolkit.Maui's IconTintColorBehavior on an Image or ImageButton: its platform-neutral
/// build, the one a Linux app runs, only declares TintColor, so the icon was never tinted. The
/// view's image is drawn in that colour (every opaque pixel), as on the other platforms, following
/// the behavior being added or removed and its TintColor changing (a binding). No compile-time
/// reference: the toolkit is optional.
/// </summary>
internal static class ToolkitIconTint
{
    private const string BehaviorTypeName = "CommunityToolkit.Maui.Behaviors.IconTintColorBehavior";

    private sealed class Link
    {
        public required View View;
        public required Action<Color?> Apply;
        public readonly List<BindableObject> Behaviors = new();
        public NotifyCollectionChangedEventHandler? OnBehaviorsChanged;
        public PropertyChangedEventHandler? OnBehaviorChanged;
    }

    private static readonly ConditionalWeakTable<View, Link> s_links = new();

    /// <summary>Tints the view's image through <paramref name="apply"/> while an IconTintColorBehavior is on it.</summary>
    internal static void Attach(View view, Action<Color?> apply)
    {
        Detach(view);
        var link = new Link { View = view, Apply = apply };
        link.OnBehaviorChanged = (s, e) =>
        {
            if (e.PropertyName == "TintColor")
                Update(link);
        };
        link.OnBehaviorsChanged = (s, e) => Update(link);
        if (view.Behaviors is INotifyCollectionChanged collection)
            collection.CollectionChanged += link.OnBehaviorsChanged;
        s_links.AddOrUpdate(view, link);
        Update(link);
    }

    internal static void Detach(View view)
    {
        if (!s_links.TryGetValue(view, out var link))
            return;
        if (view.Behaviors is INotifyCollectionChanged collection)
            collection.CollectionChanged -= link.OnBehaviorsChanged;
        foreach (var behavior in link.Behaviors)
            behavior.PropertyChanged -= link.OnBehaviorChanged;
        link.Behaviors.Clear();
        s_links.Remove(view);
    }

    private static void Update(Link link)
    {
        try
        {
            foreach (var behavior in link.Behaviors)
                behavior.PropertyChanged -= link.OnBehaviorChanged;
            link.Behaviors.Clear();

            Color? tint = null;
            foreach (var behavior in link.View.Behaviors)
            {
                if (behavior.GetType().FullName != BehaviorTypeName)
                    continue;
                behavior.PropertyChanged += link.OnBehaviorChanged;
                link.Behaviors.Add(behavior);
                tint = behavior.GetType().GetProperty("TintColor")?.GetValue(behavior) as Color;
            }
            link.Apply(tint);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ToolkitIconTint", "Applying IconTintColorBehavior failed", ex);
        }
    }
}
