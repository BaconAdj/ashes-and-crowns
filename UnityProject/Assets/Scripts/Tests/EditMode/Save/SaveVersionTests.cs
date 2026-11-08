using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveVersionTests
    {
        [Test]
        public void Version2_RoundTripsCharacterFamilyAndBaronage()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            var sheet = new CharacterSheet { Name = "Wat of Ashby", Female = false, AgeYears = 24, Rank = Rank.Peasant, BackgroundId = "farmers_son", Trade = "farmer", Coins = 6 };
            sheet.Attributes[0] = 7; sheet.Attributes[3] = 5; sheet.Set(Skill.Farming, 35f); sheet.Set(Skill.Riding, 12.5f);
            s.HasCharacter = true; s.Character = sheet;
            s.Family = new[] { new Npc { Id = 1, Name = "Edric", Age = 51, Relation = Relation.Father, TraitA = "pious", TraitB = "greedy", Disposition = 0.4f } };
            s.Baronage = new[] { new MinorLord { Id = 1, GivenName = "Hugh", House = "de Lacy", Epithet = "the Red", Age = 40, Declared = Allegiance.Stephen, Secret = Allegiance.Matilda, Loyalty = 0.4f, Knights = 12, SeatName = "Ashford", NearLocationId = "oxford", Nx = 0.5f, Nz = 0.3f, FeudWith = 2 } };
            s.SpawnLocationId = "ashby_village"; s.OpeningSeen = true;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(2, b.Version);
            Assert.IsTrue(b.HasCharacter); Assert.AreEqual("Wat of Ashby", b.Character.Name); Assert.AreEqual(7, b.Character.Attributes[0]); Assert.AreEqual(35f, b.Character.Get(Skill.Farming)); Assert.AreEqual(12.5f, b.Character.Get(Skill.Riding));
            Assert.AreEqual(1, b.Family.Length); Assert.AreEqual(Relation.Father, b.Family[0].Relation); Assert.AreEqual("greedy", b.Family[0].TraitB);
            Assert.AreEqual(1, b.Baronage.Length); Assert.AreEqual("Hugh de Lacy the Red", b.Baronage[0].FullName); Assert.IsTrue(b.Baronage[0].DoubleDealer); Assert.AreEqual(2, b.Baronage[0].FeudWith);
            Assert.AreEqual("ashby_village", b.SpawnLocationId); Assert.IsTrue(b.OpeningSeen);
        }

        [Test]
        public void Version1Files_StillLoad_WithoutACharacter()
        {
            var s = RunStateFactory.Sample(); s.Version = 1;
            byte[] bytes = SaveCodec.Encode(s);
            Assert.AreEqual(1, System.BitConverter.ToInt32(bytes, 4), "header carries the requested older version");
            var b = SaveCodec.Decode(bytes);
            Assert.AreEqual(1, b.Version); Assert.IsFalse(b.HasCharacter); Assert.IsNull(b.Character); Assert.IsEmpty(b.Family); Assert.IsEmpty(b.Baronage);
            RunStateFactory.AssertEqual(s, b);
        }
    }
}
