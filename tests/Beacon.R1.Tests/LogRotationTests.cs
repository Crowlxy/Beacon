using Beacon.WinUI;
using NUnit.Framework;

namespace Beacon.R1.Tests;

public sealed class LogRotationTests
{
    [Test]
    public void RotateLogsKeepsNewestConfiguredCount()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Beacon-log-test-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "Logs");
        Directory.CreateDirectory(logs);
        try
        {
            foreach (var date in new[] { "2026-07-18", "2026-07-19", "2026-07-20" })
                File.WriteAllText(Path.Combine(logs, $"beacon-{date}.log"), date);

            R1Storage.RotateLogs(root, 2);

            var remaining = Directory.GetFiles(logs).Select(Path.GetFileName).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(remaining, Has.Length.EqualTo(2));
                Assert.That(remaining, Does.Contain("beacon-2026-07-19.log"));
                Assert.That(remaining, Does.Contain("beacon-2026-07-20.log"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
