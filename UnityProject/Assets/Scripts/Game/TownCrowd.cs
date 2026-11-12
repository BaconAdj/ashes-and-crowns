using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A town of two thousand cannot all be bodies at once. Every household exists as a record (where it lives, what it
    /// lives by, which church and alehouse are its own); this component furnishes the houses and stands up the people of
    /// the streets you are actually in, and puts them away again behind you. Who they are is fixed by the household's
    /// number, so the baker of Fish Street is the same woman every time you pass.
    /// </summary>
    public sealed class TownCrowd : MonoBehaviour
    {
        [System.Serializable] public struct Home
        {
            public Transform building; public BuildingKind kind; public string calling, trade;
            public byte adults; public Workplace works; public Wealth wealth; public bool french, reserved;
            public Transform parish, alehouse, workSpot, corner;
        }

        public SettlementSite site;
        public WorldAssets assets;
        public Home[] homes = new Home[0];
        public Transform[] churches = new Transform[0]; public BuildingKind[] churchKinds = new BuildingKind[0];
        public Transform[] alehouses = new Transform[0];
        public float[] streets = new float[0];   // x0, z0, x1, z1 per street and road: what the townsfolk walk along
        public float furnishMetres = 60f, peopleMetres = 80f, putAwayMetres = 115f;
        public int maxBodies = 70;

        private Transform _player; private NamesData _names; private DeterministicRng _rng;
        private bool[] _furnished, _churchFurnished, _aleFurnished; private List<Villager>[] _bodies; private Transform[] _furniture;
        private int _bodyCount, _cursor; private float _next;
        private Transform _people;

        private void Start()
        {
            _furnished = new bool[homes.Length]; _bodies = new List<Villager>[homes.Length]; _furniture = new Transform[homes.Length];
            _churchFurnished = new bool[churches.Length]; _aleFurnished = new bool[alehouses.Length];
            _people = new GameObject("Townsfolk").transform; _people.SetParent(transform, false);
            _names = DataFiles.LoadNames();
            if (streets.Length >= 4) Villager.Streets = new TownRoutes(streets);
        }

        private void OnDestroy() { Villager.Streets = null; }

        private void Update()
        {
            if (Time.unscaledTime < _next || assets == null || site == null) return;
            _next = Time.unscaledTime + 0.25f;
            if (_player == null) { var pc = FindAnyObjectByType<PlayerController>(); if (pc == null) return; _player = pc.transform; }
            if (_rng == null) { if (GameBootstrap.Instance == null) return; _rng = GameBootstrap.Instance.Rng.Fork(120); }
            Vector3 p = _player.position;

            // The church and the alehouse of where you stand are the ones the prompt line and the bells mean.
            int nearChurch = Nearest(churches, p), nearAle = Nearest(alehouses, p);
            if (nearChurch >= 0)
            {
                if (!_churchFurnished[nearChurch]) { _churchFurnished[nearChurch] = true; Furnish(churches[nearChurch], churchKinds[nearChurch], LockKind.Warded, string.Empty); }
                site.church = churches[nearChurch];
            }
            if (nearAle >= 0)
            {
                if (!_aleFurnished[nearAle]) { _aleFurnished[nearAle] = true; Furnish(alehouses[nearAle], BuildingKind.Alehouse, LockKind.Bar, string.Empty); }
                site.alehouse = alehouses[nearAle];
            }

            // A slice of the households each tick: the whole town is looked at about once a second.
            int budget = 2, slice = Mathf.Max(1, homes.Length / 4);
            for (int n = 0; n < slice; n++)
            {
                int i = _cursor; _cursor = (_cursor + 1) % homes.Length;
                Transform b = homes[i].building; if (b == null) continue;
                float sq = (b.position - p).sqrMagnitude;
                if (sq < furnishMetres * furnishMetres && !_furnished[i] && budget > 0)
                {
                    budget--; _furnished[i] = true;
                    Furnish(b, homes[i].kind, homes[i].wealth == Wealth.Rich ? LockKind.Warded : LockKind.Bar, HouseholderName(i));
                    _furniture[i] = b.Find("Furniture");
                }
                if (_furniture[i] != null) { bool show = sq < (furnishMetres + 25f) * (furnishMetres + 25f); if (_furniture[i].gameObject.activeSelf != show) _furniture[i].gameObject.SetActive(show); }
                if (homes[i].reserved) continue;   // the player's own roof: its people are the player's kin
                if (sq < peopleMetres * peopleMetres && _furnished[i] && _bodies[i] == null && _bodyCount < maxBodies && budget > 0) { budget--; StandUp(i); }
                else if (sq > putAwayMetres * putAwayMetres && _bodies[i] != null) PutAway(i);
            }
        }

        private static int Nearest(Transform[] list, Vector3 p)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < list.Length; i++) { if (list[i] == null) continue; float d = (list[i].position - p).sqrMagnitude; if (d < bestD) { bestD = d; best = i; } }
            return best;
        }

        private void Furnish(Transform b, BuildingKind kind, LockKind lockKind, string householder)
        {
            if (BuildingKit.Find(b, "Door") == null)
                BuildingKit.Furnish(b, kind, assets.darkWood, assets.stone, assets.linen != null ? assets.linen : assets.wattle, assets.fire != null ? assets.fire : assets.thatch, assets.iron != null ? assets.iron : assets.slate, withLight: true);
            Door d = Door.For(b); if (d == null) { Transform h = b.Find("Hinge"); if (h != null) d = h.GetComponent<Door>(); }
            if (d != null) { d.lockKind = lockKind; d.householder = householder; }
        }

        private string HouseholderName(int i)
        {
            var gen = new NameGenerator(_names, new DeterministicRng(0x57A11UL + (ulong)i * 7919UL));
            bool female = homes[i].calling == "alewife" || homes[i].calling == "widow";
            return gen.Given(female, false) + " the " + homes[i].calling;
        }

        private void StandUp(int i)
        {
            Home h = homes[i];
            var gen = new NameGenerator(_names, new DeterministicRng(0x57A11UL + (ulong)i * 7919UL));
            var list = new List<Villager>(h.adults);
            bool headFemale = h.calling == "alewife" || h.calling == "widow";
            for (int k = 0; k < h.adults; k++)
            {
                bool female = k == 0 ? headFemale : !headFemale ? k == 1 : k != 1;
                string name = gen.Given(female, false);
                // The head of the house works the calling at its place; the rest keep the house and the shop board.
                Transform station = k == 0 ? (h.works == Workplace.Shop || h.works == Workplace.Home || h.workSpot == null ? BuildingKit.Find(h.building, "Bench") : h.workSpot) : BuildingKit.Find(h.building, "Bench");
                Transform stationBuilding = station != null && station.parent == h.building ? h.building : null;
                if (station == null) { station = BuildingKit.Find(h.building, "Hearth"); stationBuilding = h.building; }
                Mesh mesh = female && assets.woman != null ? assets.woman : assets.man;
                string trade = k == 0 ? h.calling : female ? h.calling + "'s wife" : h.calling + "'s man";
                Villager v = VillageKit.Spawn(_people, site, trade, h.building, name, female, 1000 + i * 4 + k, _rng, mesh, assets.villager != null ? assets.villager : assets.wattle, assets.skin != null ? assets.skin : assets.wattle,
                    assets.darkWood, assets.stone, assets.iron != null ? assets.iron : assets.slate, assets.terrain, kin: k > 0, station: station, stationBuilding: stationBuilding);
                v.church = h.parish; v.alehouse = h.alehouse; v.idleSpot = h.corner;
                v.Settle();
                list.Add(v);
            }
            _bodies[i] = list; _bodyCount += list.Count;
        }

        private void PutAway(int i)
        {
            foreach (Villager v in _bodies[i]) { if (v == null) continue; site.villagers.Remove(v); Destroy(v.gameObject); }
            _bodyCount -= _bodies[i].Count; _bodies[i] = null;
        }
    }
}
