using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

[TestFixture]
public sealed class DataRootResolverTests
{
    [Test]
    public void PortableFlagUsesExecutableAdjacentData()
    {
        using var directories = new TemporaryDirectories();
        File.WriteAllText(Path.Combine(directories.ExecutableRoot, "portable.flag"), string.Empty);
        var result = DataRootResolver.Resolve(directories.ExecutableRoot, directories.LocalAppDataRoot);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsPortable, Is.True);
            Assert.That(result.Path, Is.EqualTo(Path.Combine(directories.ExecutableRoot, "Data")));
            Assert.That(Directory.Exists(Path.Combine(result.Path, "Logs")), Is.True);
        });
    }

    [Test]
    public void MissingFlagAndDataUsesLocalAppData()
    {
        using var directories = new TemporaryDirectories();
        var result = DataRootResolver.Resolve(directories.ExecutableRoot, directories.LocalAppDataRoot);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsPortable, Is.False);
            Assert.That(result.Path, Is.EqualTo(Path.Combine(directories.LocalAppDataRoot, "Beacon", "Data")));
        });
    }

    [Test]
    public void MissingFlagWithExistingDataRequiresAnExplicitChoice()
    {
        using var directories = new TemporaryDirectories();
        Directory.CreateDirectory(Path.Combine(directories.ExecutableRoot, "Data"));
        var exception = Assert.Throws<DataRootResolutionException>(
            () => DataRootResolver.Resolve(directories.ExecutableRoot, directories.LocalAppDataRoot));
        Assert.That(exception!.Message, Does.Contain("portable.flag is missing"));
    }

    [Test]
    public void UnwritableDataPathFailsWithoutFallback()
    {
        using var directories = new TemporaryDirectories();
        File.WriteAllText(Path.Combine(directories.ExecutableRoot, "portable.flag"), string.Empty);
        File.WriteAllText(Path.Combine(directories.ExecutableRoot, "Data"), string.Empty);
        var exception = Assert.Throws<DataRootResolutionException>(
            () => DataRootResolver.Resolve(directories.ExecutableRoot, directories.LocalAppDataRoot));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("cannot write"));
            Assert.That(Directory.Exists(Path.Combine(directories.LocalAppDataRoot, "Beacon", "Data")), Is.False);
        });
    }

    private sealed class TemporaryDirectories : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"Beacon-Core-{Guid.NewGuid():N}");

        public TemporaryDirectories()
        {
            ExecutableRoot = Directory.CreateDirectory(Path.Combine(_root, "app")).FullName;
            LocalAppDataRoot = Directory.CreateDirectory(Path.Combine(_root, "local")).FullName;
        }

        public string ExecutableRoot { get; }
        public string LocalAppDataRoot { get; }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
