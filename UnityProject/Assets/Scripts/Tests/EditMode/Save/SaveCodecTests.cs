using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public static class RunStateFactory
    {
        public static RunState Sample(bool withBattle = true, int soldiers = 100)
        {
            var body = new BodyState(new WoundBalance());
            body.ApplyHit(BodyPart.LeftArm, 0.3f, "test"); body.Tick(3f);
            var s = new RunState
            {
                Seed = 1135, RngState = 0xDEADBEEFCAFEBABEUL, SceneId = "Phase1_England", SavedAtUtcTicks = 638000000000000000L,
                Affairs = AffairsChoice.SleepAtHome, Hostility = 0.25f, EnemiesNearby = 2, InBattle = withBattle,
                CalendarTotalHours = 37.25, Weather = new WeatherSnapshot { Kind = 3, UntilChange = 1.5f, Precipitation = 0.8f, Fog = 0.014f, Wind = 0.7f, Cloud = 1f },
                WeatherRngState = 42, PlayerName = "Wat of Ashby", PlayerX = 5052.5f, PlayerY = 36.2f, PlayerZ = 2444.1f, PlayerYaw = 123f, Mounted = true,
                HorseX = 5050f, HorseY = 36f, HorseZ = 2440f, HorseYaw = 90f, Body = body.Export(), Stamina = 61.5f,
                HoursSurvived = 37.25, Kills = 3, MenLost = 12, Battles = 1, PeakWealth = 7
            };
            if (withBattle)
            {
                var b = new BattleSnapshot { SoldierCount = soldiers, UnitCount = 4, PlayerSoldierEnd = soldiers / 2, Elapsed = 12.5f, EnemyThinkTimer = 1f, Started = true, PlayerCharacterAlive = true };
                b.Positions = new float[soldiers * 3]; b.Yaws = new float[soldiers]; b.Health = new float[soldiers]; b.Cooldowns = new float[soldiers];
                b.Alive = new byte[soldiers]; b.Side = new byte[soldiers]; b.UnitOf = new int[soldiers]; b.SlotOf = new int[soldiers]; b.Targets = new int[soldiers];
                for (int i = 0; i < soldiers; i++)
                {
                    b.Positions[i * 3] = i; b.Positions[i * 3 + 1] = 1f; b.Positions[i * 3 + 2] = -i; b.Yaws[i] = i * 0.1f; b.Health[i] = 30f - i % 7; b.Cooldowns[i] = i % 3 * 0.5f;
                    b.Alive[i] = (byte)(i % 5 == 0 ? 0 : 1); b.Side[i] = (byte)(i < soldiers / 2 ? 0 : 1); b.UnitOf[i] = i / 25; b.SlotOf[i] = i % 25; b.Targets[i] = i % 4 == 0 ? -1 : (i + 50) % soldiers;
                }
                b.UnitSide = new byte[] { 0, 0, 1, 1 }; b.UnitFormation = new byte[] { 0, 1, 2, 3 }; b.UnitOrder = new byte[] { 0, 1, 2, 0 }; b.UnitAlive = new byte[] { 1, 1, 1, 0 };
                b.UnitStart = new[] { 0, 25, 50, 75 }; b.UnitCount_ = new[] { 25, 25, 25, 25 }; b.UnitAliveCount = new[] { 20, 25, 19, 0 };
                b.UnitAnchor = new float[12]; b.UnitYaw = new float[] { 0f, 0f, 3.14f, 3.14f }; b.UnitTarget = new float[12];
                for (int i = 0; i < 12; i++) { b.UnitAnchor[i] = i * 10f; b.UnitTarget[i] = i * 11f; }
                s.Battle = b;
            }
            return s;
        }

        public static void AssertEqual(RunState a, RunState b)
        {
            Assert.AreEqual(a.Seed, b.Seed); Assert.AreEqual(a.RngState, b.RngState); Assert.AreEqual(a.SceneId, b.SceneId); Assert.AreEqual(a.SavedAtUtcTicks, b.SavedAtUtcTicks);
            Assert.AreEqual(a.Affairs, b.Affairs); Assert.AreEqual(a.Hostility, b.Hostility); Assert.AreEqual(a.EnemiesNearby, b.EnemiesNearby); Assert.AreEqual(a.InBattle, b.InBattle);
            Assert.AreEqual(a.CalendarTotalHours, b.CalendarTotalHours, 1e-9);
            Assert.AreEqual(a.Weather.Kind, b.Weather.Kind); Assert.AreEqual(a.Weather.Precipitation, b.Weather.Precipitation); Assert.AreEqual(a.WeatherRngState, b.WeatherRngState);
            Assert.AreEqual(a.PlayerName, b.PlayerName); Assert.AreEqual(a.PlayerX, b.PlayerX); Assert.AreEqual(a.PlayerYaw, b.PlayerYaw); Assert.AreEqual(a.Mounted, b.Mounted); Assert.AreEqual(a.HorseZ, b.HorseZ);
            Assert.AreEqual(a.Body.Blood, b.Body.Blood); CollectionAssert.AreEqual(a.Body.PartIntegrity, b.Body.PartIntegrity);
            Assert.AreEqual(a.Body.Wounds.Length, b.Body.Wounds.Length);
            for (int i = 0; i < a.Body.Wounds.Length; i++) { Assert.AreEqual(a.Body.Wounds[i].Part, b.Body.Wounds[i].Part); Assert.AreEqual(a.Body.Wounds[i].Bleeding, b.Body.Wounds[i].Bleeding); }
            Assert.AreEqual(a.Body.CauseOfDeath, b.Body.CauseOfDeath);
            Assert.AreEqual(a.Stamina, b.Stamina); Assert.AreEqual(a.HoursSurvived, b.HoursSurvived); Assert.AreEqual(a.Kills, b.Kills); Assert.AreEqual(a.MenLost, b.MenLost);
            Assert.AreEqual(a.Battle == null, b.Battle == null);
            if (a.Battle != null)
            {
                Assert.AreEqual(a.Battle.SoldierCount, b.Battle.SoldierCount); Assert.AreEqual(a.Battle.Elapsed, b.Battle.Elapsed); Assert.AreEqual(a.Battle.Started, b.Battle.Started);
                CollectionAssert.AreEqual(a.Battle.Positions, b.Battle.Positions); CollectionAssert.AreEqual(a.Battle.Health, b.Battle.Health); CollectionAssert.AreEqual(a.Battle.Alive, b.Battle.Alive);
                CollectionAssert.AreEqual(a.Battle.Targets, b.Battle.Targets); CollectionAssert.AreEqual(a.Battle.UnitOrder, b.Battle.UnitOrder); CollectionAssert.AreEqual(a.Battle.UnitAnchor, b.Battle.UnitAnchor);
                CollectionAssert.AreEqual(a.Battle.UnitAliveCount, b.Battle.UnitAliveCount);
            }
        }
    }

    public class SaveCodecTests
    {
        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var a = RunStateFactory.Sample();
            var b = SaveCodec.Decode(SaveCodec.Encode(a));
            RunStateFactory.AssertEqual(a, b);
        }

        [Test]
        public void RoundTrip_WithoutBattle()
        {
            var a = RunStateFactory.Sample(withBattle: false);
            var b = SaveCodec.Decode(SaveCodec.Encode(a));
            Assert.IsNull(b.Battle);
            RunStateFactory.AssertEqual(a, b);
        }

        [Test]
        public void FlippedByte_IsDetected()
        {
            byte[] bytes = SaveCodec.Encode(RunStateFactory.Sample());
            bytes[bytes.Length / 2] ^= 0x5A;
            Assert.Throws<SaveCorruptException>(() => SaveCodec.Decode(bytes));
        }

        [Test]
        public void Truncation_IsDetected()
        {
            byte[] bytes = SaveCodec.Encode(RunStateFactory.Sample());
            var cut = new byte[bytes.Length - 100];
            System.Array.Copy(bytes, cut, cut.Length);
            Assert.Throws<SaveCorruptException>(() => SaveCodec.Decode(cut));
            Assert.Throws<SaveCorruptException>(() => SaveCodec.Decode(new byte[3]));
            Assert.Throws<SaveCorruptException>(() => SaveCodec.Decode(null));
        }

        [Test]
        public void WrongMagic_IsRejected()
        {
            byte[] bytes = SaveCodec.Encode(RunStateFactory.Sample());
            bytes[0] = (byte)'X';
            Assert.Throws<SaveCorruptException>(() => SaveCodec.Decode(bytes));
        }

        [Test]
        public void ChecksumIsStable_AndCoversPayload()
        {
            byte[] a = SaveCodec.Encode(RunStateFactory.Sample());
            byte[] b = SaveCodec.Encode(RunStateFactory.Sample());
            CollectionAssert.AreEqual(a, b, "encoding must be deterministic");
            var changed = RunStateFactory.Sample(); changed.Kills++;
            CollectionAssert.AreNotEqual(a, SaveCodec.Encode(changed));
        }

        [Test]
        public void TwoThousandSoldiers_StaySmallAndFast()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            byte[] bytes = SaveCodec.Encode(RunStateFactory.Sample(soldiers: 2000));
            var back = SaveCodec.Decode(bytes);
            sw.Stop();
            Assert.AreEqual(2000, back.Battle.SoldierCount);
            Assert.Less(bytes.Length, 120_000, "save should stay under ~120 KB");
            Assert.Less(sw.ElapsedMilliseconds, 500);
        }
    }
}
