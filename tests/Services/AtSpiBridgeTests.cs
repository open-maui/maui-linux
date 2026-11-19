// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.AtSpi;
using Microsoft.Maui.Controls.Linux.Tests.Views;
using Tmds.DBus;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The AT-SPI2 bridge: the accessible model the controls give it, and the objects and events it
/// exports, read back over a private D-Bus daemon by a client as a screen reader reads them.
/// </summary>
[Collection("LinuxApplication.Current")]
public class AtSpiBridgeTests
{
    private static ContentPage Page(out Label heading, out Entry entry, out CheckBox check, out Slider slider)
    {
        heading = new Label { Text = "Settings" };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        entry = new Entry { Text = "Ada", Placeholder = "Name" };
        check = new CheckBox { IsChecked = true };
        slider = new Slider { Minimum = 0, Maximum = 10, Value = 3 };
        return new ContentPage
        {
            Content = new VerticalStackLayout
            {
                heading,
                new Label { Text = "Your name" },
                entry,
                new Entry { Text = "secret", IsPassword = true },
                new Button { Text = "Save" },
                check,
                slider,
            },
        };
    }

    private static IAccessible A(View view) => (IAccessible)view.Handler!.PlatformView!;

    [Fact]
    public void Controls_give_assistive_technology_their_role_name_states_value_and_text()
    {
        using var host = new HeadlessMauiHost(Page(out var heading, out var entry, out var check, out var slider), withEngine: true);
        host.Context.Render();
        var stack = (VerticalStackLayout)((ContentPage)host.Window.Page!).Content;

        A(heading).Role.Should().Be(AccessibleRole.Heading);
        A(heading).AccessibleName.Should().Be("Settings", "a label is named by its text");
        A(stack.Children[1] as View ?? throw new InvalidOperationException()).Role.Should().Be(AccessibleRole.Label);
        ((IAccessibleText)A(heading)).Text.Should().Be("Settings");

        A(entry).Role.Should().Be(AccessibleRole.Entry);
        A(entry).AccessibleName.Should().Be("Name", "an unlabelled entry is named by its placeholder");
        A(entry).States.Should().HaveFlag(AccessibleStates.Editable).And.HaveFlag(AccessibleStates.SingleLine);
        ((IAccessibleText)A(entry)).Text.Should().Be("Ada");

        var password = A((View)stack.Children[3]);
        password.Role.Should().Be(AccessibleRole.PasswordText);
        ((IAccessibleText)password).Text.Should().Be("••••••", "a password is read as bullets");

        var save = A((View)stack.Children[4]);
        save.Role.Should().Be(AccessibleRole.Button);
        save.AccessibleName.Should().Be("Save");
        save.Actions.Select(a => a.Name).Should().Equal("click");

        A(check).Role.Should().Be(AccessibleRole.CheckBox);
        A(check).States.Should().HaveFlag(AccessibleStates.Checked);
        A(check).DoAction("toggle").Should().BeTrue();
        check.IsChecked.Should().BeFalse();

        A(slider).Role.Should().Be(AccessibleRole.Slider);
        A(slider).Value.Should().Be(3);
        A(slider).MaxValue.Should().Be(10);
        A(slider).SetValue(7).Should().BeTrue();
        slider.Value.Should().Be(7);

        A(stack).Role.Should().Be(AccessibleRole.Filler, "a plain layout is a filler, as GTK's boxes are");
        A(stack).Children.Should().HaveCount(7);
    }

