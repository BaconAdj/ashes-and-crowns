using System.Collections.Generic;
using System.Text;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The peasant day: places you can use with E (church, alehouse, home, workplace, village masters),
    /// eating, sleeping, working the day with a hands-on moment, the market with haggling, mass and apprenticeship.
    /// </summary>
    public sealed class VillageLife : MonoBehaviour
    {
        public WorldClock clock;
        public PlayerVitals vitals;
        public PlayerController player;
        public HorseController horse;
        public HudController hud;
        public SaveManager saveManager;
        public SurvivalSystem survival;
        public Transform church, alehouse, home, workplace;
        public Transform mastersRoot;       // children named "Master_<trade>"
        public SettlementSpawner spawner;   // every settlement site, home included
        public OpeningNarration narration;  // optional: no village keys while it plays
        public EncounterDirector encounter; // optional: melees and bounties
        public MeleeController melee;       // optional: loadout refresh after trading
        public SiegeDirector siege;         // optional: sieges and castles
        public LordshipDirector lordship;   // optional: the hall
        public IntrigueDirector intrigue;   // optional: the knife in the dark
        public DevCheats cheats;            // optional: F10 / ` testing menu
        public HouseholdDirector household; // optional: a knight's duties
        public ActivityDirector activity;   // optional: kneeling prayer and the working day
        private enum Open : byte { None, Dialogue, Market, Moment }
        private Open _openKind;
        public float useRadius = 4f;
        private SettlementSite _site; private float _nextSiteScan; private string _lastSiteId = string.Empty;
        public SettlementSite CurrentSite => _site;
        public bool InChurch { get { Transform c = _site != null && _site.church != null ? _site.church : church; return c != null && Vector3.Distance(Body().position, c.position) < 10f; } }

        private Balance _balance;
        private string _prompt = string.Empty;
        private float _nextPrompt;
        private bool _busy;                 // a panel or moment is open
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly List<(string label, System.Action act)> _options = new List<(string, System.Action)>(6);
        private Transform _nearest; private string _nearestKind = string.Empty;

        public bool IsBusy => _busy;
        /// <summary>A usable place is nearer than the horse, so E uses it instead of mounting.</summary>
        public bool HasPlaceInReach => _nearest != null && !(horse != null && horse.gameObject.activeSelf && !horse.Mounted && Vector3.Distance(player.transform.position, horse.transform.position) <= _balance.player.mountRadius && _nearestKind != "home" && _nearestKind != "church");

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (ctx == null || ctx.Needs == null || vitals == null || vitals.IsDead) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (narration != null && narration.Running) return;
            vitals.StaminaRegenScale = (ctx.Needs.Hungry ? 0.4f : 1f) * (ctx.Disease.Current != Sickness.None ? 0.5f : 1f);
            var kb = Keyboard.current; if (kb == null) return;

            if (siege != null && siege.IsOpen) return;
            if (lordship != null && lordship.IsOpen) return;
            if (intrigue != null && intrigue.IsOpen) return;
            if (cheats != null && cheats.IsOpen) return;
            if (household != null && household.IsOpen) return;
            if (!_busy && survival != null && survival.PendingArrest != null) { var pending = survival.PendingArrest; survival.PendingArrest = null; Arrest(pending); return; }
            if (!_busy && ctx.PendingLevyAt != null) { var at = ctx.PendingLevyAt; ctx.PendingLevyAt = null; LevyAtTheDoor(at); return; }
            if (_busy)
            {
                if (_openKind != Open.Dialogue) return; // market and moment have their own controls
                for (int i = 0; i < _options.Count && i < 6; i++)
                    if (DigitPressed(kb, i)) { var act = _options[i].act; CloseDialogue(); act?.Invoke(); return; }
                if (kb.qKey.wasPressedThisFrame) CloseDialogue();   // Esc is the quit screen; Q closes talk
                return;
            }

            // Quick actions
            if (kb.gKey.wasPressedThisFrame) EatSomething();
            if (kb.iKey.wasPressedThisFrame) ShowInventory();

            if (Time.unscaledTime >= _nextSiteScan) { _nextSiteScan = Time.unscaledTime + 1f; ScanSite(); Passing(ctx); }
            FindNearest();
            if (Time.unscaledTime >= _nextPrompt) { _nextPrompt = Time.unscaledTime + 0.25f; hud.SetPrompt(_nearest != null ? "[E] " + PromptFor(_nearestKind) : string.Empty); UpdateHudLines(ctx); }
            if (HasPlaceInReach && GameBootstrap.Instance.Input.Mount.WasPressedThisFrame() && !(melee != null && melee.GuardMode)) Use(_nearestKind, _nearest);
        }

        private static bool DigitPressed(Keyboard kb, int i)
        {
            switch (i) { case 0: return kb.digit1Key.wasPressedThisFrame; case 1: return kb.digit2Key.wasPressedThisFrame; case 2: return kb.digit3Key.wasPressedThisFrame; case 3: return kb.digit4Key.wasPressedThisFrame; case 4: return kb.digit5Key.wasPressedThisFrame; default: return kb.digit6Key.wasPressedThisFrame; }
        }

        private Transform Body() => horse != null && horse.Mounted ? horse.transform : player.transform;

        private void FindNearest()
        {
            _nearest = null; float best = useRadius;
            Check(Spot(church, "Altar"), "church", ref best); Check(Spot(alehouse, "Bar"), "alehouse", ref best); Check(Spot(home, "Hearth"), "home", ref best); Check(workplace, "work", ref best);
            if (_site != null && _site.settlementId != RunSession.Context.HomeSettlementId) { Check(Spot(_site.church, "Altar"), "church", ref best); Check(Spot(_site.alehouse, "Bar"), "alehouse", ref best); }
            if (_site != null) foreach (Villager v in _site.villagers) if (v != null) Check(v.transform, "master", ref best);
            if (_site != null) foreach (Transform oak in _site.oaks) if (oak != null) Check(oak, "wood", ref best);
            if (mastersRoot != null) foreach (Transform m in mastersRoot) if (m.GetComponent<Villager>() == null) Check(m, "master", ref best);
        }

        private float _nextGreeting;
        /// <summary>Villagers you brush past say something, by standing: a nod, a word, or a turned back.</summary>
        private void Passing(RunContext ctx)
        {
            if (_site == null || Time.unscaledTime < _nextGreeting) return;
            Vector3 p = Body().position;
            foreach (Villager v in _site.villagers)
            {
                if (v == null || !v.WillTalk || v.Activity == VillagerActivity.Praying) continue;
                if ((v.transform.position - p).sqrMagnitude > 9f) continue;
                string name = v.Tag.displayName;
                string line = ctx.Standing.Despised ? name + " looks through you." : ctx.Standing.Respected ? name + " touches a cap to you." : v.Activity == VillagerActivity.Working ? name + " grunts without looking up." : name + " nods as you pass.";
                hud.Whisper(line);
                _nextGreeting = Time.unscaledTime + 25f;
                return;
            }
        }

        /// <summary>A building's named marker (the altar, the bar, the hearth) or the building itself when it is not furnished.</summary>
        private static Transform Spot(Transform building, string marker) { if (building == null) return null; var m = BuildingKit.Find(building, marker); return m != null ? m : building; }

        private void ScanSite()
        {
            var ctx = RunSession.Context;
            SettlementSite best = null; float bestD = float.MaxValue;
            Vector3 p = Body().position;
            if (spawner != null)
                foreach (var site in spawner.Sites)
                {
                    if (site == null || site.centre == null) continue;
                    float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(site.centre.position.x, 0f, site.centre.position.z));
                    if (d <= site.radius && d < bestD) { bestD = d; best = site; }
                }
            _site = best;
            string id = best != null ? best.settlementId : string.Empty;
            ctx.CurrentSettlementId = id;
            if (id != _lastSiteId)
            {
                _lastSiteId = id;
                var s = ctx.Current;
                if (s != null)
                {
                    hud.ShowPlaceBanner(s.Name, LordLine(ctx, s));
                    if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && ctx.ActiveJob.ToId == s.Id && ctx.ActiveJob.DaysRequired == 0)
                    {
                        if (ctx.ActiveJob.Kind == JobKind.Bounty && encounter != null) encounter.BountyTarget(ctx.ActiveJob); else DeliverJob(s);
                    }
                    LawCheck(s);
                    household?.OnArrived(s);
                }
            }
        }

        /// <summary>Arriving somewhere you are wanted: the law may take you.</summary>
        private void LawCheck(Settlement s)
        {
            var ctx = RunSession.Context;
            if (s.WantedHere <= 0f && !ctx.Crime.Outlawed) return;
            var rng = GameBootstrap.Instance.Rng.Fork(101); rng.State ^= (ulong)clock.Calendar.TotalHours;
            if (ctx.Crime.TickDay(s, InChurch, rng)) Arrest(s);
        }

        public void Arrest(Settlement s)
        {
            var ctx = RunSession.Context;
            var rng = GameBootstrap.Instance.Rng.Fork(102); rng.State ^= (ulong)clock.Calendar.TotalHours;
            int stolen = 0; foreach (var c in ctx.Crime.Crimes) if (c.Kind == CrimeKind.Theft && c.Known && !c.Tried) stolen += 6;
            Sentence sentence = ctx.Crime.Trial(ctx.Character, ctx.Standing.Value, stolen, rng, out Crime tried);
            s.WantedHere = 0f;
            string crime = tried != null ? tried.Kind.ToString().ToLowerInvariant() : "nothing";
            string outcome;
            switch (sentence)
            {
                case Sentence.Acquitted: outcome = "The reeve hears you out, looks at the people who will speak for you, and lets you go."; break;
                case Sentence.Fine: { int fine = Mathf.Min(ctx.Character.Coins, 4 + Mathf.RoundToInt(CrimeRecord.Severity(tried.Kind) * 20f)); ctx.Character.Coins -= fine; outcome = $"Fined {fine} pennies. They count them twice."; break; }
                case Sentence.Flogging: vitals.TakeHit(0.18f, "the lash"); ctx.Standing.FightLost(); outcome = "Tied to the post on the green and flogged while the village watches."; break;
                case Sentence.Mutilation: vitals.TakeHit(0.45f, "the reeve's knife"); ctx.Character.ApplyMaim(BodyPart.RightArm, rng); outcome = "They take your right hand at the wrist. The stump is seared."; break;
                case Sentence.Outlawry: ctx.Crime.Outlaw(); foreach (var o in ctx.Settlements) o.WantedHere = Mathf.Max(o.WantedHere, 0.5f); outcome = "Outlawed. Any man may kill you now and answer for nothing."; break;
                case Sentence.Hanging: outcome = "Hanged from the elm by the church. The village watches. It takes a while."; break;
                default: outcome = "Beheaded in the castle yard, as befits your blood."; break;
            }
            clock.SkipHours(sentence == Sentence.Acquitted ? 2f : 24f);
            if (CrimeRecord.IsExecution(sentence)) { vitals.Body.Drain(1f, sentence == Sentence.Hanging ? "the noose" : "the block"); }
            Dialogue("Taken by the law", $"Men with cudgels at the alehouse door. You are tried in {s.Name} for {crime} and {CrimeRecord.Describe(sentence)}.\n\n{outcome}", ("...", () => { if (vitals.Body.IsDead) vitals.NotifyDeadFromBody(); }));
        }

        private void Check(Transform t, string kind, ref float best)
        {
            if (t == null) return;
            Vector3 p = Body().position; Vector3 q = t.position; q.y = p.y;
            bool furnished = t.parent != null && t.parent.GetComponent<MeshFilter>() != null && (t.name == "Altar" || t.name == "Bar" || t.name == "Hearth");
            float d = Vector3.Distance(p, q) - (furnished ? 0f : kind == "church" ? 8f : kind == "alehouse" ? 6f : 0f);
            if (d < best) { best = d; _nearest = t; _nearestKind = kind; }
        }

        private string PromptFor(string kind)
        {
            switch (kind)
            {
                case "church": return "Kneel at the altar";
                case "alehouse": return "The alewife's bar";
                case "home": return "Your hearth";
                case "work": return "Work the day";
                case "wood": return RunSession.Context.Inventory.Count("axe") > 0 ? "Cut firewood (two hours)" : "An oak. You would need an axe.";
                case "master": { var v = _nearest != null ? _nearest.GetComponent<Villager>() : null; return (v != null && !v.WillTalk ? "Wake " : "Speak with ") + MasterName(_nearest); }
                default: return "Use";
            }
        }

        private void UpdateHudLines(RunContext ctx)
        {
            hud.SetHunger(ctx.Needs.Hunger);
            string sick = ctx.Disease.Current != Sickness.None ? "  |  sick with " + ctx.Disease.Describe() : string.Empty;
            string sept = vitals.Body.HasInfection ? "  |  a wound has turned bad" : string.Empty;
            hud.SetStandingLine($"{ctx.Character.Coins} pennies  |  standing: {ctx.Standing.Describe()}{sick}{sept}" + (ctx.Apprenticeship.Active ? $"  |  apprentice {ctx.Apprenticeship.Trade}, {ctx.Apprenticeship.SeasonsRemaining:F1} seasons left" : string.Empty));
        }

        // ------------------------------------------------------------------ places

        private void Use(string kind, Transform at)
        {
            switch (kind)
            {
                case "church": Church(); break;
                case "alehouse": Alehouse(); break;
                case "home": Home(); break;
                case "work": WorkTheDay(); break;
                case "wood": CutWood(at); break;
                case "master": Master(at); break;
            }
        }

        private void Church()
        {
            var ctx = RunSession.Context; var cal = clock.Calendar;
            long today = Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);
            if (Festivals.IsMassWindow(cal, _balance.time))
            {
                if (ctx.LastMassDay == today) { Dialogue("The church", "You have heard mass already today. The priest nods.", ("Leave", null)); return; }
                Feast f = Festivals.FeastOn(cal);
                if (activity != null)
                {
                    _busy = true; _openKind = Open.Moment;
                    Transform nave = Spot(_site != null && _site.church != null ? _site.church : church, "Nave");
                    activity.BeginRest("Mass", "E to slip out early (the village will notice)", nave, RestPose.Standing, 1.5f, _balance.activity.massTimeScale, true, (hours, early) =>
                    {
                        _busy = false; _openKind = Open.None;
                        if (early && hours < 1.0f) { ctx.Standing.MissedMass(); Dialogue("Mass", "You slip out before the elevation. Heads turn. That will be remembered.", ("Go out into the cold", null)); return; }
                        ctx.LastMassDay = today; ctx.Standing.AttendedMass();
                        Dialogue("Mass", $"You stand at the back with the village while the priest sings the {Festivals.Name(f)} mass in a Latin nobody follows. People saw you come. ({ctx.Standing.Describe()})", ("Go out into the cold", null));
                    });
                    return;
                }
                ctx.LastMassDay = today; ctx.Standing.AttendedMass();
                clock.SkipHours(1.5f);
                Dialogue("Mass", $"You stand at the back with the village while the priest sings the {Festivals.Name(f)} mass in a Latin nobody follows. People saw you come. ({ctx.Standing.Describe()})", ("Go out into the cold", null));
            }
            else
            {
                Feast f = Festivals.FeastOn(cal);
                string next = f != Feast.None ? "Mass is sung between Terce and Sext." : "There is no mass today. Sunday is " + DaysToSunday(today) + " days off.";
                if (activity != null)
                    Dialogue("The church", "Cold stone and the smell of tallow. " + next, ("Kneel at the altar", BeginPrayer), ("Leave", null));
                else
                    Dialogue("The church", "Cold stone and the smell of tallow. " + next, ("Pray a while (half an hour)", () => { clock.SkipHours(0.5f); }), ("Leave", null));
            }
        }

        private void BeginPrayer()
        {
            _busy = true; _openKind = Open.Moment;
            Transform altar = Spot(_site != null && _site.church != null ? _site.church : church, "Altar");
            activity.BeginPrayer(altar, (hours, favour) =>
            {
                _busy = false; _openKind = Open.None;
                var ctx = RunSession.Context;
                if (favour > 0f) ctx.Standing.Prayed(favour);
                int mins = Mathf.RoundToInt(hours * 60f);
                string line = mins < _balance.activity.prayMinMinutesToCount ? "A bob of the knee and out again. The priest's mouth tightens." :
                              favour > 0.03f ? $"You rise stiff after {mins / 60}h {mins % 60:00}m. Two women by the font saw the whole of it; by Vespers the village will know. ({ctx.Standing.Describe()})" :
                              favour > 0.005f ? $"You rise after {mins} minutes. The priest nods once. ({ctx.Standing.Describe()})" :
                              "You rise. God knows; the village does not yet.";
                Dialogue("The altar", line, ("Go out into the cold", null));
                saveManager?.SaveNow(AffairsChoice.None);
            });
        }

        private static string LordLine(RunContext ctx, Settlement s)
        {
            MinorLord lord = null;
            if (s.LordId >= 0) foreach (var l in ctx.Baronage) if (l.Id == s.LordId) { lord = l; break; }
            string side = s.Allegiance == Allegiance.Stephen ? "for King Stephen" : s.Allegiance == Allegiance.Matilda ? "for the Empress Matilda" : "for no one yet";
            string kind = s.Type == SettlementType.City ? "a city" : s.Type == SettlementType.Town ? "a market town" : s.Type == SettlementType.Castle ? "a castle and its vill" : "a village";
            if (lord != null) return $"{kind} held by {lord.GivenName} {lord.House}, {side}";
            return $"{kind}, {side}";
        }

        private static int DaysToSunday(long jdn) { int wd = (int)((jdn + 1) % 7); return wd == 0 ? 7 : 7 - wd; }

        private void Alehouse()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            string title = s != null ? "The alehouse of " + s.Name : "The alehouse";
            var opts = new List<(string, System.Action)>(6) { ("Buy and sell", () => OpenMarket(true)), ("Ask for work", AskForWork) };
            if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && ctx.ActiveJob.DaysRequired > 0 && s != null && ctx.ActiveJob.ToId == s.Id) opts.Add(($"Serve a day ({ctx.ActiveJob.DaysServed}/{ctx.ActiveJob.DaysRequired})", ServeDay));
            opts.Add(("A gallon of ale and the rumour of the day (1 penny)", () =>
            {
                if (ctx.Character.Coins < 1) { Dialogue("The alewife", "No penny, no ale.", ("Leave", null)); return; }
                ctx.Character.Coins -= 1; ctx.Needs.Eat(0.15f);
                if (activity == null) { clock.SkipHours(1f); Dialogue("Rumour", Rumour(), ("Enough", null)); return; }
                _busy = true; _openKind = Open.Moment;
                Transform seat = BuildingKit.Find(_site != null && _site.alehouse != null ? _site.alehouse : alehouse, "Seat", 1);
                activity.BeginRest("A pot of ale", "E to drink up and go", seat, RestPose.Sitting, 1f, _balance.activity.mealTimeScale, true, (hours, early) => { _busy = false; _openKind = Open.None; Dialogue("Rumour", Rumour(), ("Enough", null)); });
            }));
            if (encounter != null && Festivals.IsFair(Festivals.FeastOn(clock.Calendar))) opts.Add(("Enter the festival melee (stake 1 penny)", () => encounter.FestivalMelee(1)));
            var company = s != null && s.Type >= SettlementType.Town ? EnsureCompany(s) : null;
            if (company != null && !ctx.Service.Active) opts.Add(($"Sign with {company.Name} ({company.Captain}, for {company.Employer}, {_balance.service.mercenaryWagePerDay}d a day)", () => Enlist(company)));
            if (ctx.Service.Active && ctx.Siege == null)
            {
                opts.Add(($"Report to the muster of {ctx.Service.CompanyName}: march to battle", MarchToBattle));
                var hostile = NearestHostileCastle(ctx, ctx.Service.Side);
                if (hostile != null && siege != null && ctx.Service.Role != ServiceRole.Household) opts.Add(($"Report to the muster: the company marches to besiege {hostile.Name}", () => siege.BeginAsGrunt(hostile)));
                opts.Add(("Spend a day in camp", CampDay)); opts.Add(("Desert (slip away in the night)", DesertService));
            }
            if (s != null && lordship != null && ctx.Estate != null && ctx.Estate.SettlementId == s.Id) opts.Add(("Go up to the hall", lordship.Hall));
            if (household != null && household.AtPatronSeat(ctx, s)) opts.Add((ctx.Duty != null && ctx.Duty.State == DutyState.Given ? "Your orders" : "Report to the hall for duty", household.ReportForDuty));
            if (intrigue != null && ctx.Plot != null && ctx.Plot.WarningsGiven > 0) opts.Add(("Act on your suspicions", intrigue.Suspicions));
            if (s != null && s.Type == SettlementType.Village && siege != null && ctx.Siege == null && ctx.Estate == null && (ctx.Castle == null || ctx.Castle.Stage == CastleStage.None)) opts.Add(("Raise a castle on this land (unlicensed)", RaiseCastle));
            if (s != null && s.Type >= SettlementType.Town && ctx.Castle != null && ctx.Castle.Stage != CastleStage.None) opts.Add(($"Hire ten spears for your garrison ({10 * _balance.siege.garrisonHirePennies} pennies)", HireGarrison));
            if (s != null && (s.Type == SettlementType.Castle || s.Type == SettlementType.City || s.Type == SettlementType.Town)) opts.Add(("Seek an audience with the lord", Audience));
            opts.Add(("Lift a purse while nobody looks", Steal));
            opts.Add(("Leave", null));
            Dialogue(title, "Smoke, wet wool and talk. The alewife sells what the place has to spare.", opts.ToArray());
        }

        private void AskForWork()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            if (s == null) { Dialogue("Work", "Nobody here hires strangers.", ("Leave", null)); return; }
            if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken)
            {
                var to = ctx.FindSettlement(ctx.ActiveJob.ToId);
                Dialogue("Your work", $"You already carry {ctx.ActiveJob.Employer}'s business: {JobBoard.KindName(ctx.ActiveJob.Kind)} to {(to != null ? to.Name : ctx.ActiveJob.ToId)}, due in {ctx.ActiveJob.DeadlineDay - JobBoard.Today(clock.Calendar)} days.", ("Abandon it (your name suffers)", () => { ctx.ActiveJob.State = JobState.Failed; ctx.Standing.DebtDefaulted(); ctx.Reputation.Record(Deed.BrokeContract); ctx.ActiveJob = null; }), ("Keep it", null));
                return;
            }
            var rng = GameBootstrap.Instance.Rng.Fork(103); rng.State ^= (ulong)clock.Calendar.TotalHours;
            int next = ctx.NextJobId;
            JobBoard.Refresh(s, ctx.Settlements, ctx.MapSideKm, _balance.settlements, _balance.jobs, clock.Calendar, new NameGenerator(ctx.Names, rng.Fork(1)), rng, ref next);
            ctx.NextJobId = next;
            var opts = new List<(string, System.Action)>(4);
            foreach (Job j in s.Jobs)
            {
                if (j.State != JobState.Offered) continue;
                Job job = j; bool can = JobBoard.CanTake(job, ctx.Character, ctx.Inventory);
                opts.Add((JobBoard.KindName(job.Kind) + ": " + job.Blurb + (can ? "" : "  [not for you]"), () =>
                {
                    if (!can) { Dialogue(job.Employer, "\"Not you.\" The employer looks past you.", ("Leave", null)); return; }
                    JobBoard.Take(job, ctx.Character); ctx.ActiveJob = job;
                    Dialogue(job.Employer, "\"Good. Don't make me regret it.\"" + (job.Advance > 0 ? $" {job.Advance} pennies pressed into your hand." : ""), ("Leave", null));
                }));
            }
            opts.Add(("Nothing today", null));
            Dialogue("Work in " + s.Name, "The alewife knows who is hiring.", opts.ToArray());
        }

        private void DeliverJob(Settlement s)
        {
            var ctx = RunSession.Context; var job = ctx.ActiveJob;
            int pay = JobBoard.Complete(job, ctx.Character, JobBoard.Today(clock.Calendar), ctx.Standing);
            ctx.Reputation.Record(pay > 0 ? Deed.HonouredContract : Deed.BrokeContract);
            if (pay > 0) ctx.Character.Use(Skill.Haggling, _balance.character, 0.5f);
            Dialogue("At " + s.Name, pay > 0 ? $"You hand over {job.Employer}'s business and are paid {pay} pennies." : "You arrive too late. Nobody pays for yesterday's errand, and they will remember it.", ("...", null));
            ctx.ActiveJob = null;
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void ServeDay()
        {
            var ctx = RunSession.Context; var job = ctx.ActiveJob; if (job == null) return;
            job.DaysServed++;
            ctx.Needs.Work(10f); ctx.Needs.Eat(0.35f); // bread and pottage with the work
            if (job.Kind == JobKind.GarrisonDuty) ctx.Character.Use(Skill.Polearm, _balance.character, 0.5f); else ctx.Character.Use(Skill.Farming, _balance.character, 0.3f);
            clock.SkipHours(24f);
            if (job.DaysServed >= job.DaysRequired)
            {
                int pay = JobBoard.Complete(job, ctx.Character, JobBoard.Today(clock.Calendar), ctx.Standing);
                ctx.Reputation.Record(Deed.HonouredContract);
                Dialogue("Paid off", $"The days are served. {pay} pennies.", ("...", null));
                ctx.ActiveJob = null;
            }
            else Dialogue("Another day", $"Day {job.DaysServed} of {job.DaysRequired}. Bread, a fire, and the long hours.", ("...", null));
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void Steal()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            var rng = GameBootstrap.Instance.Rng.Fork(104); rng.State ^= (ulong)clock.Calendar.TotalHours;
            int witnesses = rng.Range(0, 5);
            int take = rng.Range(1, 7);
            ctx.Character.Coins += take; ctx.Character.Use(Skill.Stealth, _balance.character);
            float wanted = ctx.Crime.Commit(CrimeKind.Theft, s != null ? s.Id : "road", JobBoard.Today(clock.Calendar), witnesses, rng, ctx.Reputation);
            if (s != null) s.WantedHere = Mathf.Min(1f, s.WantedHere + wanted);
            if (wanted > 0f) ctx.Standing.TheftSeen();
            Dialogue("A purse", $"{take} pennies. " + (wanted > 0f ? "Someone saw. The room goes quiet." : "Nobody saw."), ("Leave quickly", () => { if (wanted > 0f && s != null) LawCheck(s); }));
        }

        public static Settlement NearestHostileCastle(RunContext ctx, Allegiance side)
        {
            Settlement best = null; float bestD = float.MaxValue; var here = ctx.Current;
            foreach (var t in ctx.Settlements)
            {
                if (t.Type != SettlementType.Castle || t.Allegiance == Allegiance.Neutral || t.Allegiance == side || t.LordId == CastleRules.PlayerLordId) continue;
                float d = here != null ? (t.Nx - here.Nx) * (t.Nx - here.Nx) + (t.Nz - here.Nz) * (t.Nz - here.Nz) : 0f;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private void RaiseCastle()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            if (!CastleRules.CanStart(ctx.Character, s, ctx.Castle, _balance.siege, out string why)) { Dialogue("The ditch", why, ("...", null)); return; }
            string name = ctx.Character.Name + "'s Tower";
            Dialogue("A castle", $"A motte here, a palisade, a tower: {_balance.siege.motteCost + _balance.siege.palisadeCost + _balance.siege.towerCost} pennies over two months, if nobody objects. Nobody will license it. You must be here to see it dug.",
                ($"Dig. Call it {name}", () => { ctx.Castle = CastleRules.Start(name, s); hud.Toast("The first spade goes in."); saveManager?.SaveNow(AffairsChoice.None); }),
                ("Not yet", null));
        }

        private void HireGarrison()
        {
            var ctx = RunSession.Context; int cost = 10 * _balance.siege.garrisonHirePennies;
            if (ctx.Character.Coins < cost) { hud.Toast("Not enough silver."); return; }
            ctx.Character.Coins -= cost; ctx.Garrison += 10; hud.Toast($"Ten men with spears and no questions. Garrison: {ctx.Garrison}.");
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private MercenaryCompany EnsureCompany(Settlement s)
        {
            var ctx = RunSession.Context;
            var existing = ctx.CompanyAt(s.Id); if (existing != null) return existing;
            var rng = GameBootstrap.Instance.Rng.Fork(121); rng.State ^= MapGeometry.StableHash(s.Id) ^ (ulong)(clock.Calendar.TotalHours / 24.0 / 30.0);
            if (!rng.Chance(0.6f)) return null;
            var co = ServiceRules.GenerateCompany(s.Id, s.Allegiance == Allegiance.Neutral ? (rng.Chance(0.5f) ? Allegiance.Stephen : Allegiance.Matilda) : s.Allegiance, new NameGenerator(ctx.Names, rng.Fork(2)), rng, _balance.service);
            ctx.Companies.Add(co);
            return co;
        }

        private void Enlist(MercenaryCompany co)
        {
            var ctx = RunSession.Context;
            ctx.Service.Enlist(ServiceRole.Mercenary, co.Name, co.Captain, co.Employer, _balance.service.mercenaryWagePerDay);
            if (co.Employer == Allegiance.Stephen) ctx.Reputation.Record(Deed.FoughtForStephen); else ctx.Reputation.Record(Deed.FoughtForMatilda);
            survival.InCamp = true;
            Dialogue(co.Captain, $"\"Three pennies a day when there is silver, a share of what we take, and you do what you are told. Welcome to {co.Name}.\" You are a sellsword now.", ("...", null));
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void CampDay()
        {
            var ctx = RunSession.Context;
            var rng = GameBootstrap.Instance.Rng.Fork(122); rng.State ^= (ulong)clock.Calendar.TotalHours;
            string t = ServiceRules.CampDay(ctx.Service, ctx.Character, ctx.Needs, rng, _balance.service);
            survival.InCamp = true;
            clock.SkipHours(24f);
            if (vitals.IsDead) return;
            Dialogue("Camp", t + " Dice, rumour, and a man two fires over coughing all night.", ("...", null));
            saveManager?.SaveNow(AffairsChoice.None);
        }

        public void DesertService()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            var rng = GameBootstrap.Instance.Rng.Fork(123); rng.State ^= (ulong)clock.Calendar.TotalHours;
            ctx.Service.Desert(ctx.Crime, s != null ? s.Id : "camp", JobBoard.Today(clock.Calendar), ctx.Reputation, rng);
            if (s != null) s.WantedHere = Mathf.Min(1f, s.WantedHere + CrimeRecord.Severity(CrimeKind.Desertion));
            survival.InCamp = false;
            Dialogue("Gone", "You walk out past the pickets with a story about firewood. By morning you are a deserter, and the provost hangs deserters.", ("Keep walking", null));
        }

        private void MarchToBattle()
        {
            var ctx = RunSession.Context;
            RunSession.PendingBattle = new BattleLaunch { AsGrunt = true, Side = ctx.Service.Side, CompanyName = ctx.Service.CompanyName, CompanySize = 60 };
            saveManager?.SaveNow(AffairsChoice.None);
            CarryAcrossScenes();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Phase0_Battlefield");
        }

        /// <summary>Stash what the next scene cannot read from the save: the clock and the body.</summary>
        public void CarryAcrossScenes()
        {
            if (clock != null && clock.IsReady) { RunSession.CarriedTotalHours = clock.Calendar.TotalHours; RunSession.CarriedWeather = clock.Weather.Export(); RunSession.CarriedWeatherRng = clock.Weather.RngState; }
            if (vitals != null && vitals.Body != null) RunSession.CarriedBody = vitals.Body.Export();
        }

        /// <summary>Called by the levy event: armed men at the door.</summary>
        public void LevyAtTheDoor(Settlement s)
        {
            var ctx = RunSession.Context;
            MinorLord lord = null; foreach (var l in ctx.Baronage) if (l.Id == s.LordId) lord = l;
            string lordName = lord != null ? lord.FullName : "the king's sheriff";
            Allegiance side = lord != null && lord.Declared != Allegiance.Neutral ? lord.Declared : Allegiance.Stephen;
            Dialogue("Armed men at the door", $"A sergeant with {lordName}'s badge and six spearmen. \"The lord has a quota. You, and the two next door.\" You can go, you can pay {_balance.service.scutagePennies} pennies for another man to go, or you can run.",
                ("Go with them", () => Levy(LevyChoice.Comply, s, lordName, side)),
                ($"Pay {_balance.service.scutagePennies} pennies (scutage)", () => Levy(LevyChoice.PayScutage, s, lordName, side)),
                ("Run", () => Levy(LevyChoice.Flee, s, lordName, side)));
        }

        private void Levy(LevyChoice choice, Settlement s, string lordName, Allegiance side)
        {
            var ctx = RunSession.Context;
            var rng = GameBootstrap.Instance.Rng.Fork(124); rng.State ^= (ulong)clock.Calendar.TotalHours;
            string t = ServiceRules.ResolveLevy(choice, ctx.Character, ctx.Service, side, lordName, ctx.Crime, ctx.Reputation, s.Id, JobBoard.Today(clock.Calendar), _balance.service, rng, out bool leave);
            if (ctx.Service.Deserter) s.WantedHere = Mathf.Min(1f, s.WantedHere + CrimeRecord.Severity(CrimeKind.Desertion));
            survival.InCamp = ctx.Service.Active;
            Dialogue(lordName + "'s levy", t + (ctx.Service.Active ? " Report to the muster at the alehouse when you are ready, or they will come again." : string.Empty), ("...", null));
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void Audience()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            MinorLord lord = null; foreach (var l in ctx.Baronage) if (l.Id == s.LordId) lord = l;
            if (lord == null || lordship == null) { AudienceKnighthood(); return; }
            var opts = new List<(string, System.Action)> { ("Ask for knighthood, or a command", AudienceKnighthood) };
            bool hasDaughter = (lord.Id % 3) != 0 && lord.Alive;
            var suit = ctx.Suit;
            if (ServiceGrantRules.Eligible(ctx.Service, _balance.lordship) && ctx.Service.PatronId == lord.Id.ToString() && ctx.Estate == null)
                opts.Add(("Ask for land for your service", () =>
                {
                    var manor = NearestUnheldVillage(ctx, lord);
                    if (manor == null) { Dialogue(lord.FullName, "\"I have nothing to give that is not already given.\"", ("...", null)); return; }
                    Dialogue(lord.FullName, $"\"You have bled for me. {manor.Name} is yours, and its villeins, and its troubles. Kneel.\" You swear on his sword and rise a landed man.", ("Rise", () => lordship.Grant(manor, "by " + lord.FullName + "'s grant.")));
                }));
            if (hasDaughter && (suit == null || suit.FatherLordId != lord.Id) && !(suit != null && suit.Married))
                opts.Add(("Pay court to his daughter", () => { ctx.Suit = SuitRules.Begin(lord, new NameGenerator(ctx.Names, GameBootstrap.Instance.Rng.Fork(161)), NearestUnheldVillage(ctx, lord)?.Id ?? s.Id); Visit(); }));
            else if (suit != null && suit.FatherLordId == lord.Id && !suit.Married)
            {
                opts.Add(($"Visit {suit.Heiress}", Visit));
                opts.Add(($"Ask for {suit.Heiress}'s hand", () =>
                {
                    bool ok = SuitRules.Propose(suit, ctx.Character, ctx.Service.Renown, _balance.lordship, out string text);
                    if (!ok) { Dialogue(lord.FullName, text, ("...", null)); return; }
                    Dialogue(lord.FullName, text, ("Marry her", () =>
                    {
                        var rng = GameBootstrap.Instance.Rng.Fork(162); rng.State ^= (ulong)clock.Calendar.TotalHours;
                        var wife = SuitRules.Wed(suit, ctx.NextNpcId++, lord.Declared, rng); ctx.Family.Add(wife);
                        ctx.Character.Coins += _balance.lordship.heiressDowryPennies;
                        var dowry = ctx.FindSettlement(suit.DowrySettlementId);
                        if (dowry != null && ctx.Estate == null) lordship.Grant(dowry, "by marriage."); else hud.Toast("Married. Her dowry is silver and her father's goodwill.");
                        if (ctx.Lordship.Declared == Allegiance.Neutral) ctx.Lordship.Declared = lord.Declared;
                        saveManager?.SaveNow(AffairsChoice.None);
                    }));
                }));
            }
            opts.Add(("Leave", null));
            Dialogue(lord.FullName + "'s hall", $"The hall smells of dogs and wet wool. {lord.FullName} looks up from his accounts.", opts.ToArray());
        }

        private void Visit()
        {
            var ctx = RunSession.Context; var suit = ctx.Suit; if (suit == null) return;
            var rng = GameBootstrap.Instance.Rng.Fork(163); rng.State ^= (ulong)clock.Calendar.TotalHours;
            string t = SuitRules.Visit(suit, ctx.Character, ctx.Standing.Value, rng, _balance.lordship);
            ctx.Character.Use(Skill.Leadership, _balance.character);
            clock.SkipHours(3f);
            saveManager?.SaveNow(AffairsChoice.None);
            Dialogue(suit.Heiress, t + (suit.Favour >= _balance.lordship.suitConsentFavour ? " You could ask her father now." : string.Empty), ("...", null));
        }

        private static Settlement NearestUnheldVillage(RunContext ctx, MinorLord lord)
        {
            Settlement best = null; float bestD = float.MaxValue;
            foreach (var t in ctx.Settlements)
            {
                if (t.Type != SettlementType.Village || t.LordId == CastleRules.PlayerLordId) continue;
                float d = (t.Nx - lord.Nx) * (t.Nx - lord.Nx) + (t.Nz - lord.Nz) * (t.Nz - lord.Nz);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private void AudienceKnighthood()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            MinorLord lord = null; foreach (var l in ctx.Baronage) if (l.Id == s.LordId) lord = l;
            string lordName = lord != null ? lord.FullName : s.Name + "'s castellan";
            var sv = ctx.Service;
            if (sv.PatronId != (lord != null ? lord.Id.ToString() : s.Id)) { sv.PatronId = lord != null ? lord.Id.ToString() : s.Id; sv.PatronFavour = 0f; }
            float favour = Mathf.Clamp01(sv.Renown / (float)_balance.service.knighthoodRenown * 0.6f + Mathf.Max(0f, ctx.Reputation.Get(s.Allegiance == Allegiance.Matilda ? Faction.Angevin : Faction.Crown)) * 0.5f + ctx.Standing.Value * 0.2f - (ctx.Crime.Outlawed ? 1f : 0f));
            sv.PatronFavour = Mathf.Max(sv.PatronFavour, favour);
            if (ctx.Character.Rank >= Rank.Knight && siege != null && ctx.Siege == null && sv.PatronFavour >= _balance.service.patronDispositionNeeded && lord != null)
            {
                var target = NearestHostileCastle(ctx, s.Allegiance == Allegiance.Neutral ? Allegiance.Stephen : s.Allegiance);
                if (target != null)
                {
                    int men = _balance.siege.commandBaseMen + lord.Knights * _balance.siege.commandMenPerKnight;
                    Dialogue(lordName, $"\"Sir. {target.Name} sits on my road and flies the wrong banner. Take {men} men and bring me its keys. Starve it or storm it; I want it before the harvest.\"",
                        ("Take the command", () => { sv.PatronId = lord.Id.ToString(); siege.BeginAsCommander(target, s.Allegiance == Allegiance.Neutral ? Allegiance.Stephen : s.Allegiance, men); hud.Toast($"You command {men} men."); }),
                        ("Decline", () => { sv.PatronFavour = Mathf.Max(0f, sv.PatronFavour - 0.1f); }));
                    return;
                }
            }
            if (ctx.Character.Rank >= Rank.Knight) { Dialogue(lordName, "\"Sir. What do you want?\" He is busy, and you are already what you are.", ("Leave", null)); return; }
            bool ok = ServiceRules.KnighthoodCheck(sv, ctx.Inventory, _balance.service, out string missing);
            if (ok)
            {
                Dialogue(lordName, $"\"Kneel.\" The flat of a sword on your shoulder in a cold hall with twenty men watching. \"Rise, Sir {ctx.Character.Name}.\" You are a knight of {lordName}'s household now, fed at his table and always on call.",
                    ("Rise", () => { ctx.Character.Rank = Rank.Knight; sv.Enlist(ServiceRole.Household, lordName + "'s household", lordName, s.Allegiance == Allegiance.Neutral ? Allegiance.Stephen : s.Allegiance, _balance.service.householdWagePerDay); ctx.Standing.Generosity(); hud.Toast("You are a knight."); saveManager?.SaveNow(AffairsChoice.None); }));
            }
            else
            {
                string tone = sv.PatronFavour >= _balance.service.patronDispositionNeeded ? "\"I know your name. Come back when you are equipped like a knight.\"" : sv.Renown > 0 ? "\"I have heard something of you. Not enough.\"" : "\"Who let this man in?\"";
                Dialogue(lordName, tone + "\n\nStill wanting: " + missing + ".", ("Leave", null));
            }
        }

        private string Rumour()
        {
            var ctx = RunSession.Context; var rng = GameBootstrap.Instance.Rng.Fork(61); rng.State ^= (ulong)clock.Calendar.TotalHours;
            if (ctx.Baronage.Count > 0 && rng.Chance(0.6f))
            {
                var l = ctx.Baronage[rng.Range(0, ctx.Baronage.Count)];
                return rng.Chance(0.5f) ? $"They say {l.FullName} of {l.SeatName} has {l.Knights} knights and is {l.TraitA}. He declared for {(l.Declared == Allegiance.Neutral ? "nobody yet" : l.Declared.ToString())}."
                                        : $"A carter swears {l.FullName} feasted messengers from both sides in one week.";
            }
            string[] generic = { "The king has let the Scots keep Carlisle. Men laugh and do not laugh.", "Bread will be dear by Candlemas, the alewife says, and she is usually right.", "A company of Flemings passed on the Oxford road. They paid for nothing.", "The empress is in Normandy with her husband, who nobody likes.", "Bishop Henry sits in Winchester with the treasury and smiles at everyone." };
            return generic[rng.Range(0, generic.Length)];
        }

        private void Home()
        {
            var ctx = RunSession.Context;
            Dialogue("Your hut", $"A hearth, a pallet, a chest. Hunger {Mathf.RoundToInt(ctx.Needs.Hunger * 100)}%, rest {Mathf.RoundToInt(ctx.Needs.Rest * 100)}%.",
                ("Lie down until Prime", SleepUntilPrime),
                ("Sit and eat", EatAtHearth),
                ("Clean and bind your wounds", () => { int n = vitals.Body.TreatAll(); ctx.Character.Use(Skill.Medicine, _balance.character, n > 0 ? 1f : 0.2f); clock.SkipHours(0.5f); Dialogue("Wounds", n > 0 ? $"You wash and bind {n} wound{(n == 1 ? "" : "s")} with what linen you have." : "Nothing needs binding.", ("Done", null)); }),
                ("Leave", null));
        }

        private void SleepUntilPrime()
        {
            var ctx = RunSession.Context; var cal = clock.Calendar;
            float hours = cal.HourOfDay < 6f ? 6f - cal.HourOfDay : 30f - cal.HourOfDay;
            if (activity != null)
            {
                _busy = true; _openKind = Open.Moment;
                survival.RestingAtHome = true;
                Transform bed = BuildingKit.Find(home, "Bed");
                activity.BeginRest("You lie down", "E to wake before Prime", bed, RestPose.Lying, hours, _balance.activity.sleepTimeScale, true, (slept, early) =>
                {
                    _busy = false; _openKind = Open.None;
                    survival.RestingAtHome = false;
                    ctx.Needs.Sleep(slept); vitals.Body.Rest(slept);
                    if (vitals.IsDead) return;
                    saveManager?.SaveNow(AffairsChoice.None);
                    string woke = early ? $"You wake in the dark after {slept:0.#} hours and lie listening to the rain." : $"The bells ring Prime. {clock.Calendar.LongDate()}. " + (ctx.Disease.Current != Sickness.None ? "You wake sweating and weak." : "You wake hungry.");
                    Dialogue(early ? "Night" : "Morning", woke, ("Rise", null));
                });
                return;
            }
            survival.RestingAtHome = true;
            ctx.Needs.Sleep(hours);
            vitals.Body.Rest(hours);
            clock.SkipHours(hours);
            survival.RestingAtHome = false;
            if (vitals.IsDead) return;
            saveManager?.SaveNow(AffairsChoice.None);
            Dialogue("Morning", $"The bells ring Prime. {clock.Calendar.LongDate()}. " + (ctx.Disease.Current != Sickness.None ? "You wake sweating and weak." : "You wake hungry."), ("Rise", null));
        }

        /// <summary>A proper meal at your own hearth: you sit, it takes a while, it does you more good than bread on the road.</summary>
        private void EatAtHearth()
        {
            var ctx = RunSession.Context;
            string best = BestFood(ctx, out float n);
            if (best == null) { hud.Toast("You have nothing to eat."); return; }
            if (activity == null) { EatSomething(); return; }
            _busy = true; _openKind = Open.Moment;
            Transform hearth = BuildingKit.Find(home, "Hearth");
            activity.BeginRest("A meal by the fire", "E to leave the rest", hearth, RestPose.Sitting, 0.5f, _balance.activity.mealTimeScale, true, (hours, early) =>
            {
                _busy = false; _openKind = Open.None;
                ctx.Inventory.Remove(best, 1); ctx.Needs.Eat(n * (early ? 0.8f : 1.15f));
                hud.Toast(early ? "You bolt the " + ctx.Items.Find(best).name.ToLowerInvariant() + " and go." : "You eat " + ctx.Items.Find(best).name.ToLowerInvariant() + " slowly, by your own fire.");
            });
        }

        private static string BestFood(RunContext ctx, out float bestN)
        {
            string best = null; bestN = 0f;
            for (int i = 0; i < ctx.Inventory.DistinctCount; i++)
            {
                var def = ctx.Items.Find(ctx.Inventory.IdAt(i));
                if (def != null && def.nutrition > bestN) { bestN = def.nutrition; best = def.id; }
            }
            return best;
        }

        private void EatSomething()
        {
            var ctx = RunSession.Context;
            string best = null; float bestN = 0f;
            for (int i = 0; i < ctx.Inventory.DistinctCount; i++)
            {
                var def = ctx.Items.Find(ctx.Inventory.IdAt(i));
                if (def != null && def.nutrition > bestN) { bestN = def.nutrition; best = def.id; }
            }
            if (best == null) { hud.Toast("You have nothing to eat."); return; }
            ctx.Inventory.Remove(best, 1); ctx.Needs.Eat(bestN);
            hud.Toast("You eat " + ctx.Items.Find(best).name.ToLowerInvariant() + ".");
        }

        private void ShowInventory()
        {
            var ctx = RunSession.Context; _sb.Clear();
            _sb.Append(ctx.Character.Coins).Append(" pennies\n\n");
            if (ctx.Inventory.DistinctCount == 0) _sb.Append("Nothing but the clothes you stand in.");
            for (int i = 0; i < ctx.Inventory.DistinctCount; i++)
            {
                var def = ctx.Items.Find(ctx.Inventory.IdAt(i));
                _sb.Append(ctx.Inventory.CountAt(i)).Append(" x ").Append(def != null ? def.name : ctx.Inventory.IdAt(i)).Append('\n');
            }
            _sb.Append("\nG eats the best food you carry.");
            Dialogue("What you carry", _sb.ToString(), ("Close", null));
        }

        // ------------------------------------------------------------------ work

        private void WorkTheDay()
        {
            var ctx = RunSession.Context;
            string tradeId = ctx.Apprenticeship.Active ? ctx.Apprenticeship.Trade : ctx.Character.Trade;
            TradeDef def = string.IsNullOrEmpty(tradeId) ? null : ctx.Trades.Find(tradeId);
            if (def == null) { Dialogue("Work", "You have no trade here. Hunting is open to all: speak to the hunter.", ("Leave", null)); return; }
            if (clock.Calendar.HourOfDay > 14f) { Dialogue("Work", "Too late in the day to start. Sleep, and begin at Prime.", ("Leave", null)); return; }
            if (Festivals.FeastOn(clock.Calendar) != Feast.None && ctx.Standing.Value > 0.35f) { Dialogue("Work", "Nobody works on a holy day, not where the priest can see. The village would talk.", ("Work anyway (the village will talk)", () => StartWork(def, true)), ("Leave", null)); return; }
            StartWork(def, false);
        }

        private void StartWork(TradeDef def, bool onHolyDay)
        {
            _busy = true; _openKind = Open.Moment; _options.Clear();
            System.Action<float> finish = quality =>
            {
                _busy = false; _openKind = Open.None;
                var ctx = RunSession.Context;
                float weatherPenalty = clock.Weather.Current == WeatherKind.Rain ? 0.2f : clock.Weather.Current == WeatherKind.Snow ? 0.4f : 0f;
                if (def.id == "blacksmith" || def.id == "brewer" || def.id == "carpenter" || def.id == "miller") weatherPenalty *= 0.25f;
                var rng = GameBootstrap.Instance.Rng.Fork(71); rng.State ^= (ulong)clock.Calendar.TotalHours;
                bool apprentice = ctx.Apprenticeship.Active;
                var r = TradeWork.WorkDay(def, ctx.Character, ctx.Inventory, ctx.Needs, clock.Calendar.Month, weatherPenalty, quality, _balance.character, vitals.Body, rng, 10f, apprentice);
                if (apprentice && ctx.Apprenticeship.WorkedDay(quality)) { ctx.Character.Trade = ctx.Apprenticeship.Trade; ctx.Apprenticeship.Trade = string.Empty; r.Note += " Your master calls you a " + ctx.Character.Trade + " now."; }
                if (onHolyDay) ctx.Standing.BrawlStarted();
                if (activity == null) clock.SkipHours(10f);   // the activity already ran the hours
                if (vitals.IsDead) return;
                var prod = ctx.Items.Find(r.Product);
                string made = r.UnitsProduced > 0 ? $"{r.UnitsProduced} x {(prod != null ? prod.name : r.Product)}" : "nothing worth the name";
                Dialogue("The day's work", $"Ten hours at the {def.id}'s work ({(quality > 0.75f ? "good hands today" : quality > 0.4f ? "steady" : "clumsy")}). You made {made}. {r.Note}{(r.Injured ? " You hurt yourself." : "")}", ("Done", null));
                saveManager?.SaveNow(AffairsChoice.None);
            };
            if (activity != null) activity.BeginWork(def, 10f, finish);
            else hud.ShowMoment(def.momentKind, def.id, finish);
        }

        /// <summary>Two hours at an oak with an axe: beats on the stroke, bundles of firewood by the quality. Anyone's chore.</summary>
        private void CutWood(Transform oak)
        {
            var ctx = RunSession.Context;
            if (ctx.Inventory.Count("axe") <= 0) { Dialogue("The oak", "Bark like iron. You have no axe.", ("Leave", null)); return; }
            if (activity == null) { ctx.Inventory.Add("firewood", 2); clock.SkipHours(2f); hud.Toast("Two hours of chopping. Two bundles."); return; }
            if (clock.Calendar.HourOfDay > 19f || clock.Calendar.HourOfDay < 5f) { Dialogue("The oak", "Too dark to swing an axe without losing a foot.", ("Leave", null)); return; }
            _busy = true; _openKind = Open.Moment;
            Vector3 face = oak.position - player.transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            var def = new TradeDef { id = "woodcutting", momentKind = "rhythm" };
            activity.BeginWork(def, 2f, quality =>
            {
                _busy = false; _openKind = Open.None;
                int bundles = 1 + Mathf.RoundToInt(quality * 4f);
                ctx.Inventory.Add("firewood", bundles);
                ctx.Character.Use(Skill.Carpentry, _balance.character, 0.5f);
                bool hurt = quality < 0.2f && GameBootstrap.Instance.Rng.Fork(77).Chance(0.3f);
                if (hurt) vitals.Body.ApplyHit(BodyPart.LeftLeg, 0.08f, "your own axe");
                Dialogue("Firewood", $"Two hours of swinging. {bundles} bundle{(bundles == 1 ? "" : "s")} of split oak, a penny each at any market, and a back that knows about it." + (hurt ? " The axe glanced and opened your shin." : ""), ("Done", null));
                saveManager?.SaveNow(AffairsChoice.None);
            });
        }

        // ------------------------------------------------------------------ masters and apprenticeship

        private string MasterName(Transform t)
        {
            var tag = t != null ? t.GetComponent<VillagerTag>() : null;
            return tag != null ? (tag.trade == "priest" ? "Father " + tag.displayName : tag.displayName + " the " + tag.trade) : "the master";
        }

        private void Master(Transform t)
        {
            var tag = t.GetComponent<VillagerTag>(); if (tag == null) return;
            var ctx = RunSession.Context;
            var v = t.GetComponent<Villager>();
            if (v != null && !v.WillTalk) { Dialogue(MasterName(t), $"{tag.displayName} is {v.Doing}.", ("Leave them", null)); return; }
            if (v != null && tag.trade == "priest")
            {
                Dialogue("Father " + tag.displayName, $"The priest is {v.Doing}. \"God keep you. Mass is sung on Sundays and feasts between Terce and Sext; the altar is open to any who kneel.\"", ("Confess (half an hour)", () => { clock.SkipHours(0.5f); ctx.Standing.Prayed(0.01f); Dialogue("Confession", "You tell him the smaller sins. He gives you three paternosters and a look.", ("Leave", null)); }), ("Leave", null));
                return;
            }
            string doing = v != null ? $"{tag.displayName} is {v.Doing}. " : string.Empty;
            if (ctx.Apprenticeship.Active && ctx.Apprenticeship.Trade == tag.trade)
            { Dialogue(MasterName(t), $"\"Back to work. {ctx.Apprenticeship.SeasonsRemaining:F1} seasons and you might be worth your bread.\"", ("Yes, master", null)); return; }
            if (ctx.Character.Trade == tag.trade) { Dialogue(MasterName(t), doing + "\"Weather for it, isn't it.\" You talk shop for a while.", ("Leave", null)); return; }
            Dialogue(MasterName(t), doing + $"\"{Greeting(tag)}\"",
                ("Ask to be taken on as an apprentice", () =>
                {
                    var rng = GameBootstrap.Instance.Rng.Fork(81); rng.State ^= (ulong)clock.Calendar.TotalHours;
                    bool ok = Apprenticeship.TryConvince(ctx.Character, tag.disposition, ctx.Standing.Value, rng, out string reply);
                    if (ok) ctx.Apprenticeship.Begin(tag.trade, tag.npcId, 2f);
                    ctx.Character.Use(Skill.Haggling, _balance.character, 0.3f);
                    Dialogue(MasterName(t), "\"" + reply + "\"" + (ok ? "\n\nYou are apprenticed. Half pay, two seasons, and you do as you are told." : ""), ("Leave", null));
                }),
                ("Leave", null));
        }

        private static string Greeting(VillagerTag tag)
        {
            switch (tag.trade)
            {
                case "blacksmith": return "Mind the sparks. What do you want?";
                case "miller": return "Everyone owes the mill. What is it?";
                case "hunter": return "Keep your voice down. The deer have ears.";
                case "brewer": return "If it is about the ale, it was fine when it left here.";
                case "carpenter": return "Measure twice. Speak once.";
                case "shepherd": return "The dog likes you. That is rare.";
                case "fisherman": return "The weir wants mending again. What?";
                default: return "The ground does not plough itself. Quickly.";
            }
        }

        // ------------------------------------------------------------------ market

        private void OpenMarket(bool buying)
        {
            var ctx = RunSession.Context; int month = clock.Calendar.Month; float standing = ctx.Standing.Value;
            Market market = ctx.Current != null && ctx.Current.Market != null ? ctx.Current.Market : ctx.Market;
            _busy = true; _openKind = Open.Market; _options.Clear();
            hud.ShowMarket(market, ctx.Items, ctx.Inventory, month, standing,
                onBuy: (id, haggled) =>
                {
                    int asking = market.BuyPrice(id, month, standing);
                    if (haggled)
                    {
                        var rng = GameBootstrap.Instance.Rng.Fork(91); rng.State ^= (ulong)(clock.Calendar.TotalHours * 7919) ^ (ulong)MapGeometry.StableHash(id);
                        bool ok = market.Haggle(0.8f, true, ctx.Character.Get(Skill.Haggling), standing, rng);
                        ctx.Character.Use(Skill.Haggling, _balance.character, ok ? 1f : 0.4f);
                        if (ok) { int price = Mathf.Max(1, Mathf.RoundToInt(asking * 0.8f)); var good = market.Find(id); if (ctx.Character.Coins >= price && good != null && good.Stock >= 1f) { ctx.Character.Coins -= price; good.Stock -= 1f; ctx.Inventory.Add(id); return "Grudgingly, for " + price + "."; } return "Agreed, but you cannot pay."; }
                        return "\"Away with you.\" The price stands.";
                    }
                    return market.Buy(id, 1, month, standing, ctx.Character, ctx.Inventory, out int paid) ? "Bought for " + paid + "." : "You cannot afford it, or there is none.";
                },
                onSell: id =>
                {
                    return market.Sell(id, 1, month, standing, ctx.Character, ctx.Inventory, out int earned) ? "Sold for " + earned + "." : "You have none to sell.";
                },
                onClose: () => { _busy = false; _openKind = Open.None; melee?.RefreshLoadout(); saveManager?.SaveNow(AffairsChoice.None); });
        }

        // ------------------------------------------------------------------ dialogue

        private void Dialogue(string title, string body, params (string label, System.Action act)[] options)
        {
            _options.Clear(); _options.AddRange(options);
            _busy = true; _openKind = Open.Dialogue;
            hud.ShowDialogue(title, body, options, i => { var act = _options[i].act; CloseDialogue(); act?.Invoke(); });
        }

        private void CloseDialogue() { _busy = false; _openKind = Open.None; _options.Clear(); hud.HideDialogue(); }
    }
}
