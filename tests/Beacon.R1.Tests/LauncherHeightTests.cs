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

    // StatusRowとScope Chipは自分のMarginぶんも場所を取る。呼び出し側がMargin込みの値を渡す前提で加算されること。
    [Test]
    public void StatusRowBlockIsAddedWithoutResults() =>
        Assert.That(
            LauncherHeight.Calculate(56, 52, 16, 0, 7, statusRowBlockHeight: 54),
            Is.EqualTo(110));

    [Test]
    public void StatusRowBlockIsAddedOnTopOfResults() =>
        Assert.That(
            LauncherHeight.Calculate(56, 52, 16, 3, 7, statusRowBlockHeight: 54),
            Is.EqualTo(56 + 156 + 16 + 54));

    [Test]
    public void ScopeChipAndStatusRowBlocksBothCount() =>
        Assert.That(
            LauncherHeight.Calculate(56, 52, 16, 2, 7, scopeChipBlockHeight: 34, statusRowBlockHeight: 54),
            Is.EqualTo(56 + 34 + 104 + 16 + 54));

    [Test]
    public void BlocksAreExcludedWhenHidden() =>
        Assert.That(
            LauncherHeight.Calculate(56, 52, 16, 2, 7),
            Is.EqualTo(56 + 104 + 16));
}
