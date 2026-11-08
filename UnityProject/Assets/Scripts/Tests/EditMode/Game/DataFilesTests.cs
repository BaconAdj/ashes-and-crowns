using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Game
{
    /// <summary>The committed JSON must load through Unity's JsonUtility with the shapes the Sim expects.</summary>
    public class DataFilesTests
    {
        [Test]
        public void Balance_Loads_AndValidates()
        {
            var b = DataFiles.LoadBalance();
            Assert.IsEmpty(BalanceValidator.Validate(b));
            Assert.AreEqual(12, b.weather.rainByMonth.Length);
        }

        [Test]
        public void Backgrounds_HaveEveryRank_AndParseIntoCharacters()
        {
            var data = DataFiles.LoadBackgrounds();
            Assert.AreEqual(5, data.ranks.Length);
            foreach (var r in data.ranks) Assert.GreaterOrEqual(r.backgrounds.Length, 3, r.id + " needs 3-4 backgrounds");
            foreach (var r in data.ranks)
                foreach (var bg in r.backgrounds)
                {
                    Assert.AreEqual(bg.attributeKeys.Length, bg.attributeValues.Length, bg.id);
                    Assert.AreEqual(bg.skillKeys.Length, bg.skillValues.Length, bg.id);
                    foreach (string k in bg.skillKeys) Assert.IsTrue(Skills.TryParse(k, out _), bg.id + ": unknown skill " + k);
                    foreach (string k in bg.attributeKeys) Assert.IsTrue(System.Enum.TryParse(k, true, out Attribute _), bg.id + ": unknown attribute " + k);
                    foreach (string f in bg.family) Assert.IsTrue(FamilyGenerator.TryRelation(f, out _), bg.id + ": unknown relation " + f);
                    var sheet = CharacterFactory.Create(new CharacterFactory.Choice { Name = "Test", Rank = (Rank)System.Array.IndexOf(data.ranks, r), BackgroundId = bg.id, Allocation = new[] { 7, 7, 6, 6 } }, data, new BodyState(new WoundBalance()));
                    Assert.AreEqual(bg.coins, sheet.Coins);
                }
        }

        [Test]
        public void Names_Figures_World_Coast_Load()
        {
            var names = DataFiles.LoadNames();
            Assert.Greater(names.normanMale.Length, 10); Assert.Greater(names.houses.Length, 10); Assert.Greater(names.traits.Length, 10);
            var figures = DataFiles.LoadFigures();
            Assert.IsNotNull(figures.Find("stephen")); Assert.IsNotNull(figures.Find("matilda"));
            var world = DataFiles.LoadWorld();
            Assert.IsNotNull(world.Find("ashby_village")); Assert.IsNotNull(world.Find("wallingford")); Assert.AreEqual(8192, world.overworld.terrainSizeMetres);
            var coast = DataFiles.LoadCoast();
            Assert.Greater(coast.mainland.Length, 300); Assert.Greater(coast.uplands.Length, 20);
        }
    }
}
