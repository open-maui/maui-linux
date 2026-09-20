// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Accessibility;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="ISemanticScreenReader"/>.
/// <see cref="Announce"/> routes to the AT-SPI2 <see cref="IAccessibilityService"/>
/// when one is available and enabled (Orca or another screen reader is
/// listening); otherwise it is a logged no-op so template code such as
/// <c>SemanticScreenReader.Announce(CounterBtn.Text)</c> never throws.
/// </summary>
public sealed class SemanticScreenReaderService : ISemanticScreenReader
{
    private readonly Func<IAccessibilityService?> _serviceResolver;

    /// <summary>
    /// Creates a reader that resolves the process-wide accessibility service
    /// lazily on first announcement (so constructing this at startup never
    /// forces an AT-SPI2 connection).
    /// </summary>
    public SemanticScreenReaderService()
        : this(ResolveDefault)
    {
    }

    /// <summary>
    /// Creates a reader bound to a specific accessibility service (or null for
    /// the no-op path).
    /// </summary>
    public SemanticScreenReaderService(IAccessibilityService? accessibilityService)
        : this(() => accessibilityService)
    {
    }

    internal SemanticScreenReaderService(Func<IAccessibilityService?> serviceResolver)
    {
        _serviceResolver = serviceResolver;
    }

    /// <summary>
    /// Text passed to the most recent <see cref="Announce"/> call, or null.
    /// Lets tests and diagnostics observe the no-op path.
    /// </summary>
    public string? LastAnnouncement { get; private set; }

    /// <inheritdoc />
    public void Announce(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        LastAnnouncement = text;

        IAccessibilityService? service = null;
        try { service = _serviceResolver(); }
        catch (Exception ex) { DiagnosticLog.Debug("SemanticScreenReader", $"Accessibility service unavailable: {ex.Message}"); }

        if (service == null || !service.IsEnabled)
        {
            DiagnosticLog.Debug("SemanticScreenReader", $"No screen reader active; dropped announcement: \"{text}\"");
            return;
        }

        try
        {
            service.Announce(text, AnnouncementPriority.Polite);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("SemanticScreenReader", $"Announce failed: {ex.Message}");
        }
    }

    private static IAccessibilityService? ResolveDefault()
    {
        try { return AccessibilityServiceFactory.Instance; }
        catch { return null; }
    }
}
