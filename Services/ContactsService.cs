// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux contacts, from the desktop's address books in Evolution Data Server (where GNOME
/// Contacts, Evolution and GNOME Online Accounts keep them), the way MAUI's Windows build reads
/// the Windows contact store:
/// <list type="bullet">
/// <item><c>GetAllAsync</c>: every contact of every enabled address book, converted as Windows'
/// ConvertContact does (id, name parts, display name, phones, e-mail addresses).</item>
/// <item><c>PickContactAsync</c>: a contact chooser over the app window (the Windows contact
/// picker's counterpart); null when the user cancels.</item>
/// </list>
/// Without Evolution Data Server (a desktop that keeps contacts elsewhere, KDE's Akonadi for
/// one) both throw <see cref="FeatureNotSupportedException"/>; when a sandbox does not let the
/// app reach it, <see cref="PermissionException"/>, as Windows throws when the store is denied.
/// </summary>
public class ContactsService : IContacts
{
    private readonly IContactSource _source;
    private readonly Func<IReadOnlyList<Contact>, Task<Contact?>> _chooser;

    public ContactsService()
        : this(EdsContactSource.Instance, ChooseWithDialogAsync)
    {
    }

    /// <summary>Contacts from <paramref name="source"/>, picked with <paramref name="chooser"/> (tests).</summary>
    internal ContactsService(IContactSource source, Func<IReadOnlyList<Contact>, Task<Contact?>> chooser)
    {
        _source = source;
        _chooser = chooser;
    }

    public async Task<Contact?> PickContactAsync()
    {
        var contacts = (await GetAllAsync().ConfigureAwait(false)).ToList();
        return await _chooser(contacts).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cards = await _source.GetVCardsAsync(cancellationToken).ConfigureAwait(false);
        if (cards.Count == 0)
            return Array.Empty<Contact>();
        var contacts = new List<Contact>(cards.Count);
        foreach (var card in cards)
        {
            var contact = VCard.ToContact(card);
            if (contact != null)
                contacts.Add(contact);
        }
        return contacts;
    }

    /// <summary>
    /// The label a contact has in the chooser: its display name, else its name parts, e-mail or
    /// phone; duplicates are told apart by their e-mail or phone, then by a number.
    /// </summary>
    internal static IReadOnlyList<string> ChooserLabels(IReadOnlyList<Contact> contacts)
    {
        static string Detail(Contact c) => c.Emails.FirstOrDefault()?.EmailAddress ?? c.Phones.FirstOrDefault()?.PhoneNumber ?? string.Empty;

        var labels = contacts.Select(c =>
        {
            var name = !string.IsNullOrWhiteSpace(c.DisplayName) ? c.DisplayName
                : string.Join(" ", new[] { c.NamePrefix, c.GivenName, c.MiddleName, c.FamilyName, c.NameSuffix }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(name) ? Detail(c) is { Length: > 0 } d ? d : "(no name)" : name.Trim();
        }).ToList();

        var duplicates = labels.GroupBy(l => l, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < labels.Count; i++)
        {
            if (duplicates.Contains(labels[i]) && Detail(contacts[i]) is { Length: > 0 } detail && detail != labels[i])
                labels[i] = $"{labels[i]} ({detail})";
        }

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < labels.Count; i++)
        {
            if (seen.TryGetValue(labels[i], out var count))
            {
                seen[labels[i]] = count + 1;
                labels[i] = $"{labels[i]} ({count + 1})";
            }
            else
            {
                seen[labels[i]] = 1;
            }
        }
        return labels;
    }

    private static async Task<Contact?> ChooseWithDialogAsync(IReadOnlyList<Contact> contacts)
    {
        var sorted = contacts.OrderBy(c => c.DisplayName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase).ToList();
        const string Cancel = "Cancel";
        // The sheet answers "Cancel" when dismissed; a contact by that name must not look like it.
        var labels = ChooserLabels(sorted).Select(l => l == Cancel ? l + " (contact)" : l).ToList();
        var title = sorted.Count == 0 ? "No contacts" : "Choose a contact";
        var choice = await MainThread.InvokeOnMainThreadAsync(() => LinuxDialogService.ShowActionSheetAsync(title, Cancel, null, labels)).ConfigureAwait(false);
        if (choice == null)
            return null;
        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] == choice)
                return sorted[i];
        }
        return null;
    }
}
