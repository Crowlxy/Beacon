using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class QuickKeyAndActionTargetTests
{
    [Test]
    public void MissingStorageUsesBuiltInDefaultsAndSupportsReverseLookup()
    {
        var registry = new QuickKeyRegistry(() => null, _ => { });

        var mappings = registry.Load();

        Assert.Multiple(() =>
        {
            Assert.That(mappings, Is.EqualTo(QuickKeyRegistry.DefaultMappings));
            Assert.That(registry.FindAction("rf")?.Id, Is.EqualTo("reveal"));
            Assert.That(registry.FindKey("terminal"), Is.EqualTo("term"));
        });
    }

    [Test]
    public void SavedEmptyMappingsRemainEmptyAndSavedValuesRoundTrip()
    {
        IReadOnlyDictionary<string, string>? stored = new Dictionary<string, string>();
        var registry = new QuickKeyRegistry(() => stored, values => stored = values);

        Assert.That(registry.Load(), Is.Empty);
        registry.Save(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["show"] = "reveal",
        });

        Assert.Multiple(() =>
        {
            Assert.That(stored, Contains.Key("show"));
            Assert.That(registry.FindAction("SHOW")?.Id, Is.EqualTo("reveal"));
            Assert.That(registry.FindKey("reveal"), Is.EqualTo("show"));
        });
    }

    [TestCase(ResultKind.Application, new[] { "open", "run-admin", "reveal", "copy-path" })]
    [TestCase(ResultKind.File, new[] { "open", "open-with", "reveal", "copy", "move", "rename", "zip", "copy-path" })]
    [TestCase(ResultKind.Folder, new[] { "open", "terminal", "copy", "move", "rename", "zip", "copy-path" })]
    [TestCase(ResultKind.Url, new[] { "open", "copy-path" })]
    public void ActionTableIsFilteredByTargetKind(ResultKind resultKind, string[] expectedActionIds)
    {
        var target = new SearchResultDto
        {
            Id = "target",
            ProviderId = "test",
            Title = "Target",
            Kind = resultKind,
        };
        var actions = BuiltInActions.For(ActionTargetClassifier.From(target));

        Assert.That(actions.Select(action => action.Id), Is.EquivalentTo(expectedActionIds));
    }
}

