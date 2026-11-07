using System;

namespace AshesAndCrowns.Sim
{
    public enum Weekday : byte { Sunday = 0, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday }
    public enum Season : byte { Winter, Spring, Summer, Autumn }

    /// <summary>
    /// The game's clock. Julian calendar (England, 1135), bells at the canonical hours from balance.
    /// Allocation-free after construction. Serialise with TotalHours.
    /// </summary>
    public sealed class Calendar
    {
        private static readonly string[] MonthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        private readonly TimeBalance _balance;

        public int Year { get; private set; }
        public int Month { get; private set; }   // 1..12
        public int Day { get; private set; }     // 1..31
        public float HourOfDay { get; private set; }
        public double TotalHours { get; private set; }
        public int LastBellRung { get; private set; } = -1;
        public string LastBellRungName => LastBellRung >= 0 ? _balance.bellNames[LastBellRung] : string.Empty;

        public Calendar(TimeBalance balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Year = balance.startYear; Month = balance.startMonth; Day = balance.startDay; HourOfDay = balance.startHour;
            TotalHours = 0;
        }

        public static Calendar FromTotalHours(TimeBalance balance, double totalHours)
        {
            var c = new Calendar(balance);
            double remaining = totalHours;
            while (remaining > 24.0 * 365.0) { c.AdvanceHours(24f * 365f); remaining -= 24.0 * 365.0; }
            while (remaining > 0.0) { float step = (float)Math.Min(remaining, 24.0); c.AdvanceHours(step); remaining -= step; }
            c.LastBellRung = -1;
            return c;
        }

        public static bool IsLeapYear(int year) => year % 4 == 0; // Julian rule

        public static int DaysInMonth(int year, int month)
        {
            switch (month)
            {
                case 2: return IsLeapYear(year) ? 29 : 28;
                case 4: case 6: case 9: case 11: return 30;
                default: return 31;
            }
        }

        public int DayOfYear
        {
            get { int d = Day; for (int m = 1; m < Month; m++) d += DaysInMonth(Year, m); return d; }
        }

        /// <summary>Julian Day Number for the Julian calendar date.</summary>
        public static long JulianDayNumber(int year, int month, int day)
        {
            int a = (14 - month) / 12;
            int y = year + 4800 - a;
            int m = month + 12 * a - 3;
            return day + (153 * m + 2) / 5 + 365L * y + y / 4 - 32083;
        }

        public Weekday Weekday => (Weekday)(int)((JulianDayNumber(Year, Month, Day) + 1) % 7);
        public bool IsSunday => Weekday == Weekday.Sunday;

        public Season Season
        {
            get
            {
                if (Month == 12 || Month <= 2) return Season.Winter;
                if (Month <= 5) return Season.Spring;
                if (Month <= 8) return Season.Summer;
                return Season.Autumn;
            }
        }

        public string MonthName => MonthNames[Month - 1];

        public int NextBellIndex
        {
            get
            {
                for (int i = 0; i < _balance.bellHours.Length; i++) if (_balance.bellHours[i] > HourOfDay) return i;
                return 0;
            }
        }
        public string NextBellName => _balance.bellNames[NextBellIndex];
        public float HoursUntilNextBell
        {
            get
            {
                int i = NextBellIndex;
                float h = _balance.bellHours[i] - HourOfDay;
                return h > 0f ? h : h + 24f;
            }
        }

        /// <summary>Advance time. Returns how many bells rang during the step; LastBellRung is the most recent.</summary>
        public int AdvanceHours(float hours)
        {
            if (hours <= 0f) return 0;
            int rung = 0;
            float before = HourOfDay;
            float after = before + hours;
            TotalHours += hours;
            // Count bells in (before, after], handling day wraps.
            while (after >= 24f)
            {
                rung += CountBells(before, 24f);
                after -= 24f; before = -0.0001f;
                NextDay();
            }
            rung += CountBells(before, after);
            HourOfDay = after < 0f ? 0f : after;
            return rung;
        }

        private int CountBells(float fromExclusive, float toInclusive)
        {
            int n = 0;
            for (int i = 0; i < _balance.bellHours.Length; i++)
            {
                float h = _balance.bellHours[i];
                if (h > fromExclusive && h <= toInclusive) { n++; LastBellRung = i; }
            }
            return n;
        }

        private void NextDay()
        {
            Day++;
            if (Day > DaysInMonth(Year, Month)) { Day = 1; Month++; if (Month > 12) { Month = 1; Year++; } }
        }

        private static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }

        /// <summary>"Sunday, the 22nd day of December, in the year of our Lord 1135". Allocates; call rarely.</summary>
        public string LongDate() => Weekday + ", the " + Ordinal(Day) + " day of " + MonthName + ", in the year of our Lord " + Year;

        /// <summary>"22 December 1135". Allocates; call rarely.</summary>
        public string ShortDate() => Day + " " + MonthName + " " + Year;
    }
}
