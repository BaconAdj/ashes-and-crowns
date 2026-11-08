namespace AshesAndCrowns.Sim
{
    public enum Feast : byte { None = 0, Sunday = 1, Christmas, Epiphany, Candlemas, Easter, Whitsun, Midsummer, Lammas, Michaelmas, AllSaints, Martinmas }

    /// <summary>The Church's year: Sundays, fixed feasts and the movable feasts from the Julian computus.</summary>
    public static class Festivals
    {
        /// <summary>Julian Easter (Meeus). Returns month and day.</summary>
        public static void JulianEaster(int year, out int month, out int day)
        {
            int a = year % 4, b = year % 7, c = year % 19;
            int d = (19 * c + 15) % 30;
            int e = (2 * a + 4 * b - d + 34) % 7;
            month = (d + e + 114) / 31;
            day = (d + e + 114) % 31 + 1;
        }

        public static Feast FeastOn(int year, int month, int day)
        {
            if (month == 12 && day == 25) return Feast.Christmas;
            if (month == 1 && day == 6) return Feast.Epiphany;
            if (month == 2 && day == 2) return Feast.Candlemas;
            if (month == 6 && day == 24) return Feast.Midsummer;
            if (month == 8 && day == 1) return Feast.Lammas;
            if (month == 9 && day == 29) return Feast.Michaelmas;
            if (month == 11 && day == 1) return Feast.AllSaints;
            if (month == 11 && day == 11) return Feast.Martinmas;
            JulianEaster(year, out int em, out int ed);
            long easter = Calendar.JulianDayNumber(year, em, ed);
            long today = Calendar.JulianDayNumber(year, month, day);
            if (today == easter) return Feast.Easter;
            if (today == easter + 49) return Feast.Whitsun;
            if ((today + 1) % 7 == 0) return Feast.Sunday;
            return Feast.None;
        }

        public static Feast FeastOn(Calendar c) => FeastOn(c.Year, c.Month, c.Day);

        /// <summary>Feasts that bring a market, a hiring fair or a melee.</summary>
        public static bool IsFair(Feast f) => f == Feast.Michaelmas || f == Feast.Lammas || f == Feast.Midsummer || f == Feast.Whitsun || f == Feast.Martinmas;

        public static string Name(Feast f)
        {
            switch (f)
            {
                case Feast.Sunday: return "Sunday";
                case Feast.Christmas: return "Christmas";
                case Feast.Epiphany: return "Epiphany";
                case Feast.Candlemas: return "Candlemas";
                case Feast.Easter: return "Easter";
                case Feast.Whitsun: return "Whitsun";
                case Feast.Midsummer: return "Midsummer";
                case Feast.Lammas: return "Lammas";
                case Feast.Michaelmas: return "Michaelmas";
                case Feast.AllSaints: return "All Saints";
                case Feast.Martinmas: return "Martinmas";
                default: return string.Empty;
            }
        }

        /// <summary>Mass is sung between Terce and Sext; attendance counts inside that window on Sundays and feasts.</summary>
        public static bool IsMassWindow(Calendar c, TimeBalance t)
        {
            Feast f = FeastOn(c);
            if (f == Feast.None) return false;
            float start = t.bellHours.Length > 1 ? t.bellHours[1] : 9f, end = t.bellHours.Length > 2 ? t.bellHours[2] : 12f;
            return c.HourOfDay >= start && c.HourOfDay < end;
        }
    }
}
