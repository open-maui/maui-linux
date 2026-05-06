// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux secure storage implementation using secret-tool (libsecret) or encrypted file fallback.
/// </summary>
public class SecureStorageService : ISecureStorage
{
    private const string ServiceName = "maui-secure-storage";
    private const string FallbackDirectory = ".maui-secure";
    private readonly string _fallbackPath;
    private readonly bool _useSecretService;

    public SecureStorageService()
    {
        _fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            FallbackDirectory);
        _useSecretService = CheckSecretServiceAvailable();
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
        var encryptedData = EncryptData(value);

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
