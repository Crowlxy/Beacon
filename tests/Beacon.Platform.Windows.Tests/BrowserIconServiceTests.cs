using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class BrowserIconServiceTests
{
    [Test]
    public void QuotedCommandKeepsPathWithSpaces()
    {
        Assert.That(
            BrowserIconService.ExecutableFromCommand("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\" --single-argument %1"),
            Is.EqualTo(@"C:\Program Files\Google\Chrome\Application\chrome.exe"));
    }

    [Test]
    public void UnquotedCommandStopsAtExecutableExtension()
    {
        Assert.That(
            BrowserIconService.ExecutableFromCommand(@"C:\Apps\firefox.exe -osint -url ""%1"""),
            Is.EqualTo(@"C:\Apps\firefox.exe"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("\"unterminated")]
    [TestCase(@"C:\Apps\browser -url %1")]
    public void UnparsableCommandsReturnNull(string? command)
    {
        Assert.That(BrowserIconService.ExecutableFromCommand(command), Is.Null);
    }

    [Test]
    public void UnknownBrowserHasNoIcon()
    {
        Assert.That(BrowserIconService.ForBrowser("Netscape"), Is.Null);
    }
}
