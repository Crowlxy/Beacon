using Beacon.Contracts;
using Beacon.Platform.Windows;
using Beacon.Platform.Windows.Everything;
using Beacon.Platform.Windows.Programs;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

[TestFixture]
public sealed class PlatformSearchTests
{
    [Test]
    public void ShellAdaptersReturnDataOnlyDescriptors()
    {
        var path = Path.GetFullPath("sample.txt");
        Assert.Multiple(() =>
        {
            Assert.That(ShellImageService.Icon(path), Is.EqualTo(new IconDescriptor(IconSource.FileShellIcon, path)));
            Assert.That(ShellImageService.Thumbnail(path), Is.EqualTo(new IconDescriptor(IconSource.FileThumbnail, path)));
            Assert.That(ShellLocalization.DisplayName(@"C:\Apps\Beacon.lnk"), Is.EqualTo("Beacon"));
        });
    }

    [TestCase("report.xlsx", IconSource.FileShellIcon)]
    [TestCase("report.lnk", IconSource.FileShellIcon)]
    [TestCase("archive.7z", IconSource.FileShellIcon)]
    [TestCase("photo.png", IconSource.FileThumbnail)]
    [TestCase("movie.mp4", IconSource.FileThumbnail)]
    [TestCase("document.pdf", IconSource.FileThumbnail)]
    public void FileVisualUsesExplorerIconExceptPreviewableMedia(string path, IconSource expected) =>
        Assert.That(ShellImageService.ForPath(path).Source, Is.EqualTo(expected));

    [Test]
    public void EverythingOptionsRejectNegativeBounds()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in EverythingApi.SearchAsync(new("x", Offset: -1), CancellationToken.None)) { }
        });
    }
}
