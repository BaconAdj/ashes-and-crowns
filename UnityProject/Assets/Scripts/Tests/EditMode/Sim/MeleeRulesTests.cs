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
            Assert.IsFalse(f.TryAttack(AttackDir.Left), "cannot swing mid-swing");
            Assert.Less(f.Stamina, 100f, "a swing costs stamina");
            bool struck = false; float t = 0f;
            while (t < 2f) { if (f.Tick(0.016f) == FightEvent.Strike) { Assert.IsFalse(struck); struck = true; Assert.AreEqual(FightPhase.Strike, f.Phase); } t += 0.016f; }
            Assert.IsTrue(struck); Assert.AreEqual(FightPhase.Idle, f.Phase);
        }

        [Test]
        public void Heavy_IsSlower_CostsMore_HitsHarder()
        {
            var b = B();
            var l = new FighterState(b, 100f); var h = new FighterState(b, 100f);
            l.TryAttack(AttackDir.High, AttackKind.Light); h.TryAttack(AttackDir.High, AttackKind.Heavy);
            Assert.Greater(l.Stamina, h.Stamina);
            float tl = 0f, th = 0f;
            while (l.Tick(0.01f) != FightEvent.Strike) tl += 0.01f;
            while (h.Tick(0.01f) != FightEvent.Strike) th += 0.01f;
            Assert.Greater(th, tl * 1.8f);
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            float light = 0f, heavy = 0f;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var (a, d) = Pair(out _); light += Swing(a, sword, d, bare, AttackDir.High, 1f, seed, b, kind: AttackKind.Light).Severity;
                var (a2, d2) = Pair(out _); heavy += Swing(a2, sword, d2, bare, AttackDir.High, 1f, seed, b, kind: AttackKind.Heavy).Severity;
            }
            Assert.Greater(heavy, light * 1.5f);
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

        private static HitResult Swing(FighterState a, Loadout al, FighterState d, Loadout dl, AttackDir dir, float distance, ulong seed, MeleeRulesBalance b, System.Action<FighterState> defenderAct = null, AttackKind kind = AttackKind.Light)
        {
            a.TryAttack(dir, kind);
            defenderAct?.Invoke(d);
            float t = 0f; HitResult r = default; bool landed = false;
            while (t < 2f && !landed) { d.Tick(0.016f); if (a.Tick(0.016f) == FightEvent.Strike) { r = MeleeRules.Resolve(a, al, d, dl, distance, new DeterministicRng(seed), b); landed = true; } t += 0.016f; }
            Assert.IsTrue(landed);
            return r;
        }

        [Test]
        public void Reach_Guard_Dodge_Hit()
        {
            var (a, d) = Pair(out var b);
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            Assert.AreEqual(HitOutcome.OutOfReach, Swing(a, sword, d, bare, AttackDir.High, 5f, 1, b).Outcome);
            a.Reset(); d.Reset();
            Assert.AreEqual(HitOutcome.Hit, Swing(a, sword, d, bare, AttackDir.High, 1f, 1, b).Outcome);
            a.Reset(); d.Reset();
            // A stance held in the same line blocks and costs stamina.
            var blocked = Swing(a, sword, d, bare, AttackDir.Left, 1f, 1, b, def => def.SetGuard(AttackDir.Left));
            Assert.AreEqual(HitOutcome.Blocked, blocked.Outcome); Assert.Less(d.Stamina, 100f);
            a.Reset(); d.Reset();
            // Wrong line: hit. The stance survives the hit.
            Assert.AreEqual(HitOutcome.Hit, Swing(a, sword, d, bare, AttackDir.Right, 1f, 1, b, def => def.SetGuard(AttackDir.High)).Outcome);
            Assert.AreEqual(AttackDir.High, d.GuardDir);
            a.Reset(); d.Reset();
            // Dodge timed with the strike: invulnerable frames
            a.TryAttack(AttackDir.High);
            float t = 0f; HitResult r = default;
            while (t < 1f) { if (t >= b.lightWindupSeconds - 0.1f && d.Phase == FightPhase.Idle) d.TryDodge(); d.Tick(0.016f); if (a.Tick(0.016f) == FightEvent.Strike) { r = MeleeRules.Resolve(a, sword, d, bare, 1f, new DeterministicRng(1), b); break; } t += 0.016f; }
            Assert.AreEqual(HitOutcome.Dodged, r.Outcome);
        }

        [Test]
        public void Parry_IsAHeavyInTheLine_JustBeforeTheBlow()
        {
            var (a, d) = Pair(out var b);
            var sword = Loadout.From(Inv("sword"), Items(), b);
            a.TryAttack(AttackDir.Right, AttackKind.Heavy);
            float t = 0f; HitResult r = default;
            while (t < 2f)
            {
                if (t >= b.heavyWindupSeconds - 0.08f && d.Phase == FightPhase.Idle) d.TryAttack(AttackDir.Right, AttackKind.Heavy); // timed heavy in the same line = parry
                d.Tick(0.016f);
                if (a.Tick(0.016f) == FightEvent.Strike) { r = MeleeRules.Resolve(a, sword, d, sword, 1f, new DeterministicRng(2), b); break; }
                t += 0.016f;
            }
            Assert.AreEqual(HitOutcome.Parried, r.Outcome); Assert.AreEqual(FightPhase.Stagger, a.Phase);
            Assert.AreEqual(FightPhase.Idle, d.Phase, "the parry replaces the heavy and leaves the defender free");
            float staggered = 0f; while (a.Phase == FightPhase.Stagger && staggered < 3f) { a.Tick(0.016f); staggered += 0.016f; }
            Assert.Greater(staggered, b.heavyHitStaggerSeconds, "a parry stagger lasts longer than a hit stagger");
            // A heavy in the wrong line is not a parry: the defender just gets hit mid-windup.
            var (a2, d2) = Pair(out _);
            a2.TryAttack(AttackDir.Left, AttackKind.Light);
            t = 0f; r = default;
            while (t < 2f) { if (t >= b.lightWindupSeconds - 0.05f && d2.Phase == FightPhase.Idle) d2.TryAttack(AttackDir.High, AttackKind.Heavy); d2.Tick(0.016f); if (a2.Tick(0.016f) == FightEvent.Strike) { r = MeleeRules.Resolve(a2, sword, d2, sword, 1f, new DeterministicRng(2), b); break; } t += 0.016f; }
            Assert.AreEqual(HitOutcome.Hit, r.Outcome);
        }

        [Test]
        public void Feint_CancelsAHeavy_OnlyEarlyEnough()
        {
            var b = B(); var f = new FighterState(b, 100f);
            Assert.IsFalse(f.TryFeint(), "nothing to feint");
            f.TryAttack(AttackDir.High, AttackKind.Light); Assert.IsFalse(f.TryFeint(), "lights cannot be feinted");
            f.Reset(); f.TryAttack(AttackDir.High, AttackKind.Heavy);
            f.Tick(0.1f);
            float before = f.Stamina;
            Assert.IsTrue(f.TryFeint()); Assert.AreEqual(FightPhase.Recover, f.Phase); Assert.Less(f.Stamina, before);
            float t = 0f; while (f.Phase != FightPhase.Idle && t < 1f) { Assert.AreNotEqual(FightEvent.Strike, f.Tick(0.016f)); t += 0.016f; }
            Assert.Less(t, b.feintRecoverSeconds + 0.05f, "a feint recovers fast");
            f.Reset(); f.TryAttack(AttackDir.High, AttackKind.Heavy);
            f.Tick(b.heavyWindupSeconds - 0.05f);
            Assert.IsFalse(f.TryFeint(), "too late: the blow is committed");
        }

        [Test]
        public void GuardBreak_BeatsAGuard_LosesToAnAttack_IsCountered()
        {
            var b = B();
            // Standing guard: broken and open; the next blow lands through the stance.
            var (a, d) = Pair(out _); d.SetGuard(AttackDir.High);
            Assert.IsTrue(a.TryGuardBreak()); Assert.AreEqual(FightPhase.GuardBreak, a.Phase);
            GuardBreakOutcome o = GuardBreakOutcome.OutOfReach; float t = 0f;
            while (t < 1f) { d.Tick(0.016f); if (a.Tick(0.016f) == FightEvent.GuardBreakLands) { o = MeleeRules.ResolveGuardBreak(a, d, 1f, b); break; } t += 0.016f; }
            Assert.AreEqual(GuardBreakOutcome.Broken, o); Assert.AreEqual(FightPhase.Stagger, d.Phase); Assert.IsTrue(d.Open);
            while (a.Phase != FightPhase.Idle) a.Tick(0.016f);
            var sword = Loadout.From(Inv("sword"), Items(), b);
            var r = Swing(a, sword, d, sword, AttackDir.High, 1f, 1, b);
            Assert.AreEqual(HitOutcome.Hit, r.Outcome, "an open guard does not block"); Assert.IsFalse(d.Open);
            // Attacking defender: the guard break fails and the breaker is staggered.
            var (a2, d2) = Pair(out _); d2.TryAttack(AttackDir.High, AttackKind.Heavy); a2.TryGuardBreak();
            t = 0f; o = GuardBreakOutcome.OutOfReach;
            while (t < 1f) { d2.Tick(0.016f); if (a2.Tick(0.016f) == FightEvent.GuardBreakLands) { o = MeleeRules.ResolveGuardBreak(a2, d2, 1f, b); break; } t += 0.016f; }
            Assert.AreEqual(GuardBreakOutcome.BeatenByAttack, o); Assert.AreEqual(FightPhase.Stagger, a2.Phase);
            // Countered: the defender presses guard break as it lands.
            var (a3, d3) = Pair(out _); a3.TryGuardBreak();
            t = 0f; o = GuardBreakOutcome.OutOfReach;
            while (t < 1f) { if (t >= b.guardBreakSeconds - 0.1f && d3.CanAct && !d3.CounterGuardBreakArmed) { d3.TryGuardBreak(); } d3.Tick(0.016f); if (a3.Tick(0.016f) == FightEvent.GuardBreakLands) { o = MeleeRules.ResolveGuardBreak(a3, d3, 1f, b); break; } t += 0.016f; }
            Assert.AreEqual(GuardBreakOutcome.Countered, o); Assert.IsFalse(d3.Open);
            Assert.AreEqual(GuardBreakOutcome.OutOfReach, MeleeRules.ResolveGuardBreak(new FighterState(b, 100f), new FighterState(b, 100f), 3f, b));
        }

        [Test]
        public void Revenge_FillsFromPunishment_ThenBurns()
        {
            var (a, d) = Pair(out var b);
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            Assert.IsFalse(d.TryRevenge());
            int blows = 0;
            while (!d.RevengeReady && blows < 20) { a.Reset(); d.EndAction(); Swing(a, sword, d, bare, AttackDir.High, 1f, (ulong)blows + 1, b); blows++; }
            Assert.IsTrue(d.RevengeReady); Assert.Less(blows, 8);
            d.SetStamina(5f);
            Assert.IsTrue(d.TryRevenge()); Assert.IsTrue(d.InRevenge); Assert.AreEqual(100f, d.Stamina); Assert.AreEqual(0f, d.Revenge);
            // Lights do not stagger a fighter in revenge; a revenge blow hits harder.
            a.Reset(); d.EndAction();
            var r = Swing(a, sword, d, bare, AttackDir.High, 1f, 3, b);
            Assert.AreEqual(HitOutcome.Hit, r.Outcome); Assert.AreEqual(FightPhase.Idle, d.Phase);
            float plain = 0f, furious = 0f;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var (p, q) = Pair(out _); plain += Swing(p, sword, q, bare, AttackDir.High, 1f, seed, b).Severity;
                var (p2, q2) = Pair(out _); while (!p2.RevengeReady) p2.AddRevenge(0.5f); p2.TryRevenge(); furious += Swing(p2, sword, q2, bare, AttackDir.High, 1f, seed, b).Severity;
            }
            Assert.Greater(furious, plain * 1.15f);
            float t = 0f; while (d.InRevenge && t < 20f) { d.Tick(0.1f); t += 0.1f; }
            Assert.IsFalse(d.InRevenge); Assert.Less(t, b.revengeSeconds + 0.5f);
        }

        [Test]
        public void Armour_AndParts_ShapeTheWound()
        {
            var b = B();
            var sword = Loadout.From(Inv("sword"), Items(), b); var mailed = Loadout.From(Inv("mail", "helm"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            float bareSum = 0f, mailSum = 0f, headSum = 0f, sideSum = 0f; int n = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var (a, d) = Pair(out _);
                bareSum += Swing(a, sword, d, bare, AttackDir.High, 1f, seed, b).Severity;
                var (a2, d2) = Pair(out _);
                mailSum += Swing(a2, sword, d2, mailed, AttackDir.High, 1f, seed, b).Severity;
                var (a3, d3) = Pair(out _);
                var high = Swing(a3, sword, d3, bare, AttackDir.High, 1f, seed, b); if (high.Part == BodyPart.Head) { headSum += high.Severity; n++; }
                var (a4, d4) = Pair(out _);
                var side = Swing(a4, sword, d4, bare, AttackDir.Left, 1f, seed, b); sideSum += side.Severity; Assert.IsTrue(side.Part == BodyPart.RightArm || side.Part == BodyPart.Torso);
            }
            Assert.Greater(bareSum, mailSum * 1.6f, "mail and helm absorb most of a sword");
            Assert.Greater(headSum / n, sideSum / 200f, "head blows are worse than arm blows");
        }

        [Test]
        public void Exhaustion_StopsSwinging_AndGuardCrushes()
        {
            var (a, d) = Pair(out var b);
            d.SetStamina(4f);
            Assert.IsFalse(d.TryAttack(AttackDir.High), "too tired to swing");
            var sword = Loadout.From(Inv("sword"), Items(), b); var bare = Loadout.From(new Inventory(), Items(), b);
            var r = Swing(a, sword, d, bare, AttackDir.High, 1f, 3, b, def => def.SetGuard(AttackDir.High));
            Assert.AreEqual(HitOutcome.Hit, r.Outcome, "a guard without stamina is crushed");
        }

        [Test]
        public void Brain_GuardsMoreWhenItReadsWell_BreaksAPatientGuard_AndYieldsWhenHurt()
        {
            var b = B();
            int goodGuards = 0, poorGuards = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var good = new FighterState(b, 100f); new FighterBrain(new DeterministicRng(seed), 0.5f, 0.9f).Think(good, AttackDir.High, AttackKind.Light, false, 1f, 2f, 0.016f, b);
                if (good.GuardDir == AttackDir.High) goodGuards++;
                var poor = new FighterState(b, 100f); new FighterBrain(new DeterministicRng(seed), 0.5f, 0.1f).Think(poor, AttackDir.High, AttackKind.Light, false, 1f, 2f, 0.016f, b);
                if (poor.GuardDir == AttackDir.High) poorGuards++;
            }
            Assert.Greater(goodGuards, 80); Assert.Less(poorGuards, 55, "a poor reader mostly guesses its stance");
            // Facing a guard held for seconds, the brain eventually breaks it.
            int breaks = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var me = new FighterState(b, 100f); var brain = new FighterBrain(new DeterministicRng(seed), 0.0f, 0.5f);
                for (float t = 0f; t < 6f; t += 0.05f) { brain.Think(me, null, AttackKind.Light, true, 1f, 2f, 0.05f, b); if (me.Phase == FightPhase.GuardBreak) { breaks++; break; } me.Tick(0.05f); }
            }
            Assert.Greater(breaks, 30);
            var yb = new FighterBrain(new DeterministicRng(5), 0.5f, 0.5f);
            int yields = 0; for (int i = 0; i < 100; i++) if (yb.ConsiderYield(new FighterState(b, 100f), 0.2f, b)) yields++;
            Assert.Greater(yields, 20); Assert.IsFalse(yb.ConsiderYield(new FighterState(b, 100f), 0.9f, b));
        }

        private static Inventory Inv(params string[] ids) { var i = new Inventory(); foreach (string id in ids) i.Add(id); return i; }
    }
}
