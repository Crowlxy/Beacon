using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class ShellIconServiceTests
{
    [Test]
    public void ReturnsSerializableBgraPixelsForExecutable()
    {
        var icon = ShellIconService.GetIcon(Environment.ProcessPath!);
        Assert.Multiple(() =>
        {
            Assert.That(icon.Width, Is.GreaterThan(0));
            Assert.That(icon.Height, Is.GreaterThan(0));
            Assert.That(icon.BgraPixels, Has.Length.EqualTo(icon.Width * icon.Height * 4));
        });
    }
}
