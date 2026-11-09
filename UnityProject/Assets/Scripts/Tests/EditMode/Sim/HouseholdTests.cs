using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class HouseholdTests
    {
        private static List<Settlement> World() => new List<Settlement>
        {
            new Settlement { Id = "seat", Name = "Seat", Type = SettlementType.Castle, Allegiance = Allegiance.Stephen },
            new Settlement { Id = "ox", Name = "Oxford", Type = SettlementType.Town, Allegiance = Allegiance.Matilda },
            new Settlement { Id = "v", Name = "Ashby", Type = SettlementType.Village, Allegiance = Allegiance.Stephen },
        };

        [Test]
        public void Assign_MessagesGoThroughHostileCountry()
        {
            var b = new HouseholdBalance { dutyChancePerDay = 1f };
            var lord = new MinorLord { Id = 1, Declared = Allegiance.Stephen }; var world = World();
            int messages = 0, kinds = 0; var seen = new HashSet<DutyKind>();
            for (int i = 0; i < 300; i++)
            {
                var d = HouseholdRules.Assign(lord, world[0], world, 100, null, new DeterministicRng((ulong)i + 1), b);
                Assert.IsNotNull(d); Assert.IsNotEmpty(d.Text); seen.Add(d.Kind);
                if (d.Kind == DutyKind.Message) { messages++; Assert.AreEqual("ox", d.ToId); Assert.AreEqual(100 + b.messageDays, d.DueDay); }
                if (d.Kind == DutyKind.Escort) Assert.AreNotEqual("seat", d.ToId);
            }
            kinds = seen.Count;
            Assert.Greater(messages, 20); Assert.AreEqual(5, kinds);
            Assert.IsNull(HouseholdRules.Assign(lord, world[0], world, 100, null, new DeterministicRng(1), new HouseholdBalance { dutyChancePerDay = 0f }));
        }

        [Test]
        public void Complete_Fail_Refuse_MoveFavour()
        {
            var b = new HouseholdBalance(); var s = new ServiceRecord { PatronFavour = 0.5f }; var c = new CharacterSheet { Coins = 0 };
            var d = new Duty { Kind = DutyKind.Message, ToName = "Oxford", Pay = 8, Dangerous = true, DueDay = 10 };
            Assert.IsFalse(HouseholdRules.Overdue(d, 10)); Assert.IsTrue(HouseholdRules.Overdue(d, 11));
            string t = HouseholdRules.Complete(d, s, c, b);
            Assert.AreEqual(DutyState.Done, d.State); Assert.AreEqual(8, c.Coins); Assert.AreEqual(0.56f, s.PatronFavour, 1e-4f); Assert.AreEqual(b.renownPerDangerousDuty, s.Renown); Assert.IsTrue(t.Contains("Oxford"));
            var f = new Duty { Kind = DutyKind.Escort }; HouseholdRules.Fail(f, s, b); Assert.AreEqual(DutyState.Failed, f.State); Assert.Less(s.PatronFavour, 0.56f);
            float before = s.PatronFavour; HouseholdRules.Refuse(new Duty { Kind = DutyKind.Guard }, s, b); Assert.Less(s.PatronFavour, before); Assert.AreEqual(b.infractionsOnRefusal, s.Infractions);
        }

        [Test]
        public void Feast_CharismaTurnsTheInsult()
        {
            var b = new HouseholdBalance { feastInsultChance = 1f, feastCharismaWeight = 0.05f };
            var witty = new CharacterSheet(); witty.Attributes[(int)Attribute.Charisma] = 18;
            var dull = new CharacterSheet(); dull.Attributes[(int)Attribute.Charisma] = 2;
            int wittyWins = 0, dullWins = 0;
            for (int i = 0; i < 200; i++)
            {
                var s1 = new ServiceRecord { PatronFavour = 0.5f }; var s2 = new ServiceRecord { PatronFavour = 0.5f };
                HouseholdRules.Feast(witty, s1, new DeterministicRng((ulong)i + 1), b, out bool i1); HouseholdRules.Feast(dull, s2, new DeterministicRng((ulong)i + 1), b, out bool i2);
                Assert.IsTrue(i1 && i2);
                if (s1.PatronFavour > 0.5f) wittyWins++; if (s2.PatronFavour > 0.5f) dullWins++;
            }
            Assert.Greater(wittyWins, 150); Assert.Less(dullWins, 50);
        }
    }
}
