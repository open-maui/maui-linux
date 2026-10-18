// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Tmds.DBus;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// SecureStorage over a long-lived Secret Service D-Bus session, against an in-memory Secret
/// Service that can behave as KDE's ksecretd does under load (a store acknowledged before a
/// search lists it, a deleted item still listed for a while), lock items behind a prompt, refuse
/// the encrypted transport, or lose the session.
/// </summary>
public class SecretServiceKeyringTests
{
    private const string Ns = "maui-secure-storage/com.example.app";

    #region In-memory Secret Service

    private sealed class FakeSecretService : ISecretServiceApi
    {
        private sealed class Item
        {
            public ObjectPath Path;
            public Dictionary<string, string> Attributes = new();
            public string Label = "";
            public byte[] Plain = Array.Empty<byte>();
            public byte[]? PreviousPlain;
            public int ReadsBeforeNewValue;
            public int SearchesBeforeVisible;
            public bool Deleted;
            public int SearchesStillListed;
            public bool Locked;
        }

        private readonly object _sync = new();
        private readonly List<Item> _items = new();
        private readonly Dictionary<string, byte[]?> _sessions = new();
        private readonly Dictionary<string, Func<(bool, object?)>> _prompts = new();
        private int _next;

        public const string Collection = "/org/freedesktop/secrets/collection/login";

        /// <summary>Whether OpenSession accepts the DH algorithm.</summary>
        public bool SupportsDh { get; set; } = true;
        /// <summary>The service derives a different transport key than the client (an interop bug).</summary>
        public bool BrokenDh { get; set; }
        /// <summary>Searches a new item stays unlisted for (and reads an updated item keeps its old value for).</summary>
        public int StoreLag { get; set; }
        /// <summary>Searches a deleted item stays listed (and readable) for.</summary>
        public int DeleteLag { get; set; }
        public bool CollectionLocked { get; set; }
        public bool DismissPrompts { get; set; }
        public int PromptsShown { get; private set; }
        public int SessionsOpened { get; private set; }
        public int Searches { get; private set; }
        public Exception? Unavailable { get; set; }

        public void AddItem(Dictionary<string, string> attributes, string value, bool locked = false)
        {
            lock (_sync)
                _items.Add(new Item { Path = NewPath("item"), Attributes = new(attributes), Label = attributes.GetValueOrDefault("key", ""), Plain = Encoding.UTF8.GetBytes(value), Locked = locked });
        }

        /// <summary>The values a search for the attributes would see once every lag has passed.</summary>
        public List<string> StoredValues(string ns, string key)
        {
            lock (_sync)
                return _items.Where(i => !i.Deleted && i.Attributes.GetValueOrDefault("service") == ns && i.Attributes.GetValueOrDefault("key") == key)
                    .Select(i => Encoding.UTF8.GetString(i.Plain)).ToList();
        }

        public List<Dictionary<string, string>> LiveAttributes()
        {
            lock (_sync)
                return _items.Where(i => !i.Deleted).Select(i => i.Attributes).ToList();
        }

        /// <summary>Forgets every session (the daemon restarted).</summary>
        public void DropSessions()
        {
            lock (_sync)
                _sessions.Clear();
        }

        private ObjectPath NewPath(string kind) => new($"/org/freedesktop/secrets/{kind}/{++_next}");

        private void ThrowIfUnavailable()
        {
            if (Unavailable != null)
                throw new SecretServiceUnavailableException(Unavailable.Message, Unavailable);
        }

