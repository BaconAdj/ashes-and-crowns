using System;

namespace AshesAndCrowns.Sim
{
    [Serializable] public sealed class ActivityBalance
    {
        // Prayer: kneel at the altar for as long as you like. The clock runs faster; favour grows with diminishing returns.
        public float prayTimeScale = 12f;              // game runs this many times faster while you kneel
        public float prayFavourPerHour = 0.05f;        // village standing gained by the first hour of prayer
        public float prayHalfLifeHours = 1.0f;         // each further hour of the day earns half as much as the last
        public float prayMaxPerDay = 0.09f;            // nothing more is earned after this, however long you kneel
        public float prayMinMinutesToCount = 5f;       // a token kneel earns nothing and is noticed
        // Work: a day's labour runs fast, punctuated by hands-on beats you must time with X.
        public float workTimeScale = 24f;              // ten game hours in about 37 real seconds at 90 s per hour
        public int workBeats = 6;                      // beats across the day
        public float beatSweepSeconds = 1.4f;          // marker travels end to end in this long (real seconds)
        public float beatZoneWidth = 0.18f;            // fraction of the track that counts as a good strike
        public float beatMissPenalty = 0.6f;           // a beat you never press scores this much less than a poor press
    }

    /// <summary>Pure rules for hands-on activities: prayer favour and timing beats. Deterministic, allocation-free.</summary>
    public static class ActivityRules
    {
        /// <summary>Favour earned by praying from hoursAlready to hoursAlready + hours today. Geometric decay by the hour, capped per day.</summary>
        public static float PrayerGain(float hoursAlready, float hours, ActivityBalance b)
        {
            if (hours <= 0f || b.prayHalfLifeHours <= 0f) return 0f;
            float k = (float)Math.Log(2.0) / b.prayHalfLifeHours;
            float before = Integral(hoursAlready, k, b.prayFavourPerHour);
            float after = Integral(hoursAlready + hours, k, b.prayFavourPerHour);
            float gain = Math.Min(after, b.prayMaxPerDay) - Math.Min(before, b.prayMaxPerDay);
            return Math.Max(0f, gain);
        }

        // ∫0..t r e^{-k x} dx = r (1 - e^{-k t}) / k, scaled so the first hour is worth r.
        private static float Integral(float t, float k, float r)
        {
            float firstHour = (1f - (float)Math.Exp(-k)) / k;
            return r * (1f - (float)Math.Exp(-k * t)) / k / firstHour;
        }

        /// <summary>Quality 0..1 of a beat pressed with the marker at pos (0..1, zone centred at 0.5).</summary>
        public static float BeatQuality(float pos, float zoneWidth)
        {
            float d = Math.Abs(pos - 0.5f);
            float half = zoneWidth / 2f;
            if (d <= half + 1e-4f) return 1f - Math.Min(1f, d / half) * 0.5f;   // 1.0 dead centre .. 0.5 at the edge
            return Math.Max(0f, 0.3f - d);                       // a near miss is worth a little
        }

        /// <summary>Day quality from the beats scored (missed beats count as 0) — the number work results read.</summary>
        public static float DayQuality(float sum, int pressed, int total, ActivityBalance b)
        {
            if (total <= 0) return 0.5f;
            int missed = Math.Max(0, total - pressed);
            float q = (sum - missed * b.beatMissPenalty * 0.1f) / total;
            return q < 0f ? 0f : (q > 1f ? 1f : q);
        }

        /// <summary>Marker position for a ping-pong sweep at time t (real seconds).</summary>
        public static float MarkerPosition(float t, float sweepSeconds)
        {
            if (sweepSeconds <= 0f) return 0.5f;
            float x = (t / sweepSeconds) % 2f;
            return x <= 1f ? x : 2f - x;
        }
    }
}
