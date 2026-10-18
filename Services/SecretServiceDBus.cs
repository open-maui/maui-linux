// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services;

// Proxy shapes for the freedesktop Secret Service API
// (https://specifications.freedesktop.org/secret-service/), bus name org.freedesktop.secrets.
// Tmds.DBus 0.x: a ValueTuple return is split into several out arguments unless the return
// carries [Argument], which keeps it one struct (Item.GetSecret returns one (oayays)).

[DBusInterface("org.freedesktop.Secret.Service")]
internal interface ISecretServiceProxy : IDBusObject
{
    Task<(object output, ObjectPath result)> OpenSessionAsync(string algorithm, object input);
    Task<(ObjectPath collection, ObjectPath prompt)> CreateCollectionAsync(IDictionary<string, object> properties, string alias);
    Task<(ObjectPath[] unlocked, ObjectPath[] locked)> SearchItemsAsync(IDictionary<string, string> attributes);
    Task<(ObjectPath[] unlocked, ObjectPath prompt)> UnlockAsync(ObjectPath[] objects);
    Task<ObjectPath> ReadAliasAsync(string name);
}

[DBusInterface("org.freedesktop.Secret.Collection")]
internal interface ISecretCollectionProxy : IDBusObject
{
    Task<(ObjectPath item, ObjectPath prompt)> CreateItemAsync(IDictionary<string, object> properties, (ObjectPath, byte[], byte[], string) secret, bool replace);
}

[DBusInterface("org.freedesktop.Secret.Item")]
internal interface ISecretItemProxy : IDBusObject
{
    Task<ObjectPath> DeleteAsync();

    [return: Argument("secret")]
    Task<(ObjectPath, byte[], byte[], string)> GetSecretAsync(ObjectPath session);
}

[DBusInterface("org.freedesktop.Secret.Prompt")]
internal interface ISecretPromptProxy : IDBusObject
{
    Task PromptAsync(string windowId);
    Task DismissAsync();
    Task<IDisposable> WatchCompletedAsync(Action<(bool dismissed, object result)> handler, Action<Exception>? onError = null);
}

/// <summary>A Secret Service secret: (session, parameters, value, content type).</summary>
internal readonly record struct SecretValue(ObjectPath Session, byte[] Parameters, byte[] Value, string ContentType)
{
    public (ObjectPath, byte[], byte[], string) ToTuple() => (Session, Parameters, Value, ContentType);

    public static SecretValue FromTuple((ObjectPath, byte[], byte[], string) t) => new(t.Item1, t.Item2 ?? Array.Empty<byte>(), t.Item3 ?? Array.Empty<byte>(), t.Item4 ?? "");
}

/// <summary>
/// The Secret Service calls <see cref="DBusSecretKeyring"/> makes; <see cref="TmdsSecretServiceApi"/>
/// makes them over the session bus, tests answer them in memory. Calls throw
/// <see cref="SecretServiceUnavailableException"/> when no Secret Service can be reached and
/// <see cref="DBusException"/> for errors the service returns.
/// </summary>
internal interface ISecretServiceApi
{
    Task<(object Output, ObjectPath Session)> OpenSessionAsync(string algorithm, object input);
    Task<(ObjectPath[] Unlocked, ObjectPath[] Locked)> SearchItemsAsync(IDictionary<string, string> attributes);
    Task<(ObjectPath[] Unlocked, ObjectPath Prompt)> UnlockAsync(ObjectPath[] objects);
    Task<ObjectPath> ReadAliasAsync(string name);
    Task<(ObjectPath Collection, ObjectPath Prompt)> CreateCollectionAsync(IDictionary<string, object> properties, string alias);
    Task<(ObjectPath Item, ObjectPath Prompt)> CreateItemAsync(ObjectPath collection, IDictionary<string, object> properties, SecretValue secret, bool replace);
    Task<SecretValue> GetSecretAsync(ObjectPath item, ObjectPath session);
    Task<ObjectPath> DeleteItemAsync(ObjectPath item);

