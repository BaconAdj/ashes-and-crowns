using AshesAndCrowns.Sim;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AshesAndCrowns.Battle
{
    /// <summary>Spatial hashing helpers shared by the jobs. Cells are CellSize metres square.</summary>
    [BurstCompile]
    public static class SpatialHash
    {
        public static int Key(float x, float z, float cellSize)
        {
            int cx = (int)math.floor(x / cellSize);
            int cz = (int)math.floor(z / cellSize);
            return cx * 73856093 ^ cz * 19349663;
        }
    }

    /// <summary>Insert every living soldier into the hash map (parallel writer).</summary>
    [BurstCompile]
    public struct HashSoldiersJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> Alive;
        public float CellSize;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        public void Execute(int i)
        {
            if (Alive[i] == 0) return;
            Grid.Add(SpatialHash.Key(Positions[i].x, Positions[i].z, CellSize), i);
        }
    }

    /// <summary>
    /// Pick a target for each soldier: nearest living enemy in the 3x3 neighbouring cells,
    /// within the engage range that the unit's order allows. Enemy soldiers also target the
    /// player character when they are close enough.
    /// </summary>
    [BurstCompile]
    public struct TargetingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> Alive;
        [ReadOnly] public NativeArray<byte> SideOf;
        [ReadOnly] public NativeArray<int> UnitOf;
        [ReadOnly] public NativeArray<UnitState> Units;
        [ReadOnly] public NativeArray<byte> UnitStance;   // UnitStance per unit
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
        public BattleParams P;
        public float3 PlayerPosition;
        public byte PlayerSide;
        public byte PlayerAlive;
        public NativeArray<int> Targets;

        public void Execute(int i)
        {
            if (Alive[i] == 0) { Targets[i] = TargetIds.None; return; }
            byte myStance = UnitStance[UnitOf[i]];
            if (myStance == 2 || myStance == 3) { Targets[i] = TargetIds.None; return; } // routing or surrendered men do not fight
            float3 p = Positions[i];
            byte side = SideOf[i];
            UnitState unit = Units[UnitOf[i]];
            float range = unit.Order == (byte)OrderType.Hold ? P.EngageRangeHold : P.EngageRangeAdvance;

            int best = Targets[i];
            float bestD2 = float.MaxValue;
            // Keep the current target if it is still valid and close, to avoid flicker.
            if (best >= 0 && best < Alive.Length && Alive[best] != 0 && SideOf[best] != side)
            {
                bestD2 = math.distancesq(p, Positions[best]);
                if (bestD2 > range * range) { best = TargetIds.None; bestD2 = float.MaxValue; }
            }
            else best = TargetIds.None;

            float searchR = math.min(range, P.CellSize * 1.5f);
            float searchR2 = searchR * searchR;
            int cx = (int)math.floor(p.x / P.CellSize);
            int cz = (int)math.floor(p.z / P.CellSize);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = (cx + dx) * 73856093 ^ (cz + dz) * 19349663;
                if (!Grid.TryGetFirstValue(key, out int j, out var it)) continue;
                do
                {
                    if (j == i || SideOf[j] == side) continue;
                    if (UnitStance[UnitOf[j]] == 3) continue; // prisoners are not targets
                    float d2 = math.distancesq(p, Positions[j]);
                    if (d2 < bestD2 && d2 <= searchR2) { bestD2 = d2; best = j; }
                } while (Grid.TryGetNextValue(out j, ref it));
            }

            // Enemies go for the player when the player rides into them.
            if (PlayerAlive != 0 && side != PlayerSide)
            {
                float dp2 = math.distancesq(p, PlayerPosition);
                if (dp2 <= P.PlayerEngageRange * P.PlayerEngageRange && dp2 < bestD2) best = TargetIds.PlayerCharacter;
            }
            Targets[i] = best;
        }
    }

    /// <summary>Move each soldier toward its target or its formation slot, glued to the terrain.</summary>
    [BurstCompile]
    public struct MoveJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        public NativeArray<float> Yaws;
        [ReadOnly] public NativeArray<byte> Alive;
        [ReadOnly] public NativeArray<int> UnitOf;
        [ReadOnly] public NativeArray<int> SlotOf;
        [ReadOnly] public NativeArray<int> Targets;
        [ReadOnly] public NativeArray<UnitState> Units;
        [ReadOnly] public NativeArray<float3> TargetPositionsSnapshot; // positions at frame start (read-only copy)
        [ReadOnly] public NativeArray<byte> UnitStance;
        public HeightField Ground;
        public BattleParams P;
        public float3 PlayerPosition;
        public float Dt;

        public void Execute(int i)
        {
            if (Alive[i] == 0) return;
            byte stance = UnitStance[UnitOf[i]];
            if (stance == 3) return; // surrendered: stands where he is
            float3 p = Positions[i];
            UnitState unit = Units[UnitOf[i]];
            int target = Targets[i];
            float3 desired;
            float stopDistance;
            float speed = unit.Order == (byte)OrderType.Charge ? P.ChargeSpeed : P.WalkSpeed;

            if (target == TargetIds.PlayerCharacter)
            {
                desired = PlayerPosition; stopDistance = P.PlayerReach * 0.85f; speed = P.ChargeSpeed;
            }
            else if (target >= 0)
            {
                desired = TargetPositionsSnapshot[target]; stopDistance = P.AttackRange * 0.9f; speed = P.ChargeSpeed;
            }
            else
            {
                FormationMath.SlotOffset(unit.FormationType, SlotOf[i], unit.SoldierCount, P.FormationSpacing, P.ShieldwallSpacing, out float ox, out float oz);
                float s = math.sin(unit.Yaw), c = math.cos(unit.Yaw);
                desired = unit.Anchor + new float3(ox * c + oz * s, 0f, -ox * s + oz * c);
                stopDistance = 0.05f;
            }

            float3 to = desired - p; to.y = 0f;
            float dist = math.length(to);
            float yaw = Yaws[i];
            if (dist > stopDistance)
            {
                float3 dir = to / dist;
                float step = math.min(speed * Dt, dist - stopDistance);
                p += dir * step;
                float targetYaw = math.atan2(dir.x, dir.z);
                yaw = LerpAngle(yaw, targetYaw, math.saturate(P.TurnRate * Dt));
            }
            else if (target == TargetIds.None)
            {
                yaw = LerpAngle(yaw, unit.Yaw, math.saturate(P.TurnRate * Dt));
            }
            else if (dist > 1e-3f)
            {
                yaw = LerpAngle(yaw, math.atan2(to.x, to.z), math.saturate(P.TurnRate * Dt));
            }
            p.y = Ground.Sample(p.x, p.z);
            Positions[i] = p;
            Yaws[i] = yaw;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = math.fmod(b - a + math.PI * 3f, math.PI * 2f) - math.PI;
            return a + d * t;
        }
    }

    /// <summary>
    /// Resolve soldier-vs-soldier blows. Single-threaded on purpose: it writes health of other
    /// soldiers. Fine for a few thousand units (one pass, no allocations).
    /// Attacks against the player are only recorded (PlayerAttackFlags); the main thread resolves them.
    /// </summary>
    [BurstCompile]
    public struct CombatJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<int> Targets;
        public NativeArray<byte> Alive;
        public NativeArray<float> Health;
        public NativeArray<float> Cooldowns;
        public NativeArray<byte> PlayerAttackFlags;
        public NativeArray<int> KillsThisFrame; // length 1
        [ReadOnly] public NativeArray<int> UnitOf;
        [ReadOnly] public NativeArray<byte> UnitStance;
        public float RunDownKillChance;        // per attack against a routing man
        public BattleParams P;
        public float3 PlayerPosition;
        public float Dt;
        public uint Seed;

        public void Execute()
        {
            var rng = new Unity.Mathematics.Random(Seed == 0 ? 1u : Seed);
            int kills = 0;
            for (int i = 0; i < Alive.Length; i++)
            {
                PlayerAttackFlags[i] = 0;
                if (Alive[i] == 0) continue;
                byte myStance = UnitStance[UnitOf[i]];
                if (myStance == 2 || myStance == 3) { Cooldowns[i] = 0f; continue; }
                float cd = Cooldowns[i] - Dt;
                int t = Targets[i];
                if (t == TargetIds.None) { Cooldowns[i] = math.max(0f, cd); continue; }
                if (cd > 0f) { Cooldowns[i] = cd; continue; }

                if (t == TargetIds.PlayerCharacter)
                {
                    if (math.distancesq(Positions[i], PlayerPosition) <= P.PlayerReach * P.PlayerReach)
                    {
                        PlayerAttackFlags[i] = 1;
                        Cooldowns[i] = P.AttackCooldown;
                    }
                    else Cooldowns[i] = 0f;
                    continue;
                }

                if (Alive[t] == 0) { Cooldowns[i] = 0f; continue; }
                if (math.distancesq(Positions[i], Positions[t]) > P.AttackRange * P.AttackRange) { Cooldowns[i] = 0f; continue; }
                if (UnitStance[UnitOf[t]] == 2)
                {
                    // Running men are cut down from behind: brutal, effective.
                    if (rng.NextFloat() < RunDownKillChance) { Health[t] = 0f; Alive[t] = 0; kills++; }
                    Cooldowns[i] = P.AttackCooldown * 0.5f;
                    continue;
                }
                float dmg = CombatMath.Damage(P.BaseDamage, P.DamageVariance, rng.NextFloat(), P.Armour);
                float h = Health[t] - dmg;
                Health[t] = h;
                if (h <= 0f) { Alive[t] = 0; kills++; }
                Cooldowns[i] = P.AttackCooldown * rng.NextFloat(0.8f, 1.2f);
            }
            KillsThisFrame[0] = kills;
        }
    }

    /// <summary>Build one TRS matrix per soldier. The dead lie where they fell.</summary>
    [BurstCompile]
    public struct BuildMatricesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> Yaws;
        [ReadOnly] public NativeArray<byte> Alive;
        public float3 UprightScale;
        public float UprightLift;   // half-height so the capsule stands on the ground
        public float DeadLift;
        public NativeArray<Matrix4x4> Matrices;

        public void Execute(int i)
        {
            float3 p = Positions[i];
            quaternion rot = quaternion.RotateY(Yaws[i]);
            if (Alive[i] == 0)
            {
                rot = math.mul(rot, quaternion.RotateX(math.PI * 0.5f));
                p.y += DeadLift;
            }
            else p.y += UprightLift;
            Matrices[i] = float4x4.TRS(p, rot, UprightScale);
        }
    }

    /// <summary>Per-unit bookkeeping: alive counts and current centroid (anchor for Hold orders drifts toward it).</summary>
    [BurstCompile]
    public struct UnitSummaryJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> Alive;
        public NativeArray<UnitState> Units;
        public NativeArray<float3> Centroids;

        public void Execute()
        {
            for (int u = 0; u < Units.Length; u++)
            {
                UnitState unit = Units[u];
                float3 sum = float3.zero; int alive = 0;
                for (int i = unit.SoldierStart; i < unit.SoldierStart + unit.SoldierCount; i++)
                {
                    if (Alive[i] == 0) continue;
                    sum += Positions[i]; alive++;
                }
                unit.AliveCount = alive;
                unit.Alive = (byte)(alive > 0 ? 1 : 0);
                Centroids[u] = alive > 0 ? sum / alive : unit.Anchor;
                Units[u] = unit;
            }
        }
    }
}
