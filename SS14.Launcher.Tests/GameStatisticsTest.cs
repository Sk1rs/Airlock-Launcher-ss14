using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(GameStatistics))]
public sealed class GameStatisticsTest
{
    private const string ServerA = "ss14://example.com";
    private const string ServerB = "ss14://other.example.com";

    [Test]
    public void TestFirstSession()
    {
        var entries = GameStatistics.Merge([], ServerA, "Example", TimeSpan.FromMinutes(90)).ToArray();

        Assert.That(entries, Has.Length.EqualTo(1));
        Assert.That(entries[0].Address, Is.EqualTo(ServerA));
        Assert.That(entries[0].Name, Is.EqualTo("Example"));
        Assert.That(entries[0].Sessions, Is.EqualTo(1));
        Assert.That(entries[0].Total, Is.EqualTo(TimeSpan.FromMinutes(90)));
    }

    [Test]
    public void TestSessionsAddUp()
    {
        var entries = GameStatistics.Merge([], ServerA, "Example", TimeSpan.FromMinutes(90));
        entries = GameStatistics.Merge(entries, ServerA, "Example", TimeSpan.FromMinutes(30));

        var entry = entries.Single();

        Assert.That(entry.Sessions, Is.EqualTo(2));
        Assert.That(entry.Total, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void TestFirstPlayedIsKept()
    {
        var first = GameStatistics.Merge([], ServerA, "Example", TimeSpan.FromMinutes(90)).Single();
        var second = GameStatistics.Merge([first], ServerA, "Example", TimeSpan.FromMinutes(30)).Single();

        Assert.That(second.FirstPlayed, Is.EqualTo(first.FirstPlayed));
        Assert.That(second.LastPlayed, Is.GreaterThanOrEqualTo(first.LastPlayed));
    }

    [Test]
    public void TestNameIsUpdatedButNotErased()
    {
        var entries = GameStatistics.Merge([], ServerA, null, TimeSpan.FromMinutes(10));
        entries = GameStatistics.Merge(entries, ServerA, "Renamed", TimeSpan.FromMinutes(10));

        Assert.That(entries.Single().Name, Is.EqualTo("Renamed"), "a known name replaces a missing one");

        // A connect that came without a name must not wipe the one we already had.
        entries = GameStatistics.Merge(entries, ServerA, null, TimeSpan.FromMinutes(10));

        Assert.That(entries.Single().Name, Is.EqualTo("Renamed"));
    }

    [Test]
    public void TestServersAreSeparate()
    {
        var entries = GameStatistics.Merge([], ServerA, "A", TimeSpan.FromMinutes(90));
        entries = GameStatistics.Merge(entries, ServerB, "B", TimeSpan.FromMinutes(30)).ToArray();

        Assert.That(entries.Count(), Is.EqualTo(2));
        Assert.That(entries.Single(e => e.Address == ServerA).Total, Is.EqualTo(TimeSpan.FromMinutes(90)));
        Assert.That(entries.Single(e => e.Address == ServerB).Total, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }
}

[TestFixture]
[TestOf(typeof(LauncherImport))]
public sealed class LauncherImportTest
{
    private static HashSet<string> Columns(params string[] names)
        => new(names, StringComparer.OrdinalIgnoreCase);

    [Test]
    public void TestOrdersByPositionWhenPresent()
    {
        Assert.That(
            LauncherImport.FavoriteOrderClause(Columns("Address", "Name", "Position")),
            Is.EqualTo("Position DESC"));
    }

    [Test]
    public void TestFallsBackToRaiseTime()
    {
        // The Space Wizards launcher still uses this one.
        Assert.That(
            LauncherImport.FavoriteOrderClause(Columns("Address", "Name", "RaiseTime")),
            Is.EqualTo("RaiseTime DESC"));
    }

    [Test]
    public void TestUnknownSchemaStillImports()
    {
        Assert.That(
            LauncherImport.FavoriteOrderClause(Columns("Address", "Name")),
            Is.EqualTo("Address"));
    }
}
