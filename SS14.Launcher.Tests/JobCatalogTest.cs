using System.Linq;
using NUnit.Framework;
using SS14.Launcher.Models.Character;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(EntityPrototypeIndex))]
public sealed class EntityPrototypeIndexTest
{
    private const string Clothing =
        """
        - type: entity
          abstract: true
          id: ClothingShoesBase
          components:
          - type: Sprite
            sprite: Clothing/Shoes/Boots/base.rsi

        - type: entity
          parent: ClothingShoesBase
          id: ClothingShoesColorBlack
          name: black shoes

        - type: entity
          parent: [ClothingUniformBase, BaseCommandContraband]
          id: ClothingUniformJumpsuitEngineering
          components:
          - type: Sprite
            sprite: Clothing/Uniforms/Jumpsuits/Engineering/engineering.rsi

        - type: entity
          parent: ClothingShoesBase
          id: ClothingShoesBootsCombat
          components:
          - type: Sprite
            sprite: Clothing/Shoes/Boots/icon.rsi
          - type: Clothing
            sprite: Clothing/Shoes/Boots/combat.rsi
        """;

    private static EntityPrototypeIndex Index()
    {
        var index = new EntityPrototypeIndex();
        index.AddFile(Clothing);

        return index;
    }

    [Test]
    public void TestDirectSprite()
    {
        Assert.That(Index().ResolveSprite("ClothingUniformJumpsuitEngineering"),
            Is.EqualTo("Clothing/Uniforms/Jumpsuits/Engineering/engineering.rsi"));
    }

    [Test]
    public void TestInheritedSprite()
    {
        // Plain coloured shoes carry no sprite of their own.
        Assert.That(Index().ResolveSprite("ClothingShoesColorBlack"),
            Is.EqualTo("Clothing/Shoes/Boots/base.rsi"));
    }

    [Test]
    public void TestWornSpriteWins()
    {
        // The Clothing component's sprite is the one used while worn, which is what we draw.
        Assert.That(Index().ResolveSprite("ClothingShoesBootsCombat"),
            Is.EqualTo("Clothing/Shoes/Boots/combat.rsi"));
    }

    [Test]
    public void TestFilledVariantFallsBackToTheBaseItem()
    {
        // "Filled" variants are the same garment with contents and live elsewhere in the tree.
        Assert.That(Index().ResolveSprite("ClothingShoesBootsCombatFilled"),
            Is.EqualTo("Clothing/Shoes/Boots/combat.rsi"));
    }

    [Test]
    public void TestUnknownItem()
    {
        Assert.That(Index().ResolveSprite("NoSuchThing"), Is.Null);
    }

    [Test]
    public void TestMultipleParentsAreRead()
    {
        var entity = EntityPrototypeIndex.ParseEntities(Clothing)
            .Single(e => e.Id == "ClothingUniformJumpsuitEngineering");

        Assert.That(entity.Parents, Is.EqualTo(new[] { "ClothingUniformBase", "BaseCommandContraband" }));
    }
}

[TestFixture]
[TestOf(typeof(JobCatalog))]
public sealed class JobCatalogTest
{
    private const string JobFile =
        """
        - type: job
          id: StationEngineer
          name: job-name-engineer
          startingGear: StationEngineerGear

        - type: startingGear
          id: StationEngineerGear
          equipment:
            eyes: ClothingEyesGlassesMeson
            ears: ClothingHeadsetEngineering
        """;

    private const string LoadoutFile =
        """
        - type: loadout
          id: StationEngineerJumpsuit
          equipment:
            jumpsuit: ClothingUniformJumpsuitEngineering

        - type: loadout
          id: StationEngineerJumpskirt
          equipment:
            jumpsuit: ClothingUniformJumpskirtEngineering

        - type: loadout
          id: StationEngineerHardhatYellow
          equipment:
            head: ClothingHeadHatHardhatYellow

        - type: loadout
          id: StationEngineerGlassesOverride
          equipment:
            eyes: ClothingEyesGlassesCheapSunglasses
        """;

    private static JobCatalog Catalog()
    {
        var catalog = new JobCatalog();
        catalog.AddFile("Resources/Prototypes/Roles/Jobs/Engineering/station_engineer.yml", JobFile);
        catalog.AddFile("Resources/Prototypes/Loadouts/Jobs/Engineering/station_engineer.yml", LoadoutFile);
        catalog.SortJobs();

        return catalog;
    }

    [Test]
    public void TestJobIsFound()
    {
        var job = Catalog().Jobs.Single();

        Assert.That(job.Id, Is.EqualTo("StationEngineer"));
        Assert.That(job.StartingGear, Is.EqualTo("StationEngineerGear"));
        Assert.That(job.DisplayName, Is.EqualTo("Station Engineer"));
    }

    [Test]
    public void TestOutfitTakesStartingGear()
    {
        var catalog = Catalog();
        var outfit = catalog.OutfitFor(catalog.Jobs.Single());

        Assert.That(outfit["ears"], Is.EqualTo("ClothingHeadsetEngineering"));
    }

    [Test]
    public void TestOutfitFillsGapsFromLoadouts()
    {
        var catalog = Catalog();
        var outfit = catalog.OutfitFor(catalog.Jobs.Single());

        Assert.That(outfit["jumpsuit"], Is.EqualTo("ClothingUniformJumpsuitEngineering"),
            "the first alternative offered is the default look");
        Assert.That(outfit["head"], Is.EqualTo("ClothingHeadHatHardhatYellow"));
    }

    [Test]
    public void TestStartingGearWinsOverLoadouts()
    {
        var catalog = Catalog();
        var outfit = catalog.OutfitFor(catalog.Jobs.Single());

        Assert.That(outfit["eyes"], Is.EqualTo("ClothingEyesGlassesMeson"),
            "gear the job hands out is not up for negotiation");
    }

    [Test]
    public void TestLoadoutsOfOtherJobsAreIgnored()
    {
        var catalog = new JobCatalog();
        catalog.AddFile("Resources/Prototypes/Roles/Jobs/Engineering/station_engineer.yml", JobFile);
        catalog.AddFile("Resources/Prototypes/Loadouts/Jobs/Civilian/clown.yml", LoadoutFile);

        var outfit = catalog.OutfitFor(catalog.Jobs.Single());

        Assert.That(outfit.ContainsKey("jumpsuit"), Is.False, "jobs are matched to loadouts by file name");
    }
}
