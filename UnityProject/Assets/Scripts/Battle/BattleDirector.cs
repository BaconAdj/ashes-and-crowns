using System;
using System.Collections.Generic;
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
        [Tooltip("Second material for the soldier mesh's second submesh (iron), when the Blender mesh is used.")]
        public Material soldierIronMaterial;
        private RenderParams _rpIron;
        public Transform playerCharacter;

        [Header("Runtime read-only")]
        public int SoldierCount;
        public int UnitCount;
        public int PlayerAlive, EnemyAlive;
        public int PlayerLosses, EnemyLosses;
        public bool BattleStarted;
        [Tooltip("When the player serves in the ranks, the sergeant AI also commands the player's side.")]
        public bool AiCommandsPlayerSide;

        // Soldier SoA
        private NativeArray<float3> _positions, _positionsSnapshot;
        private NativeArray<float> _yaws, _health, _cooldowns;
        private NativeArray<byte> _alive, _side, _playerAttackFlags;
        private NativeArray<int> _unitOf, _slotOf, _targets, _kills;
        private NativeList<float3> _deaths;
        /// <summary>Positions of the men who died in the last completed frame (read on the main thread after Update).</summary>
        public NativeArray<float3> DeathsThisFrame => _deaths.IsCreated ? _deaths.AsArray() : default;
        private NativeArray<Matrix4x4> _matrices;
        private NativeArray<UnitState> _units;
        private NativeArray<float3> _centroids;
        private NativeArray<byte> _unitStance;
        private UnitMorale[] _morale = new UnitMorale[0];
        private int[] _lastAlive = new int[0];
        private UnitRoster[] _rosters = new UnitRoster[0];
        private float _moraleTimer;
        private float3 _commanderPos; private bool _commanderCommanding, _commanderFighting;
        private bool _raining, _snowing;
        private NamesData _names;
        public BattleReport Report { get; private set; }
        public bool Ended => Report != null;
        public event Action<BattleReport> BattleEnded;
        public event Action<int, UnitStance> UnitStanceChanged;
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

        public void Setup(Balance balance, DeterministicRng rng, Terrain terrain, Vector3 playerArmyCentre, Vector3 enemyArmyCentre, NamesData names = null, int playerCount = -1, int enemyCount = -1, SiegeLayout siege = default)
        {
            Teardown();
            _balance = balance; _names = names; Report = null;
            _namedDeadScratch.Clear(); _playerDead.Clear(); _moraleTimer = 0f; _commanderCommanding = _commanderFighting = false; _raining = _snowing = false;
            _runDownChancePerAttack = 1f - math.exp(-balance.morale.runDownKillChancePerSecond * balance.battle.attackCooldownSeconds * 0.5f);
            _rng = rng.Fork(7);
            _params = BattleParams.From(balance.battle, balance.melee);
            _ground = HeightField.FromTerrain(terrain, Allocator.Persistent);
            _siege = siege; _ramSeconds = 0f;
            _fieldWidth = terrain != null && terrain.terrainData != null ? terrain.terrainData.size.x * 0.65f : 260f;

            int perUnitMen = Mathf.Max(1, balance.battle.soldiersPerUnit);
            int perPlayer = Mathf.Max(1, (playerCount > 0 ? playerCount : balance.battle.soldiersPerSide) / perUnitMen);
            int perEnemy = Mathf.Max(1, (enemyCount > 0 ? enemyCount : balance.battle.soldiersPerSide) / perUnitMen);
            int unitSize = Mathf.Clamp(balance.battle.unitSize, 1, Mathf.Min(perPlayer, perEnemy));
            int unitsPlayer = (perPlayer + unitSize - 1) / unitSize, unitsEnemy = (perEnemy + unitSize - 1) / unitSize;
            SoldierCount = perPlayer + perEnemy;
            UnitCount = unitsPlayer + unitsEnemy;
            _playerSoldierEnd = perPlayer; _unitsPlayer = unitsPlayer;
            bool playerDefendsWall = siege.Active == 1 && siege.AttackersArePlayer == 0;
            float playerYaw = playerDefendsWall ? math.PI : 0f, enemyYaw = playerDefendsWall ? 0f : math.PI;
            FormationType playerForm = playerDefendsWall ? FormationType.Shieldwall : FormationType.Line;
            FormationType enemyForm = siege.Active == 1 && !playerDefendsWall ? FormationType.Shieldwall : FormationType.Line;

            Allocate(SoldierCount, UnitCount);
            _morale = new UnitMorale[UnitCount]; _lastAlive = new int[UnitCount]; _rosters = new UnitRoster[UnitCount];

            int soldier = 0, unit = 0;
            SpawnSide(Side.Player, perPlayer, unitSize, unitsPlayer, playerArmyCentre, playerYaw, playerForm, ref soldier, ref unit);
            SpawnSide(Side.Enemy, perEnemy, unitSize, unitsEnemy, enemyArmyCentre, enemyYaw, enemyForm, ref soldier, ref unit);

            var nameGen = _names != null ? new NameGenerator(_names, _rng.Fork(3)) : null;
            for (int u = 0; u < UnitCount; u++)
            {
                _morale[u] = MoraleModel.Fresh(balance.morale, _units[u].SoldierCount, _rng.Chance(0.3f));
                _lastAlive[u] = _units[u].SoldierCount; _unitStance[u] = (byte)UnitStance.Steady;
                string unitName = Ordinal((u < _unitsPlayer ? u : u - _unitsPlayer) + 1) + " spears";
                _rosters[u] = nameGen != null ? UnitRoster.Create(unitName, nameGen, _rng.Fork((uint)(200 + u)), 3, false) : new UnitRoster { UnitName = unitName, Sergeant = "the sergeant", Veterans = new string[0], NamedAlive = 1 };
                _rosters[u].PlayerSide = _units[u].Side == (byte)Side.Player;
            }
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

        private void SpawnSide(Side side, int perSide, int unitSize, int unitsPerSide, Vector3 centre, float yaw, FormationType formation, ref int soldier, ref int unit)
        {
            float frontage = FormationMath.Frontage(formation, unitSize, _params.FormationSpacing, _params.ShieldwallSpacing) + 4f;
            float right = math.cos(yaw), fwd = -math.sin(yaw); // unit-local +x in world
            float faceX = math.sin(yaw), faceZ = math.cos(yaw);  // the way the line faces
            int cols = Mathf.Clamp(Mathf.FloorToInt(_fieldWidth / frontage), 1, unitsPerSide);
            float depth = Mathf.CeilToInt(unitSize / (float)FormationMath.LineRankWidth) * _params.FormationSpacing + 8f;
            for (int u = 0; u < unitsPerSide; u++)
            {
                int count = Mathf.Min(unitSize, perSide - u * unitSize);
                int row = u / cols, col = u % cols, inRow = Mathf.Min(cols, unitsPerSide - row * cols);
                float lateral = (col - (inRow - 1) * 0.5f) * frontage, back = row * depth;
                var anchor = new float3(centre.x + lateral * right - back * faceX, 0f, centre.z + lateral * fwd - back * faceZ);
                anchor.y = _ground.Sample(anchor.x, anchor.z);
                _units[unit] = new UnitState
                {
                    Side = (byte)side, Formation = (byte)formation, Order = (byte)OrderType.Hold, Alive = 1,
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
            _deaths = new NativeList<float3>(64, Allocator.Persistent);
            _matrices = new NativeArray<Matrix4x4>(soldiers, Allocator.Persistent);
            _units = new NativeArray<UnitState>(units, Allocator.Persistent);
            _centroids = new NativeArray<float3>(units, Allocator.Persistent);
            _grid = new NativeParallelMultiHashMap<int, int>(soldiers * 2, Allocator.Persistent);
            _unitStance = new NativeArray<byte>(units, Allocator.Persistent);
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
            if (_deaths.IsCreated) _deaths.Dispose();
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_units.IsCreated) _units.Dispose();
            if (_centroids.IsCreated) _centroids.Dispose();
            if (_grid.IsCreated) _grid.Dispose();
            if (_unitStance.IsCreated) _unitStance.Dispose();
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
            StepRam(dt);

            _grid.Clear();
            _positionsSnapshot.CopyFrom(_positions);

            var hash = new HashSoldiersJob { Positions = _positions, Alive = _alive, CellSize = _params.CellSize, Grid = _grid.AsParallelWriter() }
                .Schedule(SoldierCount, 64);
            var target = new TargetingJob
            {
                Positions = _positions, Alive = _alive, SideOf = _side, UnitOf = _unitOf, Units = _units, UnitStance = _unitStance, Grid = _grid, P = _params,
                PlayerPosition = playerPos, PlayerSide = (byte)Side.Player, PlayerAlive = (byte)(_playerCharacterAlive ? 1 : 0), Targets = _targets
            }.Schedule(SoldierCount, 64, hash);
            _deaths.Clear();
            var move = new MoveJob
            {
                Positions = _positions, Yaws = _yaws, Alive = _alive, UnitOf = _unitOf, SlotOf = _slotOf, Targets = _targets, Units = _units,
                TargetPositionsSnapshot = _positionsSnapshot, UnitStance = _unitStance, Ground = _ground, P = _params, PlayerPosition = playerPos, Dt = dt, Siege = _siege, PlayerSoldierEnd = _playerSoldierEnd
            }.Schedule(SoldierCount, 64, target);
            var combat = new CombatJob
            {
                Positions = _positions, Targets = _targets, Alive = _alive, Health = _health, Cooldowns = _cooldowns,
                PlayerAttackFlags = _playerAttackFlags, KillsThisFrame = _kills, Deaths = _deaths, UnitOf = _unitOf, UnitStance = _unitStance, RunDownKillChance = _runDownChancePerAttack, P = _params, PlayerPosition = playerPos, Dt = dt, Siege = _siege, PlayerSoldierEnd = _playerSoldierEnd,
                Seed = _rng.NextUInt()
            }.Schedule(move);
            var summary = new UnitSummaryJob { Positions = _positions, Alive = _alive, Units = _units, Centroids = _centroids }.Schedule(combat);
            var matrices = new BuildMatricesJob
            {
                Positions = _positions, Yaws = _yaws, Alive = _alive, Matrices = _matrices, PreviousPositions = _positionsSnapshot, Time = _elapsed,
                UprightScale = _uprightScale, UprightLift = _uprightScale.y, DeadLift = 0.3f
            }.Schedule(SoldierCount, 64, summary);
            matrices.Complete();

            if (_kills[0] > 0) BattleStarted = true;
            RecountAndDrift(dt);
            StepMorale(dt);
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
                if (unit.Order != (byte)OrderType.Hold && unit.Alive != 0 && _unitStance[u] != (byte)UnitStance.Surrendered)
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
            DrawRange(_rpPlayer, 0, _playerSoldierEnd, 0);
            DrawRange(_rpEnemy, _playerSoldierEnd, SoldierCount - _playerSoldierEnd, 0);
            if (soldierMesh.subMeshCount > 1 && soldierIronMaterial != null)
            {
                if (_rpIron.material != soldierIronMaterial) _rpIron = new RenderParams(soldierIronMaterial) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On, worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f) };
                DrawRange(_rpIron, 0, SoldierCount, 1);
            }
        }

        private void DrawRange(RenderParams rp, int start, int count, int subMesh)
        {
            while (count > 0)
            {
                int n = Mathf.Min(count, MaxInstancesPerDraw);
                Graphics.RenderMeshInstanced(rp, soldierMesh, subMesh, _matrices, n, start);
                start += n; count -= n;
            }
        }

        // ------------------------------------------------------------------ morale

        public void SetCommander(Vector3 position, bool commanding, bool fighting) { _commanderPos = position; _commanderCommanding = commanding; _commanderFighting = fighting; }
        public void SetWeather(bool raining, bool snowing) { _raining = raining; _snowing = snowing; }
        public UnitStance StanceOf(int unit) => (UnitStance)_unitStance[unit];
        public float MoraleOf(int unit) => _morale[unit].Value;
        public UnitRoster RosterOf(int unit) => _rosters[unit];
        public int Prisoners(Side side) { int n = 0; for (int u = 0; u < UnitCount; u++) if (_units[u].Side == (byte)side && _unitStance[u] == (byte)UnitStance.Surrendered) n += _units[u].AliveCount; return n; }
        public int Routers(Side side) { int n = 0; for (int u = 0; u < UnitCount; u++) if (_units[u].Side == (byte)side && _unitStance[u] == (byte)UnitStance.Routing) n += _units[u].AliveCount; return n; }

        private void StepMorale(float dt)
        {
            _moraleTimer += dt;
            if (_moraleTimer < 0.25f || Ended) return;
            float step = _moraleTimer; _moraleTimer = 0f;
            MoraleBalance mb = _balance.morale;
            float sightR2 = mb.commanderRange * mb.commanderRange;
            for (int u = 0; u < UnitCount; u++)
            {
                UnitState unit = _units[u];
                int lost = Mathf.Max(0, _lastAlive[u] - unit.AliveCount); _lastAlive[u] = unit.AliveCount;
                if (lost > 0) { _rosters[u].ApplyLosses(lost, unit.AliveCount + lost, _rng, _rosters[u].PlayerSide ? _playerDead : _namedDeadScratch); }
                if (unit.Alive == 0) { if (_unitStance[u] != (byte)UnitStance.Destroyed) { _unitStance[u] = (byte)UnitStance.Destroyed; UnitStanceChanged?.Invoke(u, UnitStance.Destroyed); } continue; }
                // Perception: enemy units around this unit's centroid.
                float3 c = _centroids[u]; float yaw = unit.Yaw; float fx = math.sin(yaw), fz = math.cos(yaw);
                int front = 0, flank = 0, rear = 0, alliesRouting = 0, enemiesRouting = 0; bool contact = false;
                for (int v = 0; v < UnitCount; v++)
                {
                    if (v == u || _units[v].Alive == 0) continue;
                    float3 d = _centroids[v] - c; float dist2 = d.x * d.x + d.z * d.z;
                    if (_units[v].Side == unit.Side) { if (dist2 < sightR2 && _unitStance[v] == (byte)UnitStance.Routing) alliesRouting++; continue; }
                    if (_unitStance[v] == (byte)UnitStance.Surrendered) continue;
                    if (dist2 < sightR2 && _unitStance[v] == (byte)UnitStance.Routing) enemiesRouting++;
                    if (dist2 > 40f * 40f) continue;
                    float dot = (d.x * fx + d.z * fz) / math.max(1e-3f, math.sqrt(dist2));
                    if (dot > 0.5f) front++; else if (dot < -0.5f) rear++; else flank++;
                    if (dist2 < 20f * 20f) contact = true;
                }
                float dc2 = math.distancesq(_commanderPos, c);
                var sit = new MoraleSituation
                {
                    Alive = unit.AliveCount, LostThisStep = lost, InContact = contact, Flanked = flank > 0 || rear > 0, Surrounded = front > 0 && rear > 0 && (flank > 0 || front + rear >= 3),
                    Moving = unit.Order != (byte)OrderType.Hold, Charging = unit.Order == (byte)OrderType.Charge, Raining = _raining, Snowing = _snowing,
                    CommanderNear = unit.Side == (byte)Side.Player && _commanderCommanding && dc2 < sightR2, CommanderFighting = unit.Side == (byte)Side.Player && _commanderFighting,
                    AlliesRoutingNearby = alliesRouting, EnemiesRoutingNearby = enemiesRouting, Veteran = false
                };
                UnitStance before = (UnitStance)_unitStance[u];
                UnitStance after = MoraleModel.Step(ref _morale[u], in sit, step, mb);
                if (after != before)
                {
                    _unitStance[u] = (byte)after;
                    if (after == UnitStance.Routing) { unit.Order = (byte)OrderType.Move; unit.Target = FleePoint(c, u); _units[u] = unit; }
                    else if (after == UnitStance.Shaken && before == UnitStance.Routing) { unit.Order = (byte)OrderType.Hold; unit.Anchor = _centroids[u]; unit.Yaw = unit.Side == (byte)Side.Enemy ? math.PI : 0f; _units[u] = unit; }
                    else if (after == UnitStance.Surrendered) { unit.Order = (byte)OrderType.Hold; _units[u] = unit; }
                    UnitStanceChanged?.Invoke(u, after);
                }
                else if (after == UnitStance.Routing) { unit.Target = FleePoint(c, u); unit.Order = (byte)OrderType.Move; _units[u] = unit; }
            }
            CheckEnd();
        }

        private SiegeLayout _siege; private float _ramSeconds, _fieldWidth; private int _unitsPlayer;
        public SiegeLayout Siege => _siege;
        public float RamProgress => _balance != null && _balance.siege.ramSecondsToBreak > 0f ? _ramSeconds / _balance.siege.ramSecondsToBreak : 0f;
        public event Action GateBroken;

        /// <summary>Enough attackers at the gate foot swing the ram; the gate gives after a while.</summary>
        private void StepRam(float dt)
        {
            if (_siege.Active != 1 || _siege.GateOpen == 1 || _siege.HasRam != 1 || Ended) return;
            Side att = _siege.AttackersArePlayer == 1 ? Side.Player : Side.Enemy;
            int crew = CountWithin(new Vector3(_siege.GateX, 0f, _siege.WallZ - _siege.WallHalf - 2f), 5f, att);
            if (crew < _balance.siege.ramCrew) return;
            _ramSeconds += dt;
            if (_ramSeconds >= _balance.siege.ramSecondsToBreak) { _siege.GateOpen = 1; GateBroken?.Invoke(); }
        }

        private readonly List<string> _namedDeadScratch = new List<string>(16);   // enemy named dead
        private readonly List<string> _playerDead = new List<string>(16);
        private float _runDownChancePerAttack;

        private float3 FleePoint(float3 from, int u)
        {
            // Away from the nearest living enemy unit, toward the map edge behind this side.
            Side side = (Side)_units[u].Side;
            int nearest = NearestLivingUnit(side == Side.Player ? Side.Enemy : Side.Player, from);
            float3 away = nearest >= 0 ? math.normalizesafe(from - _centroids[nearest], new float3(0f, 0f, side == Side.Player ? -1f : 1f)) : new float3(0f, 0f, side == Side.Player ? -1f : 1f);
            float3 p = from + away * 60f;
            p.y = _ground.Sample(p.x, p.z);
            return p;
        }

        private void CheckEnd()
        {
            if (Ended || !BattleStarted) return;
            bool playerBroken = true, enemyBroken = true;
            for (int u = 0; u < UnitCount; u++)
            {
                byte st = _unitStance[u];
                bool standing = st == (byte)UnitStance.Steady || st == (byte)UnitStance.Shaken;
                if (!standing) continue;
                if (_units[u].Side == (byte)Side.Player) playerBroken = false; else enemyBroken = false;
            }
            if (!playerBroken && !enemyBroken) return;
            var r = new BattleReport
            {
                PlayerWon = enemyBroken && !playerBroken, Draw = playerBroken && enemyBroken, Seconds = _elapsed,
                PlayerStart = _playerSoldierEnd, EnemyStart = SoldierCount - _playerSoldierEnd,
                PlayerDead = PlayerLosses, EnemyDead = EnemyLosses, PlayerRouted = Routers(Side.Player), EnemyRouted = Routers(Side.Enemy),
                PlayerCaptured = Prisoners(Side.Player), EnemyCaptured = Prisoners(Side.Enemy),
                Weather = _snowing ? "snow" : _raining ? "rain" : "grey"
            };
            foreach (string n in _playerDead) r.NamedDead.Add(n);
            if (r.NamedDead.Count == 0) foreach (string n in _namedDeadScratch) r.NamedDead.Add(n);
            Report = r;
            BattleEnded?.Invoke(r);
        }

        private static string Ordinal(int n)
        {
            switch (n) { case 1: return "first"; case 2: return "second"; case 3: return "third"; case 4: return "fourth"; case 5: return "fifth"; default: return n + "th"; }
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
                bool aiSide = unit.Side == (byte)Side.Enemy || (AiCommandsPlayerSide && unit.Side == (byte)Side.Player);
                if (!aiSide || unit.Alive == 0 || _unitStance[u] >= (byte)UnitStance.Routing) continue;
                Side opp = unit.Side == (byte)Side.Enemy ? Side.Player : Side.Enemy;
                if (_siege.Active == 1)
                {
                    bool attackerUnit = (unit.Side == (byte)Side.Player) == (_siege.AttackersArePlayer == 1);
                    if (!attackerUnit)
                    {
                        // Hold the wall unless they are already inside.
                        int n = NearestLivingUnit(opp, _centroids[u]);
                        bool inside = n >= 0 && _centroids[n].z > _siege.WallZ + _siege.WallHalf;
                        if (!inside) { unit.Order = (byte)OrderType.Hold; _units[u] = unit; continue; }
                    }
                    else
                    {
                        int lanes = _siege.LadderCount + (_siege.GateOpen == 1 ? 1 : 0);
                        bool ramCrew = _siege.HasRam == 1 && _siege.GateOpen == 0 && (lanes == 0 || u % 3 == 0);
                        float3 g;
                        if (ramCrew) g = new float3(_siege.GateX, 0f, _siege.WallZ - _siege.WallHalf - 2f);
                        else if (lanes == 0) { unit.Order = (byte)OrderType.Hold; _units[u] = unit; continue; }
                        else { int lane = u % lanes; float lx = _siege.GateOpen == 1 && lane == lanes - 1 ? _siege.GateX : _siege.LadderX(lane); g = new float3(lx, 0f, _siege.WallZ + _siege.WallHalf + 6f); }
                        g.y = _ground.Sample(g.x, g.z);
                        unit.Target = g;
                        unit.Order = math.distance(g, _centroids[u]) < _balance.battle.enemyChargeDistance ? (byte)OrderType.Charge : (byte)OrderType.Move;
                        _units[u] = unit; continue;
                    }
                }
                int nearest = NearestLivingUnit(opp, _centroids[u]);
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
                if (unit.Side != (byte)side || unit.Alive == 0 || _unitStance[u] == (byte)UnitStance.Surrendered) continue;
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
                SoldierCount = SoldierCount, UnitCount = UnitCount, PlayerSoldierEnd = _playerSoldierEnd, Elapsed = _elapsed, EnemyThinkTimer = _enemyThinkTimer, SiegeGateOpen = _siege.GateOpen == 1, RamSeconds = _ramSeconds,
                Started = BattleStarted, PlayerCharacterAlive = _playerCharacterAlive,
                Positions = new float[SoldierCount * 3], Yaws = _yaws.ToArray(), Health = _health.ToArray(), Cooldowns = _cooldowns.ToArray(),
                Alive = _alive.ToArray(), Side = _side.ToArray(), UnitOf = _unitOf.ToArray(), SlotOf = _slotOf.ToArray(), Targets = _targets.ToArray(),
                UnitSide = new byte[UnitCount], UnitFormation = new byte[UnitCount], UnitOrder = new byte[UnitCount], UnitAlive = new byte[UnitCount],
                UnitStart = new int[UnitCount], UnitCount_ = new int[UnitCount], UnitAliveCount = new int[UnitCount],
                UnitAnchor = new float[UnitCount * 3], UnitYaw = new float[UnitCount], UnitTarget = new float[UnitCount * 3],
                UnitStance = new byte[UnitCount], UnitMorale = new float[UnitCount]
            };
            for (int u = 0; u < UnitCount; u++) { b.UnitStance[u] = _unitStance[u]; b.UnitMorale[u] = _morale[u].Value; }
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
            int n = SoldierCount, u = UnitCount;
            bool shape = b.SoldierCount == n && b.UnitCount == u && b.Positions.Length == n * 3 && b.Yaws.Length == n && b.Health.Length == n && b.Cooldowns.Length == n
                      && b.Alive.Length == n && b.Side.Length == n && b.UnitOf.Length == n && b.SlotOf.Length == n && b.Targets.Length == n
                      && b.UnitSide.Length == u && b.UnitFormation.Length == u && b.UnitOrder.Length == u && b.UnitAlive.Length == u && b.UnitStart.Length == u
                      && b.UnitCount_.Length == u && b.UnitAliveCount.Length == u && b.UnitAnchor.Length == u * 3 && b.UnitYaw.Length == u && b.UnitTarget.Length == u * 3;
            if (!shape)
            {
                Debug.LogWarning("[Ashes and Crowns] saved battle does not match the current army layout; starting the battle fresh");
                return false;
            }
            for (int i = 0; i < SoldierCount; i++) _positions[i] = new float3(b.Positions[i * 3], b.Positions[i * 3 + 1], b.Positions[i * 3 + 2]);
            _yaws.CopyFrom(b.Yaws); _health.CopyFrom(b.Health); _cooldowns.CopyFrom(b.Cooldowns);
            _alive.CopyFrom(b.Alive); _side.CopyFrom(b.Side); _unitOf.CopyFrom(b.UnitOf); _slotOf.CopyFrom(b.SlotOf); _targets.CopyFrom(b.Targets);
            for (int k = 0; k < UnitCount; k++)
            {
                int u = k;
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
            _ramSeconds = b.RamSeconds; if (b.SiegeGateOpen && _siege.Active == 1) { _siege.GateOpen = 1; GateBroken?.Invoke(); }
            for (int k = 0; k < UnitCount; k++) { if (b.UnitStance != null && k < b.UnitStance.Length) { _unitStance[k] = b.UnitStance[k]; _morale[k].Stance = b.UnitStance[k]; } if (b.UnitMorale != null && k < b.UnitMorale.Length) _morale[k].Value = b.UnitMorale[k]; _lastAlive[k] = _units[k].AliveCount; }
            for (int k = 0; k < UnitCount; k++) if (_unitStance[k] != (byte)UnitStance.Steady) UnitStanceChanged?.Invoke(k, (UnitStance)_unitStance[k]);
            RecountAndDrift(0f);
            return true;
        }

        public float Elapsed => _elapsed;

        // ------------------------------------------------------------------ command API (used by CommandController)

        public void IssueOrder(int unitIndex, OrderType order, float3 target)
        {
            if (unitIndex < 0 || unitIndex >= UnitCount) return;
            if (_unitStance[unitIndex] >= (byte)UnitStance.Routing) return; // broken men hear no orders
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

        /// <summary>Where soldier slot 'slot' of a unit should stand right now.</summary>
        public Vector3 SlotWorldPosition(int unit, int slot)
        {
            UnitState u = _units[unit];
            FormationMath.SlotOffset(u.FormationType, slot, u.SoldierCount, _params.FormationSpacing, _params.ShieldwallSpacing, out float ox, out float oz);
            float s = math.sin(u.Yaw), c = math.cos(u.Yaw);
            float3 p = u.Anchor + new float3(ox * c + oz * s, 0f, -ox * s + oz * c);
            p.y = _ground.Sample(p.x, p.z);
            return p;
        }
        public OrderType OrderOf(int unit) => (OrderType)_units[unit].Order;
        public bool UnitInContact(int unit) { float3 c = _centroids[unit]; return CountWithin(c, 20f, _units[unit].Side == (byte)Side.Player ? Side.Enemy : Side.Player) > 0; }

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
