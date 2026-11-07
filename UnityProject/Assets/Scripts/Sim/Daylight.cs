using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>Solar geometry for southern England (about 52 degrees north), local solar time. Pure functions.</summary>
    public static class Daylight
    {
        public const float LatitudeDegrees = 52f;
        private const float Deg = (float)(Math.PI / 180.0);

        public static float DeclinationDegrees(int dayOfYear) => 23.44f * (float)Math.Sin(2.0 * Math.PI * (284 + dayOfYear) / 365.0);

        public static float SunElevationDegrees(float hourOfDay, int dayOfYear)
        {
            float dec = DeclinationDegrees(dayOfYear) * Deg;
            float lat = LatitudeDegrees * Deg;
            float hourAngle = (hourOfDay - 12f) * 15f * Deg;
            double sinEl = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngle);
            return (float)(Math.Asin(Math.Max(-1.0, Math.Min(1.0, sinEl))) / Deg);
        }

        /// <summary>Compass azimuth of the sun in degrees (0 = north, 90 = east, 180 = south).</summary>
        public static float SunAzimuthDegrees(float hourOfDay, int dayOfYear)
        {
            float dec = DeclinationDegrees(dayOfYear) * Deg;
            float lat = LatitudeDegrees * Deg;
            float hourAngle = (hourOfDay - 12f) * 15f * Deg;
            double el = SunElevationDegrees(hourOfDay, dayOfYear) * Deg;
            double cosAz = (Math.Sin(dec) - Math.Sin(el) * Math.Sin(lat)) / Math.Max(1e-5, Math.Cos(el) * Math.Cos(lat));
            double az = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAz))) / Deg;
            return hourOfDay < 12f ? (float)az : (float)(360.0 - az);
        }

        public static void SunriseSunset(int dayOfYear, out float sunriseHour, out float sunsetHour)
        {
            float dec = DeclinationDegrees(dayOfYear) * Deg;
            float lat = LatitudeDegrees * Deg;
            double cosH = -Math.Tan(lat) * Math.Tan(dec);
            cosH = Math.Max(-1.0, Math.Min(1.0, cosH));
            float half = (float)(Math.Acos(cosH) / Deg / 15.0);
            sunriseHour = 12f - half; sunsetHour = 12f + half;
        }

        public static float DayLengthHours(int dayOfYear) { SunriseSunset(dayOfYear, out float r, out float s); return s - r; }

        /// <summary>0 at night, 1 in full day, smooth through civil twilight (sun between -6 and +6 degrees).</summary>
        public static float DaylightFactor(float hourOfDay, int dayOfYear)
        {
            float el = SunElevationDegrees(hourOfDay, dayOfYear);
            float t = (el + 6f) / 12f;
            if (t <= 0f) return 0f; if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
