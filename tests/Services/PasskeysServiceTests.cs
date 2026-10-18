// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Passkeys;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// MAUI 11 Passkeys on FIDO2 security keys (libfido2), with a fake key and fake dialogs:
/// the options JSON is read and the response JSON built as MAUI's Windows implementation
/// does, and PIN entry, key selection, cancellation and errors follow its contract.
/// </summary>
public sealed class PasskeysServiceTests
{
    private static readonly byte[] CredId = { 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly byte[] AuthData = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();

    private const string CreationJson = """
        {
          "rp": { "id": "example.com", "name": "Example" },
          "user": { "id": "dXNlci0x", "name": "alice@example.com", "displayName": "Alice" },
          "challenge": "Y2hhbGxlbmdl",
          "pubKeyCredParams": [ { "type": "public-key", "alg": -7 }, { "type": "public-key", "alg": -257 } ],
          "authenticatorSelection": { "residentKey": "required", "userVerification": "preferred" },
          "excludeCredentials": [ { "type": "public-key", "id": "AQID" } ],
          "timeout": 30000
        }
        """;

    private const string RequestJson = """
        { "rpId": "example.com", "challenge": "Y2hhbGxlbmdl", "userVerification": "preferred", "allowCredentials": [] }
        """;

    private readonly FakeTransport _transport = new();
    private readonly FakePrompt _prompt = new();

    private PasskeysService Service => new(_transport, _prompt) { DevicePollInterval = TimeSpan.FromMilliseconds(10) };

    [Fact]
    public void IsSupported_needs_libfido2_and_a_connected_key()
    {
        _transport.Available = false;
        Service.IsSupported.Should().BeFalse();

        _transport.Available = true;
        Service.IsSupported.Should().BeFalse();

        _transport.Devices.Add(new FakeDevice());
        Service.IsSupported.Should().BeTrue();
    }

    [Fact]
    public async Task Without_libfido2_the_calls_throw_FeatureNotSupportedException()
    {
        _transport.Available = false;
        await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(CreationJson))).Should().ThrowAsync<FeatureNotSupportedException>();
        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions(RequestJson))).Should().ThrowAsync<FeatureNotSupportedException>();
        await FluentActions.Awaiting(() => Service.CreateAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_maps_the_options_and_returns_MAUIs_registration_response()
    {
        var device = new FakeDevice();
        _transport.Devices.Add(device);

        var response = await Service.CreateAsync(new PasskeyCreationOptions(CreationJson));

        var request = device.MakeCredentialRequests.Single();
        request.RpId.Should().Be("example.com");
        request.RpName.Should().Be("Example");
        request.UserId.Should().Equal(Encoding.ASCII.GetBytes("user-1"));
        request.UserName.Should().Be("alice@example.com");
        request.UserDisplayName.Should().Be("Alice");
        request.Algorithm.Should().Be(-7);
        request.ResidentKey.Should().Be(Fido2Option.True);
        request.ExcludeCredentials.Single().Should().Equal(1, 2, 3);

        response.Id.Should().Be(Base64Url.EncodeToString(CredId));
        using var json = JsonDocument.Parse(response.ToString());
        var root = json.RootElement;
        root.GetProperty("rawId").GetString().Should().Be(response.Id);
        root.GetProperty("type").GetString().Should().Be("public-key");
        var r = root.GetProperty("response");
        var clientData = Base64Url.DecodeFromChars(r.GetProperty("clientDataJSON").GetString()!);
        Encoding.UTF8.GetString(clientData).Should().Be("""{"type":"webauthn.create","challenge":"Y2hhbGxlbmdl","origin":"https://example.com","crossOrigin":false}""");
        request.ClientDataHash.Should().Equal(SHA256.HashData(clientData));
        r.GetProperty("transports")[0].GetString().Should().Be("usb");

        // Conveyance "none" (the default): fmt "none", empty attStmt, the key's authData.
        var attestation = Base64Url.DecodeFromChars(r.GetProperty("attestationObject").GetString()!);
        attestation.Should().Equal(WebAuthnMessages.BuildAttestationObject("none", Array.Empty<byte>(), AuthData));
    }

    [Fact]
    public void The_attestation_object_is_canonical_CBOR()
    {
        var bytes = WebAuthnMessages.BuildAttestationObject("packed", new byte[] { 0xA1, 0x63, (byte)'a', (byte)'l', (byte)'g', 0x26 }, new byte[300]);

        bytes[0].Should().Be(0xA3);
        bytes.AsSpan(1, 4).ToArray().Should().Equal(0x63, (byte)'f', (byte)'m', (byte)'t');
        bytes.AsSpan(5, 7).ToArray().Should().Equal(0x66, (byte)'p', (byte)'a', (byte)'c', (byte)'k', (byte)'e', (byte)'d');
        Encoding.ASCII.GetString(bytes, 13, 7).Should().Be("attStmt");
        bytes.AsSpan(20, 6).ToArray().Should().Equal(0xA1, 0x63, (byte)'a', (byte)'l', (byte)'g', 0x26);
        Encoding.ASCII.GetString(bytes, 27, 8).Should().Be("authData");
        bytes.AsSpan(35, 3).ToArray().Should().Equal(0x59, 0x01, 0x2C);
        bytes.Length.Should().Be(38 + 300);
    }

    [Fact]
    public async Task Direct_attestation_keeps_the_keys_format_and_statement()
    {
        var device = new FakeDevice { Format = "packed", Statement = new byte[] { 0xA1, 0x63, (byte)'a', (byte)'l', (byte)'g', 0x26 } };
        _transport.Devices.Add(device);

        var response = await Service.CreateAsync(new PasskeyCreationOptions(CreationJson.Replace("\"timeout\"", "\"attestation\": \"direct\", \"timeout\"")));

        using var json = JsonDocument.Parse(response.ToString());
        var attestation = Base64Url.DecodeFromChars(json.RootElement.GetProperty("response").GetProperty("attestationObject").GetString()!);
        attestation.Should().Equal(WebAuthnMessages.BuildAttestationObject("packed", device.Statement, AuthData));
    }

    [Theory]
    [InlineData("\"required\"", "null", 2)]
    [InlineData("\"preferred\"", "true", 2)]
    [InlineData("\"discouraged\"", "true", 0)]
    [InlineData("null", "true", 2)]
    [InlineData("null", "false", 0)]
    public async Task Resident_key_modes_map_as_MAUIs_MapResidentKey(string residentKey, string legacy, int expected)
    {
        var device = new FakeDevice();
        _transport.Devices.Add(device);
        var json = CreationJson.Replace("\"residentKey\": \"required\"", $"\"residentKey\": {residentKey}, \"requireResidentKey\": {legacy}");

        await Service.CreateAsync(new PasskeyCreationOptions(json));

        ((int)device.MakeCredentialRequests.Single().ResidentKey).Should().Be(expected);
    }

    [Fact]
    public async Task A_preferred_resident_key_the_key_cannot_store_falls_back_to_a_server_side_credential()
    {
        var device = new FakeDevice { RejectResidentKey = true };
        _transport.Devices.Add(device);

        await Service.CreateAsync(new PasskeyCreationOptions(CreationJson.Replace("\"residentKey\": \"required\"", "\"residentKey\": \"preferred\"")));

        device.MakeCredentialRequests.Select(r => r.ResidentKey).Should().Equal(Fido2Option.True, Fido2Option.Omit);
    }

    [Fact]
    public void Closing_an_alert_dialog_from_code_completes_it_with_false()
    {
        var dialog = new Microsoft.Maui.Platform.SkiaAlertDialog("Security key", "Touch your security key.", null, "Cancel");
        Microsoft.Maui.Platform.LinuxDialogService.Show(dialog);

        dialog.Close();

        dialog.Result.IsCompleted.Should().BeTrue();
        dialog.Result.Result.Should().BeFalse();
        Microsoft.Maui.Platform.LinuxDialogService.TopDialog.Should().NotBeSameAs(dialog);
    }

    [Fact]
    public async Task A_key_with_a_PIN_asks_for_it_and_asks_again_while_it_is_wrong()
    {
        var device = new FakeDevice { HasPin = true, CorrectPin = "1234", PinRetries = 7 };
        _transport.Devices.Add(device);
        _prompt.Pins.Enqueue("0000");
        _prompt.Pins.Enqueue("1234");

        await Service.CreateAsync(new PasskeyCreationOptions(CreationJson));

        _prompt.PinMessages.Should().HaveCount(2);
        _prompt.PinMessages[0].Should().Be("Enter the PIN for your security key. 7 attempts left.");
        _prompt.PinMessages[1].Should().StartWith("Incorrect PIN.");
        device.PinsUsed.Should().Equal("0000", "1234");
        device.MakeCredentialRequests.Last().UserVerification.Should().Be(Fido2Option.Omit);
        _prompt.Statuses.Should().Contain("Touch your security key.");
    }

    [Fact]
    public async Task Canceling_the_PIN_dialog_cancels_the_ceremony()
    {
        _transport.Devices.Add(new FakeDevice { HasPin = true, CorrectPin = "1234" });
        _prompt.Pins.Enqueue(null);

        await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(CreationJson))).Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task Built_in_verification_needs_no_PIN()
    {
        var device = new FakeDevice { HasPin = true, HasBuiltInUv = true };
        _transport.Devices.Add(device);

        await Service.CreateAsync(new PasskeyCreationOptions(CreationJson));

        _prompt.PinMessages.Should().BeEmpty();
        device.MakeCredentialRequests.Single().UserVerification.Should().Be(Fido2Option.True);
        device.PinsUsed.Should().Equal(new string?[] { null });
    }

    [Fact]
    public async Task Required_verification_on_a_key_without_PIN_or_built_in_verification_fails()
    {
        _transport.Devices.Add(new FakeDevice());
        var json = CreationJson.Replace("\"userVerification\": \"preferred\"", "\"userVerification\": \"required\"");

        await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(json))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no PIN*");
    }

    [Fact]
    public async Task A_platform_authenticator_request_fails_because_Linux_has_none()
    {
        _transport.Devices.Add(new FakeDevice());
        var json = CreationJson.Replace("\"residentKey\"", "\"authenticatorAttachment\": \"platform\", \"residentKey\"");

        await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(json))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*platform authenticator*");
    }

    [Fact]
    public async Task An_algorithm_the_key_lacks_falls_back_to_the_next_one()
    {
        var device = new FakeDevice { UnsupportedAlgorithms = { -7 } };
        _transport.Devices.Add(device);

        await Service.CreateAsync(new PasskeyCreationOptions(CreationJson));

        device.MakeCredentialRequests.Select(r => r.Algorithm).Should().Equal(-7, -257);
    }

    [Fact]
    public async Task An_excluded_credential_fails_with_InvalidOperationException()
    {
        _transport.Devices.Add(new FakeDevice { Fail = Fido2Error.CredentialExcluded });

        await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(CreationJson))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already holds a credential*");
    }

    [Theory]
    [InlineData("not json", typeof(ArgumentException))]
    [InlineData("""{ "user": { "id": "AQ" }, "challenge": "AQ" }""", typeof(ArgumentException))]
    [InlineData("""{ "rp": { "id": "x" }, "challenge": "AQ" }""", typeof(ArgumentException))]
    [InlineData("""{ "rp": { "id": "x" }, "user": { "id": "AQ" } }""", typeof(ArgumentException))]
    [InlineData("""{ "rp": { "id": "x" }, "user": { "id": "!!" }, "challenge": "AQ" }""", typeof(ArgumentException))]
    [InlineData("""{ "rp": { "id": "x" }, "user": { "id": "AQ" }, "challenge": "AQ", "timeout": -1 }""", typeof(ArgumentOutOfRangeException))]
    [InlineData("""{ "rp": { "id": "x" }, "user": { "id": "AQ" }, "challenge": "AQ", "timeout": "soon" }""", typeof(ArgumentException))]
    public async Task Malformed_creation_options_throw_as_on_Windows(string json, Type exception)
    {
        _transport.Devices.Add(new FakeDevice());

        var thrown = await FluentActions.Awaiting(() => Service.CreateAsync(new PasskeyCreationOptions(json))).Should().ThrowAsync<ArgumentException>();
        thrown.Which.Should().BeOfType(exception);
    }

    [Fact]
    public async Task AssertAsync_returns_MAUIs_assertion_response()
    {
        var device = new FakeDevice
        {
            Assertions = { new Fido2Assertion(CredId, AuthData, new byte[] { 9, 9 }, Encoding.ASCII.GetBytes("user-1"), "alice", "Alice") },
        };
        _transport.Devices.Add(device);

        var response = await Service.AssertAsync(new PasskeyRequestOptions(RequestJson));

        response.Id.Should().Be(Base64Url.EncodeToString(CredId));
        response.UserHandle.Should().Be("dXNlci0x");
        using var json = JsonDocument.Parse(response.ToString());
        var r = json.RootElement.GetProperty("response");
        Base64Url.DecodeFromChars(r.GetProperty("authenticatorData").GetString()!).Should().Equal(AuthData);
        Base64Url.DecodeFromChars(r.GetProperty("signature").GetString()!).Should().Equal(9, 9);
        var clientData = Base64Url.DecodeFromChars(r.GetProperty("clientDataJSON").GetString()!);
        Encoding.UTF8.GetString(clientData).Should().Contain("\"type\":\"webauthn.get\"");
        device.GetAssertionRequests.Single().ClientDataHash.Should().Equal(SHA256.HashData(clientData));
        device.GetAssertionRequests.Single().RpId.Should().Be("example.com");
    }

    [Fact]
    public async Task Several_passkeys_on_the_key_ask_which_account()
    {
        _transport.Devices.Add(new FakeDevice
        {
            Assertions =
            {
                new Fido2Assertion(new byte[] { 1 }, AuthData, new byte[] { 1 }, new byte[] { 0xA }, "alice", "Alice"),
                new Fido2Assertion(new byte[] { 2 }, AuthData, new byte[] { 2 }, new byte[] { 0xB }, "bob", "Bob"),
            },
        });
        _prompt.Choice = 1;

        var response = await Service.AssertAsync(new PasskeyRequestOptions(RequestJson));

        _prompt.Accounts.Should().Equal("Alice (alice)", "Bob (bob)");
        response.Id.Should().Be(Base64Url.EncodeToString(new byte[] { 2 }));
    }

    [Fact]
    public async Task No_passkey_for_the_site_fails_with_InvalidOperationException()
    {
        _transport.Devices.Add(new FakeDevice { Fail = Fido2Error.NoCredentials });

        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions(RequestJson))).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions("""{ "challenge": "AQ" }"""))).Should().ThrowAsync<ArgumentException>()
            .WithMessage("The request options are missing the 'rpId'.*");
    }

    [Fact]
    public async Task Cancellation_aborts_the_keys_operation_with_TaskCanceledException()
    {
        var device = new FakeDevice { BlockUntilCanceled = true };
        _transport.Devices.Add(device);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions(RequestJson), cts.Token)).Should().ThrowAsync<TaskCanceledException>();
        device.Canceled.Should().BeTrue();
        device.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task The_Cancel_button_of_the_touch_dialog_cancels_the_ceremony()
    {
        _transport.Devices.Add(new FakeDevice { BlockUntilCanceled = true });
        _prompt.CancelStatusAfter = TimeSpan.FromMilliseconds(50);

        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions(RequestJson))).Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task The_relying_partys_timeout_ends_the_ceremony_with_InvalidOperationException()
    {
        _transport.Devices.Add(new FakeDevice { BlockUntilCanceled = true });

        await FluentActions.Awaiting(() => Service.AssertAsync(new PasskeyRequestOptions("""{ "rpId": "example.com", "challenge": "AQ", "timeout": 100 }""")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*timed out*");
    }

    [Fact]
    public async Task Without_a_key_the_ceremony_asks_for_one_and_waits()
    {
        var task = Service.AssertAsync(new PasskeyRequestOptions(RequestJson));
        await Task.Delay(100);
        _prompt.Statuses.Should().Contain("Insert your security key.");
        _transport.Devices.Add(new FakeDevice());

        var response = await task.WaitAsync(TimeSpan.FromSeconds(10));
        response.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task With_several_keys_the_touched_one_is_used()
    {
        var first = new FakeDevice();
        var second = new FakeDevice();
        _transport.Devices.Add(first);
        _transport.Devices.Add(second);
        _transport.Touched = 1;

        await Service.AssertAsync(new PasskeyRequestOptions(RequestJson));

        first.GetAssertionRequests.Should().BeEmpty();
        second.GetAssertionRequests.Should().HaveCount(1);
        _prompt.Statuses.Should().Contain("Touch the security key you want to use.");
    }

    [Fact]
    public void Account_labels_are_made_distinct()
        => SkiaPasskeyPrompt.UniqueLabels(new[] { "Alice", "Alice", "" }).Should().Equal("Alice", "Alice (2)", "Account 3");

    [Fact]
    public void The_libfido2_transport_loads_when_installed_and_lists_keys_without_throwing()
    {
        var installed = File.Exists("/usr/lib64/libfido2.so.1") || File.Exists("/usr/lib/x86_64-linux-gnu/libfido2.so.1") || File.Exists("/usr/lib/libfido2.so.1");
        LibFido2Transport.Instance.IsAvailable.Should().Be(installed);
        var act = () => LibFido2Transport.Instance.ListDevices();
        act.Should().NotThrow();
    }

    [Fact]
    public void Passkeys_Default_is_the_Linux_service()
    {
        EssentialsPatches.Apply();
        Passkeys.Default.Should().BeOfType<PasskeysService>();
    }

    private sealed class FakeTransport : IFido2Transport
    {
        public bool Available { get; set; } = true;

        public List<FakeDevice> Devices { get; } = new();

        public int Touched { get; set; }

        public bool IsAvailable => Available;

        public IReadOnlyList<Fido2DeviceInfo> ListDevices()
        {
            lock (Devices)
                return Available ? Devices.Select((d, i) => new Fido2DeviceInfo("/dev/hidraw" + i, "Fake", "Key")).ToList() : new List<Fido2DeviceInfo>();
        }

        public IFido2Device Open(Fido2DeviceInfo info)
        {
            var device = Devices[int.Parse(info.Path["/dev/hidraw".Length..])];
            device.Info = info;
            return device;
        }

        public int SelectByTouch(IReadOnlyList<IFido2Device> devices, CancellationToken cancellationToken) => Touched;
    }

    private sealed class FakeDevice : IFido2Device
    {
        private readonly ManualResetEventSlim _canceled = new();

        public Fido2DeviceInfo Info { get; set; } = new("/dev/hidraw0", null, null);

        public bool IsFido2 { get; set; } = true;

        public bool HasPin { get; set; }

        public bool HasBuiltInUv { get; set; }

        public int PinRetries { get; set; } = -1;

        public string? CorrectPin { get; set; }

        public string Format { get; set; } = "packed";

        public byte[] Statement { get; set; } = new byte[] { 0xA0 };

        public int Fail { get; set; }

        public bool BlockUntilCanceled { get; set; }

        public HashSet<int> UnsupportedAlgorithms { get; } = new();

        public bool RejectResidentKey { get; set; }

        public List<Fido2Assertion> Assertions { get; } = new();

        public List<Fido2MakeCredentialRequest> MakeCredentialRequests { get; } = new();

        public List<Fido2GetAssertionRequest> GetAssertionRequests { get; } = new();

        public List<string?> PinsUsed { get; } = new();

        public bool Canceled { get; private set; }

        public bool Disposed { get; private set; }

        public void SetTimeout(int milliseconds)
        {
        }

        public void Cancel()
        {
            Canceled = true;
            _canceled.Set();
        }

        private void Run(string? pin)
        {
            PinsUsed.Add(pin);
            if (BlockUntilCanceled)
            {
                _canceled.Wait(TimeSpan.FromSeconds(10));
                throw new Fido2Exception(Fido2Error.KeepaliveCancel, "canceled");
            }
            if (Fail != 0)
                throw new Fido2Exception(Fail, "failed");
            if (HasPin && CorrectPin != null && pin != null && pin != CorrectPin)
                throw new Fido2Exception(Fido2Error.PinInvalid, "pin invalid");
        }

        public Fido2Credential MakeCredential(Fido2MakeCredentialRequest request, string? pin)
        {
            MakeCredentialRequests.Add(request);
            if (UnsupportedAlgorithms.Contains(request.Algorithm))
                throw new Fido2Exception(Fido2Error.UnsupportedAlgorithm, "alg");
            if (RejectResidentKey && request.ResidentKey == Fido2Option.True)
                throw new Fido2Exception(Fido2Error.KeyStoreFull, "full");
            Run(pin);
            return new Fido2Credential(CredId, Format, AuthData, Statement);
        }

        public IReadOnlyList<Fido2Assertion> GetAssertion(Fido2GetAssertionRequest request, string? pin)
        {
            GetAssertionRequests.Add(request);
            Run(pin);
            return Assertions.Count > 0 ? Assertions : new List<Fido2Assertion> { new(CredId, AuthData, new byte[] { 1 }, Array.Empty<byte>(), null, null) };
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakePrompt : IPasskeyPrompt
    {
        public Queue<string?> Pins { get; } = new();

        public List<string> PinMessages { get; } = new();

        public List<string> Statuses { get; } = new();

        public List<string> Accounts { get; } = new();

        public int Choice { get; set; }

        public TimeSpan? CancelStatusAfter { get; set; }

        public IDisposable ShowStatus(string title, string message, Action onCancel)
        {
            lock (Statuses)
                Statuses.Add(message);
            if (CancelStatusAfter is { } delay && message == "Touch your security key.")
                _ = Task.Delay(delay).ContinueWith(_ => onCancel(), TaskScheduler.Default);
            return new Nothing();
        }

        public Task<string?> RequestPinAsync(string title, string message)
        {
            PinMessages.Add(message);
            return Task.FromResult(Pins.Count > 0 ? Pins.Dequeue() : null);
        }

        public Task<int> ChooseAccountAsync(string title, IReadOnlyList<string> accounts)
        {
            Accounts.AddRange(accounts);
            return Task.FromResult(Choice);
        }

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
