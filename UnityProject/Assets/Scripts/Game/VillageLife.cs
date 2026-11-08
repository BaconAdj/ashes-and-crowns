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
        public float useRadius = 4f;

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
            vitals.StaminaRegenScale = (ctx.Needs.Hungry ? 0.4f : 1f) * (ctx.Disease.Current != Sickness.None ? 0.5f : 1f);
            var kb = Keyboard.current; if (kb == null) return;

            if (_busy)
            {
                for (int i = 0; i < _options.Count && i < 6; i++)
                    if (DigitPressed(kb, i)) { var act = _options[i].act; CloseDialogue(); act?.Invoke(); return; }
                if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) CloseDialogue();
                return;
            }

            // Quick actions
            if (kb.gKey.wasPressedThisFrame) EatSomething();
            if (kb.iKey.wasPressedThisFrame) ShowInventory();

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
            Dialogue("The alehouse", "Smoke, wet wool and talk. The alewife sells what the village has to spare.",
                ("Buy and sell", () => OpenMarket(true)),
                ("A gallon of ale and the rumour of the day (1 penny)", () => { var c = RunSession.Context; if (c.Character.Coins >= 1) { c.Character.Coins -= 1; c.Needs.Eat(0.15f); clock.SkipHours(1f); Dialogue("Rumour", Rumour(), ("Enough", null)); } else Dialogue("The alewife", "No penny, no ale.", ("Leave", null)); }),
                ("Leave", null));
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
            saveManager?.SaveNow(AffairsChoice.None);
            Dialogue("Morning", vitals.Body.IsDead ? "You do not wake." : $"The bells ring Prime. {clock.Calendar.LongDate()}. " + (ctx.Disease.Current != Sickness.None ? "You wake sweating and weak." : "You wake hungry."), ("Rise", null));
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
            _busy = true;
            hud.ShowMoment(def.momentKind, def.id, quality =>
            {
                _busy = false;
                var ctx = RunSession.Context;
                float weatherPenalty = clock.Weather.Current == WeatherKind.Rain ? 0.2f : clock.Weather.Current == WeatherKind.Snow ? 0.4f : 0f;
                if (def.id == "blacksmith" || def.id == "brewer" || def.id == "carpenter" || def.id == "miller") weatherPenalty *= 0.25f;
                var rng = GameBootstrap.Instance.Rng.Fork(71); rng.State ^= (ulong)clock.Calendar.TotalHours;
                bool apprentice = ctx.Apprenticeship.Active;
                var r = TradeWork.WorkDay(def, ctx.Character, ctx.Inventory, ctx.Needs, clock.Calendar.Month, weatherPenalty, quality, _balance.character, vitals.Body, rng, 10f, apprentice);
                if (apprentice && ctx.Apprenticeship.WorkedDay(quality)) { ctx.Character.Trade = ctx.Apprenticeship.Trade; ctx.Apprenticeship.Trade = string.Empty; r.Note += " Your master calls you a " + ctx.Character.Trade + " now."; }
                if (onHolyDay) ctx.Standing.BrawlStarted();
                clock.SkipHours(10f);
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
            _busy = true;
            hud.ShowMarket(ctx.Market, ctx.Items, ctx.Inventory, month, standing,
                onBuy: (id, haggled) =>
                {
                    int asking = ctx.Market.BuyPrice(id, month, standing);
                    if (haggled)
                    {
                        var rng = GameBootstrap.Instance.Rng.Fork(91); rng.State ^= (ulong)(clock.Calendar.TotalHours * 7919) ^ (ulong)id.GetHashCode();
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
                onClose: () => { _busy = false; saveManager?.SaveNow(AffairsChoice.None); });
        }

        // ------------------------------------------------------------------ dialogue

        private void Dialogue(string title, string body, params (string label, System.Action act)[] options)
        {
            _options.Clear(); _options.AddRange(options);
            _busy = true;
            hud.ShowDialogue(title, body, options, i => { var act = _options[i].act; CloseDialogue(); act?.Invoke(); });
        }

        private void CloseDialogue() { _busy = false; hud.HideDialogue(); }
    }

    /// <summary>Marks a village NPC placeholder with who they are.</summary>
    public sealed class VillagerTag : MonoBehaviour
    {
        public int npcId;
        public string displayName = "Someone";
        public string trade = string.Empty;
        public float disposition;
    }
}
