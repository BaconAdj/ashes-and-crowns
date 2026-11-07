namespace AshesAndCrowns.Sim
{
    /// <summary>Pure damage arithmetic shared by soldiers (Burst jobs) and the player's melee.</summary>
    public static class CombatMath
    {
        /// <param name="baseDamage">Weapon damage before armour.</param>
        /// <param name="variance">0..1: roll scales damage within [1-variance, 1+variance].</param>
        /// <param name="roll01">A uniform random number in [0,1).</param>
        /// <param name="armour">0..1 fraction of damage absorbed.</param>
        public static float Damage(float baseDamage, float variance, float roll01, float armour)
        {
            float scale = 1f + (roll01 * 2f - 1f) * variance;
            float raw = baseDamage * scale;
            float absorbed = armour < 0f ? 0f : (armour > 1f ? 1f : armour);
            float result = raw * (1f - absorbed);
            return result < 0f ? 0f : result;
        }

        /// <summary>True when target is inside a forward-facing arc. Vectors are 2D (x, z); forward need not be normalised.</summary>
        public static bool InArc(float toX, float toZ, float fwdX, float fwdZ, float arcDegrees)
        {
            float lenTo = (float)System.Math.Sqrt(toX * toX + toZ * toZ);
            float lenF = (float)System.Math.Sqrt(fwdX * fwdX + fwdZ * fwdZ);
            if (lenTo < 1e-5f || lenF < 1e-5f) return true;
            float cos = (toX * fwdX + toZ * fwdZ) / (lenTo * lenF);
            float half = arcDegrees * 0.5f * (3.14159265f / 180f);
            return cos >= (float)System.Math.Cos(half);
        }
    }
}
