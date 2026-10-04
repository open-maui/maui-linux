// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>Where contacts come from (tests substitute one).</summary>
internal interface IContactSource
{
    /// <summary>
    /// The vCards of every contact. Throws <see cref="FeatureNotSupportedException"/> when there
    /// is no contacts service and <see cref="PermissionException"/> when it may not be read.
    /// </summary>
    Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken);
}

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
internal interface IEdsSourceManagerProxy : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
}

[DBusInterface("org.gnome.evolution.dataserver.AddressBookFactory")]
internal interface IEdsAddressBookFactoryProxy : IDBusObject
{
    Task<(string objectPath, string busName)> OpenAddressBookAsync(string sourceUid);
}

[DBusInterface("org.gnome.evolution.dataserver.AddressBook")]
internal interface IEdsAddressBookProxy : IDBusObject
{
    Task<string[]> OpenAsync();
    Task<string[]> GetContactListAsync(string query);
    Task CloseAsync();
}

/// <summary>
/// Evolution Data Server's address books over D-Bus: the source registry (Sources5) lists the
/// enabled address books, the address-book factory (AddressBook10) opens each for this
/// connection, and GetContactList returns its contacts as vCards.
/// </summary>
internal sealed class EdsContactSource : IContactSource
{
    public static readonly EdsContactSource Instance = new();

    internal const string SourcesBusName = "org.gnome.evolution.dataserver.Sources5";
    internal const string SourceManagerPath = "/org/gnome/evolution/dataserver/SourceManager";
    internal const string SourceInterface = "org.gnome.evolution.dataserver.Source";
    internal const string AddressBookBusName = "org.gnome.evolution.dataserver.AddressBook10";
    internal const string AddressBookFactoryPath = "/org/gnome/evolution/dataserver/AddressBookFactory";

    /// <summary>EDS's query for every contact (e_book_query_any_field_contains("")).</summary>
    internal const string AllContactsQuery = "(contains \"x-evolution-any-field\" \"\")";

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken)
    {
        SessionBusConnection bus;
        try
        {
            bus = await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PortalUnavailableException ex)
        {
            throw new FeatureNotSupportedException($"Contacts need a D-Bus session: {ex.Message}");
        }

        IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>> objects;
        try
        {
            var manager = bus.Connection.CreateProxy<IEdsSourceManagerProxy>(SourcesBusName, new ObjectPath(SourceManagerPath));
            objects = await manager.GetManagedObjectsAsync().WaitAsync(CallTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (DBusException ex)
        {
            throw Translate(ex);
        }

        var cards = new List<string>();
        foreach (var uid in AddressBookUids(objects))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                cards.AddRange(await ReadBookAsync(bus, uid, cancellationToken).ConfigureAwait(false));
            }
            catch (DBusException ex) when (!IsAccessDenied(ex))
            {
                // One unreachable book (an offline online account) does not hide the others.
                DiagnosticLog.Warn("Contacts", $"Address book {uid} could not be read: {ex.ErrorName}: {ex.ErrorMessage}");
            }
            catch (DBusException ex)
            {
                throw Translate(ex);
            }
        }
        return cards;
    }

    private static async Task<string[]> ReadBookAsync(SessionBusConnection bus, string uid, CancellationToken cancellationToken)
    {
        var factory = bus.Connection.CreateProxy<IEdsAddressBookFactoryProxy>(AddressBookBusName, new ObjectPath(AddressBookFactoryPath));
        var (objectPath, busName) = await factory.OpenAddressBookAsync(uid).WaitAsync(CallTimeout, cancellationToken).ConfigureAwait(false);
        var book = bus.Connection.CreateProxy<IEdsAddressBookProxy>(busName, new ObjectPath(objectPath));
        try
        {
            await book.OpenAsync().WaitAsync(CallTimeout, cancellationToken).ConfigureAwait(false);
            return await book.GetContactListAsync(AllContactsQuery).WaitAsync(CallTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await book.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("Contacts", $"Closing address book {uid} failed: {ex.Message}");
            }
        }
    }

    /// <summary>The UIDs of the enabled address-book sources among the registry's objects.</summary>
    internal static IReadOnlyList<string> AddressBookUids(IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>> objects)
    {
        var uids = new List<string>();
        foreach (var (path, interfaces) in objects.OrderBy(o => o.Key.ToString(), StringComparer.Ordinal))
        {
            if (!interfaces.TryGetValue(SourceInterface, out var properties))
                continue;
            if (properties.TryGetValue("UID", out var uid) && uid is string id
                && properties.TryGetValue("Data", out var data) && data is string keyFile
                && IsEnabledAddressBook(keyFile))
            {
                uids.Add(id);
            }
        }
        return uids;
    }

    /// <summary>Whether a source's key file describes an address book that is not disabled.</summary>
    internal static bool IsEnabledAddressBook(string keyFile)
    {
        string? group = null;
        var isBook = false;
        var enabled = true;
        foreach (var raw in keyFile.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                group = line[1..^1];
                if (group == "Address Book")
                    isBook = true;
                continue;
            }
            if (group == "Data Source" && line.StartsWith("Enabled=", StringComparison.Ordinal))
                enabled = !line["Enabled=".Length..].Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        return isBook && enabled;
    }

    private static bool IsAccessDenied(DBusException ex)
        => ex.ErrorName is "org.freedesktop.DBus.Error.AccessDenied" or "org.freedesktop.portal.Error.NotAllowed";

    /// <summary>No EDS (not installed, not reachable): not supported; refused (a sandbox): permission.</summary>
    internal static Exception Translate(DBusException ex)
    {
        if (IsAccessDenied(ex))
            return new PermissionException("Permission to access the contacts was denied.");
        return new FeatureNotSupportedException(
            $"No contacts service: Evolution Data Server is not available ({ex.ErrorName}).");
    }
}
