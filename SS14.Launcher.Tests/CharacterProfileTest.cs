using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Character;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(CharacterProfile))]
public sealed class CharacterProfileTest
{
    /// <summary>
    /// Shaped after a file one fork writes: its own appearance fields, marking effects, and a
    /// colour per marking layer.
    /// </summary>
    private const string FeatureRichFile =
        """
        profile:
          preferenceUnavailable: SpawnAsOverflow
          spawnPriority: None
          _antagPreferences: []
          _traitPreferences: []
          _loadouts: {}
          name: Urist McCat
          flavorText: ""
          species: Felinid
          voice: queen_of_pain
          age: 20
          erp: Ask
          sex: Female
          gender: Female
          bodyType: HumanNormal
          appearance:
            height: 0.91170216
            width: 0.8810638
            markings:
            - markingId: CatEars
              visible: True
              markingEffects:
              - !type:ColorMarkingEffect {}
              - !type:ColorMarkingEffect {}
              markingColor:
              - '#FFFCF0FF'
              - '#FFFFFFFF'
            skinColor: '#6E3D19FF'
            eyeColor: '#000000FF'
            facialHairColor: '#FF7B9AFF'
            facialHair: HumanFacialHairFiveoclock
            hairColor: '#FF7B9AFF'
            hair: HumanHairOneshoulder
            hairMarkingEffect: null
          _jobPriorities:
            Passenger: High
        version: 1
        forkId: lust_station_stable
        ...
        """;

    /// <summary>
    /// Shaped after another fork: different key order, hair gradient fields, no markings.
    /// </summary>
    private const string OtherForkFile =
        """
        profile:
          preferenceUnavailable: SpawnAsOverflow
          spawnPriority: None
          appearance:
            markings: []
            skinColor: '#E2B493FF'
            eyeColor: '#9E29A5FF'
            facialHairColor: '#FFFACFFF'
            facialHair: FacialHairShaved
            hairGradientColor: '#FFFACFFF'
            hairGradientEnabled: False
            hairColor: '#4E3E22FF'
            hair: HumanHairClassicFloorlengthBedhead
          gender: Male
          sex: Male
          age: 21
          species: Shark
          flavorText: ""
          name: Urist McShark
          _jobPriorities:
            JobBartender: High
        version: 1
        forkId: dsfobos
        ...
        """;

    [Test]
    public void TestReadsForkSpecificFile()
    {
        var profile = CharacterProfile.Parse(FeatureRichFile);

        Assert.That(profile, Is.Not.Null, "fields this launcher does not know must not stop the read");
        Assert.That(profile!.Name, Is.EqualTo("Urist McCat"));
        Assert.That(profile.Species, Is.EqualTo("Felinid"));
        Assert.That(profile.Age, Is.EqualTo(20));
        Assert.That(profile.Female, Is.True);
        Assert.That(profile.Hair, Is.EqualTo("HumanHairOneshoulder"));
        Assert.That(profile.FacialHair, Is.EqualTo("HumanFacialHairFiveoclock"));
        Assert.That(profile.SkinColor, Is.EqualTo(CharacterColor.FromHex("#6E3D19")));
        Assert.That(profile.JobId, Is.EqualTo("Passenger"));
        Assert.That(profile.ForkId, Is.EqualTo("lust_station_stable"));
    }

    [Test]
    public void TestReadsMarkingColors()
    {
        var marking = CharacterProfile.Parse(FeatureRichFile)!.Markings.Single();

        Assert.That(marking.MarkingId, Is.EqualTo("CatEars"));
        Assert.That(marking.Colors, Has.Count.EqualTo(2), "one colour per sprite layer");
        Assert.That(marking.Colors[0], Is.EqualTo(CharacterColor.FromHex("#FFFCF0")));
    }

