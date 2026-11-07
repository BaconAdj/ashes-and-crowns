using UnityEngine;

namespace AshesAndCrowns.Battle
{
    /// <summary>Simple banner poles that follow each unit's centroid so units are readable from the hill.</summary>
    public sealed class UnitBanners : MonoBehaviour
    {
        public BattleDirector director;
        public Material playerBanner;
        public Material enemyBanner;
        private Transform[] _poles = new Transform[0];

        public void Build()
        {
            foreach (var p in _poles) if (p != null) Destroy(p.gameObject);
            _poles = new Transform[director.UnitCount];
            for (int u = 0; u < director.UnitCount; u++)
            {
                var root = new GameObject("Banner_" + u).transform;
                root.SetParent(transform, false);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(root, false);
                pole.transform.localScale = new Vector3(0.08f, 2.2f, 0.08f);
                pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                pole.GetComponent<Renderer>().sharedMaterial = enemyBanner; // dark wood-ish; shared for both
                var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(flag.GetComponent<Collider>());
                flag.transform.SetParent(root, false);
                flag.transform.localScale = new Vector3(1.4f, 0.9f, 0.04f);
                flag.transform.localPosition = new Vector3(0.7f, 3.9f, 0f);
                flag.GetComponent<Renderer>().sharedMaterial = director.Units[u].Side == (byte)Side.Player ? playerBanner : enemyBanner;
                _poles[u] = root;
            }
        }

        private void LateUpdate()
        {
            if (director == null || !director.IsReady) return;
            for (int u = 0; u < _poles.Length && u < director.UnitCount; u++)
            {
                var unit = director.Units[u];
                bool alive = unit.Alive != 0;
                if (_poles[u].gameObject.activeSelf != alive) _poles[u].gameObject.SetActive(alive);
                if (!alive) continue;
                Vector3 c = director.UnitCentroids[u];
                _poles[u].position = c;
                _poles[u].rotation = Quaternion.Euler(0f, unit.Yaw * Mathf.Rad2Deg, 0f);
            }
        }
    }
}