    /// <summary>Shows a prompt (Prompt.Prompt) and waits for its Completed signal.</summary>
    Task<(bool Dismissed, object? Result)> PromptAsync(ObjectPath prompt);
}

/// <summary>The Secret Service on the session bus (the connection the portal layer shares).</summary>
internal sealed class TmdsSecretServiceApi : ISecretServiceApi
{
    internal const string BusName = "org.freedesktop.secrets";
    internal const string ServicePath = "/org/freedesktop/secrets";

    /// <summary>How long a prompt (unlocking the keyring) may stay open before it is dismissed.</summary>
    internal static TimeSpan PromptTimeout { get; set; } = TimeSpan.FromMinutes(2);

    private readonly Func<Task<Connection>> _connect;

    public TmdsSecretServiceApi(Func<Task<Connection>> connect)
    {
        _connect = connect;
    }

    /// <summary>Over the shared session-bus connection.</summary>
    public static TmdsSecretServiceApi Session { get; } = new(async () => (await SessionBus.GetAsync().ConfigureAwait(false)).Connection);

    /// <summary>Errors meaning nothing owns (or can be activated as) org.freedesktop.secrets.</summary>
    internal static bool IsUnavailable(DBusException ex) =>
        ex.ErrorName is "org.freedesktop.DBus.Error.ServiceUnknown" or "org.freedesktop.DBus.Error.NameHasNoOwner"
        || ex.ErrorName.StartsWith("org.freedesktop.DBus.Error.Spawn.", StringComparison.Ordinal);

    private async Task<T> CallAsync<T>(Func<Connection, Task<T>> call)
    {
        Connection connection;
        try
        {
            connection = await _connect().ConfigureAwait(false);
        }
        catch (PortalUnavailableException ex)
        {
            throw new SecretServiceUnavailableException(ex.Message, ex);
        }
        try
        {
            return await call(connection).ConfigureAwait(false);
        }
        catch (DBusException ex) when (IsUnavailable(ex))
        {
            throw new SecretServiceUnavailableException($"{ex.ErrorName}: {ex.ErrorMessage}", ex);
        }
        catch (Exception ex) when (ex is DisconnectedException or ConnectException)
        {
            throw new SecretServiceUnavailableException($"The session bus connection failed: {ex.Message}", ex);
        }
    }

    private static ISecretServiceProxy Service(Connection c) => c.CreateProxy<ISecretServiceProxy>(BusName, ServicePath);

    public Task<(object Output, ObjectPath Session)> OpenSessionAsync(string algorithm, object input)
        => CallAsync(async c =>
        {
            var (output, result) = await Service(c).OpenSessionAsync(algorithm, input).ConfigureAwait(false);
            return (output, result);
        });

    public Task<(ObjectPath[] Unlocked, ObjectPath[] Locked)> SearchItemsAsync(IDictionary<string, string> attributes)
        => CallAsync(async c =>
        {
            var (unlocked, locked) = await Service(c).SearchItemsAsync(attributes).ConfigureAwait(false);
            return (unlocked ?? Array.Empty<ObjectPath>(), locked ?? Array.Empty<ObjectPath>());
        });

    public Task<(ObjectPath[] Unlocked, ObjectPath Prompt)> UnlockAsync(ObjectPath[] objects)
        => CallAsync(async c =>
        {
            var (unlocked, prompt) = await Service(c).UnlockAsync(objects).ConfigureAwait(false);
            return (unlocked ?? Array.Empty<ObjectPath>(), prompt);
        });

    public Task<ObjectPath> ReadAliasAsync(string name)
        => CallAsync(c => Service(c).ReadAliasAsync(name));

    public Task<(ObjectPath Collection, ObjectPath Prompt)> CreateCollectionAsync(IDictionary<string, object> properties, string alias)
        => CallAsync(async c =>
        {
            var (collection, prompt) = await Service(c).CreateCollectionAsync(properties, alias).ConfigureAwait(false);
            return (collection, prompt);
        });

