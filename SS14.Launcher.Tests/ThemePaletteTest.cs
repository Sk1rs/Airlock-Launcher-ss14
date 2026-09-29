using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SS14.Launcher;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(ThemeManager))]
public sealed class ThemePaletteTest
{
    /// <summary>
    /// The palettes as they sit in the repository, found by walking up from the test binary.
    /// </summary>
    private static string PaletteDir
    {
        get
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "SS14.Launcher", "Theme", "Palettes")))
            {
                dir = dir.Parent;
            }

            Assert.That(dir, Is.Not.Null, "could not find the repository root");

            return Path.Combine(dir!.FullName, "SS14.Launcher", "Theme", "Palettes");
        }
    }

    private static IEnumerable<string> ThemeIds => ThemeManager.Themes;

    private static HashSet<string> KeysOf(string themeId)
    {
        var file = Path.Combine(PaletteDir, $"{themeId}.xaml");

        Assert.That(File.Exists(file), $"{themeId} has no palette file");

        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        return XDocument.Load(file)
            .Descendants()
            .Select(element => element.Attribute(x + "Key")?.Value)
            .Where(key => key != null)
            .ToHashSet()!;
    }

    [Test]
    public void TestEveryThemeHasAPalette()
    {
        Assert.That(ThemeIds.Count(), Is.GreaterThan(1));

        foreach (var theme in ThemeIds)
        {
            Assert.That(File.Exists(Path.Combine(PaletteDir, $"{theme}.xaml")), $"{theme} is registered without a file");
        }
    }

    [Test]
    public void TestPalettesDefineTheSameKeys()
    {
        // The UI looks these up by name; a palette missing one falls back to nothing and the
        // control turns invisible, which is the kind of thing nobody notices until a user reports it.
        var expected = KeysOf(ThemeManager.DefaultThemeId);

        foreach (var theme in ThemeIds.Where(t => t != ThemeManager.DefaultThemeId))
        {
            Assert.That(KeysOf(theme), Is.EquivalentTo(expected), $"{theme} does not match the default palette");
        }
    }

    [Test]
    public void TestNoStrayPalettes()
    {
        var files = Directory.GetFiles(PaletteDir, "*.xaml")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.That(files, Is.EquivalentTo(ThemeIds), "a palette file exists that no theme offers, or vice versa");
    }
}
