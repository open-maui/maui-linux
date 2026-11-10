// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Microsoft.Maui.Platform.Linux.Services.Passkeys;

/// <summary>
/// The WebAuthn JSON the passkey service reads and writes, matching MAUI's Windows
/// implementation (its <c>WebAuthn</c> models are internal to Essentials): the relying
/// party's creation and request options in, the client data, and the registration and
/// authentication responses out.
/// </summary>
internal static class WebAuthnMessages
{
    internal sealed record CreationOptions(
        string? RpId,
        string? RpName,
        bool HasUser,
        string? UserId,
        string? UserName,
        string? UserDisplayName,
        string? Challenge,
        IReadOnlyList<int> Algorithms,
        string? UserVerification,
        string? AuthenticatorAttachment,
        string? ResidentKey,
        bool? RequireResidentKey,
        string? Attestation,
        IReadOnlyList<string> ExcludeCredentials,
        int? Timeout);

    internal sealed record RequestOptions(
        string? RpId,
        string? Challenge,
        string? UserVerification,
        IReadOnlyList<string> AllowCredentials,
        int? Timeout);

    /// <summary>Reads <c>PublicKeyCredentialCreationOptions</c>; malformed JSON throws <see cref="ArgumentException"/>.</summary>
    internal static CreationOptions ParseCreation(string json)
    {
        var root = Parse(json, "creation options");
        try
        {
            var rp = Object(root, "rp");
            var user = Object(root, "user");
            var selection = Object(root, "authenticatorSelection");
            var algorithms = new List<int>();
            if (Array(root, "pubKeyCredParams") is { } parameters)
            {
                foreach (var p in parameters.EnumerateArray())
                {
                    if (p.ValueKind == JsonValueKind.Null)
                        continue;
                    if (Int(p, "alg") is int alg)
                        algorithms.Add(alg);
                }
            }
            return new CreationOptions(
                rp is { } r ? String(r, "id") : null,
                rp is { } r2 ? String(r2, "name") : null,
                user != null,
                user is { } u ? String(u, "id") : null,
                user is { } u2 ? String(u2, "name") : null,
                user is { } u3 ? String(u3, "displayName") : null,
                String(root, "challenge"),
                algorithms,
                (selection is { } s ? String(s, "userVerification") : null) ?? String(root, "userVerification"),
                selection is { } s2 ? String(s2, "authenticatorAttachment") : null,
                selection is { } s3 ? String(s3, "residentKey") : null,
                selection is { } s4 ? Bool(s4, "requireResidentKey") : null,
                String(root, "attestation"),
                Descriptors(root, "excludeCredentials"),
                Int(root, "timeout"));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The creation options JSON could not be parsed.", "options", ex);
        }
    }

    /// <summary>Reads <c>PublicKeyCredentialRequestOptions</c>; malformed JSON throws <see cref="ArgumentException"/>.</summary>
    internal static RequestOptions ParseRequest(string json)
    {
        var root = Parse(json, "request options");
        try
        {
            return new RequestOptions(
                String(root, "rpId"),
                String(root, "challenge"),
                String(root, "userVerification"),
                Descriptors(root, "allowCredentials"),
                Int(root, "timeout"));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The request options JSON could not be parsed.", "options", ex);
        }
    }

