using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum CrimeKind : byte { Theft = 0, Assault = 1, Murder = 2, Desertion = 3, Treason = 4, Debt = 5, Poaching = 6 }
    public enum Sentence : byte { Acquitted = 0, Fine = 1, Flogging = 2, Outlawry = 3, Hanging = 4, Beheading = 5, Mutilation = 6 }

    public sealed class Crime
    {
        public CrimeKind Kind; public string Where = string.Empty; public long Day; public int Witnesses; public bool Known; public bool Tried;
    }

    [Serializable] public sealed class CrimeBalance
    {
        public float witnessReportChance = 0.45f;     // per witness
        public float arrestChancePerDayAtFullWanted = 0.5f;
        public float wantedDecayPerDay = 0.01f;
        public float sanctuaryDays = 40f;
        public float fineTheft = 4f, fineAssault = 8f;
        public float hangingThresholdTheftPennies = 12f; // theft above a shilling hangs
        public float noblePardonChance = 0.35f;        // nobles buy their way out of the rope more often
        public float standingSavesChance = 0.25f;      // a respected villager gets the benefit of the doubt
    }

    /// <summary>Your crimes, who knows, and what the law does when it catches you.</summary>
    public sealed class CrimeRecord
    {
        private readonly CrimeBalance _b;
        public readonly List<Crime> Crimes = new List<Crime>(8);
        public bool Outlawed { get; private set; }
        public float SanctuaryDaysLeft { get; private set; }
        public bool SanctuaryExpired { get; private set; }
        private bool _inSanctuary;

        public CrimeRecord(CrimeBalance balance) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); }

        /// <summary>Commit a crime. Returns how much more wanted you are here (0 if nobody saw).</summary>
        public float Commit(CrimeKind kind, string where, long day, int witnesses, DeterministicRng rng, GlobalReputation rep)
        {
            var c = new Crime { Kind = kind, Where = where, Day = day, Witnesses = witnesses };
            for (int i = 0; i < witnesses && !c.Known; i++) if (rng.Chance(_b.witnessReportChance)) c.Known = true;
            if (kind == CrimeKind.Desertion || kind == CrimeKind.Treason) c.Known = true; // the muster roll knows
            Crimes.Add(c);
            if (rep != null) { if (kind == CrimeKind.Murder) rep.Record(Deed.Murder); else if (kind == CrimeKind.Theft) rep.Record(Deed.Theft); else if (kind == CrimeKind.Desertion) rep.Record(Deed.Deserted); }
            if (!c.Known) return 0f;
            return Severity(kind);
        }

        public static float Severity(CrimeKind k)
        {
            switch (k)
            {
                case CrimeKind.Poaching: return 0.15f; case CrimeKind.Theft: return 0.3f; case CrimeKind.Debt: return 0.2f; case CrimeKind.Assault: return 0.35f;
                case CrimeKind.Desertion: return 0.6f; case CrimeKind.Murder: return 0.8f; default: return 1f;
            }
        }

        public bool HasKnownCrime { get { foreach (var c in Crimes) if (c.Known && !c.Tried) return true; return false; } }

        /// <summary>Daily: wanted levels fade, sanctuary runs out, arrest rolls where you stand.</summary>
        public bool TickDay(Settlement here, bool inChurch, DeterministicRng rng)
        {
            // Sanctuary: forty days under the Church's roof, once; then abjure the realm or face the law.
            if (inChurch && HasKnownCrime && !SanctuaryExpired)
            {
                if (!_inSanctuary) { _inSanctuary = true; SanctuaryDaysLeft = _b.sanctuaryDays; }
                SanctuaryDaysLeft -= 1f;
                if (SanctuaryDaysLeft <= 0f) { SanctuaryExpired = true; _inSanctuary = false; }
            }
            else if (!inChurch) _inSanctuary = false;
            if (here == null) return false;
            here.WantedHere = Math.Max(0f, here.WantedHere - _b.wantedDecayPerDay);
            if (inChurch && _inSanctuary && SanctuaryDaysLeft > 0f) return false;
            float chance = _b.arrestChancePerDayAtFullWanted * Math.Max(here.WantedHere, Outlawed ? 0.5f : 0f);
            return chance > 0f && rng.Chance(chance);
        }

        /// <summary>Try the worst untried known crime. Rank and standing matter; so does luck.</summary>
        public Sentence Trial(CharacterSheet accused, float standing, int stolenValuePennies, DeterministicRng rng, out Crime tried)
        {
            tried = null;
            foreach (var c in Crimes) if (c.Known && !c.Tried && (tried == null || Severity(c.Kind) > Severity(tried.Kind))) tried = c;
            if (tried == null) return Sentence.Acquitted;
            tried.Tried = true;
            SanctuaryExpired = false; _inSanctuary = false;
            if (standing >= 0.7f && rng.Chance(_b.standingSavesChance) && tried.Kind != CrimeKind.Treason && tried.Kind != CrimeKind.Murder) return Sentence.Acquitted;
            bool noble = accused.Rank >= Rank.Knight;
            switch (tried.Kind)
            {
                case CrimeKind.Poaching: return noble ? Sentence.Fine : (rng.Chance(0.5f) ? Sentence.Fine : Sentence.Mutilation);
                case CrimeKind.Debt: return Sentence.Fine;
                case CrimeKind.Theft: return stolenValuePennies >= _b.hangingThresholdTheftPennies ? (noble && rng.Chance(_b.noblePardonChance) ? Sentence.Fine : Sentence.Hanging) : (rng.Chance(0.6f) ? Sentence.Fine : Sentence.Flogging);
                case CrimeKind.Assault: return rng.Chance(0.6f) ? Sentence.Fine : Sentence.Flogging;
                case CrimeKind.Desertion: return rng.Chance(0.5f) ? Sentence.Hanging : Sentence.Flogging;
                case CrimeKind.Murder: return noble ? (rng.Chance(_b.noblePardonChance) ? Sentence.Outlawry : Sentence.Beheading) : Sentence.Hanging;
                default: return noble ? Sentence.Beheading : Sentence.Hanging;
            }
        }

        public void Outlaw() => Outlawed = true;
        public void SetOutlawed(bool v) => Outlawed = v;
        public void SetSanctuary(float daysLeft, bool expired) { SanctuaryDaysLeft = daysLeft; SanctuaryExpired = expired; _inSanctuary = daysLeft > 0f && !expired; }

        public static bool IsExecution(Sentence s) => s == Sentence.Hanging || s == Sentence.Beheading;

        public static string Describe(Sentence s)
        {
            switch (s)
            {
                case Sentence.Acquitted: return "acquitted";
                case Sentence.Fine: return "fined";
                case Sentence.Flogging: return "flogged";
                case Sentence.Outlawry: return "outlawed";
                case Sentence.Hanging: return "hanged";
                case Sentence.Beheading: return "beheaded";
                default: return "maimed";
            }
        }
    }
}