    [Fact]
    public void A_control_labelled_by_a_label_is_named_by_it_and_related_to_it()
    {
        var label = new Label { Text = "Email" };
        var email = new Entry { Placeholder = "you@example.com" };
        var described = new Entry { Placeholder = "x" };
#pragma warning disable CS0618 // AutomationProperties.LabeledBy is obsolete, but MAUI's platforms still honour it
        AutomationProperties.SetLabeledBy(email, label);
        AutomationProperties.SetLabeledBy(described, label);
#pragma warning restore CS0618
        SemanticProperties.SetDescription(described, "Backup email");
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { label, email, described } }, withEngine: true);
        host.Context.Render();

        A(email).AccessibleName.Should().Be("Email", "a labelled control takes its label's name, before its placeholder");
        A(described).AccessibleName.Should().Be("Backup email", "SemanticProperties.Description still wins");

        var emailRelations = ((IAccessibleRelations)A(email)).Relations;
        emailRelations.Should().ContainSingle(r => r.Relation == AccessibleRelation.LabelledBy)
            .Which.Targets.Should().Equal(A(label));
        var labelRelations = ((IAccessibleRelations)A(label)).Relations;
        labelRelations.Should().ContainSingle(r => r.Relation == AccessibleRelation.LabelFor)
            .Which.Targets.Should().BeEquivalentTo(new[] { A(email), A(described) });

        label.Text = "Work email";
        ((SkiaLabel)label.Handler!.PlatformView!).Text.Should().Be("Work email", "the label's view has the new text");
        A(email).AccessibleName.Should().Be("Work email", "the name follows the label");
    }

    [Fact]
    public void States_map_to_the_AT_SPI_state_bits()
    {
        // AtspiStateType: enabled 8, focusable 11, checked 4, sensitive 24, showing 25, visible 30, read-only 43.
        var set = AtSpiBridge.StateSet(AccessibleStates.Enabled | AccessibleStates.Focusable | AccessibleStates.Checked | AccessibleStates.Visible | AccessibleStates.ReadOnly, showing: true);
        ulong bits = set[0] | ((ulong)set[1] << 32);
        foreach (int state in new[] { 4, 8, 11, 24, 25, 30, 43 })
            ((bits >> state) & 1).Should().Be(1UL, $"state {state}");
        ((bits >> 12) & 1).Should().Be(0UL, "not focused");
        AtSpiBridge.AtSpiRole(AccessibleRole.Button, null!).Should().Be(43u, "ATSPI_ROLE_PUSH_BUTTON");
        AtSpiBridge.AtSpiRole(AccessibleRole.Label, null!).Should().Be(29u);
        AtSpi2AccessibilityService.StateName(AccessibleState.MultiLine).Should().Be("multi-line");
        AtSpi2AccessibilityService.StateName(AccessibleState.HasToolTip).Should().Be("has-tooltip");
    }

    [Fact]
    public void Text_ranges_follow_words_and_lines()
    {
        TextBoundaries.AtGranularity("hello big world", 7, 1).Should().Be(("big ", 6, 10));
        TextBoundaries.AtGranularity("one\ntwo\nthree", 5, 3).Should().Be(("two\n", 4, 8));
        TextBoundaries.AtGranularity("one\ntwo", 0, 0).Should().Be(("o", 0, 1));
    }

    [Fact]
    public async Task A_screen_reader_reads_the_tree_and_hears_the_changes_over_D_Bus()
    {
        var daemon = StartPrivateBus(out var address);
        if (daemon == null)
            return; // no dbus-daemon on this machine
        try
        {
            using var host = new HeadlessMauiHost(Page(out _, out var entry, out var check, out _), withEngine: true);
            host.Context.Render();
            var bridge = await AtSpiBridge.AttachAsync(address, "BridgeTest");
            try
            {
                using var client = new Connection(address);
                await client.ConnectAsync();
                var root = client.CreateProxy<IAtSpiAccessible>(bridge.BusName, new ObjectPath(AtSpiBridge.RootPath));
                (await root.GetRoleAsync()).Should().Be(75u, "ATSPI_ROLE_APPLICATION");
                ((string)await root.GetAsync("Name")).Should().Be("BridgeTest");

                var windows = await root.GetChildrenAsync();
                windows.Should().ContainSingle("one window");
                var frame = client.CreateProxy<IAtSpiAccessible>(windows[0].Item1, windows[0].Item2);
                (await frame.GetRoleAsync()).Should().Be(23u, "ATSPI_ROLE_FRAME");
                var viaIndex = (await root.GetChildAtIndexAsync(0)).Item1;
                viaIndex.Should().Be(windows[0], "GetChildAtIndex returns one (so) struct");
                ((ValueTuple<string, ObjectPath>)await frame.GetAsync("Parent")).Item2.ToString().Should().Be(AtSpiBridge.RootPath);

                // Walk down to the entry and the check box, as a screen reader does.
                var all = new List<(string, ObjectPath)>();
                var queue = new Queue<(string, ObjectPath)>(windows);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    all.Add(current);
                    foreach (var child in await client.CreateProxy<IAtSpiAccessible>(current.Item1, current.Item2).GetChildrenAsync())
                        queue.Enqueue(child);
                }
                async Task<(string, ObjectPath)> FindAsync(uint role, string name)
                {
                    foreach (var reference in all)
                    {
                        var proxy = client.CreateProxy<IAtSpiAccessible>(reference.Item1, reference.Item2);
                        if (await proxy.GetRoleAsync() == role && (string)await proxy.GetAsync("Name") == name)
                            return reference;
                    }
                    throw new InvalidOperationException($"no role {role} named '{name}'");
                }

                var label = await FindAsync(29u, "Your name");
                (await client.CreateProxy<IAtSpiAccessible>(label.Item1, label.Item2).GetInterfacesAsync())
                    .Should().Contain("org.a11y.atspi.Text");

                var entryRef = await FindAsync(79u, "Name");
                var text = client.CreateProxy<IAtSpiText>(entryRef.Item1, entryRef.Item2);
                (await text.GetTextAsync(0, -1)).Should().Be("Ada");
                ((int)await text.GetAsync("CharacterCount")).Should().Be(3);
                (await client.CreateProxy<IAtSpiComponent>(entryRef.Item1, entryRef.Item2).GetExtentsAsync(1)).Item1.Item3.Should().BeGreaterThan(0);

                // Labelled by the "Your name" label: the relation set and the name come from it.
                var nameLabel = (Label)((VerticalStackLayout)((ContentPage)host.Window.Page!).Content).Children[1];
#pragma warning disable CS0618 // AutomationProperties.LabeledBy is obsolete, but MAUI's platforms still honour it
                AutomationProperties.SetLabeledBy(entry, nameLabel);
#pragma warning restore CS0618
                var entryAccessible = client.CreateProxy<IAtSpiAccessible>(entryRef.Item1, entryRef.Item2);
                var relations = await entryAccessible.GetRelationSetAsync();
                relations.Should().ContainSingle().Which.Item1.Should().Be(2u, "ATSPI_RELATION_LABELLED_BY");
                var labelRef = await FindAsync(29u, "Your name");
                relations[0].Item2.Should().Equal(labelRef);
                ((string)await entryAccessible.GetAsync("Name")).Should().Be("Your name");
                var labelRelations = await client.CreateProxy<IAtSpiAccessible>(labelRef.Item1, labelRef.Item2).GetRelationSetAsync();
                labelRelations.Should().ContainSingle().Which.Item1.Should().Be(1u, "ATSPI_RELATION_LABEL_FOR");

                (string, ObjectPath) checkRef = default;
                foreach (var reference in all)
                {
                    if (await client.CreateProxy<IAtSpiAccessible>(reference.Item1, reference.Item2).GetRoleAsync() == 7u)
                        checkRef = reference;
                }
                var events = new List<(string, int)>();
                var received = new TaskCompletionSource();
                using var watch = await client.CreateProxy<IAtSpiObjectEvents>(checkRef.Item1, checkRef.Item2).WatchStateChangedAsync(e =>
                {
                    lock (events)
                        events.Add((e.Item1, e.Item2));
                    received.TrySetResult();
                });
                (await client.CreateProxy<IAtSpiAction>(checkRef.Item1, checkRef.Item2).DoActionAsync(1)).Should().BeTrue("toggle");
                check.IsChecked.Should().BeFalse();
                await Task.WhenAny(received.Task, Task.Delay(3000));
                events.Should().Contain(("checked", 0), "the check box tells the screen reader it was unchecked");

                // Typing into the focused entry: the inserted text and the caret move reach the screen reader.
                var textEvents = new List<(string, int, int)>();
                var caretEvents = new List<int>();
                var typed = new TaskCompletionSource();
                var entryEvents = client.CreateProxy<IAtSpiObjectEvents>(entryRef.Item1, entryRef.Item2);
                using var textWatch = await entryEvents.WatchTextChangedAsync(e =>
                {
                    lock (textEvents)
                        textEvents.Add((e.Item1, e.Item2, e.Item3));
                });
                using var caretWatch = await entryEvents.WatchTextCaretMovedAsync(e =>
                {
                    lock (caretEvents)
                        caretEvents.Add(e.Item2);
                    typed.TrySetResult();
                });
                var entryView = (SkiaEntry)entry.Handler!.PlatformView!;
                host.Context.FocusedView = entryView;
                entryView.CursorPosition = 3;
                host.DisplayWindow.RaiseTextInput("!");
                entry.Text.Should().Be("Ada!");
                await Task.WhenAny(typed.Task, Task.Delay(3000));
                await Task.Delay(200);
                lock (textEvents)
                    textEvents.Should().Contain(("insert", 3, 1), "the typed character is announced as inserted at offset 3");
                lock (caretEvents)
                    caretEvents.Should().Contain(4, "the caret moved past it");

                var states = await client.CreateProxy<IAtSpiAccessible>(checkRef.Item1, checkRef.Item2).GetStateAsync();
                ((states[0] >> 4) & 1).Should().Be(0u, "no longer checked");
                ((states[0] >> 25) & 1).Should().Be(1u, "showing");
            }
            finally
            {
                AtSpiBridge.Detach();
            }
        }
        finally
        {
            try { daemon.Kill(); } catch (InvalidOperationException) { }
            daemon.Dispose();
        }
    }

    private static Process? StartPrivateBus(out string address)
    {
        address = string.Empty;
        try
        {
            var daemon = Process.Start(new ProcessStartInfo("dbus-daemon", "--session --nofork --print-address=1")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (daemon == null)
                return null;
            address = daemon.StandardOutput.ReadLine() ?? string.Empty;
            return address.Length > 0 ? daemon : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
