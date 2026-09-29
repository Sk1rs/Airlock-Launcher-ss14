using System.Net.Http;
using NUnit.Framework;
using SS14.Launcher.Api;
using SS14.Launcher.Models;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(RoundWatcher))]
public sealed class RoundWatcherTest
{
    private const string Server = "ss14://example.org";

    private static ServerApi.ServerStatus Status(ServerApi.GameRunLevel level, int players) =>
        new("Example", players, 80, null, level, null, null);

    private sealed class MemoryStore : IRoundWatchStore
    {
        public int PlayerThreshold { get; set; }

        public string WatchedJson { get; set; } = "[]";
    }

    private static (RoundWatcher Watcher, MemoryStore Store) Make()
    {
        var store = new MemoryStore();
        return (new RoundWatcher(store, new HttpClient()), store);
    }

    [Test]
    public void TestFirstPollOnlyRecords()
    {
        var (watcher, _) = Make();

        // Opening the launcher mid-lobby is not news.
        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.PreRoundLobby, 12)), Is.Empty);
    }

    [Test]
    public void TestLobbyAfterRoundIsNews()
    {
        var (watcher, _) = Make();

        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 40));
        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.PostRound, 38));

        var events = watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.PreRoundLobby, 30));

        Assert.That(events, Is.EqualTo(new[] { RoundEvent.LobbyOpened }));
    }

    [Test]
    public void TestLobbyStayingOpenIsNotRepeated()
    {
        var (watcher, _) = Make();

        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 40));
        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.PreRoundLobby, 30));

        // Still in the lobby a poll later: the user already heard about it.
        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.PreRoundLobby, 35)), Is.Empty);
    }

    [Test]
    public void TestPlayerThresholdFiresOnCrossingUp()
    {
        var (watcher, store) = Make();
        store.PlayerThreshold = 20;

        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 15));

        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 22)),
            Is.EqualTo(new[] { RoundEvent.PlayersReached }));

        // Hovering above the line is not a new crossing.
        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 25)), Is.Empty);

        // Dropping below and climbing back is.
        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 10));
        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 20)),
            Is.EqualTo(new[] { RoundEvent.PlayersReached }));
    }

    [Test]
    public void TestThresholdOffByDefault()
    {
        var (watcher, _) = Make();

        watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 0));

        Assert.That(watcher.Evaluate(Server, Status(ServerApi.GameRunLevel.InRound, 200)), Is.Empty);
    }

    [Test]
    public void TestWatchListRoundTrips()
    {
        var (watcher, _) = Make();

        Assert.That(watcher.IsWatched(Server), Is.False);

        watcher.SetWatched(Server, true);
        Assert.That(watcher.IsWatched(Server), Is.True);
        Assert.That(watcher.IsWatched("SS14://EXAMPLE.ORG"), Is.True, "addresses compare case-insensitively");

        watcher.SetWatched(Server, false);
        Assert.That(watcher.IsWatched(Server), Is.False);
    }
}
