using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class SecureStorageService : ISecureStorage
{
	private const string ServiceName = "maui-secure-storage";

	private const string FallbackDirectory = ".maui-secure";

	private readonly string _fallbackPath;

	private readonly bool _useSecretService;

	public SecureStorageService()
	{
		_fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".maui-secure");
		_useSecretService = CheckSecretServiceAvailable();
	}

	private bool CheckSecretServiceAvailable()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "which",
				Arguments = "secret-tool",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return false;
			}
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
		{
			throw new ArgumentNullException("key");
		}
		if (_useSecretService)
		{
			return GetFromSecretServiceAsync(key);
		}
		return GetFromFallbackAsync(key);
	}

	public Task SetAsync(string key, string value)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException("key");
		}
		if (_useSecretService)
		{
			return SetInSecretServiceAsync(key, value);
		}
		return SetInFallbackAsync(key, value);
	}

	public bool Remove(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException("key");
		}
		if (_useSecretService)
		{
			return RemoveFromSecretService(key);
		}
		return RemoveFromFallback(key);
	}

	public void RemoveAll()
	{
		if (!_useSecretService && Directory.Exists(_fallbackPath))
		{
			Directory.Delete(_fallbackPath, recursive: true);
		}
	}

	private async Task<string?> GetFromSecretServiceAsync(string key)
	{
		_ = 1;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "secret-tool",
				Arguments = "lookup service maui-secure-storage key " + EscapeArg(key),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return null;
			}
			string output = await process.StandardOutput.ReadToEndAsync();
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
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "secret-tool",
				Arguments = $"store --label=\"{EscapeArg(key)}\" service {"maui-secure-storage"} key {EscapeArg(key)}",
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				throw new InvalidOperationException("Failed to start secret-tool");
			}
			await process.StandardInput.WriteAsync(value);
			process.StandardInput.Close();
			await process.WaitForExitAsync();
			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException("Failed to store secret: " + await process.StandardError.ReadToEndAsync());
			}
		}
		catch (Exception ex) when (!(ex is InvalidOperationException))
		{
			await SetInFallbackAsync(key, value);
		}
	}

	private bool RemoveFromSecretService(string key)
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "secret-tool",
				Arguments = "clear service maui-secure-storage key " + EscapeArg(key),
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return false;
			}
			process.WaitForExit();
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private async Task<string?> GetFromFallbackAsync(string key)
	{
		string fallbackFilePath = GetFallbackFilePath(key);
		if (!File.Exists(fallbackFilePath))
		{
			return null;
		}
		try
		{
			return DecryptData(await File.ReadAllBytesAsync(fallbackFilePath));
		}
		catch
		{
			return null;
		}
	}

	private async Task SetInFallbackAsync(string key, string value)
	{
		EnsureFallbackDirectory();
		string filePath = GetFallbackFilePath(key);
		byte[] bytes = EncryptData(value);
		await File.WriteAllBytesAsync(filePath, bytes);
		File.SetUnixFileMode(filePath, UnixFileMode.UserWrite | UnixFileMode.UserRead);
	}

	private bool RemoveFromFallback(string key)
	{
		string fallbackFilePath = GetFallbackFilePath(key);
		if (File.Exists(fallbackFilePath))
		{
			File.Delete(fallbackFilePath);
			return true;
		}
		return false;
	}

	private string GetFallbackFilePath(string key)
	{
		using SHA256 sHA = SHA256.Create();
		string path = Convert.ToHexString(sHA.ComputeHash(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
		return Path.Combine(_fallbackPath, path);
	}

	private void EnsureFallbackDirectory()
	{
		if (!Directory.Exists(_fallbackPath))
		{
			Directory.CreateDirectory(_fallbackPath);
			File.SetUnixFileMode(_fallbackPath, UnixFileMode.UserExecute | UnixFileMode.UserWrite | UnixFileMode.UserRead);
		}
	}

	private byte[] EncryptData(string data)
	{
		byte[] machineKey = GetMachineKey();
		using Aes aes = Aes.Create();
		aes.Key = machineKey;
		aes.GenerateIV();
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		byte[] array = cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
		byte[] array2 = new byte[aes.IV.Length + array.Length];
		Buffer.BlockCopy(aes.IV, 0, array2, 0, aes.IV.Length);
		Buffer.BlockCopy(array, 0, array2, aes.IV.Length, array.Length);
		return array2;
	}

	private string DecryptData(byte[] encryptedData)
	{
		byte[] machineKey = GetMachineKey();
		using Aes aes = Aes.Create();
		aes.Key = machineKey;
		byte[] array = new byte[aes.BlockSize / 8];
		Buffer.BlockCopy(encryptedData, 0, array, 0, array.Length);
		aes.IV = array;
		byte[] array2 = new byte[encryptedData.Length - array.Length];
		Buffer.BlockCopy(encryptedData, array.Length, array2, 0, array2.Length);
		using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
		byte[] bytes = cryptoTransform.TransformFinalBlock(array2, 0, array2.Length);
		return Encoding.UTF8.GetString(bytes);
	}

	private byte[] GetMachineKey()
	{
		string machineId = GetMachineId();
		string userName = Environment.UserName;
		string s = $"{machineId}:{userName}:{"maui-secure-storage"}";
		using SHA256 sHA = SHA256.Create();
		return sHA.ComputeHash(Encoding.UTF8.GetBytes(s));
	}

	private string GetMachineId()
	{
		try
		{
			if (File.Exists("/etc/machine-id"))
			{
				return File.ReadAllText("/etc/machine-id").Trim();
			}
			if (File.Exists("/var/lib/dbus/machine-id"))
			{
				return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
			}
			return Environment.MachineName;
		}
		catch
		{
			return Environment.MachineName;
		}
	}

	private static string EscapeArg(string arg)
	{
		return arg.Replace("\"", "\\\"").Replace("'", "\\'");
	}
}
