// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A keyring holding SecureStorage values: items carry the attributes <c>service</c> (the app's
/// namespace) and <c>key</c>, the label is the key, the secret is the UTF-8 value, as
/// <c>secret-tool store --label=KEY service NS key KEY</c> writes them, so items written by
/// either implementation are found by the other. Every call returns once the keyring reflects
/// it: a lookup after a store sees the value, a lookup after a clear does not.
/// </summary>
internal interface ISecretKeyring
{
    /// <summary>A short name for logs ("Secret Service D-Bus", "secret-tool").</summary>
    string Name { get; }

    /// <summary>The value stored for (<paramref name="ns"/>, <paramref name="key"/>), null when none.</summary>
    /// <exception cref="SecretServiceUnavailableException">No Secret Service can be reached through this keyring.</exception>
    Task<string?> LookupAsync(string ns, string key);

    /// <summary>Stores (replacing) the value.</summary>
    /// <exception cref="SecretServiceUnavailableException">No Secret Service can be reached through this keyring.</exception>
    /// <exception cref="InvalidOperationException">The Secret Service refused the store.</exception>
    Task StoreAsync(string ns, string key, string value);

    /// <summary>Removes the value; true when one was stored.</summary>
    /// <exception cref="SecretServiceUnavailableException">No Secret Service can be reached through this keyring.</exception>
    Task<bool> ClearAsync(string ns, string key);

    /// <summary>Removes every value under <paramref name="ns"/>.</summary>
    /// <exception cref="SecretServiceUnavailableException">No Secret Service can be reached through this keyring.</exception>
    Task ClearAllAsync(string ns);
}

/// <summary>No Secret Service answers through a keyring (no session bus, no keyring daemon, no secret-tool).</summary>
internal sealed class SecretServiceUnavailableException : Exception
{
    public SecretServiceUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Runs operations on the same key one after another (operations on different keys run
/// concurrently): with 100 parallel set/get/remove calls on one key, each sees the previous
/// one's effect, as on the platforms whose keystores are synchronous.
/// </summary>
internal sealed class KeyedSerializer
{
    private sealed class Gate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Users;
    }

    private readonly ConcurrentDictionary<string, Gate> _gates = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>Keys with a gate in use now (gates are dropped when their last user leaves).</summary>
    internal int ActiveKeys => _gates.Count;

    private Gate Enter(string key)
    {
        lock (_sync)
        {
            var gate = _gates.GetOrAdd(key, _ => new Gate());
            gate.Users++;
            return gate;
        }
    }

    private void Leave(string key, Gate gate)
    {
        gate.Semaphore.Release();
        lock (_sync)
        {
            if (--gate.Users == 0)
                _gates.TryRemove(new KeyValuePair<string, Gate>(key, gate));
        }
    }

    /// <summary>Runs <paramref name="operation"/> once every earlier operation on <paramref name="key"/> has finished.</summary>
    public async Task<T> RunAsync<T>(string key, Func<Task<T>> operation)
    {
        var gate = Enter(key);
        try
        {
            await gate.Semaphore.WaitAsync().ConfigureAwait(false);
        }
        catch
        {
            lock (_sync)
            {
                if (--gate.Users == 0)
                    _gates.TryRemove(new KeyValuePair<string, Gate>(key, gate));
            }
            throw;
        }
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            Leave(key, gate);
        }
    }

    /// <inheritdoc cref="RunAsync{T}(string, Func{Task{T}})"/>
    public Task RunAsync(string key, Func<Task> operation)
        => RunAsync<bool>(key, async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        });
}
