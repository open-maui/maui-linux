// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using System.Text;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux secure storage: the session's Secret Service (the desktop keyring: gnome-keyring, KDE's
/// ksecretd, KeePassXC) over one long-lived D-Bus session, else through libsecret's
/// <c>secret-tool</c>, else an encrypted file.
/// </summary>
/// <remarks>
/// The keyring is used over D-Bus (org.freedesktop.Secret.Service through Tmds.DBus, see
/// <see cref="DBusSecretKeyring"/>); when no Secret Service answers there, <c>secret-tool</c> is
/// tried (when installed), and when neither reaches one (no session bus, no keyring daemon) the
/// encrypted-file store takes over. Calls on the same key run one after another, and each returns
/// once the keyring reflects it. <c>OPENMAUI_SECURESTORAGE=secret-tool</c> skips the D-Bus path,
/// <c>=file</c> uses only the encrypted file.
///
/// Storage is per app, as on every other platform: keyring items carry the attribute
/// <c>service = maui-secure-storage/&lt;AppInfo.PackageName&gt;</c>, and the encrypted-file
/// fallback lives under <c>$XDG_DATA_HOME/&lt;app&gt;/.maui-secure</c>. Earlier releases stored
/// every app's values together (keyring service <c>maui-secure-storage</c>, files in
/// <c>~/.maui-secure</c>), so one app could read another's values and RemoveAll could not
/// clear an app's own. That store does not record which app wrote a value, so nothing is copied
/// in bulk: when an app asks for a key it has no value for, the shared store is read for that key
/// alone and the value moved into the app's own store (and left there, since another app may
/// still read it). Once the app has set, removed or looked up a key, or cleared its store, the
/// shared store is not consulted for it again.
///
/// Encrypted-file format. Version 1 (legacy, still read and written when no
/// portal key exists): [16-byte IV][AES-CBC ciphertext] under a key derived
/// from machine-id, user name and service name. Version 2 (written only when
/// the xdg-desktop-portal Secret interface supplies a per-app secret, which by
/// default means inside a sandbox, or with OPENMAUI_PORTALS=prefer):
/// ["OMSS" 0x02][12-byte nonce][16-byte tag][AES-GCM ciphertext] under an
/// HKDF-SHA256 key from the portal secret. Reads accept both, so existing v1
/// files stay readable and are rewritten as v2 the next time they are set.
/// </remarks>
public class SecureStorageService : ISecureStorage
{
    private const string ServiceName = "maui-secure-storage";
    private const string FallbackDirectory = ".maui-secure";
    // In this app's fallback directory: every legacy key is settled (after RemoveAll)...
    private const string MigratedMarker = ".migrated";
    // ...or the hashes of the keys that are, one per line.
    private const string SettledFile = ".legacy-settled";
    private readonly string? _fixedFallbackPath;
    private readonly string? _fixedNamespace;
    // The legacy shared store (directory and keyring service); null: never consulted.
    private readonly string? _legacyDir;
    private readonly string? _legacyNamespace;
    // The keyrings in fallback order (D-Bus, then secret-tool); empty: encrypted file only.
    private readonly ISecretKeyring[] _keyrings;
    // The first keyring still believed reachable. Advanced when one reports no Secret Service
    // (no session bus, no keyring daemon: CI, SSH, a bare window manager); past the end the
    // encrypted-file store takes over.
    private int _keyringIndex;
    private readonly KeyedSerializer _keys = new();
    private readonly Lazy<Task<byte[]?>>? _portalKey;
    private readonly object _legacyLock = new();
    private HashSet<string>? _settled;

