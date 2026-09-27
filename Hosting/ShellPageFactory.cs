// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Builds the page of a ShellContent's <c>ContentTemplate</c> the way MAUI
/// does: the page's own registration first (<c>GetService</c>, which honours
/// factory registrations such as <c>AddTransient(sp => new MyPage(...))</c>),
/// then constructor injection for unregistered pages, then the template's
/// parameterless constructor. Failures are logged at Error with the whole
/// exception, so the missing dependency is named in Release builds too.
/// </summary>
internal static class ShellPageFactory
{
    private static readonly PropertyInfo? s_templateType = typeof(ElementTemplate).GetProperty("Type",
        BindingFlags.NonPublic | BindingFlags.Instance);

    public static Page? Create(DataTemplate template, IServiceProvider? services)
    {
        var pageType = s_templateType?.GetValue(template) as Type;
        if (pageType != null && services != null)
        {
            var page = Resolve(pageType, services);
            if (page != null)
                return page;
        }

        try
        {
            return template.CreateContent() as Page;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ShellPageFactory",
                $"Cannot create Shell page {pageType?.FullName ?? "(template)"}: neither dependency injection nor a parameterless constructor could build it",
                ex);
            return null;
        }
    }

    internal static Page? Resolve(Type pageType, IServiceProvider services)
    {
        try
        {
            if (services.GetService(pageType) is Page registered)
                return registered;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ShellPageFactory", $"Resolving registered Shell page {pageType.FullName} failed", ex);
            return null;
        }

        try
        {
            return ActivatorUtilities.CreateInstance(services, pageType) as Page;
        }
        catch (Exception ex)
        {
            // A parameterless constructor still gets its chance through the
            // template; the injection failure is the real error only without one.
            if (pageType.GetConstructor(Type.EmptyTypes) == null)
                DiagnosticLog.Error("ShellPageFactory", $"Dependency injection could not construct Shell page {pageType.FullName}", ex);
            else
                DiagnosticLog.Debug("ShellPageFactory", $"Constructor injection failed for {pageType.Name}; using its parameterless constructor: {ex.Message}");
            return null;
        }
    }
}
