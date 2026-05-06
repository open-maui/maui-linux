// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux contacts stub. Could integrate with GNOME Contacts or Evolution Data Server.
/// </summary>
public class ContactsService : IContacts
{
    public async Task<Contact?> PickContactAsync()
        => null;

    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
        => Array.Empty<Contact>();
}
