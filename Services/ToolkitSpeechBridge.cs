// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// CommunityToolkit.Maui's <c>SpeechToText</c> and <c>OfflineSpeechToText</c> on Linux. A Linux
/// desktop has no speech-recognition service (nothing like Windows' SpeechRecognizer, Android's
/// SpeechRecognizer or Apple's SFSpeechRecognizer ships with GNOME, KDE or the freedesktop
/// stack), so the calls behave as the toolkit does on a device without a recognizer (its Android
/// build): <c>StartListenAsync</c> throws <see cref="FeatureNotSupportedException"/>, and
/// <c>StopListenAsync</c> and disposal complete, with <c>CurrentState</c> staying Stopped. The
/// platform-neutral build, the one a Linux app runs, threw <see cref="NotSupportedException"/>
/// from both, so even stopping (which Windows never fails) threw. <c>RequestPermissions</c> is the
/// toolkit's own (the microphone permission). No compile-time reference: the toolkit is optional.
/// </summary>
internal static class ToolkitSpeechBridge
{
    private const string Assembly = "CommunityToolkit.Maui.Core";

    /// <summary>The message the toolkit uses on a device without speech recognition.</summary>
    internal const string NotAvailableMessage = "Speech Recognition is not available on this device";

    internal static void Install(Harmony harmony)
    {
        var online = Resolve("CommunityToolkit.Maui.Media.SpeechToTextImplementation");
        var offline = Resolve("CommunityToolkit.Maui.Media.OfflineSpeechToTextImplementation");
        if (online != null)
        {
            Patch(harmony, online, "InternalStartListeningAsync", nameof(StartListening_Prefix));
            Patch(harmony, online, "InternalStopListeningAsync", nameof(StopListeningAsync_Prefix));
        }
        if (offline != null)
        {
            Patch(harmony, offline, "InternalStartListening", nameof(StartListening_Prefix));
            Patch(harmony, offline, "InternalStopListening", nameof(StopListening_Prefix));
        }
    }

    private static Type? Resolve(string name)
    {
        try
        {
            return Type.GetType($"{name}, {Assembly}", throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }

    private static void Patch(Harmony harmony, Type type, string method, string prefix)
    {
        var target = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        if (target == null)
        {
            DiagnosticLog.Warn("ToolkitSpeech", $"{type.Name}.{method} not found in this toolkit release; it is not bridged");
            return;
        }
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(ToolkitSpeechBridge).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    /// <summary>InternalStartListening[Async](options, token): no recognizer on this device.</summary>
    private static bool StartListening_Prefix(ref Task __result)
    {
        __result = Task.FromException(new FeatureNotSupportedException(NotAvailableMessage));
        return false;
    }

    /// <summary>InternalStopListeningAsync(token): nothing is listening.</summary>
    private static bool StopListeningAsync_Prefix(CancellationToken __0, ref Task __result)
    {
        __result = __0.IsCancellationRequested ? Task.FromCanceled(__0) : Task.CompletedTask;
        return false;
    }

    /// <summary>InternalStopListening(): nothing is listening.</summary>
    private static bool StopListening_Prefix() => false;
}
