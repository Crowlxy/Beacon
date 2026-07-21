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
}
