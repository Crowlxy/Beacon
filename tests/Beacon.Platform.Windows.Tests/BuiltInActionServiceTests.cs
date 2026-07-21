using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class BuiltInActionServiceTests
{
    [Test]
    public void RenameAndZipUseValidatedNativeFileOperations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Beacon-actions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "before.txt");
            File.WriteAllText(source, "data");
            Assert.That(BuiltInActionService.Execute("rename", source, "after.txt").Success, Is.True);
            var renamed = Path.Combine(root, "after.txt");
            Assert.That(BuiltInActionService.Execute("zip", renamed).Success, Is.True);
            Assert.That(File.Exists(renamed + ".zip"), Is.True);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void MissingSourceAndInvalidActionAreRejected()
    {
        Assert.That(BuiltInActionService.Execute("open", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).Success, Is.False);
        var path = Path.GetTempFileName();
        try { Assert.That(BuiltInActionService.Execute("unknown", path).Success, Is.False); }
        finally { File.Delete(path); }
    }
}
