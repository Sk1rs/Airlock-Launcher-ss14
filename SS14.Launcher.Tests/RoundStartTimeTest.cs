using System;
using NUnit.Framework;
using SS14.Launcher.Models.ServerStatus;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(ServerStatusCache))]
public sealed class RoundStartTimeTest
{
    [Test]
    public void TestOffsetIsHonoured()
    {
        // 15:00 at +03:00 is 12:00 UTC, whatever zone the machine is in.
        var parsed = ServerStatusCache.ParseRoundStart("2026-09-13T15:00:00+03:00");

        Assert.That(parsed, Is.EqualTo(new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc)));
        Assert.That(parsed!.Value.Kind, Is.EqualTo(DateTimeKind.Utc), "it must be comparable to UtcNow");
    }

    [Test]
    public void TestZuluAndNoZoneAreUtc()
    {
        Assert.That(ServerStatusCache.ParseRoundStart("2026-09-13T12:00:00Z"),
            Is.EqualTo(new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc)));
        Assert.That(ServerStatusCache.ParseRoundStart("2026-09-13T12:00:00"),
            Is.EqualTo(new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc)), "servers send UTC when they send nothing");
    }

    [Test]
    public void TestRubbishIsNull()
    {
        Assert.That(ServerStatusCache.ParseRoundStart(null), Is.Null);
        Assert.That(ServerStatusCache.ParseRoundStart(""), Is.Null);
        Assert.That(ServerStatusCache.ParseRoundStart("soon"), Is.Null);
    }
}
