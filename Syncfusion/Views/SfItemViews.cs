// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The item views of Syncfusion's items controls (SfCarousel, SfRotator), built
/// the way their native builds build them: an item control's own content or
/// image, else the <c>ItemTemplate</c> (or a template selector's choice) bound
/// to the data item, else a centred label with the item's text.
/// </summary>
internal static class SfItemViews
{
    /// <summary>The view for a data item (not a Syncfusion item control).</summary>
    public static View? ForData(object? item, DataTemplate? template, BindableObject container)
    {
        if (template == null)
        {
            return new Label
            {
                Text = item?.ToString(),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
            };
        }

        var chosen = template is DataTemplateSelector selector ? selector.SelectTemplate(item, container) : template;
        if (chosen?.CreateContent() is not View view)
            return null;
        view.BindingContext = item;
        return view;
    }

    /// <summary>An image item, fitted as the native builds' Image controls fit it.</summary>
    public static View ForImage(string source) => new Image { Source = source, Aspect = Aspect.AspectFit };

    /// <summary>
    /// Gives an item view the control as its logical parent (resources, styles
    /// and theme reach it, and its frame is relative to the control), unless it
    /// already has one. Returns true when the parent was set here.
    /// </summary>
    public static bool Adopt(View view, Element owner)
    {
        if (view.Parent != null)
            return false;
        view.Parent = owner;
        return true;
    }

    /// <summary>The Skia view for an item view, creating its handler on first use.</summary>
    public static SkiaView? PlatformOf(View view, IMauiContext context)
    {
        try
        {
            if (view.Handler == null)
                view.Handler = view.ToViewHandler(context);
            return view.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Creating the item view {view.GetType().Name} failed", ex);
            return null;
        }
    }
}

/// <summary>
/// Reflection access to the internal members the native builds use to report
/// back to Syncfusion's controls (event-argument setters, internal callbacks).
/// Looked up by name once; a member that is missing in some release makes the
/// report a no-op rather than an exception.
/// </summary>
internal static class SfReflect
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Sets a property whose setter is not public (event arguments' values).</summary>
    public static void Set(object target, string property, object? value)
    {
        try
        {
            target.GetType().GetProperty(property, Instance)?.SetValue(target, value);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Setting {target.GetType().Name}.{property} failed", ex);
        }
    }

    /// <summary>Reads a property that may be internal.</summary>
    public static object? Get(object target, string property)
    {
        try
        {
            return target.GetType().GetProperty(property, Instance)?.GetValue(target);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Reading {target.GetType().Name}.{property} failed", ex);
            return null;
        }
    }

    /// <summary>Calls an instance method that may be internal.</summary>
    public static void Call(object target, string method, params object?[] args)
    {
        try
        {
            target.GetType().GetMethod(method, Instance, null, args.Select(a => a?.GetType() ?? typeof(object)).ToArray(), null)
                ?.Invoke(target, args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Calling {target.GetType().Name}.{method} failed", ex);
        }
    }

    /// <summary>Creates an object through a constructor that may be internal.</summary>
    public static object? Create(Type type, params object[] args)
    {
        try
        {
            return Activator.CreateInstance(type, Instance, null, args, null);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Creating {type.Name} failed", ex);
            return null;
        }
    }
}
