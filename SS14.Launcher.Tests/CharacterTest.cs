using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Character;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(RsiMeta))]
public sealed class RsiMetaTest
{
    private const string Meta =
        """
        {
          "version": 1,
          "size": { "x": 32, "y": 32 },
          "states": [
            { "name": "full" },
            { "name": "head_m", "directions": 4 },
            { "name": "walking", "directions": 4, "delays": [[0.1,0.1,0.1],[0.1,0.1,0.1],[0.1,0.1,0.1],[0.1,0.1,0.1]] }
          ]
        }
        """;

    [Test]
    public void TestParse()
    {
        var meta = RsiMeta.Parse(Meta);

        Assert.That(meta, Is.Not.Null);
        Assert.That(meta!.Size.X, Is.EqualTo(32));
        Assert.That(meta.States, Has.Count.EqualTo(3));
        Assert.That(meta.HasState("head_m"), Is.True);
        Assert.That(meta.HasState("head_x"), Is.False);
    }

    [Test]
    public void TestStillFrameIndex()
    {
        var meta = RsiMeta.Parse(Meta)!;
        var state = meta.GetState("head_m")!;

        // One frame per direction, in south, north, east, west order.
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.South), Is.EqualTo(0));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.North), Is.EqualTo(1));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.East), Is.EqualTo(2));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.West), Is.EqualTo(3));
    }

    [Test]
    public void TestAnimatedFrameIndex()
    {
        var meta = RsiMeta.Parse(Meta)!;
        var state = meta.GetState("walking")!;

        // Each direction owns three frames, so a direction starts three frames after the last.
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.South), Is.EqualTo(0));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.North), Is.EqualTo(3));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.East), Is.EqualTo(6));
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.West), Is.EqualTo(9));
    }

    [Test]
    public void TestSingleDirectionFallsBackToFirstFrame()
    {
        var meta = RsiMeta.Parse(Meta)!;
        var state = meta.GetState("full")!;

        // A marking drawn the same from every side has one frame; asking for another facing must
        // not run off the end of the sheet.
        Assert.That(RsiMeta.FrameIndex(state, RsiDirection.West), Is.EqualTo(0));
    }
}

[TestFixture]
[TestOf(typeof(MarkingParser))]
public sealed class MarkingParserTest
{
    private const string Yaml =
        """
        - type: marking
          id: HumanHairAfro
          bodyPart: Hair
          sprites:
            - sprite: Mobs/Customization/human_hair.rsi
              state: afro

        - type: marking
          id: VulpSnout
          bodyPart: Snout
          groupWhitelist: [ Vulpkanin ]
          sprites:
            - sprite: Mobs/Customization/Vulpkanin/snout_markings.rsi
              state: snout
          coloring:
            default:
              type:
                !type:SkinColoring

        - type: entity
          id: NotAMarking

        - type: marking
          id: MothWingsFluff
          bodyPart: Tail
          speciesRestriction: [ Moth ]
          followSkinColor: true
          sprites:
            - sprite: Mobs/Customization/Moth/moth_wings.rsi
              state: fluff
            - sprite: Mobs/Customization/Moth/moth_wings.rsi
              state: fluff-overlay
        """;

    [Test]
    public void TestParsesMarkingsOnly()
    {
        var markings = MarkingParser.Parse(Yaml);

        Assert.That(markings.Select(m => m.Id),
            Is.EqualTo(new[] { "HumanHairAfro", "VulpSnout", "MothWingsFluff" }),
            "entities and other prototypes must be ignored");
    }

    [Test]
    public void TestCustomTagsDoNotBreakParsing()
    {
        // The "!type:SkinColoring" tag is what a typed deserializer would trip over.
        var vulp = MarkingParser.Parse(Yaml).Single(m => m.Id == "VulpSnout");

        Assert.That(vulp.BodyPart, Is.EqualTo("Snout"));
        Assert.That(vulp.GroupWhitelist, Is.EqualTo(new[] { "Vulpkanin" }));
    }

    [Test]
    public void TestMultipleSprites()
    {
        var moth = MarkingParser.Parse(Yaml).Single(m => m.Id == "MothWingsFluff");

        Assert.That(moth.Sprites, Has.Count.EqualTo(2));
        Assert.That(moth.Sprites[0].State, Is.EqualTo("fluff"));
        Assert.That(moth.FollowsSkinColor, Is.True);
        Assert.That(moth.SpeciesRestriction, Is.EqualTo(new[] { "Moth" }));
    }

    [Test]
    public void TestSpeciesFiltering()
    {
        var markings = MarkingParser.Parse(Yaml);

        var hair = markings.Single(m => m.Id == "HumanHairAfro");
        var moth = markings.Single(m => m.Id == "MothWingsFluff");

        Assert.That(hair.AllowedFor("Human"), Is.True, "an unrestricted marking suits anyone");
        Assert.That(hair.AllowedFor("Moth"), Is.True);
        Assert.That(moth.AllowedFor("Moth"), Is.True);
        Assert.That(moth.AllowedFor("Human"), Is.False, "restricted markings stay with their species");
    }

