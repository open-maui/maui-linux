// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Email, Sms and PhoneDialer report support from the desktop's URI-scheme associations, as
/// xdg-open resolves them, and refuse with FeatureNotSupportedException when no application
/// handles the scheme, as MAUI does; they reported true unconditionally.
/// </summary>
public class SchemeHandlerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"openmaui-schemes-{Guid.NewGuid():N}");

    public SchemeHandlerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SchemeHandlers.SearchFilesOverride = null;
        Directory.Delete(_dir, recursive: true);
    }

    private void Use(params (string Name, string Content)[] files)
    {
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(_dir, name), content);
        SchemeHandlers.SearchFilesOverride = files.Select(f => Path.Combine(_dir, f.Name)).ToList();
    }

    [Fact]
    public void A_scheme_is_handled_when_an_association_names_an_application()
    {
        Use(("mimeapps.list", "[Default Applications]\nx-scheme-handler/mailto=org.kde.kmail2.desktop\n"),
            ("mimeinfo.cache", "[MIME Cache]\nx-scheme-handler/tel=org.kde.kdeconnect.handler.desktop;\n"));

        SchemeHandlers.HasHandler("mailto").Should().BeTrue();
        SchemeHandlers.HasHandler("tel").Should().BeTrue();
        SchemeHandlers.HasHandler("sms").Should().BeFalse();
        SchemeHandlers.HasHandler("telnet").Should().BeFalse("only the exact scheme counts");
    }

    [Fact]
    public void A_removed_association_does_not_count()
    {
        Use(("mimeapps.list", "[Removed Associations]\nx-scheme-handler/sms=chatty.desktop;\n"),
            ("mimeinfo.cache", "[MIME Cache]\nx-scheme-handler/sms=chatty.desktop;\n"));

        SchemeHandlers.HasHandler("sms").Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_handler_the_services_report_unsupported_and_refuse()
    {
        Use(("mimeinfo.cache", "[MIME Cache]\ntext/plain=gedit.desktop;\n"));

        new EmailService().IsComposeSupported.Should().BeFalse();
        new SmsService().IsComposeSupported.Should().BeFalse();
        new PhoneDialerService().IsSupported.Should().BeFalse();

        await new EmailService().Invoking(s => s.ComposeAsync(new EmailMessage())).Should().ThrowAsync<FeatureNotSupportedException>();
        await new SmsService().Invoking(s => s.ComposeAsync(new SmsMessage())).Should().ThrowAsync<FeatureNotSupportedException>();
        new PhoneDialerService().Invoking(s => s.Open("+15551234")).Should().Throw<FeatureNotSupportedException>();
    }
}

/// <summary>Scheme associations for one test: the given schemes are handled (by a fake application).</summary>
internal sealed class FakeSchemeAssociations : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"openmaui-schemes-{Guid.NewGuid():N}.cache");

    public FakeSchemeAssociations(params string[] schemes)
    {
        File.WriteAllText(_file, "[MIME Cache]\n" + string.Concat(schemes.Select(s => $"x-scheme-handler/{s}=fake.desktop;\n")));
        SchemeHandlers.SearchFilesOverride = new[] { _file };
    }

    public void Dispose()
    {
        SchemeHandlers.SearchFilesOverride = null;
        File.Delete(_file);
    }
}