        public Task<(object Output, ObjectPath Session)> OpenSessionAsync(string algorithm, object input)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                SessionsOpened++;
                var path = NewPath("session");
                if (algorithm == "plain")
                {
                    _sessions[path.ToString()] = null;
                    return Task.FromResult<(object, ObjectPath)>(("", path));
                }
                if (algorithm != SecretServiceDh.Algorithm || !SupportsDh)
                    throw new DBusException("org.freedesktop.DBus.Error.NotSupported", "Algorithm not supported");
                var (priv, pub) = SecretServiceDh.GenerateKeyPair();
                var key = SecretServiceDh.DeriveKey(priv, (byte[])input);
                if (BrokenDh)
                    key = RandomNumberGenerator.GetBytes(16);
                _sessions[path.ToString()] = key;
                return Task.FromResult<(object, ObjectPath)>((pub, path));
            }
        }

        private bool Listed(Item item)
        {
            if (item.Deleted)
            {
                if (item.SearchesStillListed <= 0)
                    return false;
                item.SearchesStillListed--;
                return true;
            }
            if (item.SearchesBeforeVisible > 0)
            {
                item.SearchesBeforeVisible--;
                return false;
            }
            return true;
        }

        public Task<(ObjectPath[] Unlocked, ObjectPath[] Locked)> SearchItemsAsync(IDictionary<string, string> attributes)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                Searches++;
                var matches = _items.Where(i => attributes.All(a => i.Attributes.TryGetValue(a.Key, out var v) && v == a.Value)).Where(Listed).ToList();
                return Task.FromResult((matches.Where(i => !i.Locked).Select(i => i.Path).ToArray(), matches.Where(i => i.Locked).Select(i => i.Path).ToArray()));
            }
        }

        private ObjectPath NewPrompt(Func<(bool, object?)> complete)
        {
            var path = NewPath("prompt");
            _prompts[path.ToString()] = complete;
            return path;
        }

        public Task<(ObjectPath[] Unlocked, ObjectPath Prompt)> UnlockAsync(ObjectPath[] objects)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                var prompt = NewPrompt(() =>
                {
                    foreach (var item in _items.Where(i => objects.Contains(i.Path)))
                        item.Locked = false;
                    if (objects.Contains(new ObjectPath(Collection)))
                        CollectionLocked = false;
                    return (false, objects);
                });
                return Task.FromResult((Array.Empty<ObjectPath>(), prompt));
            }
        }

        public Task<(bool Dismissed, object? Result)> PromptAsync(ObjectPath prompt)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                PromptsShown++;
                var complete = _prompts[prompt.ToString()];
                _prompts.Remove(prompt.ToString());
                if (DismissPrompts)
                    return Task.FromResult<(bool, object?)>((true, null));
                return Task.FromResult(complete());
            }
        }

        public Task<ObjectPath> ReadAliasAsync(string name)
        {
            ThrowIfUnavailable();
            return Task.FromResult(new ObjectPath(Collection));
        }

        public Task<(ObjectPath Collection, ObjectPath Prompt)> CreateCollectionAsync(IDictionary<string, object> properties, string alias)
            => throw new NotSupportedException("the fake always has a default collection");

        private byte[]? SessionKey(ObjectPath session)
        {
            if (!_sessions.TryGetValue(session.ToString(), out var key))
                throw new DBusException("org.freedesktop.Secret.Error.NoSession", "The session does not exist");
            return key;
        }

        public Task<(ObjectPath Item, ObjectPath Prompt)> CreateItemAsync(ObjectPath collection, IDictionary<string, object> properties, SecretValue secret, bool replace)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                if (CollectionLocked)
                    throw new DBusException("org.freedesktop.Secret.Error.IsLocked", "Collection is locked");
                var key = SessionKey(secret.Session);
                byte[] plain;
                if (key == null)
                    plain = secret.Value;
                else
                {
                    try { plain = SecretServiceDh.Decrypt(key, secret.Parameters, secret.Value); }
                    catch (CryptographicException) { plain = RandomNumberGenerator.GetBytes(8); }
                }
                var attributes = new Dictionary<string, string>((IDictionary<string, string>)properties["org.freedesktop.Secret.Item.Attributes"]);
                var label = (string)properties["org.freedesktop.Secret.Item.Label"];
                var existing = replace
                    ? _items.FirstOrDefault(i => !i.Deleted && i.Attributes.Count == attributes.Count && i.Attributes.All(a => attributes.TryGetValue(a.Key, out var v) && v == a.Value))
                    : null;
                if (existing != null)
                {
                    existing.PreviousPlain = existing.Plain;
                    existing.Plain = plain;
                    existing.ReadsBeforeNewValue = StoreLag;
                    return Task.FromResult((existing.Path, new ObjectPath("/")));
                }
                var item = new Item { Path = NewPath("item"), Attributes = attributes, Label = label, Plain = plain, SearchesBeforeVisible = StoreLag };
                _items.Add(item);
                return Task.FromResult((item.Path, new ObjectPath("/")));
            }
        }

        public Task<SecretValue> GetSecretAsync(ObjectPath itemPath, ObjectPath session)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                var key = SessionKey(session);
                var item = _items.FirstOrDefault(i => i.Path == itemPath)
                    ?? throw new DBusException("org.freedesktop.Secret.Error.NoSuchObject", "No such item");
                if (item.Locked)
                    throw new DBusException("org.freedesktop.Secret.Error.IsLocked", "Item is locked");
                var plain = item.Plain;
                if (item.ReadsBeforeNewValue > 0 && item.PreviousPlain != null)
                {
                    item.ReadsBeforeNewValue--;
                    plain = item.PreviousPlain;
                }
                if (key == null)
                    return Task.FromResult(new SecretValue(session, Array.Empty<byte>(), plain, "text/plain"));
                var (iv, cipher) = SecretServiceDh.Encrypt(key, plain);
                return Task.FromResult(new SecretValue(session, iv, cipher, "text/plain"));
            }
        }

        public Task<ObjectPath> DeleteItemAsync(ObjectPath itemPath)
        {
            ThrowIfUnavailable();
            lock (_sync)
            {
                var item = _items.FirstOrDefault(i => i.Path == itemPath && !i.Deleted)
                    ?? throw new DBusException("org.freedesktop.Secret.Error.NoSuchObject", "No such item");
                item.Deleted = true;
                item.SearchesStillListed = DeleteLag;
                return Task.FromResult(new ObjectPath("/"));
            }
        }
    }

    private static DBusSecretKeyring Keyring(FakeSecretService service, bool preferEncryption = true) =>
        new(service, preferEncryption) { ConfirmDelay = TimeSpan.FromMilliseconds(1) };

    #endregion

    [Fact]
    public async Task Values_round_trip_over_an_encrypted_session()
    {
        var service = new FakeSecretService();
        var keyring = Keyring(service);

        await keyring.StoreAsync(Ns, "token", "s3cr3t wörld");

        (await keyring.LookupAsync(Ns, "token")).Should().Be("s3cr3t wörld");
        keyring.SessionAlgorithm.Should().Be(SecretServiceDh.Algorithm);
        service.StoredValues(Ns, "token").Should().Equal("s3cr3t wörld");
        service.SessionsOpened.Should().Be(1, "one long-lived session serves every call");
    }

    [Fact]
    public async Task A_service_without_the_DH_algorithm_gets_a_plain_session()
    {
        var service = new FakeSecretService { SupportsDh = false };
        var keyring = Keyring(service);

        await keyring.StoreAsync(Ns, "token", "value");

        (await keyring.LookupAsync(Ns, "token")).Should().Be("value");
        keyring.SessionAlgorithm.Should().Be("plain");
    }

    [Fact]
    public async Task A_transport_key_mismatch_is_caught_by_the_read_back_and_the_value_stored_again_in_plain()
    {
        var service = new FakeSecretService { BrokenDh = true };
        var keyring = Keyring(service);

        await keyring.StoreAsync(Ns, "token", "value");

        keyring.SessionAlgorithm.Should().Be("plain");
        service.StoredValues(Ns, "token").Should().Equal("value");
        (await keyring.LookupAsync(Ns, "token")).Should().Be("value");
    }

    [Fact]
    public void The_DH_key_agreement_gives_both_sides_the_same_key()
    {
        var (clientPrivate, clientPublic) = SecretServiceDh.GenerateKeyPair();
        var (serverPrivate, serverPublic) = SecretServiceDh.GenerateKeyPair();
        var clientKey = SecretServiceDh.DeriveKey(clientPrivate, serverPublic);
        var serverKey = SecretServiceDh.DeriveKey(serverPrivate, clientPublic);

        clientKey.Should().HaveCount(16);
        clientKey.Should().Equal(serverKey);
        var (iv, cipher) = SecretServiceDh.Encrypt(clientKey, Encoding.UTF8.GetBytes("hello"));
        iv.Should().HaveCount(16);
        Encoding.UTF8.GetString(SecretServiceDh.Decrypt(serverKey, iv, cipher)).Should().Be("hello");
        FluentActions.Invoking(() => SecretServiceDh.DeriveKey(clientPrivate, new byte[] { 1 }))
            .Should().Throw<CryptographicException>("a degenerate public key is refused");
    }

    [Fact]
    public async Task A_store_returns_only_once_a_lookup_sees_it()
    {
        // ksecretd: the store is acknowledged before a search lists the new item.
        var service = new FakeSecretService { StoreLag = 5 };
        var keyring = Keyring(service);

        await keyring.StoreAsync(Ns, "token", "first");
        service.StoreLag = 0;
        (await keyring.LookupAsync(Ns, "token")).Should().Be("first");

        // A replaced value keeps reading old for a while.
        service.StoreLag = 5;
        await keyring.StoreAsync(Ns, "token", "second");
        service.StoreLag = 0;
        (await keyring.LookupAsync(Ns, "token")).Should().Be("second");
        service.StoredValues(Ns, "token").Should().ContainSingle("replace keeps one item");
    }

    [Fact]
    public async Task A_clear_returns_only_once_a_search_no_longer_lists_the_item()
    {
        // ksecretd: a deleted item is still listed (and readable) for a while.
        var service = new FakeSecretService { DeleteLag = 5 };
        var keyring = Keyring(service);
        await keyring.StoreAsync(Ns, "token", "value");

        (await keyring.ClearAsync(Ns, "token")).Should().BeTrue();
        (await keyring.LookupAsync(Ns, "token")).Should().BeNull();
        (await keyring.ClearAsync(Ns, "token")).Should().BeFalse("nothing was stored any more");
    }

    [Fact]
    public async Task Items_secret_tool_stored_are_found_and_replaced()
    {
        var service = new FakeSecretService();
        // As `secret-tool store --label=token service NS key token` writes it.
        service.AddItem(new() { ["service"] = Ns, ["key"] = "token", ["xdg:schema"] = "org.freedesktop.Secret.Generic" }, "old");
        // And one written without the schema attribute.
        service.AddItem(new() { ["service"] = Ns, ["key"] = "token" }, "older");
        var keyring = Keyring(service);

        (await keyring.LookupAsync(Ns, "token")).Should().BeOneOf("old", "older");

        await keyring.StoreAsync(Ns, "token", "new");
        service.StoredValues(Ns, "token").Should().Equal(new[] { "new" }, "the item is replaced and the duplicate removed");
        service.LiveAttributes().Should().ContainSingle().Which.Should().Equal(new Dictionary<string, string>
        {
            ["service"] = Ns,
            ["key"] = "token",
            ["xdg:schema"] = "org.freedesktop.Secret.Generic",
        });
    }

    [Fact]
    public async Task Locked_items_are_unlocked_through_the_prompt()
    {
        var service = new FakeSecretService();
        service.AddItem(new() { ["service"] = Ns, ["key"] = "token", ["xdg:schema"] = "org.freedesktop.Secret.Generic" }, "value", locked: true);
        var keyring = Keyring(service);

        (await keyring.LookupAsync(Ns, "token")).Should().Be("value");
        service.PromptsShown.Should().Be(1);
    }

    [Fact]
    public async Task A_dismissed_unlock_prompt_reads_as_no_value()
    {
        var service = new FakeSecretService { DismissPrompts = true };
        service.AddItem(new() { ["service"] = Ns, ["key"] = "token" }, "value", locked: true);
        var keyring = Keyring(service);

        (await keyring.LookupAsync(Ns, "token")).Should().BeNull();
        service.PromptsShown.Should().Be(1);
    }

    [Fact]
    public async Task A_locked_collection_is_unlocked_before_a_store()
    {
        var service = new FakeSecretService { CollectionLocked = true };
        var keyring = Keyring(service);

        await keyring.StoreAsync(Ns, "token", "value");

        service.PromptsShown.Should().Be(1);
        service.StoredValues(Ns, "token").Should().Equal("value");
    }

    [Fact]
    public async Task ClearAll_removes_only_the_namespaces_items()
    {
        var service = new FakeSecretService { DeleteLag = 3 };
        var keyring = Keyring(service);
        await keyring.StoreAsync(Ns, "a", "1");
        await keyring.StoreAsync(Ns, "b", "2");
        await keyring.StoreAsync("maui-secure-storage/other", "a", "3");

        await keyring.ClearAllAsync(Ns);

        (await keyring.LookupAsync(Ns, "a")).Should().BeNull();
        (await keyring.LookupAsync(Ns, "b")).Should().BeNull();
        (await keyring.LookupAsync("maui-secure-storage/other", "a")).Should().Be("3");
    }

    [Fact]
    public async Task A_lost_session_is_opened_again()
    {
        var service = new FakeSecretService();
        var keyring = Keyring(service);
        await keyring.StoreAsync(Ns, "token", "value");

        service.DropSessions();

        (await keyring.LookupAsync(Ns, "token")).Should().Be("value");
        service.SessionsOpened.Should().Be(2);
    }

    [Fact]
    public async Task No_Secret_Service_is_reported_as_unavailable()
    {
        var service = new FakeSecretService { Unavailable = new InvalidOperationException("no session bus") };
        var keyring = Keyring(service);

        await FluentActions.Awaiting(() => keyring.LookupAsync(Ns, "token"))
            .Should().ThrowAsync<SecretServiceUnavailableException>();
        await FluentActions.Awaiting(() => keyring.StoreAsync(Ns, "token", "v"))
            .Should().ThrowAsync<SecretServiceUnavailableException>();
    }

    [Fact]
    public void Missing_service_errors_mean_unavailable()
    {
        TmdsSecretServiceApi.IsUnavailable(new DBusException("org.freedesktop.DBus.Error.ServiceUnknown", "")).Should().BeTrue();
        TmdsSecretServiceApi.IsUnavailable(new DBusException("org.freedesktop.DBus.Error.NameHasNoOwner", "")).Should().BeTrue();
        TmdsSecretServiceApi.IsUnavailable(new DBusException("org.freedesktop.DBus.Error.Spawn.ChildExited", "")).Should().BeTrue();
        TmdsSecretServiceApi.IsUnavailable(new DBusException("org.freedesktop.Secret.Error.IsLocked", "")).Should().BeFalse();
    }

    #region SecureStorageService over the keyrings

    private static string TempDir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "openmaui-securestorage-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class UnavailableKeyring : ISecretKeyring
    {
        public int Calls;
        public string Name => "unavailable";
        private Task<T> Fail<T>()
        {
            Interlocked.Increment(ref Calls);
            throw new SecretServiceUnavailableException("no Secret Service");
        }
        public Task<string?> LookupAsync(string ns, string key) => Fail<string?>();
        public Task StoreAsync(string ns, string key, string value) => Fail<bool>();
        public Task<bool> ClearAsync(string ns, string key) => Fail<bool>();
        public Task ClearAllAsync(string ns) => Fail<bool>();
    }

    [Fact]
    public async Task SecureStorage_uses_the_DBus_keyring_with_the_apps_namespace()
    {
        var service = new FakeSecretService();
        var storage = new SecureStorageService(TempDir("dbus"), new ISecretKeyring[] { Keyring(service) }, Ns);

        await storage.SetAsync("token", "value");

        service.StoredValues(Ns, "token").Should().Equal("value");
        (await storage.GetAsync("token")).Should().Be("value");
        storage.Remove("token").Should().BeTrue();
        (await storage.GetAsync("token")).Should().BeNull();
        storage.Remove("token").Should().BeFalse();
    }

    [Fact]
    public async Task SecureStorage_falls_back_along_the_chain_to_the_encrypted_file()
    {
        var dir = TempDir("chain");
        var first = new UnavailableKeyring();
        var service = new FakeSecretService();
        var storage = new SecureStorageService(dir, new ISecretKeyring[] { first, Keyring(service) }, Ns);

        await storage.SetAsync("token", "value");
        service.StoredValues(Ns, "token").Should().Equal(new[] { "value" }, "the next keyring takes over");
        storage.ActiveKeyring!.Name.Should().Be("Secret Service D-Bus");
        (await storage.GetAsync("token")).Should().Be("value");
        first.Calls.Should().Be(1, "an unreachable keyring is not asked again");

        var fileOnly = new SecureStorageService(dir, new ISecretKeyring[] { new UnavailableKeyring() }, Ns);
        await fileOnly.SetAsync("other", "file value");
        fileOnly.ActiveKeyring.Should().BeNull();
        Directory.EnumerateFiles(dir).Should().NotBeEmpty("the value went to the encrypted file");
        (await fileOnly.GetAsync("other")).Should().Be("file value");
    }

    [Fact]
    public async Task SecureStorage_stays_consistent_under_100_concurrent_set_get_remove_calls()
    {
        // MAUI's Set_Get_Remove_Async_MultipleTimes against a service that lags like ksecretd.
        var service = new FakeSecretService { StoreLag = 2, DeleteLag = 2 };
        var storage = new SecureStorageService(TempDir("concurrent"), new ISecretKeyring[] { Keyring(service) }, Ns);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            var key = $"key{i}";
            var value = $"value{i}";
            await storage.SetAsync(key, value);
            (await storage.GetAsync(key)).Should().Be(value);
            storage.Remove(key).Should().BeTrue();
            (await storage.GetAsync(key)).Should().BeNull();
        }));
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task SecureStorage_runs_calls_on_the_same_key_in_order()
    {
        var service = new FakeSecretService { StoreLag = 1, DeleteLag = 1 };
        var storage = new SecureStorageService(TempDir("ordered"), new ISecretKeyring[] { Keyring(service) }, Ns);

        var tasks = Enumerable.Range(0, 50).Select(i => storage.SetAsync("shared", $"v{i}")).ToArray();
        await Task.WhenAll(tasks);

        (await storage.GetAsync("shared")).Should().Be("v49", "calls on one key complete in the order they were made");
        service.StoredValues(Ns, "shared").Should().ContainSingle();
    }

    #endregion

    #region KeyedSerializer

    [Fact]
    public async Task Calls_on_one_key_never_overlap_and_other_keys_run_alongside()
    {
        var serializer = new KeyedSerializer();
        int running = 0, maxRunning = 0;
        var gate = new TaskCompletionSource();

        var sameKey = Enumerable.Range(0, 20).Select(_ => serializer.RunAsync("k", async () =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            await Task.Delay(1);
            Interlocked.Decrement(ref running);
        })).ToArray();
        await Task.WhenAll(sameKey);
        maxRunning.Should().Be(1);

        var blocked = serializer.RunAsync("a", () => gate.Task);
        var other = serializer.RunAsync("b", () => Task.CompletedTask);
        (await Task.WhenAny(other, Task.Delay(5000))).Should().BeSameAs(other, "another key is not held up");
        gate.SetResult();
        await blocked;
        serializer.ActiveKeys.Should().Be(0, "idle keys are dropped");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    #endregion
}
