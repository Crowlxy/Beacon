using Beacon.WinUI;
using NUnit.Framework;

namespace Beacon.R1.Tests;

public sealed class LauncherHeightTests
{
    [TestCase(0, 64)]
    [TestCase(1, 132)]
    [TestCase(8, 496)]
    [TestCase(20, 496)]
    public void HeightTracksVisibleRowsAndCapsAtEight(int count, double expected) =>
        Assert.That(LauncherHeight.Calculate(64, 52, 16, count, 8), Is.EqualTo(expected));
}
