// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Contacts on KDE: the vCard parser over the 2.1/3.0/4.0 forms Akonadi's files hold, the
/// discovery of Akonadi's vCard resources and KPeople's folder under a synthetic home, and the
/// precedence of Evolution Data Server over them. Every card here is synthetic.
/// </summary>
public class KdeContactsTests : IDisposable
{
    private readonly List<string> _dirs = new();

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private KdeContactPaths TempHome()
    {
        var home = Path.Combine(Path.GetTempPath(), $"openmaui-kde-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        _dirs.Add(home);
        return new KdeContactPaths(home, Path.Combine(home, ".config"), Path.Combine(home, ".local", "share"));
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string Card(string fn, string? uid = null, string extra = "")
        => "BEGIN:VCARD\r\nVERSION:3.0\r\n" + (uid != null ? $"UID:{uid}\r\n" : string.Empty) + $"FN:{fn}\r\n{extra}END:VCARD\r\n";

    // ------------------------------------------------------------------
    // vCard parsing
    // ------------------------------------------------------------------

    [Fact]
    public void VCard4_names_typed_phones_and_emails()
    {
        const string card =
            "BEGIN:VCARD\r\nVERSION:4.0\r\nUID:urn:uuid:0e7c-test\r\nFN:Prof. Erin Q. Sample III\r\n" +
            "N:Sample;Erin;Quinn,Q.;Prof.;III\r\n" +
            "TEL;VALUE=uri;TYPE=\"voice,cell\";PREF=1:tel:+1-555-0101\r\n" +
            "TEL;TYPE=work;TYPE=voice:+1 555 0102\r\n" +
            "EMAIL;TYPE=work:erin@example.test\r\n" +
            "EMAIL;TYPE=home;PREF=2:mailto:erin.home@example.test\r\n" +
            "END:VCARD\r\n";

        var contact = VCard.ToContact(card)!;
        contact.Id.Should().Be("urn:uuid:0e7c-test");
        contact.DisplayName.Should().Be("Prof. Erin Q. Sample III");
        contact.FamilyName.Should().Be("Sample");
        contact.GivenName.Should().Be("Erin");
        contact.MiddleName.Should().Be("Quinn,Q.", "MAUI's name parts are single strings; a list stays as written");
        contact.NamePrefix.Should().Be("Prof.");
        contact.NameSuffix.Should().Be("III");
        contact.Phones.Select(p => p.PhoneNumber).Should().Equal("+1-555-0101", "+1 555 0102");
        contact.Emails.Select(e => e.EmailAddress).Should().Equal("erin@example.test", "erin.home@example.test");

        var tels = VCard.Parse(card).Where(p => p.Name == "TEL").ToList();
        tels[0].Types.Should().Equal("VOICE", "CELL");
        tels[0].Parameter("PREF").Should().Be("1");
        tels[1].Types.Should().Equal("WORK", "VOICE");
    }

    [Fact]
    public void VCard21_bare_types_quoted_printable_and_charset()
    {
        // vCard 2.1: bare parameters, quoted-printable with a soft line break, a Latin-1 CHARSET.
        var latin1 = Encoding.Latin1;
        var bytes = latin1.GetBytes(
            "BEGIN:VCARD\r\nVERSION:2.1\r\n" +
            "N;CHARSET=ISO-8859-1:M\u00FCller;J\u00FCrgen;;;\r\n" +
            "FN;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:J=C3=BCrgen M=C3=BC=\r\nller\r\n" +
            "TEL;CELL;PREF:+49 30 555 0103\r\n" +
            "TEL;WORK;VOICE:+49 30 555 0104\r\n" +
            "EMAIL;INTERNET;QUOTED-PRINTABLE:j=40example.test\r\n" +
            "END:VCARD\r\n");

        var text = VCard.ReadFileText(bytes);
        var contact = VCard.ToContact(text)!;
        contact.DisplayName.Should().Be("J\u00FCrgen M\u00FCller", "QP bytes are UTF-8 and the soft break joins the lines");
        contact.FamilyName.Should().Be("M\u00FCller", "a non-UTF-8 file is read byte for byte and decoded in its CHARSET");
        contact.GivenName.Should().Be("J\u00FCrgen");
        contact.Phones.Select(p => p.PhoneNumber).Should().Equal("+49 30 555 0103", "+49 30 555 0104");
        contact.Emails.Select(e => e.EmailAddress).Should().Equal("j@example.test");
        VCard.Parse(text).First(p => p.Name == "TEL").Types.Should().Equal("CELL", "PREF");

        var qpLatin = VCard.ToContact("BEGIN:VCARD\nFN;ENCODING=QUOTED-PRINTABLE;CHARSET=ISO-8859-1:Z=F6e\nEND:VCARD")!;
        qpLatin.DisplayName.Should().Be("Z\u00F6e");
        var b64 = VCard.ToContact("BEGIN:VCARD\nFN;ENCODING=b:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Ren\u00E9e")) + "\nEND:VCARD")!;
        b64.DisplayName.Should().Be("Ren\u00E9e");
    }

