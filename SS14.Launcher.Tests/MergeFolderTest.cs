using System.IO;
using NUnit.Framework;
using SS14.Launcher;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(LauncherPaths))]
public sealed class MergeFolderTest
{
    private string _root = null!;
    private string _source = null!;
    private string _target = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "airlock-merge-" + Path.GetRandomFileName());
        _source = Path.Combine(_root, "old");
        _target = Path.Combine(_root, "new");

        Directory.CreateDirectory(Path.Combine(_source, "launcher"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void WriteSource(string name, int size) => Write(Path.Combine(_source, "launcher", name), size);

    private void WriteTarget(string name, int size) => Write(Path.Combine(_target, "launcher", name), size);

    private static void Write(string path, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
    }

    private long TargetSize(string name) => new FileInfo(Path.Combine(_target, "launcher", name)).Length;

    [Test]
    public void TestFilesMoveWhenTargetIsNew()
    {
        WriteSource("content.db", 4096);

        LauncherPaths.MergeFolder(_source, _target);

        Assert.That(TargetSize("content.db"), Is.EqualTo(4096));
        Assert.That(Directory.Exists(_source), Is.False, "an emptied folder is cleaned up");
    }

    [Test]
    public void TestBiggerOldFileWins()
    {
        // The case that bit us: a fresh, empty content.db where gigabytes should be.
        WriteSource("content.db", 4096);
        WriteTarget("content.db", 64);

        LauncherPaths.MergeFolder(_source, _target);

        Assert.That(TargetSize("content.db"), Is.EqualTo(4096), "the downloaded content must not be thrown away");
    }

    [Test]
    public void TestBiggerNewFileIsKept()
    {
        WriteSource("content.db", 64);
        WriteTarget("content.db", 4096);

        LauncherPaths.MergeFolder(_source, _target);

        Assert.That(TargetSize("content.db"), Is.EqualTo(4096), "work done since the move is not undone");
    }

    [Test]
    public void TestNestedFilesArrive()
    {
        Write(Path.Combine(_source, "launcher", "sub", "deep.json"), 10);

        LauncherPaths.MergeFolder(_source, _target);

        Assert.That(File.Exists(Path.Combine(_target, "launcher", "sub", "deep.json")), Is.True);
    }

    [Test]
    public void TestMissingSourceIsHarmless()
    {
        Assert.DoesNotThrow(() => LauncherPaths.MergeFolder(Path.Combine(_root, "nope"), _target));
    }
}
