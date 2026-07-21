using System.Text;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class ClipboardHistoryServiceTests
{
    [Test]
    public void DpapiRoundTripsForCurrentUser()
    {
        var source = Encoding.UTF8.GetBytes("Beacon clipboard test");
        var encrypted = Dpapi.Protect(source);

        Assert.That(encrypted, Is.Not.EqualTo(source));
        Assert.That(Dpapi.Unprotect(encrypted), Is.EqualTo(source));
    }

    [Test]
    public void WindowsRecentEnumerationIsBounded()
    {
        var results = WindowsRecentFiles.Get(3);

        Assert.That(results, Has.Count.LessThanOrEqualTo(3));
        Assert.That(results.All(x => x.FilePath is not null), Is.True);
    }
}
