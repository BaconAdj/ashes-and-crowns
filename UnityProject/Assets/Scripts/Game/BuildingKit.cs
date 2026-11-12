using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    public enum BuildingKind : byte { Hut = 0, Alehouse = 1, Church = 2, Hall = 3, TownHouse = 4, Chapel = 5 }   // Hall: a knight's hall house, the alehouse shell furnished for a lord of small account

    /// <summary>
    /// What goes inside the hollow buildings and where their doors are. Local positions match the generators in
    /// Tools/blender (hut door on +Z, alehouse porch on +Z, church south door on +X at z = -4.5 with the chancel on +Z). Used by the
    /// editor village builder for Ashby and by the runtime spawner for every other village, so both agree.
    /// </summary>
    public static class BuildingKit
    {
        public const float HutFloor = 0.3f, AleFloor = 0.35f, ChurchFloor = 0.4f, TownFloor = 0.3f, ChapelFloor = 0.35f;

        /// <summary>Just outside the door, in local space (where a visitor stands before stepping in).</summary>
        public static Vector3 DoorOutside(BuildingKind k)
        {
            switch (k)
            {
                case BuildingKind.Hut: return new Vector3(0f, HutFloor, 2.0f + 1.2f);
                case BuildingKind.Alehouse: case BuildingKind.Hall: return new Vector3(0f, AleFloor, 3.5f + 1.9f + 1.2f);
                case BuildingKind.TownHouse: return new Vector3(0f, TownFloor, 4.5f + 1.2f);
                case BuildingKind.Chapel: return new Vector3(2.75f + 1.4f, ChapelFloor, -2.5f);
                default: return new Vector3(4.5f + 1.5f, ChurchFloor, -4.5f);
            }
        }

        /// <summary>Just inside the door.</summary>
        public static Vector3 DoorInside(BuildingKind k)
        {
            switch (k)
            {
                case BuildingKind.Hut: return new Vector3(0f, HutFloor, 2.0f - 1.0f);
                case BuildingKind.Alehouse: case BuildingKind.Hall: return new Vector3(0f, AleFloor, 3.5f - 1.0f);
                case BuildingKind.TownHouse: return new Vector3(0f, TownFloor, 4.5f - 1.1f);
                case BuildingKind.Chapel: return new Vector3(2.75f - 1.3f, ChapelFloor, -2.5f);
                default: return new Vector3(4.5f - 1.4f, ChurchFloor, -4.5f);
            }
        }

        /// <summary>The doorway in local space: the middle of the opening on the floor, which way is out, how wide and high.</summary>
        public static void DoorFrame(BuildingKind k, out Vector3 centre, out Vector3 outward, out float width, out float height)
        {
            switch (k)
            {
                case BuildingKind.Hut: centre = new Vector3(0f, HutFloor, 2.0f - 0.14f); outward = Vector3.forward; width = 1.2f; height = 2.0f; break;
                case BuildingKind.Alehouse: case BuildingKind.Hall: centre = new Vector3(0f, AleFloor, 3.5f - 0.15f); outward = Vector3.forward; width = 1.3f; height = 2.3f; break;
                case BuildingKind.TownHouse: centre = new Vector3(0f, TownFloor, 4.5f - 0.14f); outward = Vector3.forward; width = 1.2f; height = 2.05f; break;
                case BuildingKind.Chapel: centre = new Vector3(2.75f - 0.2f, ChapelFloor, -2.5f); outward = Vector3.right; width = 1.3f; height = 2.3f; break;
                default: centre = new Vector3(4.5f - 0.25f, ChurchFloor, -4.5f); outward = Vector3.right; width = 1.6f; height = 2.6f; break;
            }
        }

        /// <summary>What secures a building of this kind. Peasant doors are barred from within; halls and churches can afford iron.</summary>
        public static LockKind LockFor(BuildingKind k) => k == BuildingKind.Hut || k == BuildingKind.TownHouse ? LockKind.Bar : k == BuildingKind.Alehouse ? LockKind.Bar : LockKind.Warded;

        /// <summary>Hang a plank door in the doorway: a hinge that stays put, a leaf that swings, iron straps. Safe to call twice.</summary>
        public static Door HangDoor(Transform b, BuildingKind k, Material wood, Material iron)
        {
            Transform old = b.Find("Hinge"); if (old != null) return old.GetComponent<Door>();
            DoorFrame(k, out Vector3 centre, out Vector3 outward, out float width, out float height);
            var hinge = new GameObject("Hinge").transform; hinge.SetParent(b, false);
            Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);
            hinge.localRotation = rot; hinge.localPosition = centre - rot * Vector3.right * (width * 0.5f);
            var leaf = new GameObject("Leaf").transform; leaf.SetParent(hinge, false);
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube); plank.name = "Plank"; plank.transform.SetParent(leaf, false);
            plank.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f); plank.transform.localScale = new Vector3(width - 0.04f, height - 0.04f, 0.07f);
            plank.GetComponent<Renderer>().sharedMaterial = wood;
            for (int i = 0; i < 2; i++)
            {
                var strap = GameObject.CreatePrimitive(PrimitiveType.Cube); strap.name = "Strap"; Object.DestroyImmediate(strap.GetComponent<Collider>()); strap.transform.SetParent(leaf, false);
                strap.transform.localPosition = new Vector3(width * 0.36f, height * (i == 0 ? 0.22f : 0.78f), 0f); strap.transform.localScale = new Vector3(width * 0.7f, 0.07f, 0.1f);
                strap.GetComponent<Renderer>().sharedMaterial = iron != null ? iron : wood;
            }
            var door = hinge.gameObject.AddComponent<Door>();
            door.building = b; door.leaf = leaf; door.plank = plank.GetComponent<Collider>(); door.plank.enabled = false; door.lockKind = LockFor(k); door.width = width;
            return door;
        }

        /// <summary>Furnish a placed building: props as children in local space plus named markers the game uses
        /// ("Door", "Inside", and "Bed"/"Hearth"/"Bar"/"Altar"/"Nave" by kind). Materials come from the world assets.</summary>
        public static void Furnish(Transform b, BuildingKind k, Material wood, Material stone, Material linen, Material fire, Material iron, bool withLight)
        {
            Marker(b, "Door", DoorOutside(k)); Marker(b, "Inside", DoorInside(k));
            HangDoor(b, k, wood, iron);
            switch (k)
            {
                case BuildingKind.TownHouse:
                    {
                        // A burgess's house, gable to the street: the shop at the front under the window, the hearth in the hall
                        // behind it, the bed and the chest at the back by the yard door.
                        float f = TownFloor;
                        Prop(b, "Counter", PrimitiveType.Cube, new Vector3(1.55f, f + 0.45f, 3.2f), new Vector3(0.7f, 0.9f, 1.8f), wood, collider: true);
                        Prop(b, "Shelf", PrimitiveType.Cube, new Vector3(2.2f, f + 1.5f, 3.2f), new Vector3(0.3f, 0.06f, 1.8f), wood);
                        Prop(b, "Pot", PrimitiveType.Sphere, new Vector3(2.2f, f + 1.65f, 3.5f), new Vector3(0.24f, 0.24f, 0.24f), iron);
                        Prop(b, "Screen", PrimitiveType.Cube, new Vector3(-1.2f, f + 1.0f, 1.9f), new Vector3(2.4f, 2.0f, 0.06f), wood);
                        Prop(b, "HearthStone", PrimitiveType.Cylinder, new Vector3(0f, f + 0.08f, 0f), new Vector3(1.1f, 0.08f, 1.1f), stone);
                        Prop(b, "Embers", PrimitiveType.Sphere, new Vector3(0f, f + 0.2f, 0f), new Vector3(0.45f, 0.18f, 0.45f), fire);
                        Prop(b, "Table", PrimitiveType.Cube, new Vector3(-1.5f, f + 0.7f, -0.6f), new Vector3(0.8f, 0.07f, 1.8f), wood, collider: true);
                        Prop(b, "Trestle", PrimitiveType.Cube, new Vector3(-1.5f, f + 0.35f, -0.6f), new Vector3(0.15f, 0.7f, 1.4f), wood);
                        Prop(b, "Bench", PrimitiveType.Cube, new Vector3(-0.85f, f + 0.22f, -0.6f), new Vector3(0.28f, 0.06f, 1.7f), wood);
                        Prop(b, "Pallet", PrimitiveType.Cube, new Vector3(-1.6f, f + 0.2f, -3.3f), new Vector3(1.3f, 0.36f, 2.0f), linen, collider: true);
                        Prop(b, "ChestBox", PrimitiveType.Cube, new Vector3(1.8f, f + 0.3f, -3.6f), new Vector3(1.0f, 0.6f, 0.55f), wood, collider: true);
                        Prop(b, "Stool", PrimitiveType.Cylinder, new Vector3(1.0f, f + 0.2f, 0.5f), new Vector3(0.4f, 0.2f, 0.4f), wood);
                        Marker(b, "Hearth", new Vector3(0.9f, f, 0f)); Marker(b, "Bed", new Vector3(-0.7f, f, -3.3f)); Marker(b, "Chest", new Vector3(1.8f, f, -2.9f));
                        Marker(b, "Bench", new Vector3(0.7f, f, 3.2f)); Marker(b, "Seat", new Vector3(-0.5f, f, -0.6f));
                        if (withLight) Light(b, new Vector3(0f, f + 0.7f, 0f), new Color(1f, 0.6f, 0.3f), 8f, 1.6f);
                        break;
                    }
                case BuildingKind.Chapel:
                    {
                        // A parish church of one cell and a chancel: altar, a font by the door, nowhere to sit but the wall bench.
                        float f = ChapelFloor;
                        Prop(b, "AltarStone", PrimitiveType.Cube, new Vector3(0f, f + 0.5f, 8.5f), new Vector3(1.6f, 1.0f, 0.8f), stone, collider: true);
                        Prop(b, "Cloth", PrimitiveType.Cube, new Vector3(0f, f + 1.02f, 8.5f), new Vector3(1.7f, 0.04f, 0.9f), linen);
                        Prop(b, "Candle", PrimitiveType.Cylinder, new Vector3(-0.5f, f + 1.3f, 8.5f), new Vector3(0.08f, 0.25f, 0.08f), linen);
                        Prop(b, "Flame", PrimitiveType.Sphere, new Vector3(-0.5f, f + 1.6f, 8.5f), new Vector3(0.08f, 0.12f, 0.08f), fire);
                        Prop(b, "Font", PrimitiveType.Cylinder, new Vector3(-1.6f, f + 0.45f, -3.8f), new Vector3(0.8f, 0.45f, 0.8f), stone, collider: true);
                        Prop(b, "Bench", PrimitiveType.Cube, new Vector3(-2.2f, f + 0.22f, 1.0f), new Vector3(0.35f, 0.08f, 5.0f), wood);
                        Marker(b, "Altar", new Vector3(0f, f, 6.9f)); Marker(b, "Nave", new Vector3(0f, f, 0.5f));
                        Marker(b, "Seat", new Vector3(-1.4f, f, 0f)); Marker(b, "Seat", new Vector3(1.2f, f, 1.5f)); Marker(b, "Seat", new Vector3(-1.2f, f, 3.0f)); Marker(b, "Seat", new Vector3(1.0f, f, -1.2f));
                        if (withLight) Light(b, new Vector3(0f, f + 1.6f, 8.0f), new Color(1f, 0.78f, 0.5f), 7f, 1.2f);
                        break;
                    }
                case BuildingKind.Hut:
                    {
                        float f = HutFloor;
                        Prop(b, "HearthStone", PrimitiveType.Cylinder, new Vector3(0f, f + 0.08f, -0.2f), new Vector3(1.1f, 0.08f, 1.1f), stone);
                        Prop(b, "Embers", PrimitiveType.Sphere, new Vector3(0f, f + 0.2f, -0.2f), new Vector3(0.45f, 0.18f, 0.45f), fire);
                        Prop(b, "Pallet", PrimitiveType.Cube, new Vector3(-1.9f, f + 0.17f, -1.0f), new Vector3(1.0f, 0.3f, 2.0f), linen, collider: true);
                        Prop(b, "ChestBox", PrimitiveType.Cube, new Vector3(2.0f, f + 0.3f, -1.3f), new Vector3(0.9f, 0.6f, 0.55f), wood, collider: true);
                        Prop(b, "Stool", PrimitiveType.Cylinder, new Vector3(1.2f, f + 0.2f, 0.4f), new Vector3(0.4f, 0.2f, 0.4f), wood);
                        Prop(b, "Shelf", PrimitiveType.Cube, new Vector3(0f, f + 1.5f, -1.75f), new Vector3(2.2f, 0.06f, 0.3f), wood);
                        Prop(b, "Pot", PrimitiveType.Sphere, new Vector3(0.4f, f + 1.65f, -1.75f), new Vector3(0.26f, 0.26f, 0.26f), iron);
                        Marker(b, "Hearth", new Vector3(0.9f, f, -0.2f)); Marker(b, "Bed", new Vector3(-1.2f, f, -1.0f)); Marker(b, "Chest", new Vector3(2.0f, f, -0.6f));
                        if (withLight) Light(b, new Vector3(0f, f + 0.7f, -0.2f), new Color(1f, 0.6f, 0.3f), 7f, 1.6f);
                        break;
                    }
                case BuildingKind.Alehouse:
                    {
                        float f = AleFloor;
                        Prop(b, "Counter", PrimitiveType.Cube, new Vector3(-3.0f, f + 0.5f, -2.3f), new Vector3(3.2f, 1.0f, 0.7f), wood, collider: true);
                        for (int i = 0; i < 3; i++) Prop(b, "Cask", PrimitiveType.Cylinder, new Vector3(-4.2f + i * 0.9f, f + 0.45f, -3.0f), new Vector3(0.7f, 0.45f, 0.7f), wood);
                        for (int i = 0; i < 2; i++)
                        {
                            float x = 1.0f + i * 2.6f;
                            Prop(b, "Table", PrimitiveType.Cube, new Vector3(x, f + 0.72f, 0f), new Vector3(1.0f, 0.08f, 2.4f), wood, collider: true);
                            Prop(b, "Trestle", PrimitiveType.Cube, new Vector3(x, f + 0.35f, 0f), new Vector3(0.2f, 0.7f, 1.8f), wood);
                            Prop(b, "Bench", PrimitiveType.Cube, new Vector3(x - 0.8f, f + 0.22f, 0f), new Vector3(0.3f, 0.06f, 2.2f), wood, collider: true);
                            Prop(b, "Bench", PrimitiveType.Cube, new Vector3(x + 0.8f, f + 0.22f, 0f), new Vector3(0.3f, 0.06f, 2.2f), wood, collider: true);
                        }
                        Prop(b, "HearthStone", PrimitiveType.Cylinder, new Vector3(-1.5f, f + 0.08f, 1.8f), new Vector3(1.3f, 0.08f, 1.3f), stone);
                        Prop(b, "Fire", PrimitiveType.Sphere, new Vector3(-1.5f, f + 0.25f, 1.8f), new Vector3(0.6f, 0.3f, 0.6f), fire);
                        Marker(b, "Bar", new Vector3(-3.0f, f, -1.4f)); Marker(b, "Hearth", new Vector3(-1.5f, f, 0.6f));
                        Prop(b, "NoticeBoard", PrimitiveType.Cube, new Vector3(1.8f, f + 1.5f, 3.5f + 0.3f), new Vector3(0.9f, 0.7f, 0.06f), wood);
                        Marker(b, "Notice", new Vector3(1.8f, f, 3.5f + 1.2f));
                        Marker(b, "Seat", new Vector3(0.2f, f, 0f)); Marker(b, "Seat", new Vector3(1.8f, f, 0f)); Marker(b, "Seat", new Vector3(2.8f, f, 0f)); Marker(b, "Seat", new Vector3(4.4f, f, 0f));
                        if (withLight) { Light(b, new Vector3(-1.5f, f + 0.9f, 1.8f), new Color(1f, 0.62f, 0.3f), 10f, 2.2f); Light(b, new Vector3(2.5f, f + 2.2f, 0f), new Color(1f, 0.75f, 0.45f), 7f, 0.9f); }
                        break;
                    }
                case BuildingKind.Hall:
                    {
                        float f = AleFloor;
                        // The dais at the west end with the high table; the central hearth; a curtained bed at the east end; chests.
                        Prop(b, "Dais", PrimitiveType.Cube, new Vector3(-3.6f, f + 0.12f, 0f), new Vector3(2.4f, 0.24f, 5.6f), wood, collider: true);
                        Prop(b, "HighTable", PrimitiveType.Cube, new Vector3(-3.6f, f + 0.24f + 0.74f, 0f), new Vector3(0.9f, 0.08f, 3.6f), wood, collider: true);
                        Prop(b, "Chair", PrimitiveType.Cube, new Vector3(-4.3f, f + 0.24f + 0.5f, 0f), new Vector3(0.5f, 1.0f, 0.5f), wood);
                        Prop(b, "HearthStone", PrimitiveType.Cylinder, new Vector3(0f, f + 0.08f, 0f), new Vector3(1.5f, 0.08f, 1.5f), stone);
                        Prop(b, "Fire", PrimitiveType.Sphere, new Vector3(0f, f + 0.28f, 0f), new Vector3(0.7f, 0.35f, 0.7f), fire);
                        for (int i = 0; i < 2; i++)
                        {
                            float z = i == 0 ? -2.0f : 2.0f;
                            Prop(b, "Trestle", PrimitiveType.Cube, new Vector3(1.0f, f + 0.72f, z), new Vector3(3.0f, 0.08f, 0.8f), wood, collider: true);
                            Prop(b, "Bench", PrimitiveType.Cube, new Vector3(1.0f, f + 0.22f, z + (i == 0 ? 0.7f : -0.7f)), new Vector3(2.8f, 0.06f, 0.3f), wood, collider: true);
                        }
                        Prop(b, "Pallet", PrimitiveType.Cube, new Vector3(3.9f, f + 0.25f, -2.0f), new Vector3(1.3f, 0.45f, 2.2f), linen, collider: true);
                        Prop(b, "Curtain", PrimitiveType.Cube, new Vector3(3.0f, f + 1.2f, -1.0f), new Vector3(0.06f, 2.3f, 2.6f), linen);
                        Prop(b, "ChestBox", PrimitiveType.Cube, new Vector3(4.2f, f + 0.35f, 1.6f), new Vector3(1.1f, 0.7f, 0.6f), wood, collider: true);
                        Prop(b, "ChestBox", PrimitiveType.Cube, new Vector3(4.2f, f + 0.35f, 2.6f), new Vector3(1.1f, 0.7f, 0.6f), wood, collider: true);
                        Prop(b, "Shield", PrimitiveType.Cylinder, new Vector3(-4.75f, f + 2.0f, 0f), new Vector3(0.9f, 0.04f, 0.9f), iron);
                        Marker(b, "Hearth", new Vector3(1.0f, f, 0f)); Marker(b, "Bed", new Vector3(3.9f, f, -0.5f)); Marker(b, "Dais", new Vector3(-3.0f, f + 0.24f, 0f)); Marker(b, "Chest", new Vector3(3.4f, f, 2.1f));
                        Marker(b, "Seat", new Vector3(1.0f, f, -1.2f)); Marker(b, "Seat", new Vector3(1.0f, f, 1.2f)); Marker(b, "Seat", new Vector3(-0.2f, f, -2.6f)); Marker(b, "Seat", new Vector3(2.2f, f, 2.6f));
                        if (withLight) { Light(b, new Vector3(0f, f + 1.0f, 0f), new Color(1f, 0.62f, 0.3f), 11f, 2.4f); Light(b, new Vector3(-3.6f, f + 2.0f, 0f), new Color(1f, 0.75f, 0.45f), 7f, 0.9f); }
                        break;
                    }
                default:
                    {
                        float f = ChurchFloor;
                        // Chancel beyond the arch on +Z: altar, two candles. Nave: a font by the door, two rows of benches.
                        Prop(b, "AltarStone", PrimitiveType.Cube, new Vector3(0f, f + 0.5f, 12.5f), new Vector3(2.0f, 1.0f, 0.9f), stone, collider: true);
                        Prop(b, "Cloth", PrimitiveType.Cube, new Vector3(0f, f + 1.02f, 12.5f), new Vector3(2.1f, 0.04f, 1.0f), linen);
                        Prop(b, "Candle", PrimitiveType.Cylinder, new Vector3(-0.7f, f + 1.3f, 12.5f), new Vector3(0.08f, 0.25f, 0.08f), linen);
                        Prop(b, "Candle", PrimitiveType.Cylinder, new Vector3(0.7f, f + 1.3f, 12.5f), new Vector3(0.08f, 0.25f, 0.08f), linen);
                        Prop(b, "Flame", PrimitiveType.Sphere, new Vector3(-0.7f, f + 1.6f, 12.5f), new Vector3(0.08f, 0.12f, 0.08f), fire);
                        Prop(b, "Flame", PrimitiveType.Sphere, new Vector3(0.7f, f + 1.6f, 12.5f), new Vector3(0.08f, 0.12f, 0.08f), fire);
                        Prop(b, "Step", PrimitiveType.Cube, new Vector3(0f, f + 0.08f, 9.4f), new Vector3(7.0f, 0.16f, 0.6f), stone, collider: true);
                        Prop(b, "Font", PrimitiveType.Cylinder, new Vector3(2.5f, f + 0.45f, -6.5f), new Vector3(0.9f, 0.45f, 0.9f), stone, collider: true);
                        for (int i = 0; i < 4; i++)
                        {
                            float z = -4.0f + i * 2.6f;
                            Prop(b, "Bench", PrimitiveType.Cube, new Vector3(-2.2f, f + 0.22f, z), new Vector3(2.6f, 0.08f, 0.35f), wood, collider: true);
                            Prop(b, "Bench", PrimitiveType.Cube, new Vector3(2.2f, f + 0.22f, z), new Vector3(2.6f, 0.08f, 0.35f), wood, collider: true);
                        }
                        Marker(b, "Altar", new Vector3(0f, f, 10.6f)); Marker(b, "Nave", new Vector3(0f, f, 0f));
                        Marker(b, "Seat", new Vector3(-2.2f, f, -4.6f)); Marker(b, "Seat", new Vector3(2.2f, f, -2.0f)); Marker(b, "Seat", new Vector3(-2.2f, f, 0.6f)); Marker(b, "Seat", new Vector3(2.2f, f, 3.2f));
                        if (withLight) { Light(b, new Vector3(0f, f + 1.8f, 12.0f), new Color(1f, 0.78f, 0.5f), 9f, 1.4f); Light(b, new Vector3(0f, f + 3.0f, -1f), new Color(0.75f, 0.8f, 0.95f), 12f, 0.5f); }
                        break;
                    }
            }
        }

        private static void Prop(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null && !collider) Object.DestroyImmediate(col);
            go.transform.SetParent(Furniture(parent), false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Everything movable inside a building hangs under one child, so a town can put interiors away at a distance.
        /// Markers and the door hinge stay direct children: the game finds those by name.</summary>
        public static Transform Furniture(Transform building)
        {
            Transform f = building.Find("Furniture");
            if (f == null) { f = new GameObject("Furniture").transform; f.SetParent(building, false); }
            return f;
        }

        private static void Marker(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        }

        private static void Light(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("InteriorLight"); go.transform.SetParent(Furniture(parent), false); go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        /// <summary>A named marker's world position, or the building's own if it has none.</summary>
        public static Transform Find(Transform building, string marker, int index = 0)
        {
            if (building == null) return null;
            int n = 0;
            for (int i = 0; i < building.childCount; i++) { var c = building.GetChild(i); if (c.name == marker) { if (n == index) return c; n++; } }
            return null;
        }
    }
}
