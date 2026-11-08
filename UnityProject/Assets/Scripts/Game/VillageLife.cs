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
        private enum Open : byte { None, Dialogue, Market, Moment }
        private Open _openKind;
        public float useRadius = 4f;
        private SettlementSite _site; private float _nextSiteScan; private string _lastSiteId = string.Empty;
        public SettlementSite CurrentSite => _site;

        private Balance _balance;
        private string _prompt = string.Empty;
        private float _nextPrompt;
        private bool _busy;                 // a panel or moment is open
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly List<(string label, System.Action act)> _options = new List<(string, System.Action)>(6);
        private Transform _nearest; private string _nearestKind = string.Empty;

        public bool IsBusy => _busy;
        /// <summary>A usable place is nearer than the horse, so E uses it instead of mounting.</summary>
        public bool HasPlaceInReach => _nearest != null && !(horse != null && !horse.Mounted && Vector3.Distance(player.transform.position, horse.transform.position) <= _balance.player.mountRadius && _nearestKind != "home" && _nearestKind != "church");

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (ctx == null || ctx.Needs == null || vitals == null || vitals.IsDead) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (narration != null && narration.Running) return;
            vitals.StaminaRegenScale = (ctx.Needs.Hungry ? 0.4f : 1f) * (ctx.Disease.Current != Sickness.None ? 0.5f : 1f);
            var kb = Keyboard.current; if (kb == null) return;

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

            if (Time.unscaledTime >= _nextSiteScan) { _nextSiteScan = Time.unscaledTime + 1f; ScanSite(); }
            FindNearest();
            if (Time.unscaledTime >= _nextPrompt) { _nextPrompt = Time.unscaledTime + 0.25f; hud.SetPrompt(_nearest != null ? "[E] " + PromptFor(_nearestKind) : string.Empty); UpdateHudLines(ctx); }
            if (HasPlaceInReach && GameBootstrap.Instance.Input.Mount.WasPressedThisFrame()) Use(_nearestKind, _nearest);
        }

        private static bool DigitPressed(Keyboard kb, int i)
        {
            switch (i) { case 0: return kb.digit1Key.wasPressedThisFrame; case 1: return kb.digit2Key.wasPressedThisFrame; case 2: return kb.digit3Key.wasPressedThisFrame; case 3: return kb.digit4Key.wasPressedThisFrame; case 4: return kb.digit5Key.wasPressedThisFrame; default: return kb.digit6Key.wasPressedThisFrame; }
        }

        private Transform Body() => horse != null && horse.Mounted ? horse.transform : player.transform;

        private void FindNearest()
        {
            _nearest = null; float best = useRadius;
            Check(church, "church", ref best); Check(alehouse, "alehouse", ref best); Check(home, "home", ref best); Check(workplace, "work", ref best);
            if (mastersRoot != null) foreach (Transform m in mastersRoot) Check(m, "master", ref best);
        }

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
                    hud.Toast("You come to " + s.Name + (s.Allegiance == Allegiance.Neutral ? "." : ", which holds for " + s.Allegiance + "."));
                    if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && ctx.ActiveJob.ToId == s.Id && ctx.ActiveJob.DaysRequired == 0) DeliverJob(s);
                    LawCheck(s);
                }
            }
        }

        /// <summary>Arriving somewhere you are wanted: the law may take you.</summary>
        private void LawCheck(Settlement s)
        {
            var ctx = RunSession.Context;
            if (s.WantedHere <= 0f && !ctx.Crime.Outlawed) return;
            var rng = GameBootstrap.Instance.Rng.Fork(101); rng.State ^= (ulong)clock.Calendar.TotalHours;
            bool inChurch = church != null && Vector3.Distance(Body().position, church.position) < 10f;
            if (ctx.Crime.TickDay(s, inChurch, rng)) Arrest(s);
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
            Dialogue("Taken by the law", $"Men with cudgels at the alehouse door. You are tried in {s.Name} for {crime} and {CrimeRecord.Describe(sentence)}.\n\n{outcome}", ("...", () =>
            {
                if (CrimeRecord.IsExecution(sentence)) { vitals.Body.Drain(1f, sentence == Sentence.Hanging ? "the noose" : "the block"); vitals.NotifyDeadFromBody(); }
            }));
        }

        private void Check(Transform t, string kind, ref float best)
        {
            if (t == null) return;
            Vector3 p = Body().position; Vector3 q = t.position; q.y = p.y;
            float d = Vector3.Distance(p, q) - (kind == "church" ? 8f : kind == "alehouse" ? 6f : 0f);
            if (d < best) { best = d; _nearest = t; _nearestKind = kind; }
        }

        private string PromptFor(string kind)
        {
            switch (kind)
            {
                case "church": return "Enter the church";
                case "alehouse": return "Enter the alehouse";
                case "home": return "Your hut";
                case "work": return "Work the day";
                case "master": return "Speak with " + MasterName(_nearest);
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
                ctx.LastMassDay = today; ctx.Standing.AttendedMass();
                Feast f = Festivals.FeastOn(cal);
                clock.SkipHours(1.5f);
                Dialogue("Mass", $"You stand at the back with the village while the priest sings the {Festivals.Name(f)} mass in a Latin nobody follows. People saw you come. ({ctx.Standing.Describe()})", ("Go out into the cold", null));
            }
            else
            {
                Feast f = Festivals.FeastOn(cal);
                string next = f != Feast.None ? "Mass is sung between Terce and Sext." : "There is no mass today. Sunday is " + DaysToSunday(today) + " days off.";
                Dialogue("The church", "Cold stone and the smell of tallow. " + next, ("Pray a while (half an hour)", () => { clock.SkipHours(0.5f); }), ("Leave", null));
            }
        }

        private static int DaysToSunday(long jdn) { int wd = (int)((jdn + 1) % 7); return wd == 0 ? 7 : 7 - wd; }

        private void Alehouse()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            string title = s != null ? "The alehouse of " + s.Name : "The alehouse";
            var opts = new List<(string, System.Action)>(6) { ("Buy and sell", () => OpenMarket(true)), ("Ask for work", AskForWork) };
            if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && ctx.ActiveJob.DaysRequired > 0 && s != null && ctx.ActiveJob.ToId == s.Id) opts.Add(($"Serve a day ({ctx.ActiveJob.DaysServed}/{ctx.ActiveJob.DaysRequired})", ServeDay));
            opts.Add(("A gallon of ale and the rumour of the day (1 penny)", () => { if (ctx.Character.Coins >= 1) { ctx.Character.Coins -= 1; ctx.Needs.Eat(0.15f); clock.SkipHours(1f); Dialogue("Rumour", Rumour(), ("Enough", null)); } else Dialogue("The alewife", "No penny, no ale.", ("Leave", null)); }));
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
            JobBoard.Refresh(s, ctx.Settlements, DataFiles.LoadCoast().mapSideKm, _balance.settlements, _balance.jobs, clock.Calendar, new NameGenerator(ctx.Names, rng.Fork(1)), rng, ref next);
            ctx.NextJobId = next;
            var opts = new List<(string, System.Action)>(4);
            foreach (Job j in s.Jobs)
            {
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
                ("Sleep until Prime", SleepUntilPrime),
                ("Eat", EatSomething),
                ("Clean and bind your wounds", () => { int n = vitals.Body.TreatAll(); ctx.Character.Use(Skill.Medicine, _balance.character, n > 0 ? 1f : 0.2f); clock.SkipHours(0.5f); Dialogue("Wounds", n > 0 ? $"You wash and bind {n} wound{(n == 1 ? "" : "s")} with what linen you have." : "Nothing needs binding.", ("Done", null)); }),
                ("Leave", null));
        }

        private void SleepUntilPrime()
        {
            var ctx = RunSession.Context; var cal = clock.Calendar;
            float hours = cal.HourOfDay < 6f ? 6f - cal.HourOfDay : 30f - cal.HourOfDay;
            survival.RestingAtHome = true;
            ctx.Needs.Sleep(hours);
            vitals.Body.Rest(hours);
            clock.SkipHours(hours);
            survival.RestingAtHome = false;
            if (vitals.IsDead) return;
            saveManager?.SaveNow(AffairsChoice.None);
            Dialogue("Morning", $"The bells ring Prime. {clock.Calendar.LongDate()}. " + (ctx.Disease.Current != Sickness.None ? "You wake sweating and weak." : "You wake hungry."), ("Rise", null));
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
            hud.ShowMoment(def.momentKind, def.id, quality =>
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
                clock.SkipHours(10f);
                if (vitals.IsDead) return;
                var prod = ctx.Items.Find(r.Product);
                string made = r.UnitsProduced > 0 ? $"{r.UnitsProduced} x {(prod != null ? prod.name : r.Product)}" : "nothing worth the name";
                Dialogue("The day's work", $"Ten hours at the {def.id}'s work ({(quality > 0.75f ? "good hands today" : quality > 0.4f ? "steady" : "clumsy")}). You made {made}. {r.Note}{(r.Injured ? " You hurt yourself." : "")}", ("Done", null));
                saveManager?.SaveNow(AffairsChoice.None);
            });
        }

        // ------------------------------------------------------------------ masters and apprenticeship

        private string MasterName(Transform t)
        {
            var tag = t != null ? t.GetComponent<VillagerTag>() : null;
            return tag != null ? tag.displayName + " the " + tag.trade : "the master";
        }

        private void Master(Transform t)
        {
            var tag = t.GetComponent<VillagerTag>(); if (tag == null) return;
            var ctx = RunSession.Context;
            if (ctx.Apprenticeship.Active && ctx.Apprenticeship.Trade == tag.trade)
            { Dialogue(MasterName(t), $"\"Back to work. {ctx.Apprenticeship.SeasonsRemaining:F1} seasons and you might be worth your bread.\"", ("Yes, master", null)); return; }
            if (ctx.Character.Trade == tag.trade) { Dialogue(MasterName(t), "\"Weather for it, isn't it.\" You talk shop for a while.", ("Leave", null)); return; }
            Dialogue(MasterName(t), $"\"{Greeting(tag)}\"",
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
            _busy = true; _openKind = Open.Market; _options.Clear();
            hud.ShowMarket(ctx.Market, ctx.Items, ctx.Inventory, month, standing,
                onBuy: (id, haggled) =>
                {
                    int asking = ctx.Market.BuyPrice(id, month, standing);
                    if (haggled)
                    {
                        var rng = GameBootstrap.Instance.Rng.Fork(91); rng.State ^= (ulong)(clock.Calendar.TotalHours * 7919) ^ (ulong)StableHash(id);
                        bool ok = ctx.Market.Haggle(0.8f, true, ctx.Character.Get(Skill.Haggling), standing, rng);
                        ctx.Character.Use(Skill.Haggling, _balance.character, ok ? 1f : 0.4f);
                        if (ok) { int price = Mathf.Max(1, Mathf.RoundToInt(asking * 0.8f)); if (ctx.Character.Coins >= price && ctx.Market.Find(id).Stock >= 1f) { ctx.Character.Coins -= price; ctx.Market.Find(id).Stock -= 1f; ctx.Inventory.Add(id); return "Grudgingly, for " + price + "."; } return "Agreed, but you cannot pay."; }
                        return "\"Away with you.\" The price stands.";
                    }
                    return ctx.Market.Buy(id, 1, month, standing, ctx.Character, ctx.Inventory, out int paid) ? "Bought for " + paid + "." : "You cannot afford it, or there is none.";
                },
                onSell: id =>
                {
                    return ctx.Market.Sell(id, 1, month, standing, ctx.Character, ctx.Inventory, out int earned) ? "Sold for " + earned + "." : "You have none to sell.";
                },
                onClose: () => { _busy = false; _openKind = Open.None; saveManager?.SaveNow(AffairsChoice.None); });
        }

        // ------------------------------------------------------------------ dialogue

        private static uint StableHash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h; }

        private void Dialogue(string title, string body, params (string label, System.Action act)[] options)
        {
            _options.Clear(); _options.AddRange(options);
            _busy = true; _openKind = Open.Dialogue;
            hud.ShowDialogue(title, body, options, i => { var act = _options[i].act; CloseDialogue(); act?.Invoke(); });
        }

        private void CloseDialogue() { _busy = false; _openKind = Open.None; _options.Clear(); hud.HideDialogue(); }
    }
}
