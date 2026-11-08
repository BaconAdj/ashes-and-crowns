using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class AbsenceResolverTests
    {
        private static AbsenceReport Run(double seconds, AffairsChoice choice, ulong seed, float hostility = 0f, bool inBattle = false, int enemies = 0, BodyState body = null, Calendar cal = null)
        {
            var b = new Balance();
            body = body ?? new BodyState(b.wounds);
            cal = cal ?? new Calendar(b.time);
            return AbsenceResolver.Resolve(new AbsenceInput { ElapsedRealSeconds = seconds, Choice = choice, Hostility = hostility, InBattle = inBattle, EnemiesNearby = enemies }, b, body, cal, new DeterministicRng(seed));
        }

        [Test]
        public void ZeroOrNegativeElapsed_ChangesNothing()
        {
            var cal = new Calendar(new Balance().time);
            var r = Run(0, AffairsChoice.SleepAtHome, 1, cal: cal);
            Assert.AreEqual(0f, r.GameHoursElapsed); Assert.IsFalse(r.Died); Assert.AreEqual(0.0, cal.TotalHours);
            r = Run(-5000, AffairsChoice.SleepAtHome, 1, cal: cal); // clock moved backwards
            Assert.AreEqual(0f, r.GameHoursElapsed); Assert.AreEqual(0.0, cal.TotalHours);
            Assert.IsNotEmpty(r.Chronicle);
        }

        [Test]
        public void ElapsedTime_IsConvertedAndCapped()
        {
            var b = new Balance();
            var r = Run(3600, AffairsChoice.SleepAtHome, 1);
            Assert.AreEqual(b.simulation.realHoursToGameHours, r.GameHoursElapsed, 1e-3f);
            Assert.IsFalse(r.Capped);
            var cal = new Calendar(b.time);
            r = Run(3600.0 * 24 * 365, AffairsChoice.SleepAtHome, 1, cal: cal); // away a year
            Assert.IsTrue(r.Capped);
            Assert.AreEqual(b.simulation.maxCatchUpGameDays * 24f, r.GameHoursElapsed, 1e-3f);
            Assert.AreEqual(b.simulation.maxCatchUpGameDays * 24.0, cal.TotalHours, 1e-3);
        }

        [Test]
        public void Deterministic_ForSameSeed()
        {
            var a = Run(3600 * 40, AffairsChoice.KeepTravelling, 77, 0.8f);
            var b = Run(3600 * 40, AffairsChoice.KeepTravelling, 77, 0.8f);
            Assert.AreEqual(a.Died, b.Died); Assert.AreEqual(a.WoundsTaken, b.WoundsTaken); Assert.AreEqual(a.CoinsLost, b.CoinsLost);
            CollectionAssert.AreEqual(a.Chronicle, b.Chronicle);
        }

        [Test]
        public void SleepingAtHome_IsSaferThanTravellingHostileCountry()
        {
            int harmHome = 0, harmRoad = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                if (Run(3600 * 60, AffairsChoice.SleepAtHome, seed, 0f).WoundsTaken > 0) harmHome++;
                if (Run(3600 * 60, AffairsChoice.KeepTravelling, seed, 1f).WoundsTaken > 0) harmRoad++;
            }
            Assert.Greater(harmRoad, harmHome * 3);
            Assert.Greater(harmRoad, 20, "danger must be real");
        }

        [Test]
        public void DeathDuringAbsence_IsPossible_AndReported()
        {
            int deaths = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var wounded = new BodyState(new WoundBalance());
                wounded.ApplyHit(BodyPart.Torso, 0.55f, "earlier"); // goes to bed already badly hurt, in enemy country
                var r = Run(3600 * 120, AffairsChoice.None, seed, 1f, body: wounded);
                if (r.Died) { deaths++; Assert.IsNotNull(r.CauseOfDeath); StringAssert.Contains("did not wake", r.Chronicle[r.Chronicle.Count - 1]); }
            }
            Assert.Greater(deaths, 0);
            Assert.Less(deaths, 200);
        }

        [Test]
        public void QuittingMidBattle_IsNotASafeExit()
        {
            int hurtStanding = 0, hurtFleeing = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                if (Run(60, AffairsChoice.StandInBattle, seed, 0.5f, inBattle: true, enemies: 4).WoundsTaken > 0) hurtStanding++;
                if (Run(60, AffairsChoice.FleeBattle, seed, 0.5f, inBattle: true, enemies: 4).WoundsTaken > 0) hurtFleeing++;
            }
            Assert.AreEqual(100, hurtStanding, "standing still among four enemies always costs blood");
            Assert.Greater(hurtFleeing, 30); Assert.Less(hurtFleeing, 100);
        }

        [Test]
        public void RestingAtHome_HealsAWoundedBody()
        {
            var body = new BodyState(new WoundBalance());
            body.ApplyHit(BodyPart.LeftLeg, 0.3f, "t");
            float before = body.Blood;
            // Use a seed that produces a quiet week.
            AbsenceReport r = null;
            for (ulong seed = 1; seed < 50; seed++) { body = new BodyState(new WoundBalance()); body.ApplyHit(BodyPart.LeftLeg, 0.3f, "t"); r = Run(3600 * 84, AffairsChoice.SleepAtHome, seed, body: body); if (r.WoundsTaken == 0 && r.BloodAfter >= before) break; }
            Assert.Greater(body.Blood, before);
        }
    }
}