    [Fact]
    public void VCard_folding_escaping_and_nested_agent()
    {
        const string card =
            "\uFEFFBEGIN:VCARD\nVERSION:3.0\nFN:Sam\\, the \\;Tester\n N\n" +
            "N:Tester\\;X;Sam;;;\n" +
            "EMAIL;TYPE=INTERNET,HOME:sam@exa\n\tmple.test\n" +
            "AGENT:\nBEGIN:VCARD\nFN:Nested Agent\nTEL:000\nEND:VCARD\n" +
            "TEL:555 0105\nEND:VCARD\n";

        var contact = VCard.ToContact(card)!;
        contact.DisplayName.Should().Be("Sam, the ;TesterN", "a space or tab folds the line");
        contact.FamilyName.Should().Be("Tester;X");
        contact.Emails.Select(e => e.EmailAddress).Should().Equal("sam@example.test");
        contact.Phones.Select(p => p.PhoneNumber).Should().Equal(new[] { "555 0105" }, "an AGENT's nested card is not this contact");
        contact.Id.Should().BeNull();
    }

    [Fact]
    public void VCard_files_split_and_get_a_fallback_uid()
    {
        var text = Card("One", "u1") + "\r\n" + Card("Two") + "junk between cards\r\n" + Card("Three");
        var cards = VCard.Split(text);
        cards.Should().HaveCount(3);
        VCard.Uid(cards[0]).Should().Be("u1");
        VCard.Uid(VCard.WithUid(cards[0], "other")).Should().Be("u1", "a card's own UID wins");
        VCard.ToContact(VCard.WithUid(cards[1], "file;name"))!.Id.Should().Be("file;name");
    }

    // ------------------------------------------------------------------
    // Akonadi resource discovery
    // ------------------------------------------------------------------

