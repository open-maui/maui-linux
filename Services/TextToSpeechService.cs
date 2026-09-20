// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Media;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux text-to-speech using espeak-ng or spd-say (speech-dispatcher).
/// </summary>
public class TextToSpeechService : ITextToSpeech
{
    /// <summary>
    /// Tool probe; tests replace it so the choice of spd-say vs espeak-ng does
    /// not depend on what is installed on the build machine.
    /// </summary>
    internal static Func<string, bool> ToolExists = File.Exists;

    public Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        // Both spd-say and espeak-ng speak the session language by default, so
        // the current UI culture is the one locale we can promise. Fall back to
        // en-US when the process runs under the invariant culture.
        var culture = CultureInfo.CurrentUICulture;
        if (string.IsNullOrEmpty(culture.Name))
            culture = CultureInfo.GetCultureInfo("en-US");

        var locale = ToLocale(culture);
        return Task.FromResult<IEnumerable<Locale>>(new[] { locale });
    }

    internal static Locale ToLocale(CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName;
        var country = "";
        try
        {
            if (!culture.IsNeutralCulture)
                country = new RegionInfo(culture.Name).TwoLetterISORegionName;
        }
        catch (ArgumentException) { }

        // Locale's (language, country, name, id) constructor is internal to the
        // Essentials assembly; the platform implementations reach it the same way.
        var locale = (Locale)Activator.CreateInstance(
            typeof(Locale),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            binder: null,
            args: new object[] { language, country, culture.DisplayName, culture.Name },
            culture: null)!;
        return locale;
    }

    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (cancellationToken.IsCancellationRequested) return;

        try
        {
            await ExternalProcess.RunAsync(BuildStartInfo(text, options), cancellationToken);
        }
        catch { }
    }

    /// <summary>
    /// spd-say (speech-dispatcher) when installed, otherwise espeak-ng. The text
    /// goes through ArgumentList so quotes and spaces are forwarded verbatim.
    /// Pitch/volume map onto espeak-ng's -p (0..99) and -a (0..200) switches.
    /// </summary>
    internal static ProcessStartInfo BuildStartInfo(string text, SpeechOptions? options)
    {
        var tool = ToolExists("/usr/bin/spd-say") ? "spd-say" : "espeak-ng";
        var psi = new ProcessStartInfo
        {
            FileName = tool,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (tool == "espeak-ng")
        {
            if (options?.Pitch is { } pitch)
            {
                psi.ArgumentList.Add("-p");
                psi.ArgumentList.Add(Math.Clamp((int)(pitch * 50), 0, 99).ToString(CultureInfo.InvariantCulture));
            }
            if (options?.Volume is { } volume)
            {
                psi.ArgumentList.Add("-a");
                psi.ArgumentList.Add(Math.Clamp((int)(volume * 200), 0, 200).ToString(CultureInfo.InvariantCulture));
            }
        }
        else
        {
            if (options?.Pitch is { } pitch)
            {
                // spd-say pitch is -100..100 with 0 as normal; MAUI pitch is 0..2 with 1 normal.
                psi.ArgumentList.Add("-p");
                psi.ArgumentList.Add(Math.Clamp((int)((pitch - 1f) * 100), -100, 100).ToString(CultureInfo.InvariantCulture));
            }
            if (options?.Volume is { } volume)
            {
                // spd-say volume is -100..100; MAUI volume is 0..1.
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(Math.Clamp((int)(volume * 200 - 100), -100, 100).ToString(CultureInfo.InvariantCulture));
            }
        }

        if (options?.Locale is { } locale && !string.IsNullOrEmpty(locale.Language))
        {
            psi.ArgumentList.Add(tool == "espeak-ng" ? "-v" : "-l");
            psi.ArgumentList.Add(locale.Language);
        }

        psi.ArgumentList.Add(text);
        return psi;
    }
}
