using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class LauncherUxTests
{
    [Test]
    public void EscapeReturnsExactlyOneStateAtATime()
    {
        var machine = new LauncherStateMachine();
        machine.EnterBrowse(BrowseCategory.Files);
        machine.OpenContextActions();
        machine.BeginActionInput();
        machine.RequestConfirmation();
        Assert.That(machine.TryBeginRunning(), Is.True);
        Assert.That(machine.TryBeginRunning(), Is.False);

        foreach (var expected in new[]
                 {
                     LauncherViewState.Confirmation,
                     LauncherViewState.ActionInput,
                     LauncherViewState.ContextActions,
                     LauncherViewState.Browse,
                     LauncherViewState.Search,
                 })
        {
            Assert.That(machine.Back(false), Is.EqualTo(BackOutcome.StateChanged));
            Assert.That(machine.State, Is.EqualTo(expected));
        }
        Assert.That(machine.Back(false), Is.EqualTo(BackOutcome.Close));
    }

    [Test]
    public void SearchWithInputClearsBeforeClose() =>
        Assert.That(new LauncherStateMachine().Back(true), Is.EqualTo(BackOutcome.ClearQuery));

    [TestCase("/app", QueryScope.Applications)]
    [TestCase("/アプリ", QueryScope.Applications)]
    [TestCase("/ファイル", QueryScope.Files)]
    [TestCase("/フォルダ", QueryScope.Folders)]
    [TestCase("/action", QueryScope.Actions)]
    [TestCase("/setting", QueryScope.Settings)]
    public void ScopeAliasesMapOnceInCore(string text, QueryScope expected)
    {
        Assert.That(QueryScopeSelection.TryParse(text, out var scope), Is.True);
        Assert.That(scope.ProviderScope, Is.EqualTo(expected));
    }

    [Test]
    public void ClipboardScopeAndQuickKeysAreDefined()
    {
        Assert.That(QueryScopeSelection.TryParse("/clipboard", out var scope) && scope.IsClipboard, Is.True);
        Assert.That(BuiltInActions.All, Has.Count.EqualTo(10));
        Assert.That(BuiltInActions.FindQuickKey("term")?.Id, Is.EqualTo("terminal"));
    }

    [Test]
    public void ActionInputFlowAdvancesOneParameterAtATime()
    {
        var descriptor = new ActionDescriptor("test", "Test", "", [
            new("name", "Name", ActionParameterKind.Text, true),
            new("choice", "Choice", ActionParameterKind.Choice, true, ["A", "B"]),
        ]);
        var flow = new ActionInputFlow(descriptor);

        Assert.That(flow.Submit(""), Is.False);
        Assert.That(flow.Submit("value"), Is.True);
        Assert.That(flow.Current?.Id, Is.EqualTo("choice"));
        Assert.That(flow.Submit("C"), Is.False);
        Assert.That(flow.Submit("B"), Is.True);
        Assert.That(flow.Complete, Is.True);
        Assert.That(flow.Values["name"], Is.EqualTo("value"));
        Assert.That(flow.Rewind(), Is.EqualTo("B"));
        Assert.That(flow.Current?.Id, Is.EqualTo("choice"));
    }
}