    public Task<(ObjectPath Item, ObjectPath Prompt)> CreateItemAsync(ObjectPath collection, IDictionary<string, object> properties, SecretValue secret, bool replace)
        => CallAsync(async c =>
        {
            var proxy = c.CreateProxy<ISecretCollectionProxy>(BusName, collection);
            var (item, prompt) = await proxy.CreateItemAsync(properties, secret.ToTuple(), replace).ConfigureAwait(false);
            return (item, prompt);
        });

    public Task<SecretValue> GetSecretAsync(ObjectPath item, ObjectPath session)
        => CallAsync(async c =>
        {
            var proxy = c.CreateProxy<ISecretItemProxy>(BusName, item);
            return SecretValue.FromTuple(await proxy.GetSecretAsync(session).ConfigureAwait(false));
        });

    public Task<ObjectPath> DeleteItemAsync(ObjectPath item)
        => CallAsync(c => c.CreateProxy<ISecretItemProxy>(BusName, item).DeleteAsync());

    public Task<(bool Dismissed, object? Result)> PromptAsync(ObjectPath prompt)
        => CallAsync(async c =>
        {
            var proxy = c.CreateProxy<ISecretPromptProxy>(BusName, prompt);
            var completed = new TaskCompletionSource<(bool, object?)>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watch = await proxy.WatchCompletedAsync(
                args => completed.TrySetResult((args.dismissed, args.result)),
                ex => completed.TrySetException(ex)).ConfigureAwait(false);
            await proxy.PromptAsync("").ConfigureAwait(false);
            try
            {
                return await completed.Task.WaitAsync(PromptTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                DiagnosticLog.Warn("SecureStorageService", "A keyring prompt was not answered; dismissing it");
                try { await proxy.DismissAsync().ConfigureAwait(false); }
                catch (DBusException) { }
                return (true, null);
            }
        });
}

/// <summary>
/// The dh-ietf1024-sha256-aes128-cbc-pkcs7 transport encryption of the Secret Service API:
/// Diffie-Hellman over the 1024-bit MODP group of RFC 2409 (generator 2), the AES-128 key is
/// HKDF-SHA256 of the shared secret (big-endian, padded to the prime's 128 bytes) with no salt
/// and no info, as libsecret and gnome-keyring derive it; each secret is AES-128-CBC with
/// PKCS#7 padding and a random 16-byte IV carried as the secret's parameters.
/// </summary>
internal static class SecretServiceDh
{
    public const string Algorithm = "dh-ietf1024-sha256-aes128-cbc-pkcs7";

    private const string PrimeHex =
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD1" +
        "29024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
        "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
        "E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
        "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE65381" +
        "FFFFFFFFFFFFFFFF";

    private const int PrimeBytes = 128;

    internal static readonly BigInteger Prime = BigInteger.Parse("0" + PrimeHex, System.Globalization.NumberStyles.HexNumber);
    internal static readonly BigInteger Generator = 2;

    /// <summary>A private exponent and the matching public key (big-endian, unsigned).</summary>
    public static (BigInteger Private, byte[] Public) GenerateKeyPair()
    {
        var bytes = RandomNumberGenerator.GetBytes(PrimeBytes);
        // In [2, p - 2].
        var priv = new BigInteger(bytes, isUnsigned: true, isBigEndian: true) % (Prime - 3) + 2;
        var pub = BigInteger.ModPow(Generator, priv, Prime);
        return (priv, pub.ToByteArray(isUnsigned: true, isBigEndian: true));
    }

    /// <summary>The AES-128 key shared with the peer whose public key is <paramref name="peerPublic"/>.</summary>
    public static byte[] DeriveKey(BigInteger priv, byte[] peerPublic)
    {
        var peer = new BigInteger(peerPublic, isUnsigned: true, isBigEndian: true);
        if (peer <= 1 || peer >= Prime - 1)
            throw new CryptographicException("Invalid Diffie-Hellman public key from the Secret Service");
        var shared = BigInteger.ModPow(peer, priv, Prime).ToByteArray(isUnsigned: true, isBigEndian: true);
        var ikm = new byte[PrimeBytes];
        shared.CopyTo(ikm, PrimeBytes - shared.Length);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt: Array.Empty<byte>(), info: Array.Empty<byte>());
    }

