using System.Text.Json;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class LegacyMigrationTests
{
    [Test]
    public void DetectReturnsNullForNewInstall()
    {
        using var fixture = new MigrationFixture();
        Assert.That(LegacyImporter.Detect(fixture.Roaming, fixture.Application), Is.Null);
    }

    [Test]
    public void ImportMapsSettingsBacksUpAndLeavesSource()
    {
        using var fixture = new MigrationFixture();
        var candidate = fixture.CreateLegacy("""{"Hotkey":"Ctrl + Space","ColorScheme":"Dark","Language":"ja","CustomShortcuts":[{"Keyword":"x"}]}""");
        var result = LegacyImporter.Import(candidate, fixture.Data, new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero));
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(candidate.SettingsPath), Is.True);
            Assert.That(File.Exists(Path.Combine(fixture.Data, "Backup", "legacy-20260723-000000", "Settings", "Settings.json")), Is.True);
            Assert.That(JsonDocument.Parse(File.ReadAllText(fixture.Settings)).RootElement.GetProperty("GlobalHotkey").GetString(), Is.EqualTo("Ctrl+Space"));
            Assert.That(File.ReadAllText(Path.Combine(fixture.Data, "State", "migration.json")), Does.Contain("Succeeded"));
        });
    }

    [Test]
    public void BrokenLegacySettingsRollBackAndRemainAvailable()
    {
        using var fixture = new MigrationFixture();
        var before = File.ReadAllText(fixture.Settings);
        var candidate = fixture.CreateLegacy("{broken");
        var result = LegacyImporter.Import(candidate, fixture.Data);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(File.ReadAllText(fixture.Settings), Is.EqualTo(before));
            Assert.That(File.Exists(candidate.SettingsPath), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(fixture.Data, "State", "migration.json")), Does.Contain("Failed"));
        });
    }

    [Test]
    public void DeclineRecordsDecisionWithoutChangingOrDeletingSource()
    {
        using var fixture = new MigrationFixture();
        var before = File.ReadAllText(fixture.Settings);
        var candidate = fixture.CreateLegacy("""{"Hotkey":"Alt + Space"}""");
        LegacyImporter.RecordDeclined(candidate, fixture.Data);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(fixture.Settings), Is.EqualTo(before));
            Assert.That(File.Exists(candidate.SettingsPath), Is.True);
            Assert.That(LegacyImporter.WasAttempted(fixture.Data), Is.True);
        });
    }

    private sealed class MigrationFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "beacon-migration-" + Guid.NewGuid().ToString("N"));
        public string Roaming => Path.Combine(_root, "roaming");
        public string Application => Path.Combine(_root, "app");
        public string Data => Path.Combine(Application, "Data");
        public string Settings => Path.Combine(Data, "Settings", "r1-settings.json");

        public MigrationFixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
            Directory.CreateDirectory(Path.Combine(Data, "State"));
            File.WriteAllText(Settings, """{"ContractVersion":1,"GlobalHotkey":"Alt+Shift+Space"}""");
        }

        public LegacyMigrationCandidate CreateLegacy(string json)
        {
            var path = Path.Combine(Roaming, "Beacon", "Settings", "Settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            return new(Path.Combine(Roaming, "Beacon"), path);
        }

        public void Dispose() => Directory.Delete(_root, true);
    }
}
