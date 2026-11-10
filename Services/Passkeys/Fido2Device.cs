// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services.Passkeys;

/// <summary>
/// The FIDO2 (CTAP) authenticators the passkey service can reach: USB and NFC
/// security keys through libfido2 (<see cref="LibFido2Transport"/>), or a fake in tests.
/// </summary>
internal interface IFido2Transport
{
    /// <summary>True when the transport's library is installed.</summary>
    bool IsAvailable { get; }

    /// <summary>The connected authenticators (empty when none, or when unavailable).</summary>
    IReadOnlyList<Fido2DeviceInfo> ListDevices();

    /// <summary>Opens an authenticator. Throws <see cref="Fido2Exception"/>.</summary>
    IFido2Device Open(Fido2DeviceInfo info);

    /// <summary>
    /// With several authenticators connected, waits until the user touches one of them
    /// (CTAP authenticatorSelection) and returns its index; -1 on <paramref name="cancellationToken"/>.
    /// </summary>
    int SelectByTouch(IReadOnlyList<IFido2Device> devices, CancellationToken cancellationToken);
}

/// <summary>An opened authenticator. Its blocking calls may be canceled from another thread with <see cref="Cancel"/>.</summary>
internal interface IFido2Device : IDisposable
{
    Fido2DeviceInfo Info { get; }

    /// <summary>Speaks CTAP2 (a U2F-only key cannot create passkeys or verify the user).</summary>
    bool IsFido2 { get; }

    /// <summary>A client PIN is set.</summary>
    bool HasPin { get; }

    /// <summary>Built-in user verification (a fingerprint) is enrolled.</summary>
    bool HasBuiltInUv { get; }

    /// <summary>Remaining PIN attempts, or -1 when unknown.</summary>
    int PinRetries { get; }

    /// <summary>Limits each blocking call (-1: none).</summary>
    void SetTimeout(int milliseconds);

    Fido2Credential MakeCredential(Fido2MakeCredentialRequest request, string? pin);

    IReadOnlyList<Fido2Assertion> GetAssertion(Fido2GetAssertionRequest request, string? pin);

    /// <summary>Aborts the call in progress (it then fails with KEEPALIVE_CANCEL).</summary>
    void Cancel();
}

/// <summary>A connected authenticator: its device path and USB strings.</summary>
internal sealed record Fido2DeviceInfo(string Path, string? Manufacturer, string? Product)
{
    /// <summary>"nfc" for an NFC reader path, else "usb".</summary>
    public string Transport => Path.StartsWith("nfc:", StringComparison.Ordinal) || Path.StartsWith("pcsc:", StringComparison.Ordinal) ? "nfc" : "usb";
}

/// <summary>A CTAP option left to the authenticator, or set on or off.</summary>
internal enum Fido2Option
{
    Omit = 0,
    False = 1,
    True = 2,
}

internal sealed record Fido2MakeCredentialRequest(
    int Algorithm,
    byte[] ClientDataHash,
    string RpId,
    string RpName,
    byte[] UserId,
    string UserName,
    string UserDisplayName,
    Fido2Option ResidentKey,
    Fido2Option UserVerification,
    IReadOnlyList<byte[]> ExcludeCredentials);

internal sealed record Fido2GetAssertionRequest(
    string RpId,
    byte[] ClientDataHash,
    IReadOnlyList<byte[]> AllowCredentials,
    Fido2Option UserVerification);

/// <summary>A new credential: its id and the parts of its attestation object.</summary>
internal sealed record Fido2Credential(byte[] CredentialId, string Format, byte[] AuthenticatorData, byte[] AttestationStatement);

/// <summary>One assertion (several when the key holds more than one discoverable credential for the RP).</summary>
internal sealed record Fido2Assertion(
    byte[] CredentialId,
    byte[] AuthenticatorData,
    byte[] Signature,
    byte[] UserId,
    string? UserName,
    string? UserDisplayName);

/// <summary>A libfido2 / CTAP error (<c>FIDO_ERR_*</c>).</summary>
internal sealed class Fido2Exception : Exception
{
    public Fido2Exception(int code, string message)
        : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}

/// <summary>The <c>FIDO_ERR_*</c> codes the service acts on (fido/err.h).</summary>
internal static class Fido2Error
{
    public const int Success = 0x00;
    public const int CredentialExcluded = 0x19;
    public const int UnsupportedAlgorithm = 0x26;
    public const int OperationDenied = 0x27;
    public const int KeyStoreFull = 0x28;
    public const int UnsupportedOption = 0x2b;
    public const int InvalidOption = 0x2c;
    public const int KeepaliveCancel = 0x2d;
    public const int NoCredentials = 0x2e;
    public const int UserActionTimeout = 0x2f;
    public const int NotAllowed = 0x30;
    public const int PinInvalid = 0x31;
    public const int PinBlocked = 0x32;
    public const int PinAuthInvalid = 0x33;
    public const int PinAuthBlocked = 0x34;
    public const int PinNotSet = 0x35;
    public const int PinRequired = 0x36;
    public const int PinPolicyViolation = 0x37;
    public const int ActionTimeout = 0x3a;
    public const int UvBlocked = 0x3c;
    public const int UvInvalid = 0x3f;
    public const int Rx = -2;
}
