// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Security.Cryptography;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>
/// Outcome codes of <c>org.freedesktop.portal.Request.Response</c>.
/// </summary>
internal enum PortalResponseCode : uint
{
    /// <summary>The interaction completed; results are valid.</summary>
    Success = 0,

    /// <summary>The user dismissed the interaction.</summary>
    Cancelled = 1,

    /// <summary>The interaction ended some other way (denied, backend error).</summary>
    Other = 2,
}

/// <summary>
/// A portal request's response: the code and the a{sv} results dictionary.
/// </summary>
internal sealed class PortalResponse
{
    public static readonly IReadOnlyDictionary<string, object> EmptyResults = new Dictionary<string, object>();

    public PortalResponse(PortalResponseCode code, IDictionary<string, object>? results)
    {
        Code = code;
        Results = results is null
            ? EmptyResults
            : new Dictionary<string, object>(results, StringComparer.Ordinal);
    }

    public PortalResponseCode Code { get; }

    public IReadOnlyDictionary<string, object> Results { get; }

    public bool IsSuccess => Code == PortalResponseCode.Success;

    /// <summary>Maps the raw uint of the signal (unknown values become <see cref="PortalResponseCode.Other"/>).</summary>
    public static PortalResponse FromSignal(uint response, IDictionary<string, object>? results)
    {
        var code = response switch
        {
            0 => PortalResponseCode.Success,
            1 => PortalResponseCode.Cancelled,
            _ => PortalResponseCode.Other,
        };
        return new PortalResponse(code, results);
    }

    public string? GetString(string key)
        => Results.TryGetValue(key, out var value) ? value as string : null;

    public bool? GetBoolean(string key)
        => Results.TryGetValue(key, out var value) && value is bool b ? b : null;

    public uint? GetUInt32(string key)
        => Results.TryGetValue(key, out var value) ? PortalVariant.ToUInt32(value) : null;

    public string[] GetStrings(string key)
    {
        if (!Results.TryGetValue(key, out var value))
            return Array.Empty<string>();
        return value switch
        {
            string[] a => a,
            IEnumerable<string> e => e.ToArray(),
            object[] o => o.OfType<string>().ToArray(),
            _ => Array.Empty<string>(),
        };
    }
}

/// <summary>
/// Helpers for the Request object protocol: handle tokens and the request
/// path the portal will use, which the client must subscribe to *before*
/// calling the method so a fast Response is not missed.
/// </summary>
internal static class PortalRequestPath
{
    public const string RequestPrefix = "/org/freedesktop/portal/desktop/request/";
    public const string SessionPrefix = "/org/freedesktop/portal/desktop/session/";

    private static int _counter;

    /// <summary>
    /// A fresh handle_token: a valid D-Bus object-path element
    /// ([A-Za-z0-9_]), unique within this process.
    /// </summary>
    public static string NewToken(string prefix = "openmaui")
    {
        var n = Interlocked.Increment(ref _counter);
        var random = RandomNumberGenerator.GetInt32(0, int.MaxValue);
        return $"{prefix}_{n.ToString(CultureInfo.InvariantCulture)}_{random.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The sender element of a request path: the unique bus name without the
    /// leading ':' and with every '.' replaced by '_' (":1.42" becomes "1_42").
    /// </summary>
    public static string SenderElement(string uniqueName)
    {
        if (string.IsNullOrEmpty(uniqueName))
            throw new ArgumentException("A unique bus name is required", nameof(uniqueName));
        return uniqueName.TrimStart(':').Replace('.', '_');
    }

    /// <summary>/org/freedesktop/portal/desktop/request/SENDER/TOKEN</summary>
    public static string ForRequest(string uniqueName, string token)
        => RequestPrefix + SenderElement(uniqueName) + "/" + token;

    /// <summary>/org/freedesktop/portal/desktop/session/SENDER/TOKEN</summary>
    public static string ForSession(string uniqueName, string token)
        => SessionPrefix + SenderElement(uniqueName) + "/" + token;

    /// <summary>True when <paramref name="token"/> is a legal path element.</summary>
    public static bool IsValidToken(string token)
        => !string.IsNullOrEmpty(token) && token.All(c => c == '_' || char.IsAsciiLetterOrDigit(c));
}

/// <summary>
/// Thrown (and caught inside the portal layer) when the portal frontend or
/// the requested interface is not present. Services treat this as a normal
/// outcome and take their fallback.
/// </summary>
internal sealed class PortalUnavailableException : Exception
{
    public PortalUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
