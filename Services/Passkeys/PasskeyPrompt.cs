// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Services.Passkeys;

/// <summary>
/// The dialogs a passkey ceremony shows, where Windows shows its own security-key
/// dialog: what to do with the key (insert it, touch it), the PIN, and the account
/// when the key holds several passkeys for the site.
/// </summary>
internal interface IPasskeyPrompt
{
    /// <summary>Shows a status dialog with a Cancel button until the returned handle is disposed.</summary>
    IDisposable ShowStatus(string title, string message, Action onCancel);

    /// <summary>Asks for the security key's PIN; null when the user cancels.</summary>
    Task<string?> RequestPinAsync(string title, string message);

    /// <summary>Asks which account to sign in with; -1 when the user cancels.</summary>
    Task<int> ChooseAccountAsync(string title, IReadOnlyList<string> accounts);
}

/// <summary>
/// <see cref="IPasskeyPrompt"/> on OpenMaui's in-app modal dialogs
/// (<see cref="LinuxDialogService"/>), shown on the UI thread. Without a running app
/// (no dispatcher) nothing is shown, and PIN and account requests are canceled.
/// </summary>
internal sealed class SkiaPasskeyPrompt : IPasskeyPrompt
{
    public static SkiaPasskeyPrompt Instance { get; } = new();

    public IDisposable ShowStatus(string title, string message, Action onCancel)
    {
        var handle = new StatusHandle();
        var dispatcher = LinuxDispatcher.Main;
        if (dispatcher == null)
            return handle;

        dispatcher.Dispatch(() =>
        {
            if (handle.Disposed)
                return;
            var dialog = new SkiaAlertDialog(title, message, null, "Cancel");
            handle.Dialog = dialog;
            LinuxDialogService.Show(dialog);
            dialog.Result.ContinueWith(_ =>
            {
                if (!handle.Disposed)
                    onCancel();
            }, TaskScheduler.Default);
        });
        return handle;
    }

    public Task<string?> RequestPinAsync(string title, string message)
        => OnUi(async () =>
        {
            var dialog = new SkiaAlertDialog(title, message, "OK", "Cancel", string.Empty)
            {
                IsPassword = true,
                MaxLength = 63,
                Placeholder = "PIN",
            };
            LinuxDialogService.Show(dialog);
            return await dialog.Result.ConfigureAwait(true) ? dialog.Input : null;
        }, (string?)null);

    public Task<int> ChooseAccountAsync(string title, IReadOnlyList<string> accounts)
        => OnUi(async () =>
        {
            var labels = UniqueLabels(accounts);
            var chosen = await LinuxDialogService.ShowActionSheetAsync(title, "Cancel", null, labels).ConfigureAwait(true);
            return chosen == null ? -1 : labels.IndexOf(chosen);
        }, -1);

    /// <summary>Account labels made distinct, so the chosen text maps back to one index.</summary>
    internal static List<string> UniqueLabels(IReadOnlyList<string> accounts)
    {
        var labels = new List<string>(accounts.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < accounts.Count; i++)
        {
            var label = string.IsNullOrWhiteSpace(accounts[i]) ? "Account " + (i + 1) : accounts[i];
            var unique = label;
            for (var n = 2; !seen.Add(unique); n++)
                unique = $"{label} ({n})";
            labels.Add(unique);
        }
        return labels;
    }

    private static Task<T> OnUi<T>(Func<Task<T>> show, T canceled)
    {
        var dispatcher = LinuxDispatcher.Main;
        if (dispatcher == null)
            return Task.FromResult(canceled);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.Dispatch(async () =>
            {
                try { tcs.TrySetResult(await show().ConfigureAwait(true)); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }))
        {
            tcs.TrySetResult(canceled);
        }
        return tcs.Task;
    }

    private sealed class StatusHandle : IDisposable
    {
        public volatile bool Disposed;

        public SkiaAlertDialog? Dialog { get; set; }

        public void Dispose()
        {
            if (Disposed)
                return;
            Disposed = true;
            LinuxDispatcher.Main?.Dispatch(() => Dialog?.Close());
        }
    }
}