    public SecureStorageService()
    {
        _legacyDir = LegacyFallbackPath;
        _legacyNamespace = ServiceName;
        _keyrings = DefaultKeyrings(useSecretService: true);
        if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred))
            _portalKey = PortalKeyFrom(DesktopPortal.Current);
    }

    /// <summary>
    /// Test/host constructor that also chooses the portal key source
    /// (<paramref name="portal"/> null keeps the legacy machine key only).
    /// </summary>
    internal SecureStorageService(string fallbackPath, bool useSecretService, IDesktopPortal? portal)
        : this(fallbackPath, useSecretService)
    {
        _portalKey = portal == null ? null : PortalKeyFrom(portal);
    }

    private static Lazy<Task<byte[]?>> PortalKeyFrom(IDesktopPortal portal)
        => new(() => Task.Run(async () =>
        {
            try
            {
                return await new PortalSecretKey(portal).DeriveStorageKeyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("SecureStorageService", $"Portal secret unavailable: {ex.Message}");
                return null;
            }
        }));

    private async Task<byte[]?> GetPortalKeyAsync()
        => _portalKey == null ? null : await _portalKey.Value.ConfigureAwait(false);

    /// <summary>
    /// Creates a store with an explicit fallback directory and backend choice
    /// (tests, hosts without a keyring). <paramref name="useSecretService"/>
    /// true uses the Secret Service (D-Bus, then <c>secret-tool</c> when on PATH).
    /// </summary>
    internal SecureStorageService(string fallbackPath, bool useSecretService)
        : this(fallbackPath, useSecretService, keyringNamespace: null)
    {
    }

    /// <summary>
    /// Explicit fallback directory, backend and keyring namespace (the <c>service</c> attribute;
    /// null derives it from AppInfo); the legacy shared store is never consulted.
    /// </summary>
    internal SecureStorageService(string fallbackPath, bool useSecretService, string? keyringNamespace)
    {
        _fixedFallbackPath = fallbackPath;
        _fixedNamespace = keyringNamespace;
        _keyrings = DefaultKeyrings(useSecretService);
    }

    /// <summary>
    /// Explicit keyrings in fallback order (tests): the encrypted file in
    /// <paramref name="fallbackPath"/> takes over once every keyring reports no Secret Service.
    /// </summary>
    internal SecureStorageService(string fallbackPath, IReadOnlyList<ISecretKeyring> keyrings, string? keyringNamespace = null)
    {
        _fixedFallbackPath = fallbackPath;
        _fixedNamespace = keyringNamespace;
        _keyrings = keyrings.ToArray();
    }

    /// <summary>The keyrings a store uses, in fallback order.</summary>
    private static ISecretKeyring[] DefaultKeyrings(bool useSecretService)
    {
        if (!useSecretService)
            return Array.Empty<ISecretKeyring>();
        var choice = Environment.GetEnvironmentVariable("OPENMAUI_SECURESTORAGE")?.Trim().ToLowerInvariant();
        if (choice == "file")
            return Array.Empty<ISecretKeyring>();
        var keyrings = new List<ISecretKeyring>();
        if (choice != "secret-tool")
            keyrings.Add(new DBusSecretKeyring(TmdsSecretServiceApi.Session));
        if (SecretToolKeyring.IsInstalled())
            keyrings.Add(new SecretToolKeyring());
        return keyrings.ToArray();
    }

    /// <summary>The keyring in use now (null: the encrypted-file store).</summary>
    internal ISecretKeyring? ActiveKeyring
    {
        get
        {
            var index = Volatile.Read(ref _keyringIndex);
            return index < _keyrings.Length ? _keyrings[index] : null;
        }
    }

    /// <summary>As above, reading <paramref name="legacyFallbackPath"/> and <paramref name="legacyNamespace"/> as the legacy shared store (tests).</summary>
    internal SecureStorageService(string fallbackPath, bool useSecretService, string? keyringNamespace, string legacyFallbackPath, string? legacyNamespace)
        : this(fallbackPath, useSecretService, keyringNamespace)
    {
        _legacyDir = legacyFallbackPath;
        _legacyNamespace = legacyNamespace;
    }

    private bool UseSecretService => ActiveKeyring != null;

    /// <summary>The messages secret-tool prints when no Secret Service can be reached at all.</summary>
    internal static bool IsUnreachable(string error) => SecretToolKeyring.IsUnreachable(error);

    /// <summary>
    /// Runs <paramref name="operation"/> on the first reachable keyring; a keyring that reports
    /// no Secret Service is skipped from then on, and with none left <paramref name="fileStore"/> runs.
    /// </summary>
    private async Task<T> OnKeyringAsync<T>(Func<ISecretKeyring, Task<T>> operation, Func<Task<T>> fileStore)
    {
        while (true)
        {
            var index = Volatile.Read(ref _keyringIndex);
            if (index >= _keyrings.Length)
                return await fileStore().ConfigureAwait(false);
            var keyring = _keyrings[index];
            try
            {
                return await operation(keyring).ConfigureAwait(false);
            }
            catch (SecretServiceUnavailableException ex)
            {
                if (Interlocked.CompareExchange(ref _keyringIndex, index + 1, index) == index)
                {
                    var next = index + 1 < _keyrings.Length ? $"trying {_keyrings[index + 1].Name}" : "using the encrypted-file store";
                    DiagnosticLog.Warn("SecureStorageService", $"No Secret Service reachable through {keyring.Name} ({ex.Message}); {next}");
                }
            }
        }
    }

    /// <summary>This app's encrypted-file directory: $XDG_DATA_HOME/&lt;app&gt;/.maui-secure.</summary>
    private string FallbackPath => _fixedFallbackPath ?? Path.Combine(DataHome(), AppDirectoryName(), FallbackDirectory);

    /// <summary>The legacy shared directory every app used before (~/.maui-secure).</summary>
    internal static string LegacyFallbackPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), FallbackDirectory);

    /// <summary>The keyring <c>service</c> attribute of this app's items.</summary>
    internal string KeyringNamespace => _fixedNamespace ?? NamespaceFor(AppPackageName());

    internal static string NamespaceFor(string packageName) => $"{ServiceName}/{packageName}";

    private static string AppPackageName()
    {
        try
        {
            var name = Microsoft.Maui.ApplicationModel.AppInfo.Current?.PackageName;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
            // The portable AppInfo stub throws until EssentialsPatches has run.
        }
        return System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "MauiApp";
    }

    private static string AppDirectoryName()
    {
        var name = AppInfoService.CurrentStorageName() ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "MauiApp";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static string DataHome()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return string.IsNullOrEmpty(dataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : dataHome;
    }

    public Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        return _keys.RunAsync(key, () => GetOwnOrLegacyAsync(key));
    }

    private async Task<string?> GetOwnOrLegacyAsync(string key)
    {
        var value = await GetFromSecretServiceOrFileAsync(key).ConfigureAwait(false);
        if (value != null || !IsLegacyUnsettled(key))
            return value;

        // Only this key, only now: the app asked for it and has none of its own.
        var legacy = await ReadLegacyAsync(key).ConfigureAwait(false);
        if (legacy != null)
            await SetOwnAsync(key, legacy).ConfigureAwait(false);
        SettleLegacy(key);
        return legacy;
    }

    public Task SetAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        return _keys.RunAsync(key, () =>
        {
            SettleLegacy(key);
            return SetOwnAsync(key, value);
        });
    }

    private Task SetOwnAsync(string key, string value) =>
        OnKeyringAsync(async keyring =>
        {
            await keyring.StoreAsync(KeyringNamespace, key, value).ConfigureAwait(false);
            // A value an unreachable keyring once sent to the file must not shadow this one.
            RemoveFromFallback(key);
            return true;
        }, async () =>
        {
            await SetInFallbackAsync(key, value).ConfigureAwait(false);
            return true;
        });

    /// <summary>Removes the key; true when it was stored.</summary>
    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        // Off the caller's thread: the keyring calls are asynchronous, and a caller on a
        // thread with a synchronization context must not be needed to finish them.
        return Task.Run(() => _keys.RunAsync(key, async () =>
        {
            // A removed key stays removed: the shared store must not bring it back.
            SettleLegacy(key);
            var removed = await OnKeyringAsync(keyring => keyring.ClearAsync(KeyringNamespace, key), () => Task.FromResult(false)).ConfigureAwait(false);
            return RemoveFromFallback(key) || removed;
        })).GetAwaiter().GetResult();
    }

    /// <summary>Removes every value this app stored (and only this app's).</summary>
    public void RemoveAll()
    {
        SettleAllLegacy();
        var ns = KeyringNamespace;
        // As for a single key: returns once the keyring no longer lists the app's items.
        Task.Run(() => OnKeyringAsync(async keyring =>
        {
            await keyring.ClearAllAsync(ns).ConfigureAwait(false);
            return true;
        }, () => Task.FromResult(true))).GetAwaiter().GetResult();

        // The fallback holds values a failed keyring write fell back to: clear it either way.
        var dir = FallbackPath;
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (Path.GetFileName(file) is MigratedMarker or SettledFile)
                    continue;
                try { File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    #region Legacy shared store

    /// <summary>True when the legacy shared store may still hold this app's value for <paramref name="key"/>.</summary>
    private bool IsLegacyUnsettled(string key)
    {
        if (_legacyDir == null)
            return false;
        lock (_legacyLock)
            return !LoadSettled().Contains(KeyHash(key)) && !File.Exists(Path.Combine(FallbackPath, MigratedMarker));
    }

    /// <summary>The legacy shared store's value for <paramref name="key"/> (keyring first, then its encrypted file).</summary>
    private async Task<string?> ReadLegacyAsync(string key)
    {
        try
        {
            string? value = null;
            if (_legacyNamespace != null)
                value = await OnKeyringAsync(keyring => keyring.LookupAsync(_legacyNamespace, key), () => Task.FromResult<string?>(null)).ConfigureAwait(false);
            if (value == null && Directory.Exists(_legacyDir)
                && Path.GetFullPath(_legacyDir!) != Path.GetFullPath(FallbackPath))
                value = await GetFromFallbackAsync(_legacyDir!, key).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("SecureStorageService", $"Reading the shared legacy store failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Records that <paramref name="key"/> no longer comes from the legacy shared store.</summary>
    private void SettleLegacy(string key)
    {
        if (_legacyDir == null)
            return;
        var hash = KeyHash(key);
        lock (_legacyLock)
        {
            if (!LoadSettled().Add(hash))
                return;
            try
            {
                EnsureFallbackDirectory();
                var path = Path.Combine(FallbackPath, SettledFile);
                File.AppendAllText(path, hash + "\n");
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("SecureStorageService", $"Recording a settled key failed: {ex.Message}");
            }
        }
    }

    /// <summary>After RemoveAll: no key comes from the legacy shared store again.</summary>
    private void SettleAllLegacy()
    {
        if (_legacyDir == null)
            return;
        try
        {
            EnsureFallbackDirectory();
            File.WriteAllText(Path.Combine(FallbackPath, MigratedMarker), "");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("SecureStorageService", $"Recording the cleared store failed: {ex.Message}");
        }
    }

    private HashSet<string> LoadSettled()
    {
        if (_settled != null)
            return _settled;
        _settled = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Combine(FallbackPath, SettledFile);
        try
        {
            if (File.Exists(path))
                foreach (var line in File.ReadAllLines(path))
                    if (line.Length > 0)
                        _settled.Add(line);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return _settled;
    }

    private static string KeyHash(string key) => Path.GetFileName(GetFallbackFilePath("", key));

    #endregion

    #region Secret Service

    /// <summary>The keyring's value, else the encrypted file's (a value stored while the keyring could not be reached).</summary>
    private async Task<string?> GetFromSecretServiceOrFileAsync(string key)
    {
        var value = await OnKeyringAsync(keyring => keyring.LookupAsync(KeyringNamespace, key), () => Task.FromResult<string?>(null)).ConfigureAwait(false);
        return value ?? await GetFromFallbackAsync(FallbackPath, key).ConfigureAwait(false);
    }

    #endregion

    #region Fallback Encrypted Storage

    private async Task<string?> GetFromFallbackAsync(string dir, string key)
    {
        var filePath = GetFallbackFilePath(dir, key);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(filePath);
            if (IsVersion2(encryptedData))
            {
                var portalKey = await GetPortalKeyAsync();
                if (portalKey != null && TryDecryptVersion2(portalKey, encryptedData, out var plain))
                    return plain;
                // A v1 file whose random IV happens to start with the v2 magic
                // is still v1; otherwise this is a v2 file without its key.
            }
            return DecryptData(encryptedData);
        }
        catch
        {
            return null;
        }
    }

    private async Task SetInFallbackAsync(string key, string value)
    {
        EnsureFallbackDirectory();

        var filePath = GetFallbackFilePath(FallbackPath, key);
        var portalKey = await GetPortalKeyAsync();
        var encryptedData = portalKey != null ? EncryptVersion2(portalKey, value) : EncryptData(value);

        await File.WriteAllBytesAsync(filePath, encryptedData);

        // Set restrictive permissions
        File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private bool RemoveFromFallback(string key)
    {
        var filePath = GetFallbackFilePath(FallbackPath, key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    private static string GetFallbackFilePath(string dir, string key)
    {
        // Hash the key to create a safe filename
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant();
        return Path.Combine(dir, fileName);
    }

    private void EnsureFallbackDirectory()
    {
        var dir = FallbackPath;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            // Set restrictive permissions on the directory
            File.SetUnixFileMode(dir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static readonly byte[] Version2Magic = { (byte)'O', (byte)'M', (byte)'S', (byte)'S', 0x02 };
    private const int GcmNonceSize = 12;
    private const int GcmTagSize = 16;

    internal static bool IsVersion2(byte[] data)
        => data.Length >= Version2Magic.Length + GcmNonceSize + GcmTagSize
           && data.AsSpan(0, Version2Magic.Length).SequenceEqual(Version2Magic);

    /// <summary>v2 record: magic, nonce, tag, AES-256-GCM ciphertext (magic bound as associated data).</summary>
    internal static byte[] EncryptVersion2(byte[] key, string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var result = new byte[Version2Magic.Length + GcmNonceSize + GcmTagSize + plain.Length];
        Version2Magic.CopyTo(result, 0);
        var nonce = result.AsSpan(Version2Magic.Length, GcmNonceSize);
        RandomNumberGenerator.Fill(nonce);
        var tag = result.AsSpan(Version2Magic.Length + GcmNonceSize, GcmTagSize);
        var cipher = result.AsSpan(Version2Magic.Length + GcmNonceSize + GcmTagSize);
        using var gcm = new AesGcm(key, GcmTagSize);
        gcm.Encrypt(nonce, plain, cipher, tag, Version2Magic);
        return result;
    }

    internal static bool TryDecryptVersion2(byte[] key, byte[] data, out string? value)
    {
        value = null;
        if (!IsVersion2(data))
            return false;
        try
        {
            var nonce = data.AsSpan(Version2Magic.Length, GcmNonceSize);
            var tag = data.AsSpan(Version2Magic.Length + GcmNonceSize, GcmTagSize);
            var cipher = data.AsSpan(Version2Magic.Length + GcmNonceSize + GcmTagSize);
            var plain = new byte[cipher.Length];
            using var gcm = new AesGcm(key, GcmTagSize);
            gcm.Decrypt(nonce, cipher, tag, plain, Version2Magic);
            value = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private byte[] EncryptData(string data)
    {
        // Use a machine-specific key derived from machine ID
        var key = GetMachineKey();

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(data);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return result;
    }

    private string DecryptData(byte[] encryptedData)
    {
        var key = GetMachineKey();

        using var aes = Aes.Create();
        aes.Key = key;

        // Extract IV from beginning of data
        var iv = new byte[aes.BlockSize / 8];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        var cipherText = new byte[encryptedData.Length - iv.Length];
        Buffer.BlockCopy(encryptedData, iv.Length, cipherText, 0, cipherText.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] GetMachineKey()
    {
        // Derive a key from machine-id and user
        var machineId = GetMachineId();
        var user = Environment.UserName;
        var combined = $"{machineId}:{user}:{ServiceName}";

        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
    }

    private string GetMachineId()
    {
        try
        {
            // Try /etc/machine-id first (systemd)
            if (File.Exists("/etc/machine-id"))
            {
                return File.ReadAllText("/etc/machine-id").Trim();
            }

            // Try /var/lib/dbus/machine-id (older systems)
            if (File.Exists("/var/lib/dbus/machine-id"))
            {
                return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }

            // Fallback to hostname
            return Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }

    #endregion
}
