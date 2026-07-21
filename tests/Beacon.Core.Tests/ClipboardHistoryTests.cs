using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class ClipboardHistoryTests
{
    [Test]
    public void HistoryDeduplicatesPrunesAndDeletes()
    {
        var now = DateTimeOffset.UtcNow;
        var old = new ClipboardHistoryItem("old", ClipboardContentKind.Text, "old", now.AddDays(-8), "old-hash");
        var history = new ClipboardHistory([old]);
        Assert.That(history.Add(ClipboardContentKind.Text, "new", now), Is.True);
        Assert.That(history.Add(ClipboardContentKind.Text, "new", now), Is.False);
        Assert.That(history.Items, Has.Count.EqualTo(1));
        Assert.That(history.Delete(history.Items[0].Id), Is.True);
        Assert.That(history.Items, Is.Empty);
    }
}
