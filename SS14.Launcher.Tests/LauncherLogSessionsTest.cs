using System;
using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(LauncherLogSessions))]
public sealed class LauncherLogSessionsTest
{
    private static string Launch(string time, string address)
        => $"2026-08-26 {time} +10:00 [DBG] Launch command: C:\\loader.exe [0] engine.zip [4] --launcher " +
           $"[5] --connect-address [6] udp://1.2.3.4:1212/ [7] --ss14-address [8] {address} [9] --cvar";

    private static string Started(string time, int pid)
        => $"2026-08-26 {time} +10:00 [DBG] Setting up manual-pipe logging for new client with PID {pid}.";

    private static string Exited(string time, int pid)
        => $"2026-08-26 {time} +10:00 [DBG] EOF, ending pipe logging for {pid}.";

    [Test]
    public void TestSingleSession()
    {
        var sessions = LauncherLogSessions.Parse([
            Launch("00:44:22.707", "ss14://example.com/"),
            Started("00:44:23.000", 1234),
            "2026-08-26 00:50:00.000 +10:00 [INF] something else entirely",
            Exited("01:44:23.000", 1234),
            // The second pipe closing must not produce a second session.
            Exited("01:44:23.100", 1234),
        ]);

        Assert.That(sessions, Has.Count.EqualTo(1));
        Assert.That(sessions[0].Address, Is.EqualTo("ss14://example.com/"));
        Assert.That(sessions[0].Duration, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void TestTwoServersInSequence()
    {
        var sessions = LauncherLogSessions.Parse([
            Launch("10:00:00.000", "ss14://first.example.com/"),
            Started("10:00:01.000", 1),
            Exited("10:30:01.000", 1),
            Launch("11:00:00.000", "ss14://second.example.com/"),
            Started("11:00:01.000", 2),
            Exited("11:10:01.000", 2),
        ]);

        Assert.That(sessions.Select(s => s.Address),
            Is.EqualTo(new[] { "ss14://first.example.com/", "ss14://second.example.com/" }));
        Assert.That(sessions[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(sessions[1].Duration, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void TestUnfinishedSessionIsDropped()
    {
        // Launcher closed while the game was still running: no exit was ever logged.
        var sessions = LauncherLogSessions.Parse([
            Launch("10:00:00.000", "ss14://example.com/"),
            Started("10:00:01.000", 1),
        ]);

        Assert.That(sessions, Is.Empty);
    }

    [Test]
    public void TestClientStartWithoutAddressIsIgnored()
    {
        // Content bundles and replays start a client with no server behind it.
        var sessions = LauncherLogSessions.Parse([
            Started("10:00:01.000", 1),
            Exited("10:30:01.000", 1),
        ]);

        Assert.That(sessions, Is.Empty);
    }

    [Test]
    public void TestSessionAcrossMidnight()
    {
        // Logs roll over daily, so the exit line lives in the next day's file.
        var sessions = LauncherLogSessions.Parse([
            "2026-08-26 23:30:00.000 +10:00 [DBG] Launch command: C:\\loader.exe [7] --ss14-address [8] ss14://example.com/",
            "2026-08-26 23:30:01.000 +10:00 [DBG] Setting up manual-pipe logging for new client with PID 5.",
            "2026-08-27 00:30:01.000 +10:00 [DBG] EOF, ending pipe logging for 5.",
        ]);

        Assert.That(sessions, Has.Count.EqualTo(1));
        Assert.That(sessions[0].Duration, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void TestOverlappingClients()
    {
        // Two clients at once: each PID has to keep its own address.
        var sessions = LauncherLogSessions.Parse([
            Launch("10:00:00.000", "ss14://first.example.com/"),
            Started("10:00:01.000", 1),
            Launch("10:05:00.000", "ss14://second.example.com/"),
            Started("10:05:01.000", 2),
            Exited("10:15:01.000", 2),
            Exited("10:30:01.000", 1),
        ]);

        Assert.That(sessions.Single(s => s.Address == "ss14://first.example.com/").Duration,
            Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(sessions.Single(s => s.Address == "ss14://second.example.com/").Duration,
            Is.EqualTo(TimeSpan.FromMinutes(10)));
    }
}
