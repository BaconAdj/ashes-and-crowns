using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Fights outside the battlefield: ambushes on dangerous roads, hunters after an outlaw, the bounty's quarry,
    /// and the festival melee. Spawns Duelists around the player and resolves what the fight meant.
    /// </summary>
    public sealed class EncounterDirector : MonoBehaviour
    {
        public MeleeController melee;
        public PlayerVitals vitals;
        public WorldClock clock;
        public HudController hud;
        public VillageLife village;
        public SaveManager saveManager;
        public Material banditMaterial, steelMaterial;
        public Transform player;

        private Balance _balance;
        private readonly List<Duelist> _active = new List<Duelist>(6);
        private string _kind = string.Empty;
        private int _meleeBoutsLeft;
        private int _prize;
        private float _nextRoadRoll;
        private DeterministicRng _rng;
        private float _robTimer;

        public bool InFight { get { foreach (var d in _active) if (d != null && !d.Out) return true; return false; } }

        private void Start()
        {
            _balance = GameBootstrap.Instance.Balance;
            _rng = GameBootstrap.Instance.Rng.Fork(115);
            if (melee != null) { melee.DuelistDowned += OnDowned; melee.OnKilledSurrendered += OnKilledSurrendered; }
            if (clock != null) clock.HoursPassed += OnHours;
        }

        private void OnDestroy() { if (clock != null) clock.HoursPassed -= OnHours; }

        private void OnHours(float hours)
        {
            var ctx = RunSession.Context;
            if (ctx == null || vitals.IsDead || InFight) return;
            _nextRoadRoll -= hours;
            if (_nextRoadRoll > 0f) return;
            _nextRoadRoll = 6f; // a roll every six game hours of travel
            bool onRoad = string.IsNullOrEmpty(ctx.CurrentSettlementId);
            if (!onRoad) return;
            var rng = GameBootstrap.Instance.Rng.Fork(111); rng.State ^= (ulong)clock.Calendar.TotalHours;
            if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && JobBoard.RoadDanger(ctx.ActiveJob, 0.5f, rng)) { Ambush(rng.Range(1, 3), rng); return; }
            if (ctx.Crime.Outlawed && rng.Chance(0.08f)) { Hunters(rng); return; }
            if (rng.Chance(0.015f)) Ambush(1, rng);
        }

        public void Ambush(int count, DeterministicRng rng)
        {
            _kind = "ambush";
            hud.Toast(count == 1 ? "A man steps out of the hedge with a knife." : $"{count} men come out of the trees. They want what you carry.");
            for (int i = 0; i < count; i++) Spawn("Bandit", rng, aggression: 0.6f, read: 0.3f, skill: rng.Range(10f, 35f), weapon: rng.Chance(0.5f) ? "knife" : rng.Chance(0.5f) ? "spear" : "axe", armour: null, bandit: true);
        }

        private void Hunters(DeterministicRng rng)
        {
            _kind = "hunters";
            hud.Toast("Riders with a rope. Someone has sold your name.");
            Spawn("Outlaw-hunter", rng, 0.7f, 0.5f, rng.Range(30f, 55f), "sword", "gambeson", false);
            Spawn("Outlaw-hunter", rng, 0.6f, 0.4f, rng.Range(20f, 45f), "spear", null, false);
        }

        /// <summary>The bounty's quarry, when you reach the right place.</summary>
        public void BountyTarget(Job job)
        {
            _kind = "bounty";
            var rng = GameBootstrap.Instance.Rng.Fork(112); rng.State ^= (ulong)job.Id;
            hud.Toast("The outlaw of the road. He has seen you first.");
            Spawn("The outlaw", rng, 0.65f, 0.55f, rng.Range(35f, 65f), rng.Chance(0.5f) ? "sword" : "axe", "gambeson", false);
        }

        /// <summary>Festival melee: three bouts, one at a time, blunted nothing. Prize for the last man standing.</summary>
        public void FestivalMelee(int stake)
        {
            var ctx = RunSession.Context;
            if (ctx.Character.Coins < stake) { hud.Toast("You cannot pay the stake."); return; }
            ctx.Character.Coins -= stake;
            _kind = "melee"; _meleeBoutsLeft = 3; _prize = stake * 12;
            hud.Toast("The ring is roped on the green. Three men stand against you, one at a time. To the yield.");
            NextBout();
        }

        private void NextBout()
        {
            var rng = GameBootstrap.Instance.Rng.Fork(113); rng.State ^= (ulong)(clock.Calendar.TotalHours * 31) + (ulong)_meleeBoutsLeft;
            string[] names = { "Edric the miller's lad", "A Fleming with a scar", "The reeve's brother", "A quiet man from Oxford" };
            var d = Spawn(names[rng.Range(0, names.Length)], rng, 0.55f, 0.35f + (3 - _meleeBoutsLeft) * 0.15f, 20f + (3 - _meleeBoutsLeft) * 15f, _meleeBoutsLeft == 1 ? "sword" : "spear", _meleeBoutsLeft == 1 ? "gambeson" : null, false);
            d.CanYield = true;
        }

        private Duelist Spawn(string name, DeterministicRng rng, float aggression, float read, float skill, string weapon, string armour, bool bandit)
        {
            var ctx = RunSession.Context;
            Vector3 pos = player.position + Quaternion.Euler(0f, rng.Range(-60f, 60f), 0f) * player.forward * rng.Range(7f, 12f);
            if (Terrain.activeTerrain != null) pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y + 0.1f;
            var go = new GameObject("Duelist_" + name);
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(body.GetComponent<Collider>()); body.transform.SetParent(go.transform, false); body.transform.localPosition = new Vector3(0f, 0.95f, 0f); body.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f); body.GetComponent<Renderer>().sharedMaterial = banditMaterial;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(head.GetComponent<Collider>()); head.transform.SetParent(go.transform, false); head.transform.localPosition = new Vector3(0f, 1.78f, 0f); head.transform.localScale = new Vector3(0.3f, 0.32f, 0.3f); head.GetComponent<Renderer>().sharedMaterial = banditMaterial;
            var pivot = new GameObject("WeaponPivot").transform; pivot.SetParent(go.transform, false); pivot.localPosition = new Vector3(0.38f, 1.2f, 0.1f);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(blade.GetComponent<Collider>()); blade.transform.SetParent(pivot, false); blade.transform.localPosition = new Vector3(0f, 0f, 0.5f); blade.transform.localScale = new Vector3(0.05f, 0.1f, weapon == "spear" ? 1.8f : weapon == "knife" ? 0.35f : 0.9f); blade.GetComponent<Renderer>().sharedMaterial = steelMaterial;
            var d = go.AddComponent<Duelist>();
            d.displayName = name; d.weaponPivot = pivot; d.IsBandit = bandit;
            var inv = new Inventory(); inv.Add(weapon); if (armour != null) inv.Add(armour);
            d.Init(_balance.duel, _balance.wounds, Loadout.From(inv, ctx.Items, _balance.duel), skill, aggression, read, rng.Fork((uint)name.Length));
            melee.RefreshLoadout();
            var gait = go.AddComponent<ProceduralGait>(); gait.visual = body.transform; gait.fullSpeed = 4f;
            melee.RegisterDuelist(d);
            _active.Add(d);
            return d;
        }

        private void OnDowned(Duelist d)
        {
            var ctx = RunSession.Context;
            if (_kind == "robbed") return;
            if (d.Dead)
            {
                ctx.Character.Use(WeaponSkill(), _balance.character, 2f);
                if (_kind == "ambush" || _kind == "hunters") { int loot = _rng.Range(0, 5); ctx.Character.Coins += loot; hud.Toast(d.displayName + " is dead." + (loot > 0 ? $" {loot} pennies in his purse." : "")); }
                else if (_kind == "melee") { ctx.Standing.FightWon(); ctx.Reputation.Record(Deed.Murder); hud.Toast("You have killed a man in a festival melee. The crowd goes quiet."); }
                else hud.Toast(d.displayName + " is dead.");
                if (_kind == "bounty" && ctx.ActiveJob != null) { int pay = JobBoard.Complete(ctx.ActiveJob, ctx.Character, JobBoard.Today(clock.Calendar), ctx.Standing); hud.Toast($"The outlaw's head is worth {pay} pennies."); ctx.ActiveJob = null; }
            }
            else if (d.Yielded)
            {
                hud.Toast(d.displayName + " drops his guard and yields. Strike again to kill him, or walk away.");
                if (_kind == "melee") { ctx.Standing.FightWon(); ctx.Character.Use(WeaponSkill(), _balance.character, 1.5f); }
                if (_kind == "bounty" && ctx.ActiveJob != null) { int pay = JobBoard.Complete(ctx.ActiveJob, ctx.Character, JobBoard.Today(clock.Calendar), ctx.Standing) / 2; ctx.Character.Coins -= pay; hud.Toast($"You take his hand instead of his head: {pay} pennies."); ctx.ActiveJob = null; }
            }
            if (!InFight) EndOfFight();
        }

        private void OnKilledSurrendered()
        {
            var ctx = RunSession.Context;
            ctx.Reputation.Record(Deed.KilledSurrendered); ctx.Standing.BrawlStarted();
        }

        private void EndOfFight()
        {
            var ctx = RunSession.Context;
            if (_kind == "melee")
            {
                _meleeBoutsLeft--;
                if (_meleeBoutsLeft > 0 && !vitals.IsDead) { hud.Toast($"The next man steps into the ring. {_meleeBoutsLeft} to go."); Invoke(nameof(NextBout), 3f); return; }
                if (!vitals.IsDead) { ctx.Character.Coins += _prize; ctx.Standing.Generosity(); hud.Toast($"Last man standing. The prize is {_prize} pennies and the village will remember it."); }
            }
            foreach (var d in _active) if (d != null && d.Yielded) d.Spare();
            Invoke(nameof(ClearBodies), 25f);
            _kind = string.Empty;
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void ClearBodies()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var d = _active[i];
                if (d == null) { _active.RemoveAt(i); continue; }
                if (!d.Out) continue;
                melee.UnregisterDuelist(d); Destroy(d.gameObject); _active.RemoveAt(i);
            }
        }

        private Skill WeaponSkill()
        {
            string w = melee.Loadout.WeaponName.ToLowerInvariant();
            if (w.Contains("spear")) return Skill.Polearm;
            if (w.Contains("axe") && melee.Loadout.Damage > 20f) return Skill.TwoHanded;
            return Skill.OneHanded;
        }

        /// <summary>When the player goes down to bandits they rob and leave; death is handled by the vitals.</summary>
        private void Update()
        {
            if (!InFight) return;
            var ctx = RunSession.Context;
            // Bandits run when they have taken enough (a roll per second, not per frame).
            _robTimer -= Time.deltaTime;
            if (_kind == "ambush" && ctx != null && vitals.Body.Blood < 0.25f && _robTimer <= 0f)
            {
                _robTimer = 1f;
                if (_rng.Chance(0.35f))
                {
                    _kind = "robbed";
                    int stolen = ctx.Character.Coins / 2; ctx.Character.Coins -= stolen;
                    hud.Toast($"They take {stolen} pennies and leave you in the road. Lucky.");
                    foreach (var d in _active) if (d != null && !d.Out) { d.Fighter.Yield(); d.Spare(); }
                    EndOfFight();
                }
            }
        }
    }
}
