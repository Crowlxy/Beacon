using Beacon.Contracts;
using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class AppSearchRankingTests
{
    [Test]
    public void VsRanksVisualStudioCodeAboveVideoStudio()
    {
        var visualStudioCode = Application("Visual Studio Code", @"C:\Shortcuts\Visual Studio Code.lnk", @"C:\Apps\Microsoft VS Code\Code.exe");
        var videoStudio = Application("VideoStudio", @"C:\Shortcuts\VideoStudio.lnk", @"C:\Apps\VideoStudio\VideoStudio.exe");

        Assert.That(
            AppSearchProvider.BestApplicationMatchScore("vs", visualStudioCode),
            Is.GreaterThan(AppSearchProvider.BestApplicationMatchScore("vs", videoStudio)));
    }

    [Test]
    public void ExecutableAndShortcutNamesParticipateButUwpPackagePathDoesNot()
    {
        var shortcut = Application("Editor", @"C:\Shortcuts\VSCode.lnk", @"C:\Apps\Code.exe");
        var uwp = Application("Editor", @"C:\Program Files\WindowsApps\Vendor.VSCode_1.0", "Windows app");

        Assert.Multiple(() =>
        {
            Assert.That(AppSearchProvider.BestApplicationMatchScore("vscode", shortcut), Is.GreaterThan(0));
            Assert.That(AppSearchProvider.BestApplicationMatchScore("vscode", uwp), Is.EqualTo(0));
        });
    }

    private static SearchResultDto Application(string title, string path, string subtitle) => new()
    {
        Id = path,
        ProviderId = AppSearchProvider.Id,
        Title = title,
        Subtitle = subtitle,
        Kind = ResultKind.Application,
        FilePath = path,
    };
}

