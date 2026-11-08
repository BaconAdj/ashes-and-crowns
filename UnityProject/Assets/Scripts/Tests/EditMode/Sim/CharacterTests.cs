using System.IO;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public static class TestData
    {
        public static string Root()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")) && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) return null;
            string unity = Path.Combine(dir, "UnityProject", "Assets", "Data");
            return Directory.Exists(unity) ? unity : Path.Combine(dir, "Assets", "Data");
        }

        public static BackgroundsData Backgrounds()
        {
            // A compact in-memory copy so the tests need no JSON library; mirrors backgrounds.json's shape.
            return new BackgroundsData
            {
                attributePoints = 10, attributeMin = 3, attributeMax = 9, attributeBase = 4,
                ranks = new[]
                {
                    new RankDef { id = "peasant", name = "Peasant", spawn = "village", backgrounds = new[]
                    {
                        new BackgroundDef { id = "farmers_son", name = "Farmer's son", trade = "farmer", coins = 6, attributeKeys = new[] { "strength", "endurance" }, attributeValues = new[] { 1, 1 }, skillKeys = new[] { "farming", "polearm" }, skillValues = new[] { 35f, 10f }, family = new[] { "father", "mother", "sibling", "sibling" } },
                        new BackgroundDef { id = "poacher", name = "Poacher", trade = "hunter", coins = 4, skillKeys = new[] { "hunting", "bow" }, skillValues = new[] { 40f, 30f }, family = new[] { "wife", "child" }, wounds = new[] { new BackgroundWound { part = "LeftLeg", severity = 0.3f } } }
                    } },
                    new RankDef { id = "warrior", name = "Warrior", spawn = "village", backgrounds = new[] { new BackgroundDef { id = "flemish_mercenary", name = "Flemish mercenary", coins = 20, family = new string[0] } } },
                    new RankDef { id = "knight", name = "Knight", spawn = "castle", backgrounds = new[] { new BackgroundDef { id = "younger_son", name = "Younger son", coins = 30, family = new[] { "elderBrother", "mother", "sister" } } } },
                    new RankDef { id = "lord", name = "Lord", spawn = "castle", backgrounds = new[] { new BackgroundDef { id = "new_man", name = "New man", coins = 120, family = new[] { "wife", "heir", "child", "steward" } } } },
                    new RankDef { id = "monarch", name = "King or Queen", spawn = "capital", backgrounds = new[] { new BackgroundDef { id = "matilda", name = "Empress Matilda", fixedFigure = "matilda", coins = 2500, family = new[] { "heir", "child", "halfBrother" } } } },
                }
            };
        }

        public static NamesData Names() => new NamesData
        {
            normanMale = new[] { "William", "Robert", "Hugh", "Geoffrey", "Ralph", "Walter" }, normanFemale = new[] { "Matilda", "Adeliza", "Mabel", "Agnes" },
            englishMale = new[] { "Wat", "Edric", "Godwin", "Ulf" }, englishFemale = new[] { "Edith", "Aldith", "Hild" },
            houses = new[] { "de Lacy", "de Vere", "Peverel", "de Clare", "Basset", "Malet", "de Say" }, placeRoots = new[] { "Ash", "Thorn", "Stan" }, placeSuffixes = new[] { "ford", "ton", "bury" },
            epithets = new[] { "the Red", "the Lame" }, traits = new[] { "brave", "greedy", "loyal", "treacherous", "pious", "cruel" }
        };
    }

    public class CharacterTests
    {
        [Test]
        public void Validate_RejectsBadAllocations_AndAcceptsTheBudget()
        {
            var data = TestData.Backgrounds();
            var c = new CharacterFactory.Choice { Name = "Wat", Rank = Rank.Peasant, BackgroundId = "farmers_son", Allocation = new[] { 4, 4, 4, 4 } };
            StringAssert.Contains("total must be", CharacterFactory.Validate(c, data));
            c.Allocation = new[] { 9, 7, 5, 5 };   // 26 = 16 + 10
            Assert.IsNull(CharacterFactory.Validate(c, data));
            c.Allocation = new[] { 10, 6, 5, 5 };
            StringAssert.Contains("3 to 9", CharacterFactory.Validate(c, data));
            c.Allocation = new[] { 9, 7, 5, 5 }; c.Name = "";
            StringAssert.Contains("name", CharacterFactory.Validate(c, data));
        }

        [Test]
        public void Create_AppliesBackgroundBonuses_SkillsAndOldWounds()
        {
            var data = TestData.Backgrounds();
            var body = new BodyState(new WoundBalance());
            var sheet = CharacterFactory.Create(new CharacterFactory.Choice { Name = " Wat ", Rank = Rank.Peasant, BackgroundId = "farmers_son", Allocation = new[] { 6, 6, 7, 7 } }, data, body);
            Assert.AreEqual("Wat", sheet.Name);
            Assert.AreEqual(7, sheet.Get(Attribute.Strength)); Assert.AreEqual(7, sheet.Get(Attribute.Endurance)); Assert.AreEqual(7, sheet.Get(Attribute.Charisma));
            Assert.AreEqual(35f, sheet.Get(Skill.Farming)); Assert.AreEqual("farmer", sheet.Trade); Assert.AreEqual(6, sheet.Coins);
            var poacher = CharacterFactory.Create(new CharacterFactory.Choice { Name = "Ulf", Rank = Rank.Peasant, BackgroundId = "poacher", Allocation = new[] { 6, 6, 7, 7 } }, data, body);
            Assert.Less(body.PartIntegrity(BodyPart.LeftLeg), 1f, "the poacher's old leg wound is applied");
            Assert.AreEqual(40f, poacher.Get(Skill.Hunting));
        }

        [Test]
        public void SkillUse_GrowsFasterWithTheGoverningAttribute_AndDiminishes()
        {
            var b = new CharacterBalance();
            var strong = new CharacterSheet(); strong.Attributes[(int)Attribute.Strength] = 8;
            var weak = new CharacterSheet(); weak.Attributes[(int)Attribute.Strength] = 3;
            float gs = strong.Use(Skill.OneHanded, b), gw = weak.Use(Skill.OneHanded, b);
            Assert.Greater(gs, gw);
            var sheet = new CharacterSheet(); sheet.Attributes[(int)Attribute.Strength] = 5;
            float early = sheet.Use(Skill.OneHanded, b);
            for (int i = 0; i < 400; i++) sheet.Use(Skill.OneHanded, b);
            float late = sheet.Use(Skill.OneHanded, b);
            Assert.Less(late, early);
            Assert.LessOrEqual(sheet.Get(Skill.OneHanded), b.skillCap);
        }

        [Test]
        public void Aging_ErodesAttributes_OnlyPastOldAge()
        {
            var b = new CharacterBalance();
            var sheet = new CharacterSheet { AgeYears = 30 };
            for (int i = 0; i < 4; i++) sheet.Attributes[i] = 6;
            var rng = new DeterministicRng(1);
            for (int y = 0; y < 15; y++) sheet.AgeOneYear(b, rng);
            Assert.AreEqual(24, sheet.Attributes[0] + sheet.Attributes[1] + sheet.Attributes[2] + sheet.Attributes[3]);
            for (int y = 0; y < 30; y++) sheet.AgeOneYear(b, rng);
            Assert.Less(sheet.Attributes[0] + sheet.Attributes[1] + sheet.Attributes[2] + sheet.Attributes[3], 24);
        }
    }

    public class GeneratorTests
    {
        [Test]
        public void Names_AreDeterministic_AndRankAppropriate()
        {
            var a = new NameGenerator(TestData.Names(), new DeterministicRng(5));
            var b = new NameGenerator(TestData.Names(), new DeterministicRng(5));
            for (int i = 0; i < 20; i++) Assert.AreEqual(a.Given(i % 2 == 0, i % 3 == 0), b.Given(i % 2 == 0, i % 3 == 0));
            CollectionAssert.Contains(TestData.Names().englishMale, a.Given(false, false));
            CollectionAssert.Contains(TestData.Names().normanFemale, a.Given(true, true));
            StringAssert.IsMatch("^(Ash|Thorn|Stan)(ford|ton|bury)$", a.PlaceName());
        }

        [Test]
        public void Family_FollowsTheTemplate_WithSaneAges()
        {
            var data = TestData.Backgrounds();
            var player = new CharacterSheet { Name = "Wat", AgeYears = 24, Rank = Rank.Peasant };
            var fam = FamilyGenerator.Generate(data.Find("farmers_son").family, player, new NameGenerator(TestData.Names(), new DeterministicRng(2)), new DeterministicRng(3));
            Assert.AreEqual(4, fam.Count);
            Assert.AreEqual(Relation.Father, fam[0].Relation); Assert.Greater(fam[0].Age, player.AgeYears + 15); Assert.IsFalse(fam[0].Female);
            Assert.AreEqual(Relation.Mother, fam[1].Relation); Assert.IsTrue(fam[1].Female);
            foreach (var n in fam) { Assert.IsNotEmpty(n.Name); Assert.IsNotEmpty(n.TraitA); Assert.AreNotEqual(n.TraitA, n.TraitB); Assert.IsTrue(n.Alive); }
            var lord = new CharacterSheet { Name = "Hugh", AgeYears = 40, Rank = Rank.Lord };
            var lf = FamilyGenerator.Generate(data.Find("new_man").family, lord, new NameGenerator(TestData.Names(), new DeterministicRng(2)), new DeterministicRng(3));
            Assert.AreEqual(Relation.Heir, lf[1].Relation); Assert.Less(lf[1].Age, lord.AgeYears - 14); Assert.AreEqual(Relation.Steward, lf[3].Relation);
        }

        [Test]
        public void Baronage_IsDeterministic_UniqueNames_RegionalAllegiance_SymmetricFeuds()
        {
            var b = new BaronageBalance { minorLordCount = 40 };
            var anchors = new[] { new BaronageGenerator.Anchor { LocationId = "bristol", Nx = 0.3f, Nz = 0.3f }, new BaronageGenerator.Anchor { LocationId = "london", Nx = 0.7f, Nz = 0.27f } };
            var lords = BaronageGenerator.Generate(b, anchors, 673f, new NameGenerator(TestData.Names(), new DeterministicRng(8)), new DeterministicRng(9));
            var again = BaronageGenerator.Generate(b, anchors, 673f, new NameGenerator(TestData.Names(), new DeterministicRng(8)), new DeterministicRng(9));
            Assert.AreEqual(40, lords.Count);
            for (int i = 0; i < lords.Count; i++) { Assert.AreEqual(lords[i].FullName, again[i].FullName); Assert.AreEqual(lords[i].Declared, again[i].Declared); }
            var names = new System.Collections.Generic.HashSet<string>(); foreach (var l in lords) names.Add(l.FullName);
            Assert.GreaterOrEqual(names.Count, 38, "names should be nearly all unique with a small pool");
            foreach (var l in lords)
                if (l.FeudWith > 0) { var other = lords.Find(x => x.Id == l.FeudWith); Assert.IsNotNull(other); Assert.AreEqual(l.Id, other.FeudWith, "feuds are mutual"); }
            // Regional bias is statistical: check it on a large sample.
            var many = BaronageGenerator.Generate(new BaronageBalance { minorLordCount = 400 }, anchors, 673f, new NameGenerator(TestData.Names(), new DeterministicRng(8)), new DeterministicRng(10));
            int westMatilda = 0, west = 0, eastStephen = 0, east = 0, doubleDealers = 0;
            foreach (var l in many)
            {
                if (l.DoubleDealer) doubleDealers++;
                if (l.Declared == Allegiance.Neutral) continue;
                if (l.Nx < b.divideNx) { west++; if (l.Declared == Allegiance.Matilda) westMatilda++; } else { east++; if (l.Declared == Allegiance.Stephen) eastStephen++; }
            }
            Assert.Greater(westMatilda / (float)west, 0.55f); Assert.Greater(eastStephen / (float)east, 0.55f);
            lords = many;
            Assert.Greater(doubleDealers, 0); Assert.Less(doubleDealers, lords.Count / 2);
        }

        [Test]
        public void Spawn_FollowsRank_AndNarrationNamesThePlayer()
        {
            var rng = new DeterministicRng(1);
            Assert.AreEqual("ashby_village", RunSetup.ChooseSpawn(new CharacterSheet { Rank = Rank.Peasant }, new[] { "ashby_village" }, new[] { "wallingford" }, rng));
            Assert.AreEqual("wallingford", RunSetup.ChooseSpawn(new CharacterSheet { Rank = Rank.Knight }, new[] { "ashby_village" }, new[] { "wallingford" }, rng));
            Assert.AreEqual("gloucester", RunSetup.ChooseSpawn(new CharacterSheet { Rank = Rank.Monarch, FixedFigureId = "matilda" }, null, null, rng));
            var lines = RunSetup.OpeningNarration(new CharacterSheet { Name = "Wat", Rank = Rank.Peasant, Trade = "farmer" }, new Calendar(new TimeBalance()));
            Assert.AreEqual(4, lines.Length); StringAssert.Contains("Wat", lines[2]); StringAssert.Contains("1135", lines[3]);
        }
    }
}
