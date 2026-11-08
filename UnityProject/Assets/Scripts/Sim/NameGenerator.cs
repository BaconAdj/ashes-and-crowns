using System;

namespace AshesAndCrowns.Sim
{
    [Serializable] public sealed class NamesData
    {
        public string[] normanMale = new string[0], normanFemale = new string[0], englishMale = new string[0], englishFemale = new string[0];
        public string[] houses = new string[0], placeRoots = new string[0], placeSuffixes = new string[0], epithets = new string[0], traits = new string[0];
    }

    /// <summary>Deterministic names from names.json. Norman names for the nobility, English for the village.</summary>
    public sealed class NameGenerator
    {
        private readonly NamesData _d;
        private readonly DeterministicRng _rng;

        public NameGenerator(NamesData data, DeterministicRng rng)
        {
            _d = data ?? throw new ArgumentNullException(nameof(data));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        private string Pick(string[] a, string fallback) => a == null || a.Length == 0 ? fallback : a[_rng.Range(0, a.Length)];

        public string Given(bool female, bool norman) =>
            female ? Pick(norman ? _d.normanFemale : _d.englishFemale, "Edith") : Pick(norman ? _d.normanMale : _d.englishMale, "Wat");

        public string House() => Pick(_d.houses, "de Nulle");
        public string Epithet() => Pick(_d.epithets, "the Quiet");
        public string Trait() => Pick(_d.traits, "stubborn");
        public string PlaceName() => Pick(_d.placeRoots, "Ash") + Pick(_d.placeSuffixes, "ford");

        /// <summary>Two distinct traits.</summary>
        public void TwoTraits(out string a, out string b)
        {
            a = Trait(); b = Trait();
            int guard = 0;
            while (b == a && guard++ < 8) b = Trait();
        }
    }
}
