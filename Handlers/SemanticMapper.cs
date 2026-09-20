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
/// <c>AutomationProperties.Name</c> / <c>HelpText</c> / <c>IsInAccessibleTree</c>
/// fill <see cref="SkiaView.SemanticName"/>, <see cref="SkiaView.SemanticDescription"/>
/// and <see cref="SkiaView.IsInAccessibleTree"/>. MAUI's core ViewMapper does not
/// invoke platform mappers for these on Linux, so the platform host calls
/// <see cref="Apply"/> once per handler; the first call also subscribes to the
/// view's <see cref="INotifyPropertyChanged"/> so later XAML/binding updates
/// propagate.
/// </summary>
public static class SemanticMapper
{
    // Tracks views already subscribed so repeated Apply calls stay idempotent.
    private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertyChangedEventHandler> _subscriptions = new();

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
        if (view is BindableObject bindable)
        {
            automationName = NullIfEmpty(AutomationProperties.GetName(bindable));
            helpText = NullIfEmpty(AutomationProperties.GetHelpText(bindable));
            inTree = AutomationProperties.GetIsInAccessibleTree(bindable);

            if (automationName == null && AutomationProperties.GetLabeledBy(bindable) is VisualElement labeledBy)
                automationName = NullIfEmpty(DescribeLabel(labeledBy));
        }

        platformView.SemanticName = description ?? automationName;
        platformView.SemanticHint = hint ?? helpText;
        platformView.SemanticDescription = helpText;
        platformView.SemanticHeadingLevel = headingLevel;
        platformView.IsInAccessibleTree = inTree;

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