    internal static byte[] DecodeRequired(string? value, string name)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"The options are missing the '{name}'.", "options");
        return Decode(value, name);
    }

    internal static byte[] Decode(string value, string name)
    {
        try
        {
            return Base64Url.DecodeFromChars(value);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"The options contain an invalid '{name}' Base64Url value.", "options", ex);
        }
    }

    /// <summary>The ceremony timeout in milliseconds (WebAuthn's default is 60 s).</summary>
    internal static int GetTimeout(int? timeout)
    {
        var value = timeout ?? 60000;
        if (value < 0)
            throw new ArgumentOutOfRangeException("options", "The WebAuthn timeout cannot be negative.");
        return value;
    }

    /// <summary>
    /// The client data the authenticator signs, as MAUI builds it on Windows: the
    /// ceremony type, the challenge, origin <c>https://&lt;rpId&gt;</c>, not cross-origin.
    /// </summary>
    internal static byte[] BuildClientDataJson(string type, byte[] challenge, string rpId)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", type);
            w.WriteString("challenge", Base64Url.EncodeToString(challenge));
            w.WriteString("origin", $"https://{rpId}");
            w.WriteBoolean("crossOrigin", false);
            w.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static string BuildRegistrationResponseJson(byte[] credentialId, byte[] attestationObject, byte[] clientDataJson, string transport)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("id", Base64Url.EncodeToString(credentialId));
            w.WriteString("rawId", Base64Url.EncodeToString(credentialId));
            w.WriteString("type", "public-key");
            w.WriteStartObject("response");
            w.WriteString("clientDataJSON", Base64Url.EncodeToString(clientDataJson));
            w.WriteString("attestationObject", Base64Url.EncodeToString(attestationObject));
            w.WriteStartArray("transports");
            w.WriteStringValue(transport);
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteString("authenticatorAttachment", "cross-platform");
            w.WriteStartObject("clientExtensionResults");
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string BuildAssertionResponseJson(byte[] credentialId, byte[] authenticatorData, byte[] signature, byte[] clientDataJson, byte[] userHandle)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("id", Base64Url.EncodeToString(credentialId));
            w.WriteString("rawId", Base64Url.EncodeToString(credentialId));
            w.WriteString("type", "public-key");
            w.WriteStartObject("response");
            w.WriteString("clientDataJSON", Base64Url.EncodeToString(clientDataJson));
            w.WriteString("authenticatorData", Base64Url.EncodeToString(authenticatorData));
            w.WriteString("signature", Base64Url.EncodeToString(signature));
            if (userHandle.Length > 0)
                w.WriteString("userHandle", Base64Url.EncodeToString(userHandle));
            w.WriteEndObject();
            w.WriteString("authenticatorAttachment", "cross-platform");
            w.WriteStartObject("clientExtensionResults");
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The CBOR attestation object <c>{ "fmt", "attStmt", "authData" }</c> (in CTAP2
    /// canonical key order) from libfido2's parts: <paramref name="attestationStatement"/>
    /// is already CBOR; empty means an empty map.
    /// </summary>
    internal static byte[] BuildAttestationObject(string format, byte[] attestationStatement, byte[] authenticatorData)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0xA3);
        WriteText(stream, "fmt");
        WriteText(stream, format);
        WriteText(stream, "attStmt");
        if (attestationStatement.Length == 0)
            stream.WriteByte(0xA0);
        else
            stream.Write(attestationStatement);
        WriteText(stream, "authData");
        WriteHead(stream, 2, (ulong)authenticatorData.Length);
        stream.Write(authenticatorData);
        return stream.ToArray();
    }

    private static void WriteText(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        WriteHead(stream, 3, (ulong)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteHead(Stream stream, int major, ulong value)
    {
        var m = (byte)(major << 5);
        if (value < 24)
        {
            stream.WriteByte((byte)(m | (byte)value));
        }
        else if (value <= byte.MaxValue)
        {
            stream.WriteByte((byte)(m | 24));
            stream.WriteByte((byte)value);
        }
        else if (value <= ushort.MaxValue)
        {
            stream.WriteByte((byte)(m | 25));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
        else
        {
            stream.WriteByte((byte)(m | 26));
            for (var shift = 24; shift >= 0; shift -= 8)
                stream.WriteByte((byte)(value >> shift));
        }
    }

    private static JsonElement Parse(string json, string what)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Null)
                throw new ArgumentException($"The {what} JSON was empty.", "options");
            if (root.ValueKind != JsonValueKind.Object)
                throw new ArgumentException($"The {what} JSON could not be parsed.", "options");
            return root;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The {what} JSON could not be parsed.", "options", ex);
        }
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null)
            return true;
        value = default;
        return false;
    }

    private static JsonElement? Object(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
            return null;
        return v.ValueKind == JsonValueKind.Object ? v : throw new FormatException(name);
    }

    private static JsonElement? Array(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
            return null;
        return v.ValueKind == JsonValueKind.Array ? v : throw new FormatException(name);
    }

    private static string? String(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
            return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException(name);
    }

    private static int? Int(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
            return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : throw new FormatException(name);
    }

    private static bool? Bool(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
            return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException(name),
        };
    }

    private static IReadOnlyList<string> Descriptors(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (Array(obj, name) is not { } array)
            return list;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null)
                continue;
            if (item.ValueKind != JsonValueKind.Object)
                throw new FormatException(name);
            if (String(item, "id") is { Length: > 0 } id)
                list.Add(id);
        }
        return list;
    }
}
