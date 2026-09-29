using NUnit.Framework;
using SS14.Launcher.Models;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(ServerNotes))]
public sealed class ServerNotesTest
{
    private const string Server = "ss14://example.org";

    private sealed class MemoryStore : IServerNoteStore
    {
        public string NotesJson { get; set; } = "{}";
    }

    private static ServerNotes Make() => new(new MemoryStore());

    [Test]
    public void TestRoundTrip()
    {
        var notes = Make();

        Assert.That(notes.Get(Server), Is.Empty, "a server nobody wrote about has no note");

        notes.Set(Server, "good chemistry, strict admins");

        Assert.That(notes.Get(Server), Is.EqualTo("good chemistry, strict admins"));
    }

    [Test]
    public void TestAddressCaseDoesNotMatter()
    {
        var notes = Make();
        notes.Set(Server, "mine");

        Assert.That(notes.Get("SS14://EXAMPLE.ORG"), Is.EqualTo("mine"));
    }

    [Test]
    public void TestBlankNoteRemovesIt()
    {
        var notes = Make();
        notes.Set(Server, "temporary");
        notes.Set(Server, "   ");

        Assert.That(notes.Get(Server), Is.Empty);
    }

    [Test]
    public void TestLongNotesAreCut()
    {
        var notes = Make();
        notes.Set(Server, new string('x', ServerNotes.MaxLength + 50));

        Assert.That(notes.Get(Server), Has.Length.EqualTo(ServerNotes.MaxLength),
            "a note must not be able to wreck the list layout");
    }

    [Test]
    public void TestNotesAreIndependent()
    {
        var notes = Make();
        notes.Set(Server, "first");
        notes.Set("ss14://other.example", "second");

        Assert.That(notes.Get(Server), Is.EqualTo("first"));
        Assert.That(notes.Get("ss14://other.example"), Is.EqualTo("second"));
    }
}
