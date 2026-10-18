// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;

namespace Microsoft.Maui.Platform.Linux.Services.Passkeys;

/// <summary>
/// Linux implementation of MAUI's <see cref="IPasskeys"/>: creates and uses passkeys on
/// FIDO2 security keys (USB, or NFC where libfido2 has a reader) through libfido2, as
/// Windows' webauthn.dll does for roaming authenticators. Linux desktops have no
/// platform authenticator (Windows Hello, Touch ID): a request for
/// <c>authenticatorAttachment: "platform"</c> fails, and there is no phone (hybrid) sign-in.
/// The options and responses are the WebAuthn JSON MAUI uses on Windows, including its
/// client data (origin <c>https://&lt;rpId&gt;</c>). PIN entry, "touch your key" and the
/// account choice are OpenMaui's in-app dialogs, where Windows shows its system dialog.
/// </summary>
public sealed class PasskeysService : IPasskeys
{
    // COSE algorithms libfido2 creates credentials with: ES256, EdDSA, ES384, RS256.
    private static readonly HashSet<int> s_supportedAlgorithms = new() { -7, -8, -35, -257 };

    private readonly IFido2Transport _transport;
    private readonly IPasskeyPrompt _prompt;

    /// <summary>Creates the service over libfido2 and OpenMaui's dialogs.</summary>
    public PasskeysService()
        : this(LibFido2Transport.Instance, SkiaPasskeyPrompt.Instance)
    {
    }

    internal PasskeysService(IFido2Transport transport, IPasskeyPrompt prompt)
    {
        _transport = transport;
        _prompt = prompt;
    }