    public static (byte[] Iv, byte[] Cipher) Encrypt(byte[] key, byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = RandomNumberGenerator.GetBytes(16);
        return (iv, aes.EncryptCbc(plain, iv, PaddingMode.PKCS7));
    }

    public static byte[] Decrypt(byte[] key, byte[] iv, byte[] cipher)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptCbc(cipher, iv, PaddingMode.PKCS7);
    }
}

/// <summary>
/// The keyring over a long-lived Secret Service D-Bus session (org.freedesktop.Secret.Service):
/// one OpenSession for the process (dh-ietf1024-sha256-aes128-cbc-pkcs7, else plain, which
/// libsecret also accepts on the session bus), SearchItems, CreateItem with replace in the
/// "default" collection, Item.GetSecret and Item.Delete, and Unlock with its prompt when items or
/// the collection are locked. Items have the attributes secret-tool gives them (service, key and
/// xdg:schema = org.freedesktop.Secret.Generic), so values stored by earlier releases through
/// secret-tool are found and replaced. Calls run one at a time and each returns once a search
/// reflects it, which keeps a daemon that acknowledges writes before reads see them (KDE's
/// ksecretd) consistent for the caller.
/// </summary>
internal sealed class DBusSecretKeyring : ISecretKeyring
{
    internal const string GenericSchema = "org.freedesktop.Secret.Generic";
    internal const string DefaultAliasPath = "/org/freedesktop/secrets/aliases/default";
    private const string PlainAlgorithm = "plain";
    private const string ContentType = "text/plain";
    private const string LabelProperty = "org.freedesktop.Secret.Item.Label";
    private const string AttributesProperty = "org.freedesktop.Secret.Item.Attributes";
    private const string CollectionLabelProperty = "org.freedesktop.Secret.Collection.Label";
    private static readonly ObjectPath NoPrompt = new("/");

    private sealed record Session(ObjectPath Path, byte[]? Key);

    private sealed class SecretDecodeException : Exception
    {
        public SecretDecodeException(Exception inner) : base("The secret could not be decrypted", inner) { }
    }

    private readonly ISecretServiceApi _api;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private Session? _session;
    private bool _plainOnly;

