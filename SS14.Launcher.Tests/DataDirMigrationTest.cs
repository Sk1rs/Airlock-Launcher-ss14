using System.IO;
using NUnit.Framework;
using SS14.Launcher;

namespace SS14.Launcher.Tests;

/// <summary>
/// The rules that decide which folder holds the player's data after the rename.
/// </summary>
/// <remarks>
/// <see cref="LauncherPaths"/> resolves this once per process against the real profile, so these
/// exercise the merge it is built on, with the folder shapes that caused trouble in the wild.
/// </remarks>
[TestFixture]
[TestOf(typeof(LauncherPaths))]
public sealed class DataDirMigrationTest
{
    private string _root = null!;
    private string _old = null!;
    private string _new = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "airlock-datadir-" + Path.GetRandomFileName());
        _old = Path.Combine(_root, "old name");
        _new = Path.Combine(_root, "Airlock Launcher");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void WriteSettings(string dataDir, int size)
    {
        var path = Path.Combine(dataDir, "launcher", "settings.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
    }

    private long NewSettingsSize() => new FileInfo(Path.Combine(_new, "launcher", "settings.db")).Length;

    [Test]
    public void TestSettingsArriveWhenTheNewFolderIsJustLogs()
    {
        // What a crashed or interrupted first run leaves behind: the folder is there, the data is not.
        WriteSettings(_old, 4096);
        Directory.CreateDirectory(Path.Combine(_new, "launcher", "logs"));
        File.WriteAllText(Path.Combine(_new, "launcher", "logs", "launcher.log"), "x");

        LauncherPaths.MergeFolder(_old, _new);

        Assert.That(NewSettingsSize(), Is.EqualTo(4096), "the settings must follow the player to the new folder");
    }

    [Test]
    public void TestExistingSettingsAreNotOverwrittenByAnEmptyOne()
    {
        WriteSettings(_old, 64);
        WriteSettings(_new, 4096);

        LauncherPaths.MergeFolder(_old, _new);

        Assert.That(NewSettingsSize(), Is.EqualTo(4096), "work done since the move must survive");
    }

    [Test]
    public void TestEverythingElseComesAlong()
    {
        WriteSettings(_old, 32);
        var extra = Path.Combine(_old, "data", "CHARACTERS", "someone.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(extra)!);
        File.WriteAllText(extra, "profile:");

        LauncherPaths.MergeFolder(_old, _new);

        Assert.That(File.Exists(Path.Combine(_new, "data", "CHARACTERS", "someone.yml")), Is.True,
            "characters, engines and the rest travel with the settings");
    }
}