    [Fact]
    public void Akonadi_resources_are_found_from_agentsrc_and_their_rc_files()
    {
        var paths = TempHome();
        Write(Path.Combine(paths.ConfigHome, "akonadi", "agentsrc"),
            "[InstanceCounters]\nakonadi_contacts_resource\\InstanceCounter=1\n\n[Instances]\n" +
            "akonadi_contacts_resource_0\\AgentType=akonadi_contacts_resource\n" +
            "akonadi_vcarddir_resource_0\\AgentType=akonadi_vcarddir_resource\n" +
            "akonadi_vcard_resource_0\\AgentType=akonadi_vcard_resource\n" +
            "akonadi_vcard_resource_1\\AgentType=akonadi_vcard_resource\n" +
            "akonadi_davgroupware_resource_0\\AgentType=akonadi_davgroupware_resource\n" +
            "akonadi_maildir_resource_0\\AgentType=akonadi_maildir_resource\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_contacts_resource_0rc"), "[General]\nIsConfigured=true\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_vcarddir_resource_0rc"), "[General]\nPath[$e]=$HOME/Cards\\sDir/\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_vcard_resource_0rc"), $"[General]\nPath=file://{paths.Home}/book.vcf\nMonitorFile=true\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_vcard_resource_1rc"), "[General]\nPath=https://example.test/book.vcf\n");
        Directory.CreateDirectory(Path.Combine(paths.DataHome, "kpeoplevcard"));

        var stores = AkonadiContactSource.Discover(paths);
        stores.Select(s => (s.Id, s.Kind, s.Path)).Should().Equal(
            ("akonadi_contacts_resource_0", KdeContactStoreKind.ContactsDirectory, Path.Combine(paths.Home, ".local", "share", "contacts")),
            ("akonadi_vcarddir_resource_0", KdeContactStoreKind.VCardDirectory, Path.Combine(paths.Home, "Cards Dir")),
            ("akonadi_vcard_resource_0", KdeContactStoreKind.VCardFile, Path.Combine(paths.Home, "book.vcf")),
            ("kpeoplevcard", KdeContactStoreKind.KPeopleDirectory, Path.Combine(paths.DataHome, "kpeoplevcard")));
    }

    [Fact]
    public void Without_agentsrc_the_vcard_resource_rc_files_are_used()
    {
        var paths = TempHome();
        Write(Path.Combine(paths.ConfigHome, "akonadi_contacts_resource_2rc"), $"[General]\nPath={paths.Home}/Personal\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_ical_resource_0rc"), "[General]\nPath=/x.ics\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_contacts_resource_xrc"), "[General]\n");

        AkonadiContactSource.ResourceInstances(paths).Should().Equal(("akonadi_contacts_resource_2", AkonadiContactSource.ContactsResource));
        AkonadiContactSource.Discover(paths).Single().Path.Should().Be(Path.Combine(paths.Home, "Personal"));
    }

    [Fact]
    public void Sandboxed_apps_look_under_the_real_home()
    {
        var env = new Dictionary<string, string?>
        {
            ["HOME"] = "/home/u",
            ["XDG_CONFIG_HOME"] = "/home/u/.var/app/org.x/config",
            ["XDG_DATA_HOME"] = "/home/u/.var/app/org.x/data",
        };
        KdeContactPaths.FromEnvironment(k => env.GetValueOrDefault(k), flatpak: true)
            .Should().Be(new KdeContactPaths("/home/u", "/home/u/.config", "/home/u/.local/share"));
        KdeContactPaths.FromEnvironment(k => env.GetValueOrDefault(k), flatpak: false)
            .Should().Be(new KdeContactPaths("/home/u", "/home/u/.var/app/org.x/config", "/home/u/.var/app/org.x/data"));

        env["SNAP"] = "/snap/x/1";
        env["SNAP_REAL_HOME"] = "/home/real";
        KdeContactPaths.FromEnvironment(k => env.GetValueOrDefault(k), flatpak: false).ConfigHome.Should().Be("/home/real/.config");
    }

    [Fact]
    public async Task Akonadi_stores_are_read_into_contacts()
    {
        var paths = TempHome();
        Write(Path.Combine(paths.ConfigHome, "akonadi", "agentsrc"),
            "[Instances]\nakonadi_contacts_resource_0\\AgentType=akonadi_contacts_resource\n" +
            "akonadi_vcarddir_resource_0\\AgentType=akonadi_vcarddir_resource\n" +
            "akonadi_vcard_resource_0\\AgentType=akonadi_vcard_resource\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_vcarddir_resource_0rc"), $"[General]\nPath={paths.Home}/vcarddir\n");
        Write(Path.Combine(paths.ConfigHome, "akonadi_vcard_resource_0rc"), $"[General]\nPath={paths.Home}/all.vcf\n");

        var personal = Path.Combine(paths.Home, ".local", "share", "contacts");
        Write(Path.Combine(personal, "WARNING_README.txt"), "not a card");
        Write(Path.Combine(personal, "a1.vcf"), Card("Alpha One", "a1"));
        Write(Path.Combine(personal, "sub", "b2.vcf"), Card("Beta Two"));
        Write(Path.Combine(personal, "group.ctg"), "not a contact");
        Write(Path.Combine(paths.Home, "vcarddir", "c3"), Card("Gamma Three", "c3"));
        Write(Path.Combine(paths.Home, "vcarddir", "WARNING_README.txt"), "not a card");
        Write(Path.Combine(paths.Home, "all.vcf"), Card("Delta Four") + Card("Echo Five", "e5"));
        Write(Path.Combine(paths.DataHome, "kpeoplevcard", "kdeconnect-dev", "f6.vcf"), Card("Foxtrot Six"));

        var service = new ContactsService(new AkonadiContactSource(paths), _ => Task.FromResult<Contact?>(null));
        var contacts = (await service.GetAllAsync()).ToList();
        contacts.Select(c => (c.Id, c.DisplayName)).Should().Equal(
            ("a1", "Alpha One"),
            ("b2", "Beta Two"),
            ("c3", "Gamma Three"),
            ("akonadi_vcard_resource_0:0", "Delta Four"),
            ("e5", "Echo Five"),
            ("f6", "Foxtrot Six"));
    }

    [Fact]
    public async Task No_kde_store_is_not_supported()
    {
        var paths = TempHome();
        var act = () => new AkonadiContactSource(paths).GetVCardsAsync(CancellationToken.None);
        await act.Should().ThrowAsync<FeatureNotSupportedException>();

        // The Personal Contacts resource exists but Akonadi has not created its folder yet: no contacts, no error.
        Write(Path.Combine(paths.ConfigHome, "akonadi", "agentsrc"), "[Instances]\nakonadi_contacts_resource_0\\AgentType=akonadi_contacts_resource\n");
        (await new AkonadiContactSource(paths).GetVCardsAsync(CancellationToken.None)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Source precedence
    // ------------------------------------------------------------------

    private sealed class FakeSource(Func<IReadOnlyList<string>> read) : IContactSource
    {
        public Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken) => Task.FromResult(read());
    }

    private static IContactSource Cards(params string[] cards) => new FakeSource(() => cards);

    private static IContactSource Throws(Exception ex) => new FakeSource(() => throw ex);

    [Fact]
    public async Task Evolution_data_server_comes_first_and_kde_fills_in()
    {
        var source = new DesktopContactSource(
            Cards(Card("Eds Same", "same"), Card("Eds Only", "eds")),
            Cards(Card("Kde Same", "same"), Card("Kde Only", "kde"), Card("No Uid")));
        var cards = await source.GetVCardsAsync(CancellationToken.None);
        cards.Select(c => VCard.ToContact(c)!.DisplayName).Should().Equal("Eds Same", "Eds Only", "Kde Only", "No Uid");
    }

    [Fact]
    public async Task Kde_is_used_when_evolution_data_server_is_missing_or_refused()
    {
        var kde = Cards(Card("Kde Only", "kde"));
        (await new DesktopContactSource(Throws(new FeatureNotSupportedException("no eds")), kde).GetVCardsAsync(CancellationToken.None))
            .Should().HaveCount(1);
        (await new DesktopContactSource(Throws(new PermissionException("sandbox")), kde).GetVCardsAsync(CancellationToken.None))
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task Without_either_the_evolution_data_server_error_is_thrown()
    {
        var noKde = Throws(new FeatureNotSupportedException("no kde"));
        await new DesktopContactSource(Throws(new FeatureNotSupportedException("no eds")), noKde)
            .Invoking(s => s.GetVCardsAsync(CancellationToken.None)).Should().ThrowAsync<FeatureNotSupportedException>().WithMessage("no eds");
        await new DesktopContactSource(Throws(new PermissionException("sandbox")), noKde)
            .Invoking(s => s.GetVCardsAsync(CancellationToken.None)).Should().ThrowAsync<PermissionException>();

        // KDE's stores refused while EDS answers: EDS's contacts alone.
        (await new DesktopContactSource(Cards(Card("Eds", "e")), Throws(new PermissionException("denied"))).GetVCardsAsync(CancellationToken.None))
            .Should().HaveCount(1);
    }
}