    /// <summary>How often a ceremony looks for a security key while asking for one.</summary>
    internal TimeSpan DevicePollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// True when libfido2 is installed and a security key is connected. Without the
    /// library <see cref="CreateAsync"/> and <see cref="AssertAsync"/> throw
    /// <see cref="FeatureNotSupportedException"/>; with it but no key, they ask for one,
    /// as Windows does.
    /// </summary>
    public bool IsSupported
    {
        get
        {
            try
            {
                return _transport.IsAvailable && _transport.ListDevices().Count > 0;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("Passkeys", $"Listing security keys failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <inheritdoc />
    public async Task<PasskeyCreationResponse> CreateAsync(PasskeyCreationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        var json = await Task.Run(() => MakeCredentialAsync(options, cancellationToken), cancellationToken).ConfigureAwait(false);
        return CreateResponse<PasskeyCreationResponse>(json);
    }

    /// <inheritdoc />
    public async Task<PasskeyAssertionResponse> AssertAsync(PasskeyRequestOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        var json = await Task.Run(() => GetAssertionAsync(options, cancellationToken), cancellationToken).ConfigureAwait(false);
        return CreateResponse<PasskeyAssertionResponse>(json);
    }

    private void EnsureSupported()
    {
        if (!_transport.IsAvailable)
            throw new FeatureNotSupportedException("Passkeys on Linux require libfido2 (libfido2.so.1) and a FIDO2 security key.");
    }

    private async Task<string> MakeCredentialAsync(PasskeyCreationOptions options, CancellationToken cancellationToken)
    {
        var creation = WebAuthnMessages.ParseCreation(options.ToString());
        var rpId = creation.RpId ?? throw new ArgumentException("The creation options are missing the 'rp.id'.", nameof(options));
        var rpName = creation.RpName ?? rpId;
        if (!creation.HasUser)
            throw new ArgumentException("The creation options are missing the 'user'.", nameof(options));
        var userId = WebAuthnMessages.DecodeRequired(creation.UserId, "user.id");
        var userName = creation.UserName ?? string.Empty;
        var userDisplayName = creation.UserDisplayName ?? userName;
        var challenge = WebAuthnMessages.DecodeRequired(creation.Challenge, "challenge");
        var exclude = creation.ExcludeCredentials.Select(id => WebAuthnMessages.Decode(id, "credential.id")).ToList();
        var timeout = WebAuthnMessages.GetTimeout(creation.Timeout);
        var clientDataJson = WebAuthnMessages.BuildClientDataJson("webauthn.create", challenge, rpId);
        var clientDataHash = SHA256.HashData(clientDataJson);

        if (creation.AuthenticatorAttachment == "platform")
        {
            throw new InvalidOperationException(
                "WebAuthn operation failed: the relying party asked for a platform authenticator, and Linux has none. " +
                "Passkeys on Linux are created on FIDO2 security keys.");
        }

        var algorithms = creation.Algorithms.Count > 0 ? creation.Algorithms.ToList() : new List<int> { -7, -257 };
        algorithms = algorithms.Where(s_supportedAlgorithms.Contains).ToList();
        if (algorithms.Count == 0)
            throw new InvalidOperationException("WebAuthn operation failed: none of the requested public key algorithms is supported.");

        var rkRequired = creation.ResidentKey == "required" || (creation.ResidentKey == null && creation.RequireResidentKey == true);
        var rkPreferred = creation.ResidentKey == "preferred";

        await using var ceremony = new Ceremony(this, timeout, cancellationToken);
        var device = await ceremony.AcquireDeviceAsync().ConfigureAwait(false);

        if (!device.IsFido2 && (rkRequired || creation.UserVerification == "required"))
            throw new InvalidOperationException("WebAuthn operation failed: this security key does not support passkeys (it is U2F only).");

        var rk = rkRequired || (rkPreferred && device.IsFido2) ? Fido2Option.True : Fido2Option.Omit;
        var credential = await ceremony.RunWithUserVerificationAsync(creation.UserVerification, pin =>
        {
            Fido2Exception? last = null;
            foreach (var alg in algorithms)
            {
                var request = new Fido2MakeCredentialRequest(alg, clientDataHash, rpId, rpName, userId, userName, userDisplayName,
                    rk, ceremony.UvOption(pin), exclude);
                try
                {
                    return device.MakeCredential(request, pin);
                }
                catch (Fido2Exception ex) when (ex.Code == Fido2Error.UnsupportedAlgorithm)
                {
                    last = ex;
                }
                catch (Fido2Exception ex) when (rkPreferred && rk == Fido2Option.True &&
                    ex.Code is Fido2Error.UnsupportedOption or Fido2Error.InvalidOption or Fido2Error.KeyStoreFull)
                {
                    // Discoverable only preferred: make a server-side credential instead.
                    rk = Fido2Option.Omit;
                    return device.MakeCredential(request with { ResidentKey = Fido2Option.Omit }, pin);
                }
            }
            throw last!;
        }).ConfigureAwait(false);

        // Attestation conveyance "none" (the default) gets the "none" format, as browsers send it.
        var conveyance = creation.Attestation ?? "none";
        var attestationObject = conveyance == "none"
            ? WebAuthnMessages.BuildAttestationObject("none", Array.Empty<byte>(), credential.AuthenticatorData)
            : WebAuthnMessages.BuildAttestationObject(credential.Format, credential.AttestationStatement, credential.AuthenticatorData);
        return WebAuthnMessages.BuildRegistrationResponseJson(credential.CredentialId, attestationObject, clientDataJson, device.Info.Transport);
    }

    private async Task<string> GetAssertionAsync(PasskeyRequestOptions options, CancellationToken cancellationToken)
    {
        var request = WebAuthnMessages.ParseRequest(options.ToString());
        var rpId = request.RpId ?? throw new ArgumentException("The request options are missing the 'rpId'.", nameof(options));
        var challenge = WebAuthnMessages.DecodeRequired(request.Challenge, "challenge");
        var allow = request.AllowCredentials.Select(id => WebAuthnMessages.Decode(id, "credential.id")).ToList();
        var timeout = WebAuthnMessages.GetTimeout(request.Timeout);
        var clientDataJson = WebAuthnMessages.BuildClientDataJson("webauthn.get", challenge, rpId);
        var clientDataHash = SHA256.HashData(clientDataJson);

        await using var ceremony = new Ceremony(this, timeout, cancellationToken);
        var device = await ceremony.AcquireDeviceAsync().ConfigureAwait(false);

        var assertions = await ceremony.RunWithUserVerificationAsync(request.UserVerification, pin =>
            device.GetAssertion(new Fido2GetAssertionRequest(rpId, clientDataHash, allow, ceremony.UvOption(pin)), pin)).ConfigureAwait(false);
        if (assertions.Count == 0)
            throw new InvalidOperationException("WebAuthn operation failed: the security key returned no assertion.");

        var chosen = assertions[0];
        if (assertions.Count > 1)
        {
            var labels = assertions.Select(a => a.UserDisplayName is { Length: > 0 } display
                ? (a.UserName is { Length: > 0 } name && name != display ? $"{display} ({name})" : display)
                : a.UserName is { Length: > 0 } n ? n : Base64Url.EncodeToString(a.UserId)).ToList();
            var index = await _prompt.ChooseAccountAsync("Choose an account", labels).ConfigureAwait(false);
            if (index < 0 || index >= assertions.Count)
                throw new TaskCanceledException();
            chosen = assertions[index];
        }

        var credentialId = chosen.CredentialId.Length > 0 ? chosen.CredentialId : (allow.Count == 1 ? allow[0] : chosen.CredentialId);
        return WebAuthnMessages.BuildAssertionResponseJson(credentialId, chosen.AuthenticatorData, chosen.Signature, clientDataJson, chosen.UserId);
    }

    /// <summary>
    /// Builds MAUI's response type from the response JSON. Their constructors are internal
    /// to Essentials (they read the credential id and user handle from the JSON), so they
    /// are called by reflection, as OpenMaui registers its other Essentials implementations.
    /// </summary>
    internal static T CreateResponse<T>(string json)
        where T : class
    {
        var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, new[] { typeof(string) })
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no (string) constructor.");
        try
        {
            return (T)ctor.Invoke(new object[] { json });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// One ceremony: finding the key, PIN entry, the "touch your key" dialog, timeout
    /// and cancellation (the caller's token or the dialog's Cancel button abort the key's
    /// operation, and the call ends with <see cref="TaskCanceledException"/>, as on Windows).
    /// </summary>
    private sealed class Ceremony : IAsyncDisposable
    {
        private readonly PasskeysService _owner;
        private readonly CancellationToken _callerToken;
        private readonly CancellationTokenSource _userCancel = new();
        private readonly CancellationTokenSource _timeout;
        private readonly CancellationTokenSource _linked;
        private readonly List<IFido2Device> _opened = new();
        private IFido2Device? _device;

        public Ceremony(PasskeysService owner, int timeoutMilliseconds, CancellationToken callerToken)
        {
            _owner = owner;
            _callerToken = callerToken;
            _timeout = new CancellationTokenSource();
            if (timeoutMilliseconds > 0)
                _timeout.CancelAfter(timeoutMilliseconds);
            _linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _userCancel.Token, _timeout.Token);
            _linked.Token.Register(() =>
            {
                try { _device?.Cancel(); } catch (Exception) { }
            });
        }

        private CancellationToken Token => _linked.Token;

        private bool _wantBuiltInUv;

        /// <summary>The CTAP "uv" option: set for built-in verification; a PIN verifies the user by itself.</summary>
        public Fido2Option UvOption(string? pin)
            => pin != null ? Fido2Option.Omit : (_wantBuiltInUv ? Fido2Option.True : Fido2Option.Omit);

        public async Task<IFido2Device> AcquireDeviceAsync()
        {
            var devices = _owner._transport.ListDevices();
            if (devices.Count == 0)
            {
                using var status = _owner._prompt.ShowStatus("Security key", "Insert your security key.", _userCancel.Cancel);
                while (devices.Count == 0)
                {
                    try
                    {
                        await Task.Delay(_owner.DevicePollInterval, Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        ThrowCanceledOrTimedOut();
                    }
                    devices = _owner._transport.ListDevices();
                }
            }

            foreach (var info in devices)
            {
                try
                {
                    _opened.Add(_owner._transport.Open(info));
                }
                catch (Fido2Exception ex)
                {
                    DiagnosticLog.Debug("Passkeys", $"Cannot open {info.Path}: {ex.Message}");
                }
            }
            if (_opened.Count == 0)
                throw new InvalidOperationException("WebAuthn operation failed: the security key could not be opened.");

            var index = 0;
            if (_opened.Count > 1)
            {
                using var status = _owner._prompt.ShowStatus("Security key", "Touch the security key you want to use.", _userCancel.Cancel);
                index = _owner._transport.SelectByTouch(_opened, Token);
                if (index < 0)
                    ThrowCanceledOrTimedOut();
            }
            _device = _opened[index];
            return _device;
        }

        /// <summary>
        /// Runs <paramref name="operation"/> with the user verification the relying party
        /// asked for: the key's built-in verification (a fingerprint) when it has it, else
        /// its PIN, asked for in a dialog and asked again while it is wrong.
        /// </summary>
        public async Task<T> RunWithUserVerificationAsync<T>(string? userVerification, Func<string?, T> operation)
        {
            var device = _device!;
            var wantUv = userVerification != "discouraged";
            string? pin = null;
            if (wantUv && device.IsFido2)
            {
                if (device.HasBuiltInUv)
                {
                    _wantBuiltInUv = true;
                }
                else if (device.HasPin)
                {
                    pin = await AskPinAsync(device, wrong: false).ConfigureAwait(false);
                }
                else if (userVerification == "required")
                {
                    throw new InvalidOperationException(
                        "WebAuthn operation failed: the relying party requires user verification, and the security key has no PIN. Set a PIN on the key.");
                }
            }

            device.SetTimeout(-1);
            while (true)
            {
                Token.ThrowIfCancellationRequested();
                try
                {
                    using var status = _owner._prompt.ShowStatus("Security key", "Touch your security key.", _userCancel.Cancel);
                    return await Task.Run(() => operation(pin)).ConfigureAwait(false);
                }
                catch (Fido2Exception ex)
                {
                    if (Token.IsCancellationRequested || ex.Code == Fido2Error.KeepaliveCancel)
                        ThrowCanceledOrTimedOut();

                    switch (ex.Code)
                    {
                        case Fido2Error.PinInvalid:
                        case Fido2Error.PinAuthInvalid:
                            pin = await AskPinAsync(device, wrong: true).ConfigureAwait(false);
                            continue;
                        case Fido2Error.PinRequired when pin == null && device.HasPin:
                        case Fido2Error.UvInvalid when pin == null && device.HasPin:
                        case Fido2Error.UvBlocked when pin == null && device.HasPin:
                            // The key wants a PIN (always-UV keys, CTAP 2.0 keys with a PIN for discoverable
                            // credentials) or built-in verification failed: fall back to the PIN.
                            _wantBuiltInUv = false;
                            pin = await AskPinAsync(device, wrong: false).ConfigureAwait(false);
                            continue;
                    }
                    throw Map(ex);
                }
            }
        }

        private async Task<string> AskPinAsync(IFido2Device device, bool wrong)
        {
            var retries = device.PinRetries;
            var message = wrong ? "Incorrect PIN. Enter the PIN for your security key." : "Enter the PIN for your security key.";
            if (retries >= 0)
                message += retries == 1 ? " 1 attempt left." : $" {retries} attempts left.";
            var pin = await _owner._prompt.RequestPinAsync("Security key PIN", message).ConfigureAwait(false);
            if (pin == null)
                throw new TaskCanceledException();
            ThrowIfCanceled();
            return pin;
        }

        private void ThrowIfCanceled()
        {
            if (Token.IsCancellationRequested)
                ThrowCanceledOrTimedOut();
        }

        private void ThrowCanceledOrTimedOut()
        {
            if (_callerToken.IsCancellationRequested)
                throw new TaskCanceledException(null, null, _callerToken);
            if (_timeout.IsCancellationRequested && !_userCancel.IsCancellationRequested)
                throw new InvalidOperationException("WebAuthn operation failed: the operation timed out.");
            throw new TaskCanceledException();
        }

        private Exception Map(Fido2Exception ex) => ex.Code switch
        {
            Fido2Error.CredentialExcluded => new InvalidOperationException(
                "WebAuthn operation failed: this security key already holds a credential for this account.", ex),
            Fido2Error.NoCredentials => new InvalidOperationException(
                "WebAuthn operation failed: the security key holds no passkey for this site.", ex),
            Fido2Error.PinBlocked or Fido2Error.PinAuthBlocked => new InvalidOperationException(
                "WebAuthn operation failed: the security key's PIN is blocked. Reset the key, or remove and reinsert it to try again.", ex),
            Fido2Error.PinRequired or Fido2Error.PinNotSet => new InvalidOperationException(
                "WebAuthn operation failed: the security key requires a PIN, and none is set. Set a PIN on the key.", ex),
            Fido2Error.UserActionTimeout or Fido2Error.ActionTimeout => new InvalidOperationException(
                "WebAuthn operation failed: the security key was not touched in time.", ex),
            _ => new InvalidOperationException($"WebAuthn operation failed ({ex.Code}): {ex.Message}", ex),
        };

        public ValueTask DisposeAsync()
        {
            _linked.Dispose();
            foreach (var d in _opened)
            {
                try { d.Dispose(); } catch (Exception) { }
            }
            _timeout.Dispose();
            _userCancel.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
