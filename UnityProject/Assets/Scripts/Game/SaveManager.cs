using System;
using System.Collections.Generic;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The ironman save, one per run, hidden in persistentDataPath. Autosaves on a timer, on significant
    /// events and on quit (including mid-battle). On launch it resumes exactly, resolves the time the
    /// game was closed, and shows the chronicle. On death it deletes the save synchronously before the
    /// death screen appears. Also owns the "leave your affairs" quit screen.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SaveManager : MonoBehaviour
    {
        public const string SaveFolder = "saves";

        [Header("Wiring")]
        public WorldClock clock;             // optional
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;
        public MeleeController melee;        // optional
        public BattleDirector director;      // optional
        public HudController hud;
        public Transform home;               // where "sleep at home" is possible; optional
        public ModeManager modes;            // optional (battlefield)
        public Phase1Scene englandScene;     // optional (England)
        public CharacterCreationUI creation; // optional; without it a default character is created silently
        public OpeningNarration narration;   // optional
        public PlayerAppearance appearance;  // optional
        public Transform spawnPoints;        // optional: children named by location id

        private Balance _balance;
        private SaveStore _store;
        private float _nextAutosave;
        private bool _lastMounted;
        private bool _dead;
        private bool _quitScreenOpen;
        private bool _chronicleOpen;
        private bool _quitting;
        private bool _creationOpen;
        private double _hoursSurvivedAtLoad;
        private int _battles, _peakWealth;
        private readonly List<string> _lines = new List<string>(8);
        private readonly List<AffairsChoice> _quitOptions = new List<AffairsChoice>(4);

        public bool IsQuitScreenOpen => _quitScreenOpen || _chronicleOpen || _creationOpen;
        public SaveStore Store => _store;

        /// <summary>Override for tests. Defaults to persistentDataPath/saves.</summary>
        public static string SaveDirectoryOverride;
        public static string SaveDirectory => SaveDirectoryOverride ?? System.IO.Path.Combine(Application.persistentDataPath, SaveFolder);

        private void Awake()
        {
            _store = new SaveStore(SaveDirectory);
        }

        private void Start()
        {
            _balance = GameBootstrap.Instance.Balance;
            vitals.Died += OnDied;
            vitals.Wounded += (_, __) => MarkDirty();
            if (clock != null) clock.BellRang += (_, __) => MarkDirty();
            if (director != null) director.PlayerKilledSoldier += _ => MarkDirty();
            _lastMounted = horse.Mounted;
            _nextAutosave = Time.unscaledTime + _balance.save.autosaveIntervalSeconds;

            RunState pending = RunSession.PendingResume;
            RunSession.PendingResume = null;
            if (pending != null) { Apply(pending, resolveAbsence: true); return; }
            if (!RunSession.LaunchHandled)
            {
                RunSession.LaunchHandled = true;
                LoadResult r = _store.Load();
                if (r.Found)
                {
                    if (r.Source == LoadSource.Backup) Debug.LogWarning("[save] main file unreadable (" + r.MainError + "); resumed from backup");
                    if (r.State.SceneId != SceneManager.GetActiveScene().name)
                    {
                        if (Application.CanStreamedLevelBeLoaded(r.State.SceneId))
                        {
                            RunSession.PendingResume = r.State;
                            SceneManager.LoadScene(r.State.SceneId);
                            return;
                        }
                        Debug.LogWarning("[save] save belongs to scene " + r.State.SceneId + " which is not in the build; starting this scene fresh");
                    }
                    else { Apply(r.State, resolveAbsence: true); return; }
                }
                if (r.MainError != null && r.MainError != "no main save") Debug.LogWarning("[save] no usable save: " + r.MainError + " / " + r.BackupError);
            }
            StartNewCharacter();
        }

        private void Update()
        {
            if (_creationOpen) return;
            if (_dead)
            {
                if (GameBootstrap.Instance.Input.Restart.WasPressedThisFrame()) BeginAgain();
                return;
            }
            if (_chronicleOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current; var mouse = UnityEngine.InputSystem.Mouse.current;
                if ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) CloseChronicle();
                return;
            }
            if (GameBootstrap.Instance.Input.ToggleCursor.WasPressedThisFrame())
            {
                if (_quitScreenOpen) CloseQuitScreen(); else OpenQuitScreen();
                return;
            }
            if (_quitScreenOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && _quitOptions.Count > 0)
                {
                    if (kb.digit1Key.wasPressedThisFrame && _quitOptions.Count > 0) OnQuitChoice(_quitOptions[0]);
                    else if (kb.digit2Key.wasPressedThisFrame && _quitOptions.Count > 1) OnQuitChoice(_quitOptions[1]);
                    else if (kb.digit3Key.wasPressedThisFrame && _quitOptions.Count > 2) OnQuitChoice(_quitOptions[2]);
                }
                return;
            }
            if (horse.Mounted != _lastMounted) { _lastMounted = horse.Mounted; MarkDirty(); }
            if (Time.unscaledTime >= _nextAutosave) SaveNow(AffairsChoice.None);
        }

        /// <summary>Coalesce event-driven saves: schedule one soon rather than fsync on every blow.</summary>
        private void MarkDirty() { if (!_dead && !_quitting) _nextAutosave = Mathf.Min(_nextAutosave, Time.unscaledTime + _balance.save.dirtySaveDelaySeconds); }

        // ------------------------------------------------------------------ capture / apply

        public RunState Capture(AffairsChoice affairs)
        {
            var boot = GameBootstrap.Instance;
            var s = new RunState
            {
                Seed = boot.Seed, RngState = boot.Rng.State, SceneId = SceneManager.GetActiveScene().name, SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                Affairs = affairs, Hostility = CurrentHostility(), InBattle = InBattle(), EnemiesNearby = EnemiesNearby(),
                PlayerName = "A nameless rider", Mounted = horse.Mounted, Body = vitals.Body.Export(), Stamina = vitals.Stamina,
                Kills = melee != null ? melee.Kills : 0, MenLost = director != null ? director.PlayerLosses : 0, Battles = _battles, PeakWealth = _peakWealth
            };
            RunContext ctx = RunSession.Context;
            if (ctx != null && ctx.Character != null)
            {
                s.HasCharacter = true; s.Character = ctx.Character; s.PlayerName = ctx.Character.Name;
                s.Family = ctx.Family.ToArray(); s.Baronage = ctx.Baronage.ToArray(); s.SpawnLocationId = ctx.SpawnLocationId; s.OpeningSeen = ctx.OpeningSeen;
                if (ctx.Character.Coins > _peakWealth) { _peakWealth = ctx.Character.Coins; s.PeakWealth = _peakWealth; }
                if (ctx.Needs != null)
                {
                    s.Hunger = ctx.Needs.Hunger; s.Rest = ctx.Needs.Rest; s.SicknessKind = (byte)ctx.Disease.Current; s.SickDays = ctx.Disease.DaysSick;
                    int n = ctx.Inventory.DistinctCount; s.InventoryIds = new string[n]; s.InventoryCounts = new int[n];
                    for (int i = 0; i < n; i++) { s.InventoryIds[i] = ctx.Inventory.IdAt(i); s.InventoryCounts[i] = ctx.Inventory.CountAt(i); }
                    s.StandingValue = ctx.Standing.Value; s.MassesAttended = ctx.Standing.MassesAttended; s.MassesMissed = ctx.Standing.MassesMissed;
                    s.ApprenticeTrade = ctx.Apprenticeship.Trade; s.ApprenticeMaster = ctx.Apprenticeship.MasterNpcId; s.ApprenticeSeasons = ctx.Apprenticeship.SeasonsRemaining; s.ApprenticeCompetence = ctx.Apprenticeship.Competence;
                    int g = ctx.Market.Goods.Count; s.MarketIds = new string[g]; s.MarketStocks = new float[g];
                    for (int i = 0; i < g; i++) { s.MarketIds[i] = ctx.Market.Goods[i].Id; s.MarketStocks[i] = ctx.Market.Goods[i].Stock; }
                    s.MarketShock = (byte)ctx.Market.Shock; s.MarketShockStrength = ctx.Market.ShockStrength; s.MarketWealth = ctx.Market.Wealth;
                    s.Outbreak = ctx.Outbreak; s.LastMassDay = ctx.LastMassDay;
                }
            }
            if (clock != null && clock.IsReady)
            {
                s.CalendarTotalHours = clock.Calendar.TotalHours; s.Weather = clock.Weather.Export(); s.WeatherRngState = clock.Weather.RngState;
                s.HoursSurvived = clock.Calendar.TotalHours;
            }
            else s.HoursSurvived = _hoursSurvivedAtLoad + Time.timeSinceLevelLoad / _balance.time.realSecondsPerGameHour;
            Transform body = horse.Mounted ? horse.transform : player.transform;
            s.PlayerX = body.position.x; s.PlayerY = body.position.y; s.PlayerZ = body.position.z; s.PlayerYaw = body.eulerAngles.y;
            s.HorseX = horse.transform.position.x; s.HorseY = horse.transform.position.y; s.HorseZ = horse.transform.position.z; s.HorseYaw = horse.transform.eulerAngles.y;
            if (director != null && director.IsReady) { s.Battle = director.ExportSnapshot(); if (director.BattleStarted && _battles == 0) { _battles = 1; s.Battles = 1; } }
            return s;
        }

        public void Apply(RunState s, bool resolveAbsence)
        {
            var boot = GameBootstrap.Instance;
            boot.Rng.State = s.RngState;
            _battles = s.Battles; _peakWealth = s.PeakWealth; _hoursSurvivedAtLoad = s.HoursSurvived;
            if (clock != null && clock.IsReady) clock.Resume(s.CalendarTotalHours, s.Weather, s.WeatherRngState);
            vitals.Body.Import(s.Body);
            vitals.SetStamina(s.Stamina);
            if (s.HasCharacter && s.Character != null)
            {
                RunSession.Context = new RunContext { Character = s.Character, Figures = DataFiles.LoadFigures(), SpawnLocationId = s.SpawnLocationId, OpeningSeen = s.OpeningSeen };
                RunSession.Context.Family.AddRange(s.Family ?? new Npc[0]);
                RunSession.Context.Baronage.AddRange(s.Baronage ?? new MinorLord[0]);
            }
            else if (RunSession.Context == null)
            {
                // A save from before characters existed: give it a default one so the run goes on.
                RunSession.Context = RunFactory.CreateDefault(null);
                RunSession.Context.OpeningSeen = true;
            }
            RunContext rc = RunSession.Context;
            rc.EnsureSurvival(_balance, DataFiles.LoadItems(), DataFiles.LoadTrades());
            if (s.Version >= 4)
            {
                rc.Needs.Set(s.Hunger, s.Rest); rc.Disease.Set((Sickness)s.SicknessKind, s.SickDays);
                rc.Inventory.Clear(); for (int i = 0; i < s.InventoryIds.Length && i < s.InventoryCounts.Length; i++) rc.Inventory.Add(s.InventoryIds[i], s.InventoryCounts[i]);
                rc.Standing.Set(s.StandingValue, s.MassesAttended, s.MassesMissed);
                rc.Apprenticeship.Trade = s.ApprenticeTrade ?? string.Empty; rc.Apprenticeship.MasterNpcId = s.ApprenticeMaster; rc.Apprenticeship.SeasonsRemaining = s.ApprenticeSeasons; rc.Apprenticeship.Competence = s.ApprenticeCompetence;
                for (int i = 0; i < s.MarketIds.Length && i < s.MarketStocks.Length; i++) { var good = rc.Market.Find(s.MarketIds[i]); if (good != null) good.Stock = s.MarketStocks[i]; }
                rc.Market.RestoreShock((WarShock)s.MarketShock, s.MarketShockStrength);
                rc.Market.Wealth = s.MarketWealth; rc.Outbreak = s.Outbreak; rc.LastMassDay = s.LastMassDay;
            }
            if (appearance != null) appearance.Apply(RunSession.Context.Character);
            var cc = player.GetComponent<CharacterController>();
            if (horse.Mounted && !s.Mounted) horse.Dismount(player.transform);
            horse.transform.SetPositionAndRotation(new Vector3(s.HorseX, s.HorseY, s.HorseZ), Quaternion.Euler(0f, s.HorseYaw, 0f));
            if (s.Mounted)
            {
                if (!horse.Mounted) horse.Mount(player.transform);
            }
            else
            {
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(s.PlayerX, s.PlayerY, s.PlayerZ), Quaternion.Euler(0f, s.PlayerYaw, 0f));
                if (cc != null) cc.enabled = true;
            }
            _lastMounted = horse.Mounted;
            if (director != null && director.IsReady && s.Battle != null) director.ImportSnapshot(s.Battle);
            if (modes != null) modes.RefreshAfterResume();
            if (englandScene != null) englandScene.RefreshAfterResume();

            if (resolveAbsence)
            {
                double elapsed = (DateTime.UtcNow.Ticks - s.SavedAtUtcTicks) / (double)TimeSpan.TicksPerSecond;
                var calendar = clock != null && clock.IsReady ? clock.Calendar : new Calendar(_balance.time);
                var input = new AbsenceInput { ElapsedRealSeconds = elapsed, Choice = s.Affairs, Hostility = s.Hostility, InBattle = s.InBattle, EnemiesNearby = s.EnemiesNearby };
                AbsenceReport report = AbsenceResolver.Resolve(input, _balance, vitals.Body, calendar, boot.Rng.Fork(99));
                if (rc.Needs != null && report.GameHoursElapsed > 0f)
                {
                    // Away from the table: hunger runs on, slower while sleeping at home where there is bread.
                    float hungerHours = AbsenceResolver.IsResting(s.Affairs) ? report.GameHoursElapsed * 0.25f : report.GameHoursElapsed;
                    rc.Needs.Tick(Mathf.Min(hungerHours, 24f), vitals.Body);
                    rc.Needs.Sleep(report.GameHoursElapsed * 0.3f);
                    int days = Mathf.FloorToInt(report.GameHoursElapsed / 24f);
                    for (int d = 0; d < days; d++) { rc.Market.TickDay(); rc.Standing.TickDay(); }
                }
                if (clock != null && clock.IsReady && report.GameHoursElapsed > 0f) clock.Weather.AdvanceHours(report.GameHoursElapsed, clock.Calendar.Month);
                Debug.Log($"[save] resumed after {elapsed / 3600.0:F1} real hours = {report.GameHoursElapsed:F1} game hours ({s.Affairs}); died={report.Died}");
                if (report.Died || vitals.Body.IsDead)
                {
                    _dead = true;
                    bool gone = _store.DeleteAll();
                    if (!gone) Debug.LogError("[save] could not delete the save after death");
                    if (modes != null) modes.NotifyDied();
                    Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                    hud.ShowDeath(vitals.Body.CauseOfDeath ?? report.CauseOfDeath ?? "unknown", s.HoursSurvived / 24.0, s.Kills, s.MenLost, report.Chronicle, neverWoke: true);
                    return;
                }
                _lines.Clear(); _lines.AddRange(report.Chronicle);
                if (report.CoinsLost > 0) _lines.Add($"You are {report.CoinsLost} pennies poorer.");
                hud.ShowChronicle("While you slept...", _lines);
                _chronicleOpen = true;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            SaveNow(AffairsChoice.None);
        }

        // ------------------------------------------------------------------ new character

        private void StartNewCharacter()
        {
            if (creation != null)
            {
                _creationOpen = true;
                SetBodyControl(false);
                creation.Show(DataFiles.LoadBackgrounds(), OnCharacterChosen);
                return;
            }
            RunSession.Context = RunFactory.CreateDefault(vitals);
            RunSession.Context.OpeningSeen = true;
            if (appearance != null) appearance.Apply(RunSession.Context.Character);
            SaveNow(AffairsChoice.None); // a run exists from its first frame
        }

        private void OnCharacterChosen(CharacterFactory.Choice choice)
        {
            RunContext ctx;
            try { ctx = RunFactory.Create(choice, vitals); }
            catch (Exception e) { Debug.LogError("[run] could not create the character: " + e.Message); creation.Show(DataFiles.LoadBackgrounds(), OnCharacterChosen); return; }
            RunSession.Context = ctx;
            if (appearance != null) appearance.Apply(ctx.Character);
            Transform spawn = FindSpawn(ctx.SpawnLocationId);
            if (spawn != null) PlaceBody(spawn.position, spawn.rotation);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            string[] lines = RunSetup.OpeningNarration(ctx.Character, clock != null && clock.IsReady ? clock.Calendar : null);
            if (narration != null) narration.Play(lines, player.transform, FinishOpening);
            else FinishOpening();
        }

        private void FinishOpening()
        {
            if (RunSession.Context != null) RunSession.Context.OpeningSeen = true;
            _creationOpen = false;
            SetBodyControl(true);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            SaveNow(AffairsChoice.None);
        }

        private void SetBodyControl(bool on)
        {
            if (player != null) player.enabled = on && !horse.Mounted;
            if (horse != null) horse.enabled = on;
        }

        private Transform FindSpawn(string locationId)
        {
            if (spawnPoints == null || string.IsNullOrEmpty(locationId)) return null;
            Transform t = spawnPoints.Find(locationId);
            if (t == null) Debug.LogWarning("[run] no spawn point for " + locationId + "; staying put");
            return t;
        }

        private void PlaceBody(Vector3 position, Quaternion rotation)
        {
            var cc = player.GetComponent<CharacterController>();
            if (horse.Mounted) horse.Dismount(player.transform);
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            if (cc != null) cc.enabled = true;
            var hcc = horse.GetComponent<CharacterController>();
            if (hcc != null) hcc.enabled = false;
            horse.transform.SetPositionAndRotation(position + rotation * new Vector3(3f, 0f, -2f), Quaternion.Euler(0f, rotation.eulerAngles.y + 20f, 0f));
            if (hcc != null) hcc.enabled = true;
            if (Terrain.activeTerrain != null)
            {
                Vector3 p = player.transform.position; p.y = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y + 0.2f;
                if (cc != null) cc.enabled = false; player.transform.position = p; if (cc != null) cc.enabled = true;
                Vector3 h = horse.transform.position; h.y = Terrain.activeTerrain.SampleHeight(h) + Terrain.activeTerrain.transform.position.y + 0.2f;
                if (hcc != null) hcc.enabled = false; horse.transform.position = h; if (hcc != null) hcc.enabled = true;
            }
        }

        // ------------------------------------------------------------------ save / quit / death

        public void SaveNow(AffairsChoice affairs)
        {
            if (_dead) return;
            try { _store.Save(Capture(affairs)); }
            catch (Exception e) { Debug.LogError("[save] write failed: " + e.Message); }
            _nextAutosave = Time.unscaledTime + _balance.save.autosaveIntervalSeconds;
        }

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            _quitScreenOpen = false; hud.HideQuitScreen();
            bool gone = _store.DeleteAll();           // synchronous, before anything renders
            if (!gone) Debug.LogError("[save] could not delete the save after death");
            double days = (clock != null && clock.IsReady ? clock.Calendar.TotalHours : _hoursSurvivedAtLoad) / 24.0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            hud.ShowDeath(vitals.CauseOfDeath ?? "unknown", days, melee != null ? melee.Kills : 0, director != null ? director.PlayerLosses : 0, null, neverWoke: false);
        }

        private void BeginAgain()
        {
            _store.DeleteAll();
            RunSession.NextSeed = DateTime.UtcNow.Ticks & 0x7FFFFFFF;
            RunSession.LaunchHandled = true;
            SceneManager.LoadScene(0);
        }

        private void OnApplicationQuit()
        {
            if (_dead || _quitting || _creationOpen) return;
            // Closed the window without choosing: careless. Mid-battle counts as standing still.
            SaveNow(InBattle() ? AffairsChoice.StandInBattle : AffairsChoice.None);
        }

        private void OnApplicationPause(bool paused) { if (paused && !_dead && !_creationOpen) SaveNow(AffairsChoice.None); }

        // ------------------------------------------------------------------ quit screen

        private void OpenQuitScreen()
        {
            if (_dead) return;
            _quitScreenOpen = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var options = new List<(string label, AffairsChoice choice)>(6);
            if (InBattle())
            {
                options.Add(("Stand your ground (the fight goes on without you)", AffairsChoice.StandInBattle));
                options.Add(("Run for it (blows may find your back)", AffairsChoice.FleeBattle));
            }
            else
            {
                if (IsHome()) options.Add(("Sleep at home", AffairsChoice.SleepAtHome));
                options.Add(("Keep travelling", AffairsChoice.KeepTravelling));
                options.Add(("Hide and wait", AffairsChoice.Hide));
            }
            _quitOptions.Clear(); foreach (var o in options) _quitOptions.Add(o.choice);
            hud.ShowQuitScreen("Leave your affairs", options, OnQuitChoice, CloseQuitScreen);
        }

        private void CloseQuitScreen()
        {
            _quitScreenOpen = false;
            hud.HideQuitScreen();
            if (modes == null || modes.Mode != GameMode.Command) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        private void OnQuitChoice(AffairsChoice choice)
        {
            _quitting = true;
            SaveNow(choice);
            Debug.Log("[save] quit with affairs = " + choice);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CloseChronicle()
        {
            _chronicleOpen = false;
            hud.HideChronicle();
            if (modes == null || modes.Mode != GameMode.Command) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        // ------------------------------------------------------------------ context

        private bool InBattle() => director != null && director.IsReady && director.EnemyAlive > 0 && director.CountWithin(BodyPosition(), _balance.melee.enemyEngagePlayerRange * 2f, Side.Enemy) > 0;
        private int EnemiesNearby() => director != null && director.IsReady ? director.CountWithin(BodyPosition(), _balance.melee.enemyReach * 2f, Side.Enemy) : 0;
        private bool IsHome() => home != null && Vector3.Distance(BodyPosition(), home.position) <= _balance.absence.homeRadiusMetres;
        private float CurrentHostility()
        {
            if (director != null) return _balance.absence.hostilityBattlefield;
            return IsHome() ? _balance.absence.hostilityHome : _balance.absence.hostilityCountry;
        }
        private Vector3 BodyPosition() => horse.Mounted ? horse.transform.position : player.transform.position;
    }
}
