// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The desktop's contacts: Evolution Data Server's address books first, then KDE's
/// (<see cref="AkonadiContactSource"/>), a KDE contact whose UID an earlier one has left out.
/// Both are read when both exist, as a KDE desktop often has Evolution Data Server too (pulled
/// in by GNOME applications) with only its empty system address book. Only when neither
/// exists is Evolution Data Server's error thrown, as before: <see cref="FeatureNotSupportedException"/>
/// without it, <see cref="PermissionException"/> when a sandbox refuses it.
/// </summary>
internal sealed class DesktopContactSource : IContactSource
{
    public static readonly DesktopContactSource Instance = new(EdsContactSource.Instance, AkonadiContactSource.Default);

    private readonly IContactSource _eds;
    private readonly IContactSource _kde;

    internal DesktopContactSource(IContactSource eds, IContactSource kde)
    {
        _eds = eds;
        _kde = kde;
    }

    public async Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? eds = null;
        Exception? edsError = null;
        try
        {
            eds = await _eds.GetVCardsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or PermissionException)
        {
            edsError = ex;
        }

        IReadOnlyList<string>? kde = null;
        try
        {
            kde = await _kde.GetVCardsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FeatureNotSupportedException)
        {
        }
        catch (PermissionException ex) when (eds != null)
        {
            DiagnosticLog.Warn("Contacts", $"KDE contacts skipped: {ex.Message}");
        }

        if (kde == null)
            return eds ?? throw edsError!;
        if (eds == null)
        {
            DiagnosticLog.Debug("Contacts", $"Evolution Data Server unavailable ({edsError!.Message}); using KDE's contacts.");
            return kde;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in eds)
        {
            if (VCard.Uid(card) is { } uid)
                seen.Add(uid);
        }
        var cards = new List<string>(eds.Count + kde.Count);
        cards.AddRange(eds);
        foreach (var card in kde)
        {
            if (VCard.Uid(card) is not { } uid || seen.Add(uid))
                cards.Add(card);
        }
        return cards;
    }
}
