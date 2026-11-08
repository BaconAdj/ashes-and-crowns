using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum TaxPolicy : byte { Light = 0, Customary = 1, Harsh = 2 }
    public enum JusticePolicy : byte { Lenient = 0, Customary = 1, Harsh = 2 }
    public enum EstateCrisis : byte { None = 0, Blight = 1, Murrain = 2, Raid = 3, Bandits = 4, StewardCaught = 5, Fever = 6, Refugees = 7 }
    public enum CourtCase : byte { Poaching = 0, Boundary = 1, Theft = 2, Dower = 3, Runaway = 4, Assault = 5 }
    public enum Verdict : byte { Dismiss = 0, Fine = 1, Flog = 2, Hang = 3, Settle = 4 }
    public enum MonarchAct : byte { GrantLand = 0, TakeHostage = 1, Pardon = 2, DebaseCoin = 3, Tallage = 4, AppeaseChurch = 5 }

    [Serializable] public sealed class LordshipBalance
    {
        public float taxPerHeadPerMonthLight = 0.6f, taxPerHeadPerMonthCustomary = 1.0f, taxPerHeadPerMonthHarsh = 1.7f;
        public float unrestPerDayLight = -0.004f, unrestPerDayCustomary = 0f, unrestPerDayHarsh = 0.012f;
        public float unrestPerDayNoCourt = 0.002f; public int courtEveryDays = 14;
        public float stewardSkimMax = 0.4f, stewardCompetenceIncome = 0.3f;
        public float crisisChancePerDay = 0.025f, revoltAbove = 0.85f, revoltCalm = 0.5f;
        public int courtCasesPerSitting = 3;
        public float secretDealDiscoveryPerDay = 0.015f; public int secretDealPennies = 60; public float suspicionPerDeal = 0.25f, suspicionDecayPerDay = 0.003f;
        public float demandChancePerDay = 0.02f; public int demandPennies = 40;
        public float suitBaseChance = 0.35f, suitCharismaWeight = 0.04f, suitConsentFavour = 0.7f; public int heiressDowryPennies = 150;
        public float serviceGrantFavour = 0.9f; public int serviceGrantRenown = 60;
        public int seasonDays = 91; public float lordSwitchChancePerSeason = 0.08f, campaignFlipChance = 0.5f, churchSwing = 0.1f;
        public int crownTreasuryStart = 3000, crownIncomePerSeason = 900, crownCampaignCost = 700;
        public int grantLandCost = 400, appeaseChurchCost = 300, debaseGain = 600, tallageGain = 800;
        public float tallageLoyaltyLoss = 0.08f, debaseUnrest = 0.1f;
    }

    // ------------------------------------------------------------------ the estate
    public sealed class Estate
    {
        public string SettlementId = string.Empty, Name = string.Empty;
        public TaxPolicy Tax = TaxPolicy.Customary; public JusticePolicy Justice = JusticePolicy.Customary;
        public string StewardName = string.Empty; public float StewardHonesty = 0.7f, StewardCompetence = 0.5f;
        public int Treasury; public float Unrest = 0.2f, Prosperity = 1f, Granary = 60f;
        public int DaysSinceCourt; public int GarrisonMen = 20;
        public EstateCrisis PendingCrisis; public int IncomeThisMonth, SkimmedThisMonth;
        public bool InRevolt;
    }

    public static class EstateRules
    {
        public static Estate Grant(Settlement s, NameGenerator names, DeterministicRng rng)
        {
            return new Estate
            {
                SettlementId = s.Id, Name = s.Name, StewardName = names != null ? names.Given(false, true) + " the steward" : "the steward",
                StewardHonesty = rng.Range(0.35f, 0.95f), StewardCompetence = rng.Range(0.3f, 0.9f), Treasury = (int)(s.Population * s.Wealth * 0.5f), GarrisonMen = s.Type == SettlementType.Castle ? 25 : 8
            };
        }

        public static float TaxRate(TaxPolicy t, LordshipBalance b) => t == TaxPolicy.Light ? b.taxPerHeadPerMonthLight : t == TaxPolicy.Harsh ? b.taxPerHeadPerMonthHarsh : b.taxPerHeadPerMonthCustomary;

        /// <summary>One day. Income trickles in daily; the steward keeps what he can; unrest drifts with policy and neglect.</summary>
        public static void DayTick(Estate e, Settlement s, bool lordPresent, DeterministicRng rng, LordshipBalance b, List<string> log)
        {
            if (e == null || s == null) return;
            float gross = s.Population * s.Wealth * e.Prosperity * TaxRate(e.Tax, b) / 30f * (1f + (e.StewardCompetence - 0.5f) * b.stewardCompetenceIncome);
            float skim = gross * (1f - e.StewardHonesty) * b.stewardSkimMax * (lordPresent ? 0.5f : 1f);
            int net = (int)Math.Round(gross - skim);
            e.Treasury += net; e.IncomeThisMonth += net; e.SkimmedThisMonth += (int)Math.Round(skim);
            float dU = e.Tax == TaxPolicy.Light ? b.unrestPerDayLight : e.Tax == TaxPolicy.Harsh ? b.unrestPerDayHarsh : b.unrestPerDayCustomary;
            e.DaysSinceCourt++;
            if (e.DaysSinceCourt > b.courtEveryDays) dU += b.unrestPerDayNoCourt;
            if (e.Justice == JusticePolicy.Lenient) dU += 0.001f;          // thieves get bold
            if (e.Granary <= 0f) dU += 0.01f;
            e.Granary = Math.Max(0f, e.Granary - 1f + (e.Prosperity > 1f ? 0.5f : 0f));
            e.Unrest = Clamp01(e.Unrest + dU);
            if (e.InRevolt && e.Unrest < b.revoltCalm) { e.InRevolt = false; log?.Add("The villeins have gone back to the fields. Nobody looks you in the eye."); }
            if (!e.InRevolt && e.Unrest >= b.revoltAbove) { e.InRevolt = true; log?.Add("They have burned the tithe barn and the steward's house. The manor is in revolt."); }
            if (e.PendingCrisis == EstateCrisis.None && rng.Chance(b.crisisChancePerDay))
            {
                e.PendingCrisis = (EstateCrisis)rng.Range(1, 8);
                if (e.PendingCrisis == EstateCrisis.StewardCaught && e.StewardHonesty > 0.8f) e.PendingCrisis = EstateCrisis.Bandits;
            }
        }

        public static string Describe(EstateCrisis k, Estate e) => k switch
        {
            EstateCrisis.Blight => "Black spots on the wheat from the river meadow to the ridge. The reeve says half the harvest.",
            EstateCrisis.Murrain => "The cattle are dying in the byres, tongues swollen. The herdsman wants them all killed and burned.",
            EstateCrisis.Raid => "Riders from the other side burned two steadings in the night and drove off the sheep. The people want vengeance, or walls.",
            EstateCrisis.Bandits => "Outlaws in the wood have taken the carrier's cart and a girl from the mill. The road is closed.",
            EstateCrisis.StewardCaught => $"The priest brings you {e.StewardName}'s second tally stick. He has been keeping a third of everything.",
            EstateCrisis.Fever => "Fever in the lower village: six houses, three dead. The priest asks whether to shut the church.",
            EstateCrisis.Refugees => "Forty people from a burned town at your gate, with carts and nothing in them. Winter is six weeks off.",
            _ => string.Empty
        };

        /// <summary>Three options per crisis; index 0..2. Returns the result text.</summary>
        public static string[] Options(EstateCrisis k) => k switch
        {
            EstateCrisis.Blight => new[] { "Open the granary to the village", "Buy grain from the market (costs the treasury)", "Let them fend for themselves" },
            EstateCrisis.Murrain => new[] { "Kill and burn the herds now", "Pay compensation to the herdsmen", "Wait and pray" },
            EstateCrisis.Raid => new[] { "Ride after them with the garrison", "Pay for a palisade around the village", "Do nothing; it is war" },
            EstateCrisis.Bandits => new[] { "Hunt them with the garrison", "Pay them off through the priest", "Close the road and wait" },
            EstateCrisis.StewardCaught => new[] { "Hang him in front of the hall", "Fine him and keep him", "Dismiss him and find another" },
            EstateCrisis.Fever => new[] { "Shut the church and the market", "Send the physician and pay for it", "Carry on" },
            EstateCrisis.Refugees => new[] { "Take them in and feed them", "Give them bread and send them on", "Turn them away at spear-point" },
            _ => new[] { "...", "...", "..." }
        };

        public static string Resolve(Estate e, Settlement s, int choice, CharacterSheet c, GlobalReputation rep, NameGenerator names, DeterministicRng rng, LordshipBalance b)
        {
            EstateCrisis k = e.PendingCrisis; e.PendingCrisis = EstateCrisis.None;
            switch (k)
            {
                case EstateCrisis.Blight:
                    if (choice == 0) { e.Granary -= 30f; e.Unrest -= 0.15f; e.Prosperity *= 0.85f; return "The granary doors open. They will remember it when you need them."; }
                    if (choice == 1) { e.Treasury -= 120; e.Prosperity *= 0.9f; e.Unrest -= 0.05f; return "Carts of grain at twice the price. The treasury groans."; }
                    e.Unrest += 0.2f; e.Prosperity *= 0.7f; s.Population = (int)(s.Population * 0.95f); return "Thin children by spring. The priest preaches about rich men.";
                case EstateCrisis.Murrain:
                    if (choice == 0) { e.Prosperity *= 0.8f; e.Unrest += 0.05f; return "Smoke over the byres for a week, and the sickness stops at the river."; }
                    if (choice == 1) { e.Treasury -= 80; e.Prosperity *= 0.75f; e.Unrest -= 0.05f; return "The herdsmen bless you and the sickness spreads anyway."; }
                    e.Prosperity *= 0.6f; e.Unrest += 0.1f; return "Every beast on the manor is dead by Michaelmas.";
                case EstateCrisis.Raid:
                    if (choice == 0) { bool won = rng.Chance(0.6f); e.GarrisonMen -= rng.Range(1, 4); if (won) { e.Unrest -= 0.1f; e.Treasury += 40; return "You catch them at the ford. Sheep recovered, and two heads on the gate."; } e.Unrest += 0.05f; return "They were more than you thought. You come back with fewer men and no sheep."; }
                    if (choice == 1) { e.Treasury -= 150; e.Unrest -= 0.1f; return "A palisade and a ditch by autumn. The village sleeps better."; }
                    e.Unrest += 0.15f; e.Prosperity *= 0.9f; return "They come again a month later.";
                case EstateCrisis.Bandits:
                    if (choice == 0) { e.GarrisonMen -= rng.Range(0, 3); e.Unrest -= 0.1f; rep?.Record(Deed.HelpedRefugees); return "Three days in the wood. The girl comes home; four men hang at the crossroads."; }
                    if (choice == 1) { e.Treasury -= 60; return "The priest comes back with the girl and without the silver. The road opens."; }
                    e.Unrest += 0.1f; e.Prosperity *= 0.9f; return "The road stays closed through the harvest market.";
                case EstateCrisis.StewardCaught:
                    if (choice == 0) { e.StewardName = names != null ? names.Given(false, true) + " the steward" : "the new steward"; e.StewardHonesty = rng.Range(0.6f, 0.95f); e.StewardCompetence = rng.Range(0.3f, 0.7f); e.Unrest -= 0.05f; rep?.Record(Deed.Murder); return "He hangs. The new man is honest and knows nothing."; }
                    if (choice == 1) { e.Treasury += 100; e.StewardHonesty = Math.Min(0.95f, e.StewardHonesty + 0.3f); return "He pays back what he can and keeps his place, watched."; }
                    e.StewardName = names != null ? names.Given(false, true) + " the steward" : "the new steward"; e.StewardHonesty = rng.Range(0.4f, 0.95f); e.StewardCompetence = rng.Range(0.3f, 0.9f); return "He goes with a cart and more than he came with. A new man takes the tallies.";
                case EstateCrisis.Fever:
                    if (choice == 0) { e.Prosperity *= 0.9f; e.Unrest += 0.05f; rep?.Record(Deed.AttendedMass); return "Church and market shut for a month. It burns out in the lower village and goes no further."; }
                    if (choice == 1) { e.Treasury -= 50; e.Unrest -= 0.05f; return "Bleeding and prayers. Four more die; it might have been forty."; }
                    s.Population = (int)(s.Population * 0.9f); e.Unrest += 0.1f; return "It runs through the manor like fire through thatch.";
                case EstateCrisis.Refugees:
                    if (choice == 0) { e.Granary -= 20f; s.Population += 30; e.Unrest += 0.05f; rep?.Record(Deed.HelpedRefugees); return "Forty new mouths and forty new pairs of hands. The old villeins grumble."; }
                    if (choice == 1) { e.Granary -= 5f; rep?.Record(Deed.GaveAlms); return "They go on toward the abbey with bread in their shirts."; }
                    rep?.Record(Deed.RobbedTraveller); e.Unrest -= 0.02f; return "They go. One of them curses your name at the gate, loudly, for a long time.";
            }
            e.Unrest = Clamp01(e.Unrest);
            return string.Empty;
        }

        public static void EndMonth(Estate e) { e.IncomeThisMonth = 0; e.SkimmedThisMonth = 0; }

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // ------------------------------------------------------------------ manor court
    public sealed class CourtMatter { public CourtCase Kind; public string Text = string.Empty; public string Plaintiff = string.Empty, Defendant = string.Empty; public int Pennies; }

    public static class ManorCourt
    {
        public static List<CourtMatter> Sitting(int count, NameGenerator names, DeterministicRng rng)
        {
            var list = new List<CourtMatter>(count);
            for (int i = 0; i < count; i++)
            {
                var m = new CourtMatter { Kind = (CourtCase)rng.Range(0, 6), Plaintiff = names.Given(rng.Chance(0.4f), false), Defendant = names.Given(rng.Chance(0.3f), false), Pennies = rng.Range(2, 30) };
                m.Text = m.Kind switch
                {
                    CourtCase.Poaching => $"{m.Defendant} was taken in your wood with a hare in his shirt. He says his children were crying.",
                    CourtCase.Boundary => $"{m.Plaintiff} says {m.Defendant} moved the mere-stone three furrows into his strip. Both have witnesses; the witnesses are their brothers.",
                    CourtCase.Theft => $"{m.Plaintiff} accuses {m.Defendant} of taking {m.Pennies} pennies from under the hearth-stone. {m.Defendant} was seen drunk that night.",
                    CourtCase.Dower => $"{m.Plaintiff}, a widow, claims a third of her dead husband's holding. His brother {m.Defendant} has the plough and says she can go to her own people.",
                    CourtCase.Runaway => $"{m.Defendant}, a villein, ran to the town a year and a day ago less a week. The reeve brought him back in a rope.",
                    _ => $"{m.Defendant} broke {m.Plaintiff}'s head with a flail over a woman. {m.Plaintiff} will live. The woman is here too.",
                };
                list.Add(m);
            }
            return list;
        }

        public static string Judge(Estate e, CourtMatter m, Verdict v, GlobalReputation rep)
        {
            e.DaysSinceCourt = 0;
            switch (v)
            {
                case Verdict.Dismiss: e.Unrest = EstateRules.Clamp01(e.Unrest + (m.Kind == CourtCase.Theft || m.Kind == CourtCase.Assault ? 0.03f : -0.02f)); return "Dismissed. " + (m.Kind == CourtCase.Theft ? "The plaintiff spits." : "Both go home grumbling, which is justice of a kind.");
                case Verdict.Fine: e.Treasury += Math.Max(2, m.Pennies); e.Unrest = EstateRules.Clamp01(e.Unrest + 0.01f); return $"Amerced {Math.Max(2, m.Pennies)} pennies, to the lord. The reeve writes it on his stick.";
                case Verdict.Flog: e.Unrest = EstateRules.Clamp01(e.Unrest + (m.Kind == CourtCase.Poaching ? 0.04f : 0f)); return "Twenty strokes at the church gate. The village watches in silence.";
                case Verdict.Hang:
                    e.Unrest = EstateRules.Clamp01(e.Unrest + (m.Kind == CourtCase.Theft && m.Pennies > 12 ? 0.02f : 0.12f));
                    rep?.Record(m.Kind == CourtCase.Theft && m.Pennies > 12 ? Deed.PaidTithe : Deed.Murder);
                    return m.Kind == CourtCase.Theft && m.Pennies > 12 ? "He hangs for a shilling and more. The law allows it. Nobody argues." : "He hangs. The priest will not look at you. This was not a hanging matter and everyone knows it.";
                default:
                    e.Unrest = EstateRules.Clamp01(e.Unrest - 0.04f);
                    return m.Kind == CourtCase.Dower ? "She keeps her third and the brother keeps the plough. Both hate you a little less than each other." : m.Kind == CourtCase.Runaway ? "He stays on the land and pays a fine in work. He will run again." : "Settled by oath and a handshake you made them give.";
            }
        }
    }

    // ------------------------------------------------------------------ allegiance in the murk
    public sealed class LordAllegiance
    {
        public Allegiance Declared;
        public Allegiance SecretDeal;      // who you are quietly promising things to
        public int DealsTaken; public float Suspicion;
        public int DemandsRefusedStephen, DemandsRefusedMatilda;
        public bool Discovered;
    }

    public static class AllegianceRules
    {
        public static void Declare(LordAllegiance a, Allegiance side, GlobalReputation rep)
        {
            a.Declared = side; if (a.SecretDeal == side) a.SecretDeal = Allegiance.Neutral;
            if (side == Allegiance.Stephen) rep.Record(Deed.FoughtForStephen); else if (side == Allegiance.Matilda) rep.Record(Deed.FoughtForMatilda);
        }

        public static int DealSecretly(LordAllegiance a, Allegiance side, LordshipBalance b)
        {
            if (side == a.Declared || side == Allegiance.Neutral) return 0;
            a.SecretDeal = side; a.DealsTaken++; a.Suspicion = Math.Min(1f, a.Suspicion + b.suspicionPerDeal);
            return b.secretDealPennies;
        }

        /// <summary>Daily. Returns true the day a captured courier or a bribed servant gives you away.</summary>
        public static bool DayTick(LordAllegiance a, DeterministicRng rng, LordshipBalance b)
        {
            if (a.SecretDeal == Allegiance.Neutral || a.Discovered) { a.Suspicion = Math.Max(0f, a.Suspicion - b.suspicionDecayPerDay); return false; }
            if (rng.Chance(b.secretDealDiscoveryPerDay * (0.5f + a.Suspicion))) { a.Discovered = true; return true; }
            return false;
        }

        /// <summary>What discovery costs: treason against your declared side.</summary>
        public static void OnDiscovered(LordAllegiance a, CrimeRecord crime, GlobalReputation rep, string where, long day, DeterministicRng rng)
        {
            crime.Commit(CrimeKind.Treason, where, day, 3, rng, rep);
            if (a.Declared == Allegiance.Stephen) rep.Set(Faction.Crown, Math.Min(rep.Get(Faction.Crown), -0.5f)); else if (a.Declared == Allegiance.Matilda) rep.Set(Faction.Angevin, Math.Min(rep.Get(Faction.Angevin), -0.5f));
            a.SecretDeal = Allegiance.Neutral;
        }

        public static bool DemandArrives(LordAllegiance a, DeterministicRng rng, LordshipBalance b, out Allegiance from)
        {
            from = rng.Chance(0.5f) ? Allegiance.Stephen : Allegiance.Matilda;
            return rng.Chance(b.demandChancePerDay);
        }

        public static string AnswerDemand(LordAllegiance a, Allegiance from, bool comply, Estate e, GlobalReputation rep, LordshipBalance b)
        {
            if (comply)
            {
                e.Treasury -= b.demandPennies;
                rep.Record(from == Allegiance.Stephen ? Deed.FoughtForStephen : Deed.FoughtForMatilda);
                if (a.Declared != Allegiance.Neutral && from != a.Declared) a.Suspicion = Math.Min(1f, a.Suspicion + 0.1f);
                return $"{b.demandPennies} pennies go out of the gate under their banner.";
            }
            if (from == Allegiance.Stephen) a.DemandsRefusedStephen++; else a.DemandsRefusedMatilda++;
            rep.Set(from == Allegiance.Stephen ? Faction.Crown : Faction.Angevin, rep.Get(from == Allegiance.Stephen ? Faction.Crown : Faction.Angevin) - 0.08f);
            return "The messenger rides off without his answer. They will remember the gate that stayed shut.";
        }
    }

    // ------------------------------------------------------------------ marriage
    public sealed class Suit { public string Heiress = string.Empty; public int FatherLordId; public string DowrySettlementId = string.Empty; public float Favour; public int Visits; public bool Consented, Married; }

    public static class SuitRules
    {
        public static Suit Begin(MinorLord father, NameGenerator names, string dowrySettlementId) => new Suit { Heiress = names.Given(true, true) + " " + father.House, FatherLordId = father.Id, DowrySettlementId = dowrySettlementId, Favour = 0.1f };

        public static string Visit(Suit s, CharacterSheet c, float standing, DeterministicRng rng, LordshipBalance b)
        {
            s.Visits++;
            float chance = b.suitBaseChance + (c.Get(Attribute.Charisma) - 8) * b.suitCharismaWeight + (standing - 0.5f) * 0.3f + (c.Rank >= Rank.Knight ? 0.1f : -0.15f);
            if (rng.Chance(Math.Max(0.05f, Math.Min(0.95f, chance)))) { s.Favour = Math.Min(1f, s.Favour + rng.Range(0.12f, 0.25f)); return s.Favour > 0.6f ? $"{s.Heiress} laughs at the right places and her father watches you count his knights." : $"{s.Heiress} is polite. Her father is less so."; }
            s.Favour = Math.Max(0f, s.Favour - 0.08f);
            return $"You talk about horses too long. {s.Heiress}'s mother takes her away.";
        }

        public static bool Propose(Suit s, CharacterSheet c, int renown, LordshipBalance b, out string text)
        {
            bool ok = s.Favour >= b.suitConsentFavour && (c.Rank >= Rank.Knight || renown >= b.serviceGrantRenown / 2);
            s.Consented = ok;
            text = ok ? $"Her father names the dowry: the manor of {s.DowrySettlementId}, and {b.heiressDowryPennies} pennies. The priest reads the banns on Sunday." : s.Favour < b.suitConsentFavour ? "\"Not yet,\" her father says, which means no." : "\"A landless man. My daughter.\" He does not finish the sentence.";
            return ok;
        }

        public static Npc Wed(Suit s, int npcId, Allegiance fatherSide, DeterministicRng rng)
        {
            s.Married = true;
            return new Npc { Id = npcId, Name = s.Heiress, Female = true, Age = rng.Range(16, 24), Relation = Relation.Spouse, Disposition = s.Favour - 0.3f, Note = "an heiress; her family are " + (fatherSide == Allegiance.Neutral ? "careful" : fatherSide.ToString() + "'s men") };
        }
    }

    public static class ServiceGrantRules
    {
        public static bool Eligible(ServiceRecord s, LordshipBalance b) => s.PatronFavour >= b.serviceGrantFavour && s.Renown >= b.serviceGrantRenown && !string.IsNullOrEmpty(s.PatronId);
    }

    // ------------------------------------------------------------------ the realm
    public sealed class RealmState
    {
        public int CrownTreasury; public float ChurchFavour;   // -1 Matilda .. +1 Stephen
        public int SeasonsElapsed; public float StephenStrength, MatildaStrength;
        public int SettlementsStephen, SettlementsMatilda;
        public bool StephenCaptive, MatildaFled;
        public readonly List<string> Chronicle = new List<string>(16);
    }

    public static class RealmRules
    {
        public static RealmState Fresh(LordshipBalance b) => new RealmState { CrownTreasury = b.crownTreasuryStart, ChurchFavour = 0.3f };

        public static void Measure(RealmState r, IList<MinorLord> lords, IList<Settlement> settlements)
        {
            r.StephenStrength = 0f; r.MatildaStrength = 0f; r.SettlementsStephen = 0; r.SettlementsMatilda = 0;
            foreach (var l in lords) { if (!l.Alive) continue; if (l.Declared == Allegiance.Stephen) r.StephenStrength += l.Knights; else if (l.Declared == Allegiance.Matilda) r.MatildaStrength += l.Knights; }
            foreach (var s in settlements) { if (s.Allegiance == Allegiance.Stephen) r.SettlementsStephen++; else if (s.Allegiance == Allegiance.Matilda) r.SettlementsMatilda++; }
            r.StephenStrength += r.CrownTreasury / 100f + r.ChurchFavour * 20f;
            r.MatildaStrength += 30f - r.ChurchFavour * 20f;
        }

        /// <summary>A season of the war: lords drift toward the winning side, one campaign flips ground, the Church leans.</summary>
        public static void Season(RealmState r, IList<MinorLord> lords, IList<Settlement> settlements, DeterministicRng rng, LordshipBalance b, List<string> log)
        {
            r.SeasonsElapsed++;
            Measure(r, lords, settlements);
            float total = Math.Max(1f, r.StephenStrength + r.MatildaStrength);
            float stephenShare = r.StephenStrength / total;
            foreach (var l in lords)
            {
                if (!l.Alive || l.Declared == Allegiance.Neutral) continue;
                Allegiance stronger = stephenShare >= 0.5f ? Allegiance.Stephen : Allegiance.Matilda;
                if (l.Declared == stronger) continue;
                float p = b.lordSwitchChancePerSeason * (1f - l.Loyalty) * (0.5f + l.Ambition) * (Math.Abs(stephenShare - 0.5f) * 4f);
                if (l.Secret != Allegiance.Neutral && l.Secret == stronger) p *= 2f;
                if (rng.Chance(p)) { l.Declared = stronger; l.Loyalty = 0.4f; log?.Add($"{l.FullName} of {l.SeatName} has gone over to {(stronger == Allegiance.Stephen ? "the king" : "the Empress")}."); }
            }
            r.CrownTreasury += b.crownIncomePerSeason;
            Allegiance attacker = rng.Chance(stephenShare) ? Allegiance.Stephen : Allegiance.Matilda;
            if (attacker == Allegiance.Stephen) { if (r.CrownTreasury < b.crownCampaignCost) attacker = Allegiance.Matilda; else r.CrownTreasury -= b.crownCampaignCost; }
            Allegiance defender = attacker == Allegiance.Stephen ? Allegiance.Matilda : Allegiance.Stephen;
            Settlement target = null; int n = 0;
            foreach (var s in settlements) { if (s.Allegiance != defender || s.LordId == CastleRules.PlayerLordId) continue; n++; if (rng.Chance(1f / n)) target = s; }
            if (target != null)
            {
                bool flipped = rng.Chance(b.campaignFlipChance * (attacker == Allegiance.Stephen ? stephenShare * 2f : (1f - stephenShare) * 2f));
                if (flipped) { target.Allegiance = attacker; target.Wealth *= 0.9f; log?.Add($"{(attacker == Allegiance.Stephen ? "The king" : "The Empress's men")} took {target.Name} after a siege. The country round it is burned."); }
                else log?.Add($"{(attacker == Allegiance.Stephen ? "The king" : "The Empress's men")} sat before {target.Name} all season and went home when the money ran out.");
            }
            r.ChurchFavour = Math.Max(-1f, Math.Min(1f, r.ChurchFavour + (stephenShare - 0.5f) * b.churchSwing + rng.Range(-0.05f, 0.05f)));
            if (r.ChurchFavour < -0.4f && !r.MatildaFled && log != null && rng.Chance(0.2f)) log.Add("The bishops have written to Rome. They no longer say 'king' in Winchester.");
            Measure(r, lords, settlements);
        }

        public static string Act(RealmState r, MonarchAct act, MinorLord lord, IList<MinorLord> lords, LordshipBalance b, DeterministicRng rng)
        {
            switch (act)
            {
                case MonarchAct.GrantLand:
                    if (lord == null) return "Grant land to whom?"; if (r.CrownTreasury < b.grantLandCost) return "The treasury cannot bear another gift.";
                    r.CrownTreasury -= b.grantLandCost; lord.Loyalty = Math.Min(1f, lord.Loyalty + 0.3f); lord.Knights += 3;
                    foreach (var o in lords) if (o != lord && o.Alive && o.Declared == lord.Declared) o.Loyalty = Math.Max(0f, o.Loyalty - 0.03f);
                    return $"{lord.FullName} kneels for the manor and rises richer. The others count what they did not get.";
                case MonarchAct.TakeHostage:
                    if (lord == null) return "Whose son?"; lord.Loyalty = Math.Min(1f, lord.Loyalty + 0.25f); lord.Ambition = Math.Max(0f, lord.Ambition - 0.2f); r.ChurchFavour -= 0.05f;
                    return $"{lord.FullName}'s boy rides to court with a smile he does not mean. His father will behave, for a while.";
                case MonarchAct.Pardon:
                    if (lord == null) return "Pardon whom?"; lord.Loyalty = Math.Min(1f, lord.Loyalty + 0.15f); lord.Alive = true;
                    foreach (var o in lords) if (o != lord && o.Alive) o.Loyalty = Math.Max(0f, o.Loyalty - 0.05f);
                    return $"{lord.FullName} is forgiven, again. Men who stayed loyal wonder why they bothered.";
                case MonarchAct.DebaseCoin:
                    r.CrownTreasury += b.debaseGain; r.ChurchFavour -= 0.1f;
                    foreach (var o in lords) if (o.Alive) o.Loyalty = Math.Max(0f, o.Loyalty - 0.04f);
                    return "The mints strike thinner pennies with your face on them. Prices know before the barons do.";
                case MonarchAct.Tallage:
                    r.CrownTreasury += b.tallageGain;
                    foreach (var o in lords) if (o.Alive) o.Loyalty = Math.Max(0f, o.Loyalty - b.tallageLoyaltyLoss);
                    return "The sheriffs go out with the writs. Silver comes in; so do the complaints, and a burned tax-house in the west.";
                default:
                    if (r.CrownTreasury < b.appeaseChurchCost) return "The bishops want silver you do not have.";
                    r.CrownTreasury -= b.appeaseChurchCost; r.ChurchFavour = Math.Min(1f, r.ChurchFavour + 0.25f);
                    return "A new roof for the cathedral and a charter for the abbey. Henry of Blois smiles, which is worse.";
            }
        }
    }
}
