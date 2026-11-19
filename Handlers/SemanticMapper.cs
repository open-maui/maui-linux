// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Copies MAUI accessibility metadata onto a Skia platform view so the
/// AT-SPI2 bridge exposes it: <c>SemanticProperties.Description</c> becomes
/// <see cref="SkiaView.SemanticName"/>, <c>SemanticProperties.Hint</c> becomes
/// <see cref="SkiaView.SemanticHint"/>, <c>SemanticProperties.HeadingLevel</c>
/// becomes <see cref="SkiaView.SemanticHeadingLevel"/>, and the legacy
/// <c>AutomationProperties.Name</c> / <c>HelpText</c> / <c>IsInAccessibleTree</c> /
/// <c>ExcludedWithChildren</c> fill <see cref="SkiaView.SemanticName"/>,
/// <see cref="SkiaView.SemanticDescription"/>, <see cref="SkiaView.IsInAccessibleTree"/>
/// and <see cref="SkiaView.IsExcludedWithChildren"/>. MAUI's core ViewMapper does not
/// invoke platform mappers for these on Linux, so the platform host calls
/// <see cref="Apply"/> once per handler; the first call also subscribes to the
/// view's <see cref="INotifyPropertyChanged"/> so later XAML/binding updates
/// propagate.
/// </summary>
public static class SemanticMapper
{
    // Tracks views already subscribed so repeated Apply calls stay idempotent.
    private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertyChangedEventHandler> _subscriptions = new();

    // AutomationProperties.LabeledBy: the label each control is followed through, and the
    // controls each label names (the AT-SPI label-for relation).
    private sealed class LabelLink
    {
        public required VisualElement Label;
        public required PropertyChangedEventHandler Handler;
    }

    private static readonly ConditionalWeakTable<SkiaView, LabelLink> _labelLinks = new();
    private static readonly ConditionalWeakTable<VisualElement, List<WeakReference<SkiaView>>> _labelled = new();

    /// <summary>The Skia views of the controls <paramref name="label"/> currently labels (AutomationProperties.LabeledBy).</summary>
    internal static IReadOnlyList<SkiaView> LabelledBy(VisualElement label)
    {
        if (!_labelled.TryGetValue(label, out var targets))
            return Array.Empty<SkiaView>();
        lock (targets)
        {
            var found = new List<SkiaView>();
            targets.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var weak in targets)
            {
                if (weak.TryGetTarget(out var target) && _labelLinks.TryGetValue(target, out var link) && ReferenceEquals(link.Label, label))
                    found.Add(target);
            }
            return found;
        }
    }

    /// <summary>
    /// Follows the label <paramref name="platformView"/>'s control is labelled by: a change of its
    /// text or description renames the control (the name it took was a copy).
    /// </summary>
    private static void FollowLabel(IView view, SkiaView platformView, VisualElement? label)
    {
        if (_labelLinks.TryGetValue(platformView, out var old))
        {
            if (ReferenceEquals(old.Label, label))
                return;
            old.Label.PropertyChanged -= old.Handler;
            _labelLinks.Remove(platformView);
        }
        if (label == null)
            return;
        var weakPlatform = new WeakReference<SkiaView>(platformView);
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is not (nameof(Label.Text) or nameof(Label.FormattedText)) && e.PropertyName != SemanticProperties.DescriptionProperty.PropertyName)
                return;
            if (!weakPlatform.TryGetTarget(out var target))
                return;
            Sync(view, target);
            Services.AtSpi.AtSpiBridge.Instance?.NameChanged(target);
        };
        label.PropertyChanged += handler;
        _labelLinks.Add(platformView, new LabelLink { Label = label, Handler = handler });
        var targets = _labelled.GetValue(label, _ => new List<WeakReference<SkiaView>>());
        lock (targets)
            targets.Add(weakPlatform);
    }

    // Property names BindableObject raises for the attached properties we map.
    private static readonly HashSet<string> SemanticPropertyNames = new(StringComparer.Ordinal)
    {
        SemanticProperties.DescriptionProperty.PropertyName,
        SemanticProperties.HintProperty.PropertyName,
        SemanticProperties.HeadingLevelProperty.PropertyName,
        AutomationProperties.NameProperty.PropertyName,
        AutomationProperties.HelpTextProperty.PropertyName,
        AutomationProperties.IsInAccessibleTreeProperty.PropertyName,
        AutomationProperties.LabeledByProperty.PropertyName,
        AutomationProperties.ExcludedWithChildrenProperty.PropertyName,
    };

    /// <summary>
    /// Applies the view's semantics to <paramref name="platformView"/> now and
    /// keeps them in sync on subsequent property changes.
    /// </summary>
    public static void Apply(IView view, SkiaView platformView)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(platformView);

        Sync(view, platformView);

        if (view is INotifyPropertyChanged notifier && !_subscriptions.TryGetValue(notifier, out _))
        {
            var weakPlatform = new WeakReference<SkiaView>(platformView);
            PropertyChangedEventHandler handler = (_, e) =>
            {
                if (e.PropertyName != null && !SemanticPropertyNames.Contains(e.PropertyName))
                    return;
                if (weakPlatform.TryGetTarget(out var target))
                    Sync(view, target);
            };
            _subscriptions.Add(notifier, handler);
            notifier.PropertyChanged += handler;
        }
    }

    /// <summary>
    /// Stops tracking <paramref name="view"/>. Safe to call for views that were
    /// never applied.
    /// </summary>
    public static void Detach(IView view)
    {
        if (view is INotifyPropertyChanged notifier && _subscriptions.TryGetValue(notifier, out var handler))
        {
            notifier.PropertyChanged -= handler;
            _subscriptions.Remove(notifier);
        }
    }

    /// <summary>
    /// One-shot copy of the semantics without subscribing to changes.
    /// </summary>
    public static void Sync(IView view, SkiaView platformView)
    {
        var semantics = view.Semantics;
        string? description = NullIfEmpty(semantics?.Description);
        string? hint = NullIfEmpty(semantics?.Hint);
        var headingLevel = semantics?.HeadingLevel ?? SemanticHeadingLevel.None;

        string? automationName = null;
        string? helpText = null;
        bool? inTree = null;
        bool excludedWithChildren = false;
        if (view is BindableObject bindable)
        {
            excludedWithChildren = AutomationProperties.GetExcludedWithChildren(bindable) == true;
            automationName = NullIfEmpty(AutomationProperties.GetName(bindable));
            helpText = NullIfEmpty(AutomationProperties.GetHelpText(bindable));
            inTree = AutomationProperties.GetIsInAccessibleTree(bindable);

            var labeledBy = AutomationProperties.GetLabeledBy(bindable);
            if (automationName == null && labeledBy != null)
                automationName = NullIfEmpty(DescribeLabel(labeledBy));
            FollowLabel(view, platformView, labeledBy);
        }

        platformView.SemanticName = description ?? automationName;
        platformView.SemanticHint = hint ?? helpText;
        platformView.SemanticDescription = helpText;
        platformView.SemanticHeadingLevel = headingLevel;
        platformView.IsInAccessibleTree = inTree;
        platformView.IsExcludedWithChildren = excludedWithChildren;

        DiagnosticLog.Debug("SemanticMapper",
            $"{view.GetType().Name}: name='{platformView.SemanticName}' hint='{platformView.SemanticHint}' heading={headingLevel}");
    }

    private static string? DescribeLabel(VisualElement labeledBy)
    {
        if (labeledBy is IView labelView && labelView.Semantics?.Description is { Length: > 0 } d)
            return d;
        if (labeledBy is IText text)
            return text.Text;
        return null;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
