using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SS14.Launcher;

namespace SS14.Launcher.Tests;

/// <summary>
/// The background picture's settings, and the palettes it has to stay readable over.
/// </summary>
/// <remarks>
/// <see cref="SS14.Launcher.Models.LauncherBackground"/> itself needs the config database and a
/// UI thread to load bitmaps, so what is worth pinning here is the arithmetic the sliders feed
/// and the palette shape every theme must keep.
/// </remarks>
[TestFixture]
public sealed class LauncherBackgroundTest
{
    private static string PaletteDir
    {
        get
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "SS14.Launcher", "Theme", "Palettes")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir!.FullName, "SS14.Launcher", "Theme", "Palettes");
        }
    }

    [Test]
    public void TestEveryPaletteHasAReadableForeground()
    {
        // A background picture sits behind the text, so any palette whose text is too close to its
        // own background would be unreadable before a picture is even involved.
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        foreach (var file in Directory.GetFiles(PaletteDir, "*.xaml"))
        {
            var colors = XDocument.Load(file)
                .Descendants()
                .Where(e => e.Name.LocalName == "Color")
                .ToDictionary(e => e.Attribute(x + "Key")!.Value, e => e.Value);

            var background = Luminance(colors["ThemeBackgroundColor"]);
            var foreground = Luminance(colors["ThemeForegroundColor"]);

            Assert.That(System.Math.Abs(foreground - background), Is.GreaterThan(100),
                $"{Path.GetFileNameWithoutExtension(file)}: text and background are too close");
        }
    }

    /// <summary>
    /// Rough perceived brightness of a #RRGGBB value, 0 to 255.
    /// </summary>
    private static double Luminance(string hex)
    {
        var value = int.Parse(hex.TrimStart('#')[..6], System.Globalization.NumberStyles.HexNumber);

        var r = (value >> 16) & 0xFF;
        var g = (value >> 8) & 0xFF;
        var b = value & 0xFF;

        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    [Test]
    public void TestStoredBackgroundLivesWithTheLauncherData()
    {
        // Kept next to the other generated files, not in the install folder, so it survives an update.
        Assert.That(SS14.Launcher.Models.LauncherBackground.StoredPath,
            Does.StartWith(LauncherPaths.DirLocalData));
    }
}