    [Test]
    public void TestReadsOtherForkFile()
    {
        var profile = CharacterProfile.Parse(OtherForkFile);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.Name, Is.EqualTo("Urist McShark"));
        Assert.That(profile.Species, Is.EqualTo("Shark"), "a species this launcher never heard of still reads");
        Assert.That(profile.Female, Is.False);
        Assert.That(profile.Markings, Is.Empty);
        Assert.That(profile.JobId, Is.EqualTo("JobBartender"));
        Assert.That(profile.ForkId, Is.EqualTo("dsfobos"));
    }

    [Test]
    public void TestRoundTrip()
    {
        var original = new CharacterProfile(
            "Urist McHands",
            "Human",
            33,
            Female: true,
            CharacterColor.FromHex("#E2B493"),
            CharacterColor.FromHex("#9E29A5"),
            "HumanHairAfro",
            CharacterColor.FromHex("#3A2A1A"),
            "HumanFacialHairShaved",
            CharacterColor.FromHex("#3A2A1A"),
            [new ProfileMarking("CatEars", [CharacterColor.FromHex("#FFFCF0"), CharacterColor.White])],
            "StationEngineer",
            "space_station_14");

        var parsed = CharacterProfile.Parse(original.ToYaml());

        Assert.That(parsed, Is.Not.Null);

        // Compared field by field: the record holds lists, which compare by identity.
        Assert.That(parsed! with { Markings = [] }, Is.EqualTo(original with { Markings = [] }),
            "what we write we must be able to read back");

        Assert.That(parsed.Markings.Select(marking => marking.MarkingId),
            Is.EqualTo(original.Markings.Select(marking => marking.MarkingId)));
        Assert.That(parsed.Markings[0].Colors, Is.EqualTo(original.Markings[0].Colors));
    }

    [Test]
    public void TestWritesNullsForMissingHair()
    {
        var profile = new CharacterProfile(
            "Bald",
            "Human",
            18,
            Female: false,
            CharacterColor.White,
            CharacterColor.White,
            null,
            CharacterColor.White,
            null,
            CharacterColor.White,
            [],
            null,
            "space_station_14");

        var yaml = profile.ToYaml();

        Assert.That(yaml, Does.Contain("hair: null"));
        Assert.That(yaml, Does.Contain("markings: []"));
        Assert.That(yaml, Does.Contain("_jobPriorities: {}"));

        var parsed = CharacterProfile.Parse(yaml);

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.Markings, Is.Empty);
        Assert.That(parsed with { Markings = [] }, Is.EqualTo(profile with { Markings = [] }));
    }

    [Test]
    public void TestWritesTheDocumentTheGameExpects()
    {
        var yaml = CharacterProfile.Parse(OtherForkFile)!.ToYaml();

        Assert.That(yaml, Does.StartWith("profile:"));
        Assert.That(yaml, Does.Contain("version: 1"));
        Assert.That(yaml, Does.Contain("forkId: dsfobos"));
        Assert.That(yaml.TrimEnd(), Does.EndWith("..."), "the game ends these documents explicitly");
    }

    [Test]
    public void TestNamesWithQuotesSurvive()
    {
        var profile = CharacterProfile.Parse(OtherForkFile)! with { Name = "\"Doc\" O'Malley" };

        Assert.That(CharacterProfile.Parse(profile.ToYaml())!.Name, Is.EqualTo("\"Doc\" O'Malley"));
    }

    [Test]
    public void TestRubbishIsRejected()
    {
        Assert.That(CharacterProfile.Parse("hello: world"), Is.Null, "no profile block, no character");
        Assert.That(CharacterProfile.Parse("this: is: not: yaml:"), Is.Null);
    }

    [Test]
    public void TestColorsKeepTheirAlpha()
    {
        // Files carry eight digit colours; the editor works in six and writes the alpha back.
        Assert.That(CharacterColor.FromHex("#6E3D19FF"), Is.EqualTo(CharacterColor.FromHex("#6E3D19")));
        Assert.That(CharacterColor.FromHex("#6E3D19").ToProfileHex(), Is.EqualTo("#6E3D19FF"));
    }
}