    public DBusSecretKeyring(ISecretServiceApi api, bool preferEncryption = true)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _plainOnly = !preferEncryption;
    }

    public string Name => "Secret Service D-Bus";

    /// <summary>How many times a call checks that the keyring reflects it, and the wait between checks.</summary>
    internal int ConfirmAttempts { get; set; } = 60;
    internal TimeSpan ConfirmDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>The session's algorithm ("plain" or the DH one), null before the first call.</summary>
    internal string? SessionAlgorithm => _session == null ? null : _session.Key == null ? PlainAlgorithm : SecretServiceDh.Algorithm;

    internal static Dictionary<string, string> SearchAttributes(string ns, string key) =>
        new(StringComparer.Ordinal) { ["service"] = ns, ["key"] = key };

    internal static Dictionary<string, string> ItemAttributes(string ns, string key) =>
        new(StringComparer.Ordinal) { ["service"] = ns, ["key"] = key, ["xdg:schema"] = GenericSchema };

    public Task<string?> LookupAsync(string ns, string key)
        => Serialized(() => WithSession(_ => LookupCoreAsync(ns, key)));

    public Task StoreAsync(string ns, string key, string value)
        => Serialized(async () =>
        {
            await StoreCoreAsync(ns, key, value).ConfigureAwait(false);
            return true;
        });

    public Task<bool> ClearAsync(string ns, string key)
        => Serialized(() => ClearCoreAsync(SearchAttributes(ns, key)));

    public Task ClearAllAsync(string ns)
        => Serialized(() => ClearCoreAsync(new Dictionary<string, string>(StringComparer.Ordinal) { ["service"] = ns }));

    private async Task<T> Serialized<T>(Func<Task<T>> operation)
    {
        await _operations.WaitAsync().ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }
    }

    #region Session

    private async Task<Session> EnsureSessionAsync()
    {
        if (_session != null)
            return _session;
        if (!_plainOnly)
        {
            var (priv, pub) = SecretServiceDh.GenerateKeyPair();
            try
            {
                var (output, path) = await _api.OpenSessionAsync(SecretServiceDh.Algorithm, pub).ConfigureAwait(false);
                if (output is byte[] peer && peer.Length > 0)
                    return _session = new Session(path, SecretServiceDh.DeriveKey(priv, peer));
                DiagnosticLog.Debug("SecureStorageService", "The Secret Service sent no DH public key; using a plain session");
            }
            catch (DBusException ex)
            {
                // org.freedesktop.DBus.Error.NotSupported (or a daemon's own error name).
                DiagnosticLog.Debug("SecureStorageService", $"Encrypted Secret Service session refused ({ex.ErrorName}); using a plain session");
            }
            catch (CryptographicException ex)
            {
                DiagnosticLog.Debug("SecureStorageService", $"Encrypted Secret Service session failed ({ex.Message}); using a plain session");
            }
            _plainOnly = true;
        }
        var (_, plainPath) = await _api.OpenSessionAsync(PlainAlgorithm, "").ConfigureAwait(false);
        return _session = new Session(plainPath, null);
    }

    /// <summary>
    /// Runs a call that uses the session; a session the service no longer knows (the bus
    /// reconnected, the daemon restarted) is opened again and the call repeated once. A secret
    /// that does not decrypt drops to a plain session for good.
    /// </summary>
    private async Task<T> WithSession<T>(Func<Session, Task<T>> call)
    {
        for (var attempt = 0; ; attempt++)
        {
            var session = await EnsureSessionAsync().ConfigureAwait(false);
            try
            {
                return await call(session).ConfigureAwait(false);
            }
            catch (SecretDecodeException ex) when (attempt == 0 && session.Key != null)
            {
                DiagnosticLog.Warn("SecureStorageService", $"Secret Service transport encryption failed ({ex.InnerException?.Message}); using a plain session");
                _plainOnly = true;
                _session = null;
            }
            catch (DBusException ex) when (attempt == 0 && IsSessionError(ex))
            {
                DiagnosticLog.Debug("SecureStorageService", $"Secret Service session lost ({ex.ErrorName}); opening a new one");
                _session = null;
            }
        }
    }

    private static bool IsSessionError(DBusException ex) =>
        ex.ErrorName is "org.freedesktop.Secret.Error.NoSession" or "org.freedesktop.DBus.Error.UnknownObject"
            or "org.freedesktop.DBus.Error.UnknownMethod" or "org.freedesktop.DBus.Error.InvalidArgs";

    private SecretValue Encode(Session session, string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        if (session.Key == null)
            return new SecretValue(session.Path, Array.Empty<byte>(), plain, ContentType);
        var (iv, cipher) = SecretServiceDh.Encrypt(session.Key, plain);
        return new SecretValue(session.Path, iv, cipher, ContentType);
    }

    private static string Decode(Session session, SecretValue secret)
    {
        if (session.Key == null)
            return Encoding.UTF8.GetString(secret.Value);
        try
        {
            return Encoding.UTF8.GetString(SecretServiceDh.Decrypt(session.Key, secret.Parameters, secret.Value));
        }
        catch (CryptographicException ex)
        {
            throw new SecretDecodeException(ex);
        }
    }

    #endregion

    #region Items

    /// <summary>The items matching <paramref name="attributes"/>, locked ones unlocked first (a dismissed prompt leaves them out).</summary>
    private async Task<List<ObjectPath>> FindAsync(IDictionary<string, string> attributes)
    {
        var (unlocked, locked) = await _api.SearchItemsAsync(attributes).ConfigureAwait(false);
        var result = new List<ObjectPath>(unlocked);
        if (locked.Length > 0)
            result.AddRange(await UnlockAsync(locked).ConfigureAwait(false));
        return result;
    }

    /// <summary>Whether a search lists any item (locked or not) for <paramref name="attributes"/>.</summary>
    private async Task<int> CountAsync(IDictionary<string, string> attributes)
    {
        var (unlocked, locked) = await _api.SearchItemsAsync(attributes).ConfigureAwait(false);
        return unlocked.Length + locked.Length;
    }

    private async Task<ObjectPath[]> UnlockAsync(ObjectPath[] objects)
    {
        var (unlocked, prompt) = await _api.UnlockAsync(objects).ConfigureAwait(false);
        if (prompt == NoPrompt || string.IsNullOrEmpty(prompt.ToString()))
            return unlocked;
        var (dismissed, result) = await _api.PromptAsync(prompt).ConfigureAwait(false);
        if (dismissed)
        {
            DiagnosticLog.Warn("SecureStorageService", "Unlocking the keyring was dismissed");
            return unlocked;
        }
        return result is ObjectPath[] paths ? unlocked.Concat(paths).ToArray() : objects;
    }

    private async Task<string?> LookupCoreAsync(string ns, string key)
    {
        var session = await EnsureSessionAsync().ConfigureAwait(false);
        foreach (var item in await FindAsync(SearchAttributes(ns, key)).ConfigureAwait(false))
        {
            SecretValue secret;
            try
            {
                secret = await _api.GetSecretAsync(item, session.Path).ConfigureAwait(false);
            }
            catch (DBusException ex) when (ex.ErrorName is "org.freedesktop.Secret.Error.NoSuchObject" or "org.freedesktop.Secret.Error.IsLocked")
            {
                // Deleted (or locked again) since the search.
                continue;
            }
            return Decode(session, secret);
        }
        return null;
    }

    private async Task StoreCoreAsync(string ns, string key, string value)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var created = await WithSession(session => CreateAsync(session, ns, key, value)).ConfigureAwait(false);

            // An item stored without xdg:schema (or a duplicate a racing writer created) also
            // matches the lookup: keep only the item just written (when the service said which).
            if (created is { } createdItem)
            {
                foreach (var other in await FindAsync(SearchAttributes(ns, key)).ConfigureAwait(false))
                {
                    if (other != createdItem)
                        await DeleteAsync(other).ConfigureAwait(false);
                }
            }

            // Return once a lookup reads the value back, as on the other platforms.
            bool encrypted = _session?.Key != null;
            bool undecryptable = false;
            string? seen = null;
            for (var attempt = 0; attempt < ConfirmAttempts; attempt++)
            {
                try
                {
                    seen = await LookupCoreAsync(ns, key).ConfigureAwait(false);
                }
                catch (SecretDecodeException) when (encrypted)
                {
                    undecryptable = true;
                    break;
                }
                catch (DBusException ex) when (IsSessionError(ex))
                {
                    _session = null;
                    seen = null;
                    continue;
                }
                if (seen == value)
                    return;
                await Task.Delay(ConfirmDelay).ConfigureAwait(false);
            }

            if (encrypted && (undecryptable || seen != null))
            {
                // What the service holds does not read back as what we sent: it did not derive
                // the same transport key. Store the value again over a plain session.
                DiagnosticLog.Warn("SecureStorageService", "A stored secret did not read back over the encrypted session; storing it again over a plain session");
                _plainOnly = true;
                _session = null;
                continue;
            }
            DiagnosticLog.Warn("SecureStorageService", $"The keyring did not reflect the change to '{key}' in time");
            return;
        }
    }

    /// <summary>Creates (replacing) the item; its path, or null when a prompt completed without naming it.</summary>
    private async Task<ObjectPath?> CreateAsync(Session session, string ns, string key, string value)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [LabelProperty] = key,
            [AttributesProperty] = ItemAttributes(ns, key),
        };
        var collection = await DefaultCollectionAsync().ConfigureAwait(false);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var (item, prompt) = await _api.CreateItemAsync(collection, properties, Encode(session, value), replace: true).ConfigureAwait(false);
                if (prompt == NoPrompt)
                    return item;
                var (dismissed, result) = await _api.PromptAsync(prompt).ConfigureAwait(false);
                if (dismissed)
                    throw new InvalidOperationException("Failed to store secret: the keyring prompt was dismissed");
                return result is ObjectPath createdPath ? createdPath : null;
            }
            catch (DBusException ex) when (attempt == 0 && ex.ErrorName == "org.freedesktop.Secret.Error.IsLocked")
            {
                await UnlockAsync(new[] { collection }).ConfigureAwait(false);
            }
            catch (DBusException ex) when (attempt == 0 && ex.ErrorName is "org.freedesktop.Secret.Error.NoSuchObject" or "org.freedesktop.DBus.Error.UnknownObject"
                && collection.ToString() == DefaultAliasPath)
            {
                // No default collection at all: create it (the service prompts for its password).
                collection = await CreateDefaultCollectionAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>The "default" alias's collection, or the alias path itself (as libsecret uses it) when the alias reads "/".</summary>
    private async Task<ObjectPath> DefaultCollectionAsync()
    {
        try
        {
            var alias = await _api.ReadAliasAsync("default").ConfigureAwait(false);
            if (alias != NoPrompt && !string.IsNullOrEmpty(alias.ToString()))
                return alias;
        }
        catch (DBusException ex)
        {
            DiagnosticLog.Debug("SecureStorageService", $"ReadAlias(default) failed: {ex.ErrorName}");
        }
        return new ObjectPath(DefaultAliasPath);
    }

    private async Task<ObjectPath> CreateDefaultCollectionAsync()
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal) { [CollectionLabelProperty] = "Login" };
        var (collection, prompt) = await _api.CreateCollectionAsync(properties, "default").ConfigureAwait(false);
        if (prompt == NoPrompt)
            return collection;
        var (dismissed, result) = await _api.PromptAsync(prompt).ConfigureAwait(false);
        if (dismissed || result is not ObjectPath created)
            throw new InvalidOperationException("Failed to store secret: creating the default keyring was dismissed");
        return created;
    }

    private async Task DeleteAsync(ObjectPath item)
    {
        try
        {
            var prompt = await _api.DeleteItemAsync(item).ConfigureAwait(false);
            if (prompt != NoPrompt && !string.IsNullOrEmpty(prompt.ToString()))
                await _api.PromptAsync(prompt).ConfigureAwait(false);
        }
        catch (DBusException ex) when (ex.ErrorName is "org.freedesktop.Secret.Error.NoSuchObject" or "org.freedesktop.DBus.Error.UnknownObject")
        {
            // Already gone.
        }
    }

    private async Task<bool> ClearCoreAsync(IDictionary<string, string> attributes)
    {
        var items = await FindAsync(attributes).ConfigureAwait(false);
        foreach (var item in items)
            await DeleteAsync(item).ConfigureAwait(false);
        // Return once a search no longer lists them (ksecretd briefly lists deleted items).
        for (var attempt = 0; attempt < ConfirmAttempts && await CountAsync(attributes).ConfigureAwait(false) > 0; attempt++)
        {
            if (attempt > 0 && attempt % 10 == 0)
            {
                // Still listed: delete what is left again (a racing writer, a dropped delete).
                foreach (var item in await FindAsync(attributes).ConfigureAwait(false))
                    await DeleteAsync(item).ConfigureAwait(false);
            }
            await Task.Delay(ConfirmDelay).ConfigureAwait(false);
        }
        return items.Count > 0;
    }

    #endregion
}
