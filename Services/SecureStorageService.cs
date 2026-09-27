// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using System.Text;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux secure storage implementation using secret-tool (libsecret) or encrypted file fallback.
/// </summary>
/// <remarks>
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
    private readonly string _fallbackPath;
    private readonly bool _useSecretService;
    private readonly Lazy<Task<byte[]?>>? _portalKey;

    public SecureStorageService()
    {
        _fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            FallbackDirectory);
        _useSecretService = CheckSecretServiceAvailable();
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
    /// true still requires <c>secret-tool</c> on PATH at call time.
    /// </summary>
    internal SecureStorageService(string fallbackPath, bool useSecretService)
    {
        _fallbackPath = fallbackPath;
        _useSecretService = useSecretService && CheckSecretServiceAvailable();
    }

    private bool CheckSecretServiceAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "secret-tool",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_useSecretService)
        {
            return GetFromSecretServiceAsync(key);
        }
        else
        {
            return GetFromFallbackAsync(key);
        }
    }

    public Task SetAsync(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_useSecretService)
        {
            return SetInSecretServiceAsync(key, value);
        }
        else
        {
            return SetInFallbackAsync(key, value);
        }
    }

    public bool Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_useSecretService)
        {
            return RemoveFromSecretService(key);
        }
        else
        {
            return RemoveFromFallback(key);
        }
    }

    public void RemoveAll()
    {
        if (_useSecretService)
        {
            // Cannot easily remove all from secret service without knowing all keys
            // This would require additional tracking
        }
        else
        {
            if (Directory.Exists(_fallbackPath))
            {
                Directory.Delete(_fallbackPath, true);
            }
        }
    }

    #region Secret Service (libsecret)

    private async Task<string?> GetFromSecretServiceAsync(string key)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "secret-tool",
                Arguments = $"lookup service {ServiceName} key {EscapeArg(key)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                return output.TrimEnd('\n');
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task SetInSecretServiceAsync(string key, string value)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "secret-tool",
                Arguments = $"store --label=\"{EscapeArg(key)}\" service {ServiceName} key {EscapeArg(key)}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start secret-tool");

            await process.StandardInput.WriteAsync(value);
            process.StandardInput.Close();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Failed to store secret: {error}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Fall back to file storage
            await SetInFallbackAsync(key, value);
        }
    }

    private bool RemoveFromSecretService(string key)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "secret-tool",
                Arguments = $"clear service {ServiceName} key {EscapeArg(key)}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Fallback Encrypted Storage

    private async Task<string?> GetFromFallbackAsync(string key)
    {
        var filePath = GetFallbackFilePath(key);
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

        var filePath = GetFallbackFilePath(key);
        var portalKey = await GetPortalKeyAsync();
        var encryptedData = portalKey != null ? EncryptVersion2(portalKey, value) : EncryptData(value);

        await File.WriteAllBytesAsync(filePath, encryptedData);

        // Set restrictive permissions
        File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private bool RemoveFromFallback(string key)
    {
        var filePath = GetFallbackFilePath(key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    private string GetFallbackFilePath(string key)
    {
        // Hash the key to create a safe filename
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant();
        return Path.Combine(_fallbackPath, fileName);
    }

    private void EnsureFallbackDirectory()
    {
        if (!Directory.Exists(_fallbackPath))
        {
            Directory.CreateDirectory(_fallbackPath);
            // Set restrictive permissions on the directory
            File.SetUnixFileMode(_fallbackPath,
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

    private static string EscapeArg(string arg)
    {
        return arg.Replace("\"", "\\\"").Replace("'", "\\'");
    }
}
