using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Zapret;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(ZapretHosts))]
public sealed class ZapretHostsTest
{
    [Test]
    public void TestGameHostsAreCovered()
    {
        // The ones that actually get throttled here, and the ones the editor needs.
        Assert.That(ZapretHosts.Domains, Does.Contain("playss14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("spacestation14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("raw.githubusercontent.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("station14.ru"));

        // The engine download hosts, measured at a few KB/s on a throttled connection.
        Assert.That(ZapretHosts.Domains, Does.Contain("robust-builds.cdn.spacestation14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("robust-builds.playss14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("cdn.spacestation14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("launcher-data.cdn.spacestation14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("hub.spacestation14.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("ss14.su"));
        Assert.That(ZapretHosts.Domains, Does.Contain("amazonaws.com"));
        Assert.That(ZapretHosts.Domains, Does.Contain("cloudfront.net"));
    }

    [Test]
    public void TestListIsCleanAndSorted()
    {
        Assert.That(ZapretHosts.Domains, Is.Unique);
        Assert.That(ZapretHosts.Domains, Is.Ordered);
        Assert.That(ZapretHosts.Domains, Has.All.Matches<string>(host => host == host.Trim().ToLowerInvariant()));
        Assert.That(ZapretHosts.Domains, Has.None.Contains("/"), "these are host names, not URLs");
    }

    [Test]
    public void TestFileSaysWhoWroteIt()
    {
        var text = ZapretHosts.FileContents();

        Assert.That(text, Does.StartWith("#"), "a comment, so the file is not mistaken for one of theirs");
        Assert.That(text, Does.Contain(ZapretHosts.Header));
        Assert.That(text.TrimEnd().Split('\n').Length, Is.EqualTo(ZapretHosts.Domains.Count + 1));
    }
}

[TestFixture]
[TestOf(typeof(ZapretManager))]
public sealed class ZapretArchiveTest
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "airlock-zapret-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string MakeArchive(params string[] entries)
    {
        var path = Path.Combine(_dir, "archive.zip");

        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        foreach (var entry in entries)
        {
            using var stream = zip.CreateEntry(entry).Open();
            stream.WriteByte(1);
        }

        return path;
    }

    [Test]
    public void TestWrapperFolderIsDropped()
    {
        // Releases are packed inside one versioned folder; unpacking that verbatim would bury
        // winws.exe one level too deep and nothing would find it.
        var archive = MakeArchive("zapret-1.10.3/bin/winws.exe", "zapret-1.10.3/general.bat");
        var target = Path.Combine(_dir, "out");

        ZapretManager.ExtractFlattened(archive, target);

        Assert.That(File.Exists(Path.Combine(target, "bin", "winws.exe")), Is.True);
        Assert.That(File.Exists(Path.Combine(target, "general.bat")), Is.True);
    }

    [Test]
    public void TestFlatArchiveIsLeftAlone()
    {
        var archive = MakeArchive("bin/winws.exe", "general.bat");
        var target = Path.Combine(_dir, "out");

        ZapretManager.ExtractFlattened(archive, target);

        Assert.That(File.Exists(Path.Combine(target, "bin", "winws.exe")), Is.True);
    }

    [Test]
    public void TestEntriesEscapingTheFolderAreSkipped()
    {
        // A zip that tries to write outside the folder it is unpacked into must get nowhere.
        var archive = MakeArchive("../escaped.txt", "general.bat");
        var target = Path.Combine(_dir, "out");

        ZapretManager.ExtractFlattened(archive, target);

        Assert.That(File.Exists(Path.Combine(_dir, "escaped.txt")), Is.False);
        Assert.That(Directory.GetFiles(target, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName), Does.Not.Contain("escaped.txt"));
    }

    [Test]
    public void TestCommonPrefix()
    {
        Assert.That(ZapretManager.CommonPrefix(["a/b", "a/c"]), Is.EqualTo("a/"));
        Assert.That(ZapretManager.CommonPrefix(["a/b", "z/c"]), Is.Empty, "no single root, nothing to strip");
        Assert.That(ZapretManager.CommonPrefix(["loose.txt", "a/b"]), Is.Empty);
        Assert.That(ZapretManager.CommonPrefix([]), Is.Empty);
    }
}