    [Test]
    public void TestNamesAreReadable()
    {
        Assert.That(
            MarkingParser.Parse(Yaml).Single(m => m.Id == "HumanHairAfro").DisplayName,
            Is.EqualTo("Human Hair Afro"));
    }

    [Test]
    public void TestGarbageIsSkipped()
    {
        Assert.That(MarkingParser.Parse("this: is: not: valid: yaml:"), Is.Empty);
    }
}

[TestFixture]
[TestOf(typeof(CharacterColor))]
public sealed class CharacterColorTest
{
    [Test]
    public void TestHexRoundTrip()
    {
        Assert.That(CharacterColor.FromHex("#8B4513").ToHex(), Is.EqualTo("#8B4513"));
        Assert.That(CharacterColor.FromHex("8B4513").ToHex(), Is.EqualTo("#8B4513"));
        Assert.That(CharacterColor.FromHex("nonsense"), Is.EqualTo(CharacterColor.White));
    }

    [Test]
    public void TestSkinToneStaysSkinColoured()
    {
        // Across the whole scale the tone should read as skin: red above green above blue.
        for (var tone = 0; tone <= 100; tone += 10)
        {
            var color = CharacterColor.FromSkinTone(tone);

            Assert.That(color.R, Is.GreaterThanOrEqualTo(color.G), $"tone {tone}");
            Assert.That(color.G, Is.GreaterThanOrEqualTo(color.B), $"tone {tone}");
        }
    }

    [Test]
    public void TestSkinToneGetsDarker()
    {
        // Past the midpoint the scale drops brightness, so higher means darker.
        Assert.That(CharacterColor.FromSkinTone(90).R, Is.LessThan(CharacterColor.FromSkinTone(40).R));
    }

    [Test]
    public void TestSkinToneIsClamped()
    {
        Assert.That(CharacterColor.FromSkinTone(-50), Is.EqualTo(CharacterColor.FromSkinTone(0)));
        Assert.That(CharacterColor.FromSkinTone(500), Is.EqualTo(CharacterColor.FromSkinTone(100)));
    }
}

[TestFixture]
[TestOf(typeof(CharacterRenderer))]
public sealed class CharacterCompositeTest
{
    private static byte[] Pixel(byte b, byte g, byte r, byte a) => [b, g, r, a];

    [Test]
    public void TestTintMultiplies()
    {
        var canvas = new byte[4];

        // A white pixel drawn in red comes out red.
        CharacterRenderer.Composite(canvas, Pixel(255, 255, 255, 255), new CharacterColor(255, 0, 0));

        Assert.That(canvas[2], Is.EqualTo(255), "red channel");
        Assert.That(canvas[1], Is.EqualTo(0), "green channel");
        Assert.That(canvas[0], Is.EqualTo(0), "blue channel");
        Assert.That(canvas[3], Is.EqualTo(255), "alpha");
    }

    [Test]
    public void TestTransparentPixelsLeaveTheCanvasAlone()
    {
        var canvas = Pixel(10, 20, 30, 255);

        CharacterRenderer.Composite(canvas, Pixel(255, 255, 255, 0), CharacterColor.White);

        Assert.That(canvas, Is.EqualTo(Pixel(10, 20, 30, 255)));
    }

    [Test]
    public void TestLayersStack()
    {
        var canvas = new byte[4];

        CharacterRenderer.Composite(canvas, Pixel(0, 0, 255, 255), CharacterColor.White);
        CharacterRenderer.Composite(canvas, Pixel(255, 0, 0, 255), CharacterColor.White);

        Assert.That(canvas[0], Is.EqualTo(255), "the later layer wins where it is opaque");
        Assert.That(canvas[2], Is.EqualTo(0));
    }

    [Test]
    public void TestBodyLayerOrder()
    {
        var order = CharacterRenderer.LayerOrder.ToList();

        Assert.That(order.IndexOf("Hair"), Is.GreaterThan(order.IndexOf("Head")));
        Assert.That(order.IndexOf("Eyes"), Is.GreaterThan(order.IndexOf("Head")));

        // The game's own layer list draws tails over the body, not behind it.
        Assert.That(order.IndexOf("Tail"), Is.GreaterThan(order.IndexOf("Chest")));
    }

    [Test]
    public void TestClothingLayerOrder()
    {
        var order = CharacterRenderer.LayerOrder.ToList();

        int Layer(string name) => order.IndexOf(name);

        Assert.That(Layer(CharacterRenderer.Slot("jumpsuit")), Is.GreaterThan(Layer("Chest")),
            "a uniform covers the body");
        Assert.That(Layer(CharacterRenderer.Slot("outerClothing")), Is.GreaterThan(Layer(CharacterRenderer.Slot("jumpsuit"))),
            "a vest goes over the uniform");
        Assert.That(Layer(CharacterRenderer.Slot("shoes")), Is.GreaterThan(Layer("LFoot")),
            "shoes cover feet");
        Assert.That(Layer(CharacterRenderer.Slot("head")), Is.GreaterThan(Layer("Hair")),
            "hats sit on top of hair");
        Assert.That(Layer(CharacterRenderer.Slot("eyes")), Is.LessThan(Layer(CharacterRenderer.Slot("head"))),
            "glasses go under a helmet");
    }
}
