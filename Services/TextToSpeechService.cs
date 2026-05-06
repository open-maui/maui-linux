// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Media;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux text-to-speech using espeak-ng or spd-say (speech-dispatcher).
/// </summary>
public class TextToSpeechService : ITextToSpeech
{
    public async Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        // Return a basic default locale
        // Locale constructor varies by MAUI version; return empty for now.
        // TTS works via espeak-ng/spd-say regardless of locale reporting.
        return Array.Empty<Locale>();
    }

    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            // Try spd-say (speech-dispatcher) first, then espeak-ng
            var tool = File.Exists("/usr/bin/spd-say") ? "spd-say" : "espeak-ng";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tool,
                Arguments = $"\"{text.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (options?.Pitch.HasValue == true && tool == "espeak-ng")
                psi.Arguments = $"-p {(int)(options.Pitch.Value * 50)} {psi.Arguments}";
            if (options?.Volume.HasValue == true && tool == "espeak-ng")
                psi.Arguments = $"-a {(int)(options.Volume.Value * 200)} {psi.Arguments}";

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch { }
    }
}
