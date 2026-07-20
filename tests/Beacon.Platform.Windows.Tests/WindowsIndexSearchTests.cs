using Beacon.Contracts;
using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class WindowsIndexSearchTests
{
    [Test]
    public void QueryKeepsJapaneseAndEscapesSingleQuotes()
    {
        var sql = WindowsIndexSearch.BuildQuery("日本語 O'Brien");
        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("LIKE '日本語 O''Brien%'") );
            Assert.That(sql, Does.Contain("CONTAINS(System.FileName,'\"日本語 O''Brien*\"')"));
            Assert.That(sql, Does.Contain("scope='file:'"));
        });
    }

    [TestCase("#")]
    [TestCase("＠＃")]
    public void ReservedOnlyQueryIsRejected(string query) =>
        Assert.That(WindowsIndexSearch.BuildQuery(query), Is.Null);

    [Test]
    public void ItemUrlPreservesHashAndDirectoryKind()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WindowsIndexSearch.ItemUrlToPath("file:///C:/Temp/a#b.txt"), Is.EqualTo(@"C:\Temp\a#b.txt"));
            Assert.That(WindowsIndexSearch.ItemTypeToKind("Directory"), Is.EqualTo(ResultKind.Folder));
            Assert.That(WindowsIndexSearch.ItemTypeToKind(".txt"), Is.EqualTo(ResultKind.File));
        });
    }
}
