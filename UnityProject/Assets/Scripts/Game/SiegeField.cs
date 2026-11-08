using AshesAndCrowns.Battle;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>The curtain wall of the siege scene: parapet, gate and towers are built by the editor; ladders are placed at play time from the layout.</summary>
    public sealed class SiegeField : MonoBehaviour
    {
        public GameObject gate;
        public Material ladderMaterial;
        public Transform ladderRoot;
        public float wallHeight = 5f;

        public void Show(SiegeLayout layout, Terrain terrain)
        {
            if (gate != null) gate.SetActive(layout.GateOpen == 0);
            if (ladderRoot == null) { ladderRoot = new GameObject("Ladders").transform; ladderRoot.SetParent(transform, false); }
            for (int i = ladderRoot.childCount - 1; i >= 0; i--) Destroy(ladderRoot.GetChild(i).gameObject);
            for (int k = 0; k < layout.LadderCount; k++)
            {
                float x = layout.LadderX(k), z = layout.WallZ - layout.WallHalf * 0.5f;
                float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, layout.WallZ - layout.WallHalf)) + terrain.transform.position.y : 0f;
                var ladder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ladder.name = "Ladder_" + k;
                Object.Destroy(ladder.GetComponent<Collider>());
                ladder.transform.SetParent(ladderRoot, false);
                ladder.transform.position = new Vector3(x, y + wallHeight * 0.6f, z);
                ladder.transform.rotation = Quaternion.Euler(-18f, 0f, 0f);
                ladder.transform.localScale = new Vector3(0.6f, wallHeight * 1.3f, 0.15f);
                if (ladderMaterial != null) ladder.GetComponent<MeshRenderer>().sharedMaterial = ladderMaterial;
            }
        }

        public void OnGateBroken() { if (gate != null) gate.SetActive(false); }
    }
}
