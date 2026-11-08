using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class MeleeRulesTests
    {
        private static MeleeRulesBalance B() => new MeleeRulesBalance();
        private static ItemsData Items() => new ItemsData { items = new[]
        {
            new ItemDef { id = "sword", name = "Sword", category = "weapon", damage = 24 }, new ItemDef { id = "spear", name = "Spear", category = "weapon", damage = 18 },
            new ItemDef { id = "knife", name = "Knife", category = "weapon", damage = 8 }, new ItemDef { id = "mail", category = "armour", armour = 55 }, new ItemDef { id = "helm", category = "armour", armour = 15 }, new ItemDef { id = "shield", category = "armour", armour = 15 }
        } };

        [Test]
        public void Phases_RunInOrder_AndStrikeFiresOnce()
        {
            var b = B(); var f = new FighterState(b, 100f);
            Assert.IsTrue(f.TryAttack(AttackDir.High)); Assert.AreEqual(FightPhase.Windup, f.Phase);
            Assert.IsFalse(f.TryAttack(AttackDir.Low), "cannot swing mid-swing");
            bool struck = false; float t = 0f;
            while (t < 2f) { if (f.Tick(0.016f)) { Assert.IsFalse(struck); struck = true; Assert.AreEqual(FightPhase.Strike, f.Phase); } t += 0.016f; }
            Assert.IsTrue(struck); Assert.AreEqual(FightPhase.Idle, f.Phase);
            Assert.Less(f.Stamina, 100f);
        }

        [Test]
        public void Loadout_ReadsWeaponsAndArmour_SpearReaches()
        {
            var inv = new Inventory(); inv.Add("spear"); inv.Add("knife"); inv.Add("mail"); inv.Add("helm"); inv.Add("shield");
            var l = Loadout.From(inv, Items(), B());
            Assert.AreEqual(18f, l.Damage); Assert.AreEqual("Spear", l.WeaponName); Assert.Greater(l.Reach, 2f);
            Assert.AreEqual(0.55f, l.ArmourTorso, 1e-4f); Assert.AreEqual(0.5f, l.ArmourHead, 1e-4f); Assert.IsTrue(l.HasShield);
            var bare = Loadout.From(new Inventory(), Items(), B());
            Assert.AreEqual(B().unarmedDamage, bare.Damage); Assert.AreEqual(0f, bare.ArmourTorso);
        }

        private static (FighterState a, FighterState d) Pair(out MeleeRulesBalance b) { b = B(); return (new FighterState(b, 100f), new FighterState(b, 100f)); }

        private static HitResult Swing(FighterState a, Loadout al, FighterState d, Loadout dl, AttackDir dir, float distance, ulong seed, MeleeRulesBalance b, System.Action<FighterState> defenderAct = null)
        {
            a.TryAttack(dir);
            defenderAct?.Invoke(d);
            float t = 0f; HitResult r = default; bool landed = false;
            while (t < 1f && !landed) { d.Tick(0.016f); if (a.Tick(0.016f)) { r = MeleeRules.Resolve(a, al, d, dl, distance, new DeterministicRng(seed), b); landed = true; } t += 0.016f; }
            Assert.IsTrue(landed);
            return r;
        }

        [Test]
        public void Reach_Block_Parry_Dodge_Hit()
        {
            var (a, d) = Pair(out var b);
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            Assert.AreEqual(HitOutcome.OutOfReach, Swing(a, sword, d, bare, AttackDir.High, 5f, 1, b).Outcome);
            a.Reset(); d.Reset();
            Assert.AreEqual(HitOutcome.Hit, Swing(a, sword, d, bare, AttackDir.High, 1f, 1, b).Outcome);
            a.Reset(); d.Reset();
            // Block raised early in the same line: blocked (parry window has passed by the time the strike lands)
            var blocked = Swing(a, sword, d, bare, AttackDir.Left, 1f, 1, b, def => def.TryBlock(AttackDir.Left));
            Assert.AreEqual(HitOutcome.Blocked, blocked.Outcome); Assert.Less(d.Stamina, 100f);
            a.Reset(); d.Reset();
            // Wrong line: hit
            Assert.AreEqual(HitOutcome.Hit, Swing(a, sword, d, bare, AttackDir.Low, 1f, 1, b, def => def.TryBlock(AttackDir.High)).Outcome);
            a.Reset(); d.Reset();
            // Dodge timed with the strike: invulnerable frames
            a.TryAttack(AttackDir.High);
            float t = 0f; HitResult r = default;
            while (t < 1f) { if (t >= b.windupSeconds - 0.1f && d.Phase == FightPhase.Idle) d.TryDodge(); d.Tick(0.016f); if (a.Tick(0.016f)) { r = MeleeRules.Resolve(a, sword, d, bare, 1f, new DeterministicRng(1), b); break; } t += 0.016f; }
            Assert.AreEqual(HitOutcome.Dodged, r.Outcome);
        }

        [Test]
        public void Parry_InTheWindow_StaggersTheAttacker()
        {
            var (a, d) = Pair(out var b);
            var sword = Loadout.From(Inv("sword"), Items(), b);
            a.TryAttack(AttackDir.Right);
            float t = 0f; HitResult r = default;
            while (t < 1f)
            {
                if (t >= b.windupSeconds - 0.05f && d.Phase == FightPhase.Idle) d.TryBlock(AttackDir.Right); // late block = parry
                d.Tick(0.016f);
                if (a.Tick(0.016f)) { r = MeleeRules.Resolve(a, sword, d, sword, 1f, new DeterministicRng(2), b); break; }
                t += 0.016f;
            }
            Assert.AreEqual(HitOutcome.Parried, r.Outcome); Assert.AreEqual(FightPhase.Stagger, a.Phase);
            float staggered = 0f; while (a.Phase == FightPhase.Stagger && staggered < 3f) { a.Tick(0.016f); staggered += 0.016f; }
            Assert.Greater(staggered, b.hitStaggerSeconds, "a parry stagger lasts longer than a hit stagger");
        }

        [Test]
        public void Armour_AndParts_ShapeTheWound()
        {
            var b = B();
            var sword = Loadout.From(Inv("sword"), Items(), b); var mailed = Loadout.From(Inv("mail", "helm"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            float bareSum = 0f, mailSum = 0f, headSum = 0f, lowSum = 0f; int n = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var (a, d) = Pair(out _);
                bareSum += Swing(a, sword, d, bare, AttackDir.High, 1f, seed, b).Severity;
                var (a2, d2) = Pair(out _);
                mailSum += Swing(a2, sword, d2, mailed, AttackDir.High, 1f, seed, b).Severity;
                var (a3, d3) = Pair(out _);
                var high = Swing(a3, sword, d3, bare, AttackDir.High, 1f, seed, b); if (high.Part == BodyPart.Head) { headSum += high.Severity; n++; }
                var (a4, d4) = Pair(out _);
                var low = Swing(a4, sword, d4, bare, AttackDir.Low, 1f, seed, b); lowSum += low.Severity; Assert.IsTrue(low.Part == BodyPart.LeftLeg || low.Part == BodyPart.RightLeg);
            }
            Assert.Greater(bareSum, mailSum * 1.6f, "mail and helm absorb most of a sword");
            Assert.Greater(headSum / n, lowSum / 200f, "head blows are worse than leg blows");
        }

        [Test]
        public void Exhaustion_StopsSwinging_AndGuardBreaks()
        {
            var (a, d) = Pair(out var b);
            d.SetStamina(4f);
            Assert.IsFalse(d.TryAttack(AttackDir.High), "too tired to swing");
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            var r = Swing(a, sword, d, bare, AttackDir.High, 1f, 3, b, def => def.TryBlock(AttackDir.High));
            Assert.AreEqual(HitOutcome.Hit, r.Outcome, "a block without stamina breaks");
        }

        [Test]
        public void Brain_GuardsMoreWhenItReadsWell_AndYieldsWhenHurt()
        {
            var b = B();
            int goodGuards = 0, poorGuards = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var good = new FighterState(b, 100f); new FighterBrain(new DeterministicRng(seed), 0.5f, 0.9f).Think(good, AttackDir.High, 1f, 2f, 0.016f, b);
                if (good.Phase == FightPhase.Block && good.Dir == AttackDir.High) goodGuards++;
                var poor = new FighterState(b, 100f); new FighterBrain(new DeterministicRng(seed), 0.5f, 0.1f).Think(poor, AttackDir.High, 1f, 2f, 0.016f, b);
                if (poor.Phase == FightPhase.Block && poor.Dir == AttackDir.High) poorGuards++;
            }
            Assert.Greater(goodGuards, poorGuards * 3);
            var brain = new FighterBrain(new DeterministicRng(5), 0.5f, 0.5f);
            int yields = 0; for (int i = 0; i < 100; i++) if (brain.ConsiderYield(new FighterState(b, 100f), 0.2f, b)) yields++;
            Assert.Greater(yields, 20); Assert.IsFalse(brain.ConsiderYield(new FighterState(b, 100f), 0.9f, b));
        }

        private static Inventory Inv(params string[] ids) { var i = new Inventory(); foreach (string id in ids) i.Add(id); return i; }
    }
}
