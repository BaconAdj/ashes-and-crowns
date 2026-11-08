using System.Collections.Generic;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Serving in the ranks: you hold a place in a formation, the sergeant screams the orders, a soft ring marks your slot,
    /// and the provost reads the sergeants' report afterwards. No command. If your side breaks, you run with the rest.
    /// </summary>
    public sealed class GruntMode : MonoBehaviour
    {
        public BattleDirector director;
        public ModeManager modes;
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;
        public MeleeController melee;
        public HudController hud;
        public Material ringMaterial;
        public int unitIndex;
        public int slotIndex;

        private Balance _balance;
        private LineRenderer _ring;
        private OrderBanner _lastBanner = (OrderBanner)255;
        private float _contactInSlotSeconds, _standingAloneSeconds;
        private bool _lineCredited, _stoodCredited, _resolved;
        private DeterministicRng _rng;
        private readonly List<string> _chronicle = new List<string>(8);

        private int _companySize = 60, _lastInfractionToast;

        public void Init(Balance balance, bool placeBody = true)
        {
            _balance = balance;
            _rng = GameBootstrap.Instance.Rng.Fork(131);
            var ctx = RunSession.Context;
            // Your unit: the first of your side; your slot: the last man of the front rank.
            unitIndex = 0; slotIndex = Mathf.Min(director.Units[0].SoldierCount - 1, FormationMath.LineRankWidth - 1);
            if (modes != null) { modes.commandAllowed = false; }
            if (RunSession.PendingBattle != null) _companySize = RunSession.PendingBattle.CompanySize;
            Vector3 slot = director.SlotWorldPosition(unitIndex, slotIndex);
            if (placeBody)
            {
                var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
                player.transform.position = slot + Vector3.up * 0.2f; player.transform.rotation = Quaternion.Euler(0f, director.Units[unitIndex].Yaw * Mathf.Rad2Deg, 0f);
                if (cc != null) cc.enabled = true;
                if (horse != null) horse.transform.position = slot + new Vector3(-30f, 0f, -20f);
            }
            _ring = new GameObject("SlotRing").AddComponent<LineRenderer>();
            _ring.loop = true; _ring.positionCount = 24; _ring.startWidth = _ring.endWidth = 0.12f; _ring.material = ringMaterial; _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            melee.Swung += OnSwung;
            director.PlayerKilledSoldier += OnKill;
            director.BattleEnded += OnEnded;
            hud.SetMode("Ranks");
            hud.Toast(ctx != null ? $"You stand in the line of {ctx.Service.CompanyName}. Watch the banner. Hold your place." : "You stand in the line.");
        }

        private void OnDestroy() { if (melee != null) melee.Swung -= OnSwung; if (director != null) { director.PlayerKilledSoldier -= OnKill; director.BattleEnded -= OnEnded; } }

        private void Update()
        {
            if (_balance == null || !director.IsReady || vitals.IsDead || _resolved) return;
            var ctx = RunSession.Context; if (ctx == null) return;
            float dt = Time.deltaTime;
            UnitStance stance = director.StanceOf(unitIndex);
            OrderBanner banner = ServiceRules.BannerFor(director.OrderOf(unitIndex), stance);
            if (banner != _lastBanner)
            {
                _lastBanner = banner;
                string shout = ServiceRules.Shout(banner, _rng);
                hud.SetOrderBanner(banner.ToString().ToUpperInvariant(), shout);
                Debug.Log("[audio stub] sergeant_" + banner.ToString().ToLowerInvariant() + ".ogg: " + shout);
            }
            Vector3 slot = director.SlotWorldPosition(unitIndex, slotIndex);
            for (int k = 0; k < 24; k++) { float a = k / 24f * Mathf.PI * 2f; float x = slot.x + Mathf.Cos(a) * 0.8f, z = slot.z + Mathf.Sin(a) * 0.8f; _ring.SetPosition(k, new Vector3(x, director.GroundHeight(x, z) + 0.1f, z)); }
            Vector3 body = horse != null && horse.Mounted ? horse.transform.position : player.transform.position;
            float dist = Vector2.Distance(new Vector2(body.x, body.z), new Vector2(slot.x, slot.z));
            bool contact = director.UnitInContact(unitIndex);
            float added = ServiceRules.TickDiscipline(ctx.Service, dist, stance, contact, dt, _balance.service);
            int tens = Mathf.FloorToInt(ctx.Service.Infractions) / 10;
            if (added > 0f && ctx.Service.Infractions > _balance.service.floggingAbove && tens != _lastInfractionToast) { _lastInfractionToast = tens; hud.Toast("The sergeant has seen you out of the line."); }
            if (contact && dist <= _balance.service.formationSlack && stance <= UnitStance.Shaken) { _contactInSlotSeconds += dt; if (!_lineCredited && _contactInSlotSeconds >= 30f) { _lineCredited = true; ServiceRules.HeldTheLine(ctx.Service, _balance.service); hud.Toast("You held the line. The sergeant saw."); } }
            if (stance == UnitStance.Routing && director.CountWithin(body, 8f, Side.Enemy) > 0) { _standingAloneSeconds += dt; if (!_stoodCredited && _standingAloneSeconds >= 8f) { _stoodCredited = true; ServiceRules.StoodWhileOthersRan(ctx.Service, _balance.service); hud.Toast("Your unit ran. You did not. Someone will remember."); } }
            hud.SetVantageDistance(0f);
        }

        private void OnSwung() { }

        private void OnKill(int soldier)
        {
            var ctx = RunSession.Context; if (ctx == null) return;
            Vector3 c = director.UnitCentroids[unitIndex];
            if (Vector3.Distance(player.transform.position, c) <= _balance.service.deedKillWitnessRange) { ServiceRules.WitnessedKill(ctx.Service, _balance.service); }
        }

        private void OnEnded(BattleReport report)
        {
            if (_resolved) return; _resolved = true;
            var ctx = RunSession.Context; if (ctx == null) { hud.ShowBattleReport(report); return; }
            var sv = ctx.Service; var b = _balance.service;
            _chronicle.Clear();
            _chronicle.Add(report.Summary());
            bool won = report.PlayerWon;
            if (won)
            {
                int loot = ServiceRules.LootShare(report.EnemyDead, _companySize, b, _rng);
                ctx.Character.Coins += loot; _chronicle.Add($"Loot divided by the fire: {loot} pennies to you.");
                ctx.Reputation.Record(sv.Side == Allegiance.Stephen ? Deed.FoughtForStephen : Deed.FoughtForMatilda);
            }
            ProvostVerdict verdict = ServiceRules.Provost(sv, won, _rng, b, out string provostText);
            _chronicle.Add(provostText);
            if (verdict == ProvostVerdict.Flogging) vitals.TakeHitAt(BodyPart.Torso, 0.18f, "the provost's lash");
            if (verdict == ProvostVerdict.Hanging) { vitals.Body.Drain(1f, "the provost's rope"); }
            if (!won && !vitals.IsDead)
            {
                UnitStance st = director.StanceOf(unitIndex);
                bool caught = st == UnitStance.Surrendered || (st == UnitStance.Routing && director.CountWithin(player.transform.position, 15f, Side.Enemy) > 0 && _rng.Chance(0.5f));
                if (caught)
                {
                    CaptureOutcome o = ServiceRules.Capture(ctx.Character, ctx.Reputation, sv.Side == Allegiance.Stephen ? Allegiance.Matilda : Allegiance.Stephen, _rng, b, out string capText);
                    _chronicle.Add("Taken. " + capText);
                    if (o == CaptureOutcome.Sword) vitals.Body.Drain(1f, "the sword, after the battle");
                    if (o == CaptureOutcome.Mercy) ctx.Inventory.Clear();
                    sv.Discharge();
                }
                else _chronicle.Add("You got away with the rest of the broken company.");
            }
            foreach (string n in report.NamedDead) _chronicle.Add(n);
            if (sv.Renown > 0) _chronicle.Add($"Renown: {sv.Renown}.");
            if (vitals.IsDead) { RunSession.ReturnChronicle = new List<string>(_chronicle); vitals.NotifyDeadFromBody(); return; }
            hud.ShowChronicle(won ? "The field is yours" : "The field is lost", _chronicle);
            hud.SetOrderBanner(string.Empty, "Press R to march home.");
            RunSession.ReturnChronicle = new List<string>(_chronicle);
            _waitingReturn = true;
        }

        private bool _waitingReturn;
        private void LateUpdate()
        {
            if (!_waitingReturn || vitals.IsDead) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                _waitingReturn = false;
                var ctx = RunSession.Context; if (ctx != null) ctx.Service.InCamp = ctx.Service.Active;
                if (vitals.Body != null) RunSession.CarriedBody = vitals.Body.Export();
                SceneManager.LoadScene("Phase1_England");
            }
        }
    }
}
