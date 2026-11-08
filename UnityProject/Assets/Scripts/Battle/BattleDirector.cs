using System;
using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshesAndCrowns.Battle
{
    /// <summary>
    /// Owns every soldier on the field as structure-of-arrays NativeArrays, runs the Burst jobs each frame,
    /// and draws the rank-and-file with Graphics.RenderMeshInstanced. No per-frame managed allocations.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class BattleDirector : MonoBehaviour
    {
        public const int MaxInstancesPerDraw = 1023;

        [Header("Wiring (set by the scene builder)")]
        public Material playerSoldierMaterial;
        public Material enemySoldierMaterial;
        public Mesh soldierMesh;
        public Transform playerCharacter;

        [Header("Runtime read-only")]
        public int SoldierCount;
        public int UnitCount;
        public int PlayerAlive, EnemyAlive;
        public int PlayerLosses, EnemyLosses;
        public bool BattleStarted;

        // Soldier SoA
        private NativeArray<float3> _positions, _positionsSnapshot;
        private NativeArray<float> _yaws, _health, _cooldowns;
        private NativeArray<byte> _alive, _side, _playerAttackFlags;
        private NativeArray<int> _unitOf, _slotOf, _targets, _kills;
        private NativeArray<Matrix4x4> _matrices;
        private NativeArray<UnitState> _units;
        private NativeArray<float3> _centroids;
        private NativeParallelMultiHashMap<int, int> _grid;
        private HeightField _ground;
        private BattleParams _params;
        private Balance _balance;
        private DeterministicRng _rng;
        private int _playerSoldierEnd; // soldiers [0, _playerSoldierEnd) belong to the player side
        private float _elapsed, _enemyThinkTimer;
        private bool _ready;
        private bool _playerCharacterAlive = true;
        private RenderParams _rpPlayer, _rpEnemy;
        private readonly Vector3 _uprightScale = new Vector3(0.62f, 0.9f, 0.62f);

        public Balance Balance => _balance;
        public bool IsReady => _ready;
        public NativeArray<UnitState> Units => _units;
        public NativeArray<float3> UnitCentroids => _centroids;
        public float3 SoldierPosition(int i) => _positions[i];
        public bool SoldierAlive(int i) => _alive[i] != 0;
        public Side SoldierSide(int i) => (Side)_side[i];

        /// <summary>Raised on the main thread for every enemy blow that lands on the player this frame (soldier index).</summary>
        public event Action<int> EnemyStruckPlayer;
        /// <summary>Raised when the player's melee kills a soldier.</summary>
        public event Action<int> PlayerKilledSoldier;

        public void SetPlayerCharacterAlive(bool alive) => _playerCharacterAlive = alive;

        // ------------------------------------------------------------------ setup

        public void Setup(Balance balance, DeterministicRng rng, Terrain terrain, Vector3 playerArmyCentre, Vector3 enemyArmyCentre)
        {
            Teardown();
            _balance = balance;
            _rng = rng.Fork(7);
            _params = BattleParams.From(balance.battle, balance.melee);
            _ground = HeightField.FromTerrain(terrain, Allocator.Persistent);

            int perSide = Mathf.Max(1, balance.battle.soldiersPerSide / Mathf.Max(1, balance.battle.soldiersPerUnit));
            int unitSize = Mathf.Clamp(balance.battle.unitSize, 1, perSide);
            int unitsPerSide = (perSide + unitSize - 1) / unitSize;
            SoldierCount = perSide * 2;
            UnitCount = unitsPerSide * 2;
            _playerSoldierEnd = perSide;

            Allocate(SoldierCount, UnitCount);

            int soldier = 0, unit = 0;
            SpawnSide(Side.Player, perSide, unitSize, unitsPerSide, playerArmyCentre, 0f, ref soldier, ref unit);
            SpawnSide(Side.Enemy, perSide, unitSize, unitsPerSide, enemyArmyCentre, math.PI, ref soldier, ref unit);

            // Place soldiers on their slots immediately so the field is formed at frame 0.
            for (int i = 0; i < SoldierCount; i++)
            {
                UnitState u = _units[_unitOf[i]];
                FormationMath.SlotOffset(u.FormationType, _slotOf[i], u.SoldierCount, _params.FormationSpacing, _params.ShieldwallSpacing, out float ox, out float oz);
                float s = math.sin(u.Yaw), c = math.cos(u.Yaw);
                float3 p = u.Anchor + new float3(ox * c + oz * s, 0f, -ox * s + oz * c);
                p.y = _ground.Sample(p.x, p.z);
                _positions[i] = p;
                _yaws[i] = u.Yaw;
            }
            _positionsSnapshot.CopyFrom(_positions);

            var bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size * 2f);
            _rpPlayer = new RenderParams(playerSoldierMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            _rpEnemy = new RenderParams(enemySoldierMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            _elapsed = 0f; _enemyThinkTimer = 0f; PlayerLosses = 0; EnemyLosses = 0; BattleStarted = false;
            _ready = true;
        }

        private void SpawnSide(Side side, int perSide, int unitSize, int unitsPerSide, Vector3 centre, float yaw, ref int soldier, ref int unit)
        {
            float frontage = FormationMath.Frontage(FormationType.Line, unitSize, _params.FormationSpacing, _params.ShieldwallSpacing) + 4f;
            float right = math.cos(yaw), fwd = -math.sin(yaw); // unit-local +x in world
            for (int u = 0; u < unitsPerSide; u++)
            {
                int count = Mathf.Min(unitSize, perSide - u * unitSize);
                float lateral = (u - (unitsPerSide - 1) * 0.5f) * frontage;
                var anchor = new float3(centre.x + lateral * right, 0f, centre.z + lateral * fwd);
                anchor.y = _ground.Sample(anchor.x, anchor.z);
                _units[unit] = new UnitState
                {
                    Side = (byte)side, Formation = (byte)FormationType.Line, Order = (byte)OrderType.Hold, Alive = 1,
                    SoldierStart = soldier, SoldierCount = count, Anchor = anchor, Yaw = yaw, Target = anchor, AliveCount = count
                };
                for (int k = 0; k < count; k++)
                {
                    int i = soldier + k;
                    _unitOf[i] = unit; _slotOf[i] = k; _side[i] = (byte)side; _alive[i] = 1;
                    _health[i] = _balance.battle.soldierHealth; _cooldowns[i] = _rng.Range(0f, _params.AttackCooldown);
                    _targets[i] = TargetIds.None;
                }
                soldier += count; unit++;
            }
        }

        private void Allocate(int soldiers, int units)
        {
            _positions = new NativeArray<float3>(soldiers, Allocator.Persistent);
            _positionsSnapshot = new NativeArray<float3>(soldiers, Allocator.Persistent);
            _yaws = new NativeArray<float>(soldiers, Allocator.Persistent);
            _health = new NativeArray<float>(soldiers, Allocator.Persistent);
            _cooldowns = new NativeArray<float>(soldiers, Allocator.Persistent);
            _alive = new NativeArray<byte>(soldiers, Allocator.Persistent);
            _side = new NativeArray<byte>(soldiers, Allocator.Persistent);
            _playerAttackFlags = new NativeArray<byte>(soldiers, Allocator.Persistent);
            _unitOf = new NativeArray<int>(soldiers, Allocator.Persistent);
            _slotOf = new NativeArray<int>(soldiers, Allocator.Persistent);
            _targets = new NativeArray<int>(soldiers, Allocator.Persistent);
            _kills = new NativeArray<int>(1, Allocator.Persistent);
            _matrices = new NativeArray<Matrix4x4>(soldiers, Allocator.Persistent);
            _units = new NativeArray<UnitState>(units, Allocator.Persistent);
            _centroids = new NativeArray<float3>(units, Allocator.Persistent);
            _grid = new NativeParallelMultiHashMap<int, int>(soldiers * 2, Allocator.Persistent);
        }

        private void Teardown()
        {
            if (!_ready && !_positions.IsCreated) return;
            _ready = false;
            if (_positions.IsCreated) _positions.Dispose();
            if (_positionsSnapshot.IsCreated) _positionsSnapshot.Dispose();
            if (_yaws.IsCreated) _yaws.Dispose();
            if (_health.IsCreated) _health.Dispose();
            if (_cooldowns.IsCreated) _cooldowns.Dispose();
            if (_alive.IsCreated) _alive.Dispose();
            if (_side.IsCreated) _side.Dispose();
            if (_playerAttackFlags.IsCreated) _playerAttackFlags.Dispose();
            if (_unitOf.IsCreated) _unitOf.Dispose();
            if (_slotOf.IsCreated) _slotOf.Dispose();
            if (_targets.IsCreated) _targets.Dispose();
            if (_kills.IsCreated) _kills.Dispose();
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_units.IsCreated) _units.Dispose();
            if (_centroids.IsCreated) _centroids.Dispose();
            if (_grid.IsCreated) _grid.Dispose();
            _ground.Dispose();
        }

        private void OnDestroy() => Teardown();

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            _elapsed += dt;
            float3 playerPos = playerCharacter != null ? (float3)playerCharacter.position : new float3(-9999f, 0f, -9999f);

            EnemyCommanderThink(dt);

            _grid.Clear();
            _positionsSnapshot.CopyFrom(_positions);

            var hash = new HashSoldiersJob { Positions = _positions, Alive = _alive, CellSize = _params.CellSize, Grid = _grid.AsParallelWriter() }
                .Schedule(SoldierCount, 64);
            var target = new TargetingJob
            {
                Positions = _positions, Alive = _alive, SideOf = _side, UnitOf = _unitOf, Units = _units, Grid = _grid, P = _params,
                PlayerPosition = playerPos, PlayerSide = (byte)Side.Player, PlayerAlive = (byte)(_playerCharacterAlive ? 1 : 0), Targets = _targets
            }.Schedule(SoldierCount, 64, hash);
            var move = new MoveJob
            {
                Positions = _positions, Yaws = _yaws, Alive = _alive, UnitOf = _unitOf, SlotOf = _slotOf, Targets = _targets, Units = _units,
                TargetPositionsSnapshot = _positionsSnapshot, Ground = _ground, P = _params, PlayerPosition = playerPos, Dt = dt
            }.Schedule(SoldierCount, 64, target);
            var combat = new CombatJob
            {
                Positions = _positions, Targets = _targets, Alive = _alive, Health = _health, Cooldowns = _cooldowns,
                PlayerAttackFlags = _playerAttackFlags, KillsThisFrame = _kills, P = _params, PlayerPosition = playerPos, Dt = dt,
                Seed = _rng.NextUInt()
            }.Schedule(move);
            var summary = new UnitSummaryJob { Positions = _positions, Alive = _alive, Units = _units, Centroids = _centroids }.Schedule(combat);
            var matrices = new BuildMatricesJob
            {
                Positions = _positions, Yaws = _yaws, Alive = _alive, Matrices = _matrices,
                UprightScale = _uprightScale, UprightLift = _uprightScale.y, DeadLift = 0.3f
            }.Schedule(SoldierCount, 64, summary);
            matrices.Complete();

            if (_kills[0] > 0) BattleStarted = true;
            RecountAndDrift(dt);
            DispatchPlayerAttacks();
            Render();
        }

        private void RecountAndDrift(float dt)
        {
            int pa = 0, ea = 0;
            for (int u = 0; u < UnitCount; u++)
            {
                UnitState unit = _units[u];
                if (unit.Side == (byte)Side.Player) pa += unit.AliveCount; else ea += unit.AliveCount;

                // Advance the anchor toward the order target so the formation walks as a block.
                if (unit.Order != (byte)OrderType.Hold && unit.Alive != 0)
                {
                    float3 to = unit.Target - unit.Anchor; to.y = 0f;
                    float dist = math.length(to);
                    if (dist > _params.ArrivalRadius)
                    {
                        float speed = unit.Order == (byte)OrderType.Charge ? _params.ChargeSpeed : _params.WalkSpeed;
                        float3 step = to / dist * math.min(speed * dt, dist);
                        unit.Anchor += step;
                        unit.Yaw = math.atan2(to.x, to.z);
                    }
                    else unit.Order = (byte)OrderType.Hold;
                    unit.Anchor.y = _ground.Sample(unit.Anchor.x, unit.Anchor.z);
                    _units[u] = unit;
                }
            }
            PlayerLosses = _playerSoldierEnd - pa; EnemyLosses = (SoldierCount - _playerSoldierEnd) - ea;
            PlayerAlive = pa; EnemyAlive = ea;
        }

        private void DispatchPlayerAttacks()
        {
            if (EnemyStruckPlayer == null) return;
            for (int i = _playerSoldierEnd; i < SoldierCount; i++)
                if (_playerAttackFlags[i] != 0) EnemyStruckPlayer.Invoke(i);
        }

        private void Render()
        {
            if (soldierMesh == null) return;
            DrawRange(_rpPlayer, 0, _playerSoldierEnd);
            DrawRange(_rpEnemy, _playerSoldierEnd, SoldierCount - _playerSoldierEnd);
        }

        private void DrawRange(RenderParams rp, int start, int count)
        {
            while (count > 0)
            {
                int n = Mathf.Min(count, MaxInstancesPerDraw);
                Graphics.RenderMeshInstanced(rp, soldierMesh, 0, _matrices, n, start);
                start += n; count -= n;
            }
        }

        // ------------------------------------------------------------------ enemy "commander" AI (sergeant level, Phase 0)

        private void EnemyCommanderThink(float dt)
        {
            if (_elapsed < _balance.battle.enemyAdvanceDelaySeconds) return;
            _enemyThinkTimer -= dt;
            if (_enemyThinkTimer > 0f) return;
            _enemyThinkTimer = 3f;
            for (int u = 0; u < UnitCount; u++)
            {
                UnitState unit = _units[u];
                if (unit.Side != (byte)Side.Enemy || unit.Alive == 0) continue;
                int nearest = NearestLivingUnit(Side.Player, _centroids[u]);
                if (nearest < 0) { unit.Order = (byte)OrderType.Hold; _units[u] = unit; continue; }
                float3 goal = _centroids[nearest];
                float dist = math.distance(goal, _centroids[u]);
                unit.Target = goal;
                unit.Order = dist < _balance.battle.enemyChargeDistance ? (byte)OrderType.Charge : (byte)OrderType.Move;
                _units[u] = unit;
            }
        }

        public int NearestLivingUnit(Side side, float3 from)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int u = 0; u < UnitCount; u++)
            {
                UnitState unit = _units[u];
                if (unit.Side != (byte)side || unit.Alive == 0) continue;
                float d = math.distancesq(_centroids[u], from);
                if (d < bestD) { bestD = d; best = u; }
            }
            return best;
        }

        // ------------------------------------------------------------------ persistence

        public BattleSnapshot ExportSnapshot()
        {
            if (!_ready) return null;
            var b = new BattleSnapshot
            {
                SoldierCount = SoldierCount, UnitCount = UnitCount, PlayerSoldierEnd = _playerSoldierEnd, Elapsed = _elapsed, EnemyThinkTimer = _enemyThinkTimer,
                Started = BattleStarted, PlayerCharacterAlive = _playerCharacterAlive,
                Positions = new float[SoldierCount * 3], Yaws = _yaws.ToArray(), Health = _health.ToArray(), Cooldowns = _cooldowns.ToArray(),
                Alive = _alive.ToArray(), Side = _side.ToArray(), UnitOf = _unitOf.ToArray(), SlotOf = _slotOf.ToArray(), Targets = _targets.ToArray(),
                UnitSide = new byte[UnitCount], UnitFormation = new byte[UnitCount], UnitOrder = new byte[UnitCount], UnitAlive = new byte[UnitCount],
                UnitStart = new int[UnitCount], UnitCount_ = new int[UnitCount], UnitAliveCount = new int[UnitCount],
                UnitAnchor = new float[UnitCount * 3], UnitYaw = new float[UnitCount], UnitTarget = new float[UnitCount * 3]
            };
            for (int i = 0; i < SoldierCount; i++) { float3 p = _positions[i]; b.Positions[i * 3] = p.x; b.Positions[i * 3 + 1] = p.y; b.Positions[i * 3 + 2] = p.z; }
            for (int u = 0; u < UnitCount; u++)
            {
                UnitState s = _units[u];
                b.UnitSide[u] = s.Side; b.UnitFormation[u] = s.Formation; b.UnitOrder[u] = s.Order; b.UnitAlive[u] = s.Alive;
                b.UnitStart[u] = s.SoldierStart; b.UnitCount_[u] = s.SoldierCount; b.UnitAliveCount[u] = s.AliveCount;
                b.UnitAnchor[u * 3] = s.Anchor.x; b.UnitAnchor[u * 3 + 1] = s.Anchor.y; b.UnitAnchor[u * 3 + 2] = s.Anchor.z;
                b.UnitYaw[u] = s.Yaw; b.UnitTarget[u * 3] = s.Target.x; b.UnitTarget[u * 3 + 1] = s.Target.y; b.UnitTarget[u * 3 + 2] = s.Target.z;
            }
            return b;
        }

        /// <summary>Restore a battle in progress. Call after Setup with the same balance; returns false if the layout no longer matches.</summary>
        public bool ImportSnapshot(BattleSnapshot b)
        {
            if (!_ready || b == null) return false;
            if (b.SoldierCount != SoldierCount || b.UnitCount != UnitCount || b.Positions.Length != SoldierCount * 3 || b.UnitAnchor.Length != UnitCount * 3)
            {
                Debug.LogWarning("[Ashes and Crowns] saved battle does not match the current army layout; starting the battle fresh");
                return false;
            }
            for (int i = 0; i < SoldierCount; i++) _positions[i] = new float3(b.Positions[i * 3], b.Positions[i * 3 + 1], b.Positions[i * 3 + 2]);
            _yaws.CopyFrom(b.Yaws); _health.CopyFrom(b.Health); _cooldowns.CopyFrom(b.Cooldowns);
            _alive.CopyFrom(b.Alive); _side.CopyFrom(b.Side); _unitOf.CopyFrom(b.UnitOf); _slotOf.CopyFrom(b.SlotOf); _targets.CopyFrom(b.Targets);
            for (int u = 0; u < UnitCount; u++)
            {
                _units[u] = new UnitState
                {
                    Side = b.UnitSide[u], Formation = b.UnitFormation[u], Order = b.UnitOrder[u], Alive = b.UnitAlive[u],
                    SoldierStart = b.UnitStart[u], SoldierCount = b.UnitCount_[u], AliveCount = b.UnitAliveCount[u],
                    Anchor = new float3(b.UnitAnchor[u * 3], b.UnitAnchor[u * 3 + 1], b.UnitAnchor[u * 3 + 2]), Yaw = b.UnitYaw[u],
                    Target = new float3(b.UnitTarget[u * 3], b.UnitTarget[u * 3 + 1], b.UnitTarget[u * 3 + 2])
                };
                _centroids[u] = _units[u].Anchor;
            }
            _positionsSnapshot.CopyFrom(_positions);
            _playerSoldierEnd = b.PlayerSoldierEnd; _elapsed = b.Elapsed; _enemyThinkTimer = b.EnemyThinkTimer; BattleStarted = b.Started; _playerCharacterAlive = b.PlayerCharacterAlive;
            RecountAndDrift(0f);
            return true;
        }

        public float Elapsed => _elapsed;

        // ------------------------------------------------------------------ command API (used by CommandController)

        public void IssueOrder(int unitIndex, OrderType order, float3 target)
        {
            if (unitIndex < 0 || unitIndex >= UnitCount) return;
            UnitState unit = _units[unitIndex];
            unit.Order = (byte)order;
            if (order == OrderType.Hold) unit.Target = unit.Anchor;
            else { unit.Target = target; unit.Target.y = _ground.Sample(target.x, target.z); }
            _units[unitIndex] = unit;
        }

        public void SetFormation(int unitIndex, FormationType formation)
        {
            if (unitIndex < 0 || unitIndex >= UnitCount) return;
            UnitState unit = _units[unitIndex];
            unit.Formation = (byte)formation;
            _units[unitIndex] = unit;
        }

        public float GroundHeight(float x, float z) => _ground.Sample(x, z);

        // ------------------------------------------------------------------ melee API (used by Combat)

        /// <summary>
        /// Strike every living enemy soldier inside a forward arc. Returns the number hit.
        /// </summary>
        public int StrikeArc(Vector3 origin, Vector3 forward, float reach, float arcDegrees, float damage, Side attacker)
        {
            int hits = 0;
            float r2 = reach * reach;
            for (int i = 0; i < SoldierCount; i++)
            {
                if (_alive[i] == 0 || _side[i] == (byte)attacker) continue;
                float3 p = _positions[i];
                float dx = p.x - origin.x, dz = p.z - origin.z;
                if (dx * dx + dz * dz > r2) continue;
                if (!CombatMath.InArc(dx, dz, forward.x, forward.z, arcDegrees)) continue;
                float h = _health[i] - damage;
                _health[i] = h;
                hits++;
                if (h <= 0f)
                {
                    _alive[i] = 0;
                    BattleStarted = true;
                    PlayerKilledSoldier?.Invoke(i);
                }
            }
            return hits;
        }

        /// <summary>Count living soldiers of a side within radius of a point (for HUD threat indicator).</summary>
        public int CountWithin(Vector3 origin, float radius, Side side)
        {
            int n = 0; float r2 = radius * radius;
            for (int i = 0; i < SoldierCount; i++)
            {
                if (_alive[i] == 0 || _side[i] != (byte)side) continue;
                if (math.distancesq(_positions[i], (float3)origin) <= r2) n++;
            }
            return n;
        }
    }
}
