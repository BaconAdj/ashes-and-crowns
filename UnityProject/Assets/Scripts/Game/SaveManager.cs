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
        public GruntMode grunt;              // optional (battlefield)
        public HudController hud;
        public Transform home;               // where "sleep at home" is possible; optional
        public ModeManager modes;            // optional (battlefield)
        public Phase1Scene englandScene;     // optional (England)
        public CharacterCreationUI creation; // optional; without it a default character is created silently
        public OpeningNarration narration;   // optional
        public PlayerAppearance appearance;  // optional
        public Transform spawnPoints;        // optional: children named by location id
        public SettlementSpawner spawner;    // optional (England): builds the run's villages once the context exists
        public VillageLife villageLife;      // optional: Esc belongs to its dialogues while they are open

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
            if (RunSession.Context != null)
            {
                // Came from another scene in this process (battle and back): keep the run, place the body.
                if (appearance != null) appearance.Apply(RunSession.Context.Character);
                if (clock != null && clock.IsReady && RunSession.CarriedTotalHours >= 0) clock.Resume(RunSession.CarriedTotalHours, RunSession.CarriedWeather, RunSession.CarriedWeatherRng);
                if (RunSession.CarriedBody != null && vitals != null && vitals.Body != null) { vitals.Body.Import(RunSession.CarriedBody); RunSession.CarriedBody = null; }
                if (spawner != null && RunSession.Context.Settlements.Count > 0) spawner.Build(RunSession.Context.Settlements, GameBootstrap.Instance.World.overworld.terrainSizeMetres, GameBootstrap.Instance.Rng.Fork(46));
                Transform back = FindSpawn(RunSession.Context.HomeSettlementId);
                if (back != null && spawnPoints != null) PlaceBody(back.position, back.rotation);
                if (RunSession.ReturnChronicle != null && RunSession.ReturnChronicle.Count > 0) { hud.ShowChronicle("After the battle", RunSession.ReturnChronicle); _chronicleOpen = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; RunSession.ReturnChronicle = null; }
                SaveNow(AffairsChoice.None);
                return;
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
                if (villageLife != null && villageLife.IsBusy && !_quitScreenOpen) return;
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
                if (ctx.Settlements != null && ctx.Settlements.Count > 0)
                {
                    s.Settlements = new SettlementSnapshot[ctx.Settlements.Count];
                    for (int i = 0; i < ctx.Settlements.Count; i++) s.Settlements[i] = SnapshotSettlement(ctx.Settlements[i]);
                    for (int i = 0; i < 4; i++) s.Reputation[i] = ctx.Reputation.Get((Faction)i); s.Notoriety = ctx.Reputation.Notoriety;
                    s.Crimes = new CrimeSnapshot[ctx.Crime.Crimes.Count];
                    for (int i = 0; i < s.Crimes.Length; i++) { var c = ctx.Crime.Crimes[i]; s.Crimes[i] = new CrimeSnapshot { Kind = (byte)c.Kind, Where = c.Where, Day = c.Day, Witnesses = c.Witnesses, Known = c.Known, Tried = c.Tried }; }
                    s.Outlawed = ctx.Crime.Outlawed; s.SanctuaryDaysLeft = ctx.Crime.SanctuaryDaysLeft; s.SanctuaryExpired = ctx.Crime.SanctuaryExpired;
                    s.ActiveJob = ctx.ActiveJob != null ? SnapshotJob(ctx.ActiveJob) : null; s.NextJobId = ctx.NextJobId; s.HomeSettlementId = ctx.HomeSettlementId;
                }
                var sv = ctx.Service;
                s.ServiceRole = (byte)sv.Role; s.ServiceCompany = sv.CompanyName; s.ServiceCaptain = sv.CaptainName; s.ServiceSide = (byte)sv.Side; s.ServiceWage = sv.WagePerDay; s.ServiceDays = sv.DaysServed; s.ServiceOwed = sv.WagesOwed;
                s.ServiceBattles = sv.BattlesFought; s.ServiceRenown = sv.Renown; s.ServiceInfractions = sv.Infractions; s.ServiceDeserter = sv.Deserter; s.ServiceInCamp = sv.InCamp; s.ServiceKills = sv.WitnessedKills; s.ServiceLines = sv.LinesHeld; s.ServicePatron = sv.PatronId; s.ServicePatronFavour = sv.PatronFavour;
                s.Companies = new CompanySnapshot[ctx.Companies.Count];
                for (int i = 0; i < s.Companies.Length; i++) { var co = ctx.Companies[i]; s.Companies[i] = new CompanySnapshot { Name = co.Name, Captain = co.Captain, Employer = (byte)co.Employer, Size = co.Size, Reputation = co.Reputation, BasedAtId = co.BasedAtId, ContractDaysLeft = co.ContractDaysLeft }; }
                var sg = ctx.Siege;
                if (sg != null)
                {
                    s.HasSiege = true; s.SiegeTargetId = sg.TargetId; s.SiegeTargetName = sg.TargetName; s.SiegeBesieger = (byte)sg.Besieger; s.SiegeOutcome = (byte)sg.Outcome;
                    s.SiegePlayerAttacking = sg.PlayerAttacking; s.SiegePlayerCommands = sg.PlayerCommands; s.SiegeRamBuilt = sg.RamBuilt; s.SiegeTowerBuilt = sg.TowerBuilt; s.SiegeGateBroken = sg.GateBroken; s.SiegeInAssault = sg.InAssault;
                    s.SiegeDay = sg.Day; s.SiegeAttackers = sg.Attackers; s.SiegeDefenders = sg.Defenders; s.SiegeCoinSpent = sg.CoinSpent; s.SiegeLadders = sg.Ladders; s.SiegeRamDays = sg.RamDays; s.SiegeTowerDays = sg.TowerDays; s.SiegeAssaults = sg.Assaults; s.SiegeLordId = sg.LordId; s.SiegeAttDead = sg.AttackerDeadInAssaults; s.SiegeDefDead = sg.DefenderDeadInAssaults;
                    s.SiegeFood = sg.DefenderFoodDays; s.SiegeDefSick = sg.DefenderSickness; s.SiegeAttSick = sg.AttackerSickness; s.SiegeDefMorale = sg.DefenderMorale; s.SiegeRelief = sg.ReliefStrength;
                }
                var cb = ctx.Castle;
                if (cb != null && cb.Stage != CastleStage.None) { s.HasCastle = true; s.CastleName = cb.Name; s.CastleSiteId = cb.SiteSettlementId; s.CastleNx = cb.Nx; s.CastleNz = cb.Nz; s.CastleStage = (byte)cb.Stage; s.CastleDays = cb.DaysIntoStage; s.CastleCoin = cb.CoinSpent; s.CastleObjector = cb.ObjectorLordId; }
                s.Garrison = ctx.Garrison;
                var es = ctx.Estate;
                if (es != null)
                {
                    s.HasEstate = true; s.EstateSettlementId = es.SettlementId; s.EstateName = es.Name; s.StewardName = es.StewardName; s.EstateTax = (byte)es.Tax; s.EstateJustice = (byte)es.Justice; s.EstateCrisis = (byte)es.PendingCrisis;
                    s.StewardHonesty = es.StewardHonesty; s.StewardCompetence = es.StewardCompetence; s.EstateUnrest = es.Unrest; s.EstateProsperity = es.Prosperity; s.EstateGranary = es.Granary;
                    s.EstateTreasury = es.Treasury; s.EstateDaysSinceCourt = es.DaysSinceCourt; s.EstateGarrison = es.GarrisonMen; s.EstateIncomeMonth = es.IncomeThisMonth; s.EstateSkimMonth = es.SkimmedThisMonth; s.EstateRevolt = es.InRevolt;
                }
                var la = ctx.Lordship; s.LordDeclared = (byte)la.Declared; s.LordSecret = (byte)la.SecretDeal; s.LordDeals = la.DealsTaken; s.LordRefusedStephen = la.DemandsRefusedStephen; s.LordRefusedMatilda = la.DemandsRefusedMatilda; s.LordSuspicion = la.Suspicion; s.LordDiscovered = la.Discovered;
                var su = ctx.Suit; if (su != null) { s.HasSuit = true; s.SuitHeiress = su.Heiress; s.SuitDowryId = su.DowrySettlementId; s.SuitFather = su.FatherLordId; s.SuitVisits = su.Visits; s.SuitFavour = su.Favour; s.SuitConsented = su.Consented; s.SuitMarried = su.Married; }
                var rl = ctx.Realm; if (rl != null) { s.HasRealm = true; s.RealmTreasury = rl.CrownTreasury; s.RealmSeasons = rl.SeasonsElapsed; s.RealmChurch = rl.ChurchFavour; s.RealmChronicle = rl.Chronicle.ToArray(); }
                s.DaysIntoSeason = ctx.DaysIntoSeason; s.NextNpcId = ctx.NextNpcId; s.PendingDemand = (byte)ctx.PendingDemand;
                var pl = ctx.Plot; if (pl != null) { s.HasPlot = true; s.PlotKind = (byte)pl.Kind; s.PlotterKind = (byte)pl.Plotter; s.PlotterId = pl.PlotterId; s.PlotDaysUntil = pl.DaysUntil; s.PlotWarnings = pl.WarningsGiven; s.PlotterName = pl.PlotterName; s.PlotGuarded = pl.Guarded; s.PlotTasted = pl.Tasted; s.PlotWarningLines = pl.Warnings.ToArray(); }
                s.PendingRaiders = ctx.PendingRaiders; s.EstateDaysThisMonth = ctx.EstateDaysThisMonth; s.WarHeat = ctx.WarHeat;
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
                for (int i = 0; i < s.MarketIds.Length && i < s.MarketStocks.Length; i++) { var good = rc.Market.Find(s.MarketIds[i]); if (good == null) { rc.Market.Stock(s.MarketIds[i], 0.5f, 0.5f); good = rc.Market.Find(s.MarketIds[i]); } good.Stock = s.MarketStocks[i]; }
                rc.Market.RestoreShock((WarShock)s.MarketShock, s.MarketShockStrength);
                rc.Market.Wealth = s.MarketWealth; rc.Outbreak = s.Outbreak; rc.LastMassDay = s.LastMassDay;
            }
            RestoreEngland(s, rc);
            if (s.Version >= 7)
            {
                var sv = rc.Service;
                sv.Role = (ServiceRole)s.ServiceRole; sv.CompanyName = s.ServiceCompany ?? string.Empty; sv.CaptainName = s.ServiceCaptain ?? string.Empty; sv.Side = (Allegiance)s.ServiceSide; sv.WagePerDay = s.ServiceWage; sv.DaysServed = s.ServiceDays; sv.WagesOwed = s.ServiceOwed;
                sv.BattlesFought = s.ServiceBattles; sv.Renown = s.ServiceRenown; sv.Infractions = s.ServiceInfractions; sv.Deserter = s.ServiceDeserter; sv.InCamp = s.ServiceInCamp; sv.WitnessedKills = s.ServiceKills; sv.LinesHeld = s.ServiceLines; sv.PatronId = s.ServicePatron ?? string.Empty; sv.PatronFavour = s.ServicePatronFavour;
                rc.Companies.Clear();
                foreach (var co in s.Companies ?? new CompanySnapshot[0]) rc.Companies.Add(new MercenaryCompany { Name = co.Name, Captain = co.Captain, Employer = (Allegiance)co.Employer, Size = co.Size, Reputation = co.Reputation, BasedAtId = co.BasedAtId, ContractDaysLeft = co.ContractDaysLeft });
            }
            if (s.Version >= 8)
            {
                rc.Siege = !s.HasSiege ? null : new SiegeState
                {
                    TargetId = s.SiegeTargetId ?? string.Empty, TargetName = s.SiegeTargetName ?? string.Empty, Besieger = (Allegiance)s.SiegeBesieger, Outcome = (SiegeOutcome)s.SiegeOutcome,
                    PlayerAttacking = s.SiegePlayerAttacking, PlayerCommands = s.SiegePlayerCommands, RamBuilt = s.SiegeRamBuilt, TowerBuilt = s.SiegeTowerBuilt, GateBroken = s.SiegeGateBroken, InAssault = s.SiegeInAssault,
                    Day = s.SiegeDay, Attackers = s.SiegeAttackers, Defenders = s.SiegeDefenders, CoinSpent = s.SiegeCoinSpent, Ladders = s.SiegeLadders, RamDays = s.SiegeRamDays, TowerDays = s.SiegeTowerDays, Assaults = s.SiegeAssaults, LordId = s.SiegeLordId, AttackerDeadInAssaults = s.SiegeAttDead, DefenderDeadInAssaults = s.SiegeDefDead,
                    DefenderFoodDays = s.SiegeFood, DefenderSickness = s.SiegeDefSick, AttackerSickness = s.SiegeAttSick, DefenderMorale = s.SiegeDefMorale, ReliefStrength = s.SiegeRelief
                };
                rc.Castle = !s.HasCastle ? null : new CastleBuild { Name = s.CastleName ?? string.Empty, SiteSettlementId = s.CastleSiteId ?? string.Empty, Nx = s.CastleNx, Nz = s.CastleNz, Stage = (CastleStage)s.CastleStage, DaysIntoStage = s.CastleDays, CoinSpent = s.CastleCoin, ObjectorLordId = s.CastleObjector };
                rc.Garrison = s.Garrison;
                if (rc.Castle != null && rc.Castle.Active && rc.Castle.ObjectorLordId >= 0 && rc.Siege == null) rc.PendingObjector = rc.LordById(rc.Castle.ObjectorLordId);
            }
            if (s.Version >= 9)
            {
                rc.Estate = !s.HasEstate ? null : new Estate
                {
                    SettlementId = s.EstateSettlementId ?? string.Empty, Name = s.EstateName ?? string.Empty, StewardName = s.StewardName ?? string.Empty, Tax = (TaxPolicy)s.EstateTax, Justice = (JusticePolicy)s.EstateJustice, PendingCrisis = (EstateCrisis)s.EstateCrisis,
                    StewardHonesty = s.StewardHonesty, StewardCompetence = s.StewardCompetence, Unrest = s.EstateUnrest, Prosperity = s.EstateProsperity, Granary = s.EstateGranary,
                    Treasury = s.EstateTreasury, DaysSinceCourt = s.EstateDaysSinceCourt, GarrisonMen = s.EstateGarrison, IncomeThisMonth = s.EstateIncomeMonth, SkimmedThisMonth = s.EstateSkimMonth, InRevolt = s.EstateRevolt
                };
                rc.Lordship = new LordAllegiance { Declared = (Allegiance)s.LordDeclared, SecretDeal = (Allegiance)s.LordSecret, DealsTaken = s.LordDeals, DemandsRefusedStephen = s.LordRefusedStephen, DemandsRefusedMatilda = s.LordRefusedMatilda, Suspicion = s.LordSuspicion, Discovered = s.LordDiscovered };
                rc.Suit = !s.HasSuit ? null : new Suit { Heiress = s.SuitHeiress ?? string.Empty, DowrySettlementId = s.SuitDowryId ?? string.Empty, FatherLordId = s.SuitFather, Visits = s.SuitVisits, Favour = s.SuitFavour, Consented = s.SuitConsented, Married = s.SuitMarried };
                if (s.HasRealm) { rc.Realm = new RealmState { CrownTreasury = s.RealmTreasury, SeasonsElapsed = s.RealmSeasons, ChurchFavour = s.RealmChurch }; if (s.RealmChronicle != null) rc.Realm.Chronicle.AddRange(s.RealmChronicle); }
                rc.DaysIntoSeason = s.DaysIntoSeason; rc.NextNpcId = s.NextNpcId > 0 ? s.NextNpcId : 100; rc.PendingDemand = (Allegiance)s.PendingDemand;
            }
            if (s.Version >= 10)
            {
                if (s.HasPlot) { rc.Plot = new Plot { Kind = (PlotKind)s.PlotKind, Plotter = (PlotterKind)s.PlotterKind, PlotterId = s.PlotterId, DaysUntil = s.PlotDaysUntil, WarningsGiven = s.PlotWarnings, PlotterName = s.PlotterName ?? string.Empty, Guarded = s.PlotGuarded, Tasted = s.PlotTasted }; if (s.PlotWarningLines != null) rc.Plot.Warnings.AddRange(s.PlotWarningLines); }
                rc.PendingRaiders = s.PendingRaiders; rc.EstateDaysThisMonth = s.EstateDaysThisMonth; rc.WarHeat = s.WarHeat;
            }
            if (spawner != null && rc.Settlements.Count > 0) spawner.Build(rc.Settlements, boot.World.overworld.terrainSizeMetres, boot.Rng.Fork(46));
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
            if (director != null && grunt != null && !grunt.enabled && rc.Service.Active && rc.Service.Role != ServiceRole.Commander && !(rc.Siege != null && rc.Siege.PlayerCommands)) { grunt.enabled = true; director.AiCommandsPlayerSide = true; grunt.Init(_balance, placeBody: false); }
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

        // ------------------------------------------------------------------ England snapshots

        private static SettlementSnapshot SnapshotSettlement(Settlement t)
        {
            var o = new SettlementSnapshot
            {
                Id = t.Id, Name = t.Name, Type = (byte)t.Type, Nx = t.Nx, Nz = t.Nz, Population = t.Population, Wealth = t.Wealth, Allegiance = (byte)t.Allegiance, LordId = t.LordId,
                Outbreak = t.Outbreak, WantedHere = t.WantedHere, Coastal = t.Coastal, Upland = t.Upland, River = t.River, IsFixed = t.IsFixed
            };
            if (t.Market != null)
            {
                int g = t.Market.Goods.Count; o.GoodIds = new string[g]; o.GoodStocks = new float[g]; o.GoodBaselines = new float[g]; o.GoodDemands = new float[g];
                for (int i = 0; i < g; i++) { var m = t.Market.Goods[i]; o.GoodIds[i] = m.Id; o.GoodStocks[i] = m.Stock; o.GoodBaselines[i] = m.Baseline; o.GoodDemands[i] = m.Demand; }
                o.Shock = (byte)t.Market.Shock; o.ShockStrength = t.Market.ShockStrength;
            }
            o.Jobs = new JobSnapshot[t.Jobs.Count]; for (int i = 0; i < o.Jobs.Length; i++) o.Jobs[i] = SnapshotJob(t.Jobs[i]);
            return o;
        }

        private static JobSnapshot SnapshotJob(Job j) => new JobSnapshot { Id = j.Id, Kind = (byte)j.Kind, FromId = j.FromId, ToId = j.ToId, Employer = j.Employer, Blurb = j.Blurb, Pay = j.Pay, Advance = j.Advance, DaysRequired = j.DaysRequired, DaysServed = j.DaysServed, DeadlineDay = j.DeadlineDay, Danger = j.Danger, State = (byte)j.State };
        private static Job RestoreJob(JobSnapshot j) => new Job { Id = j.Id, Kind = (JobKind)j.Kind, FromId = j.FromId, ToId = j.ToId, Employer = j.Employer, Blurb = j.Blurb, Pay = j.Pay, Advance = j.Advance, DaysRequired = j.DaysRequired, DaysServed = j.DaysServed, DeadlineDay = j.DeadlineDay, Danger = j.Danger, State = (JobState)j.State };

        private void RestoreEngland(RunState s, RunContext rc)
        {
            var boot = GameBootstrap.Instance;
            rc.EnsureEngland(_balance);
            if (rc.Names == null) rc.Names = DataFiles.LoadNames();
            if (s.Version >= 5 && s.Settlements != null && s.Settlements.Length > 0)
            {
                rc.Settlements.Clear();
                foreach (var t in s.Settlements)
                {
                    var o = new Settlement
                    {
                        Id = t.Id, Name = t.Name, Type = (SettlementType)t.Type, Nx = t.Nx, Nz = t.Nz, Population = t.Population, Wealth = t.Wealth, Allegiance = (Allegiance)t.Allegiance, LordId = t.LordId,
                        Outbreak = t.Outbreak, WantedHere = t.WantedHere, Coastal = t.Coastal, Upland = t.Upland, River = t.River, IsFixed = t.IsFixed, Market = new Market(_balance.economy, rc.Items)
                    };
                    for (int i = 0; i < t.GoodIds.Length; i++) { o.Market.Stock(t.GoodIds[i], i < t.GoodBaselines.Length ? t.GoodBaselines[i] : 1f, i < t.GoodDemands.Length ? t.GoodDemands[i] : 1f); var g = o.Market.Find(t.GoodIds[i]); if (g != null && i < t.GoodStocks.Length) g.Stock = t.GoodStocks[i]; }
                    o.Market.RestoreShock((WarShock)t.Shock, t.ShockStrength); o.Market.Wealth = t.Wealth;
                    foreach (var j in t.Jobs) o.Jobs.Add(RestoreJob(j));
                    rc.Settlements.Add(o);
                }
                for (int i = 0; i < 4 && i < s.Reputation.Length; i++) rc.Reputation.Set((Faction)i, s.Reputation[i]); rc.Reputation.SetNotoriety(s.Notoriety);
                rc.Crime.Crimes.Clear(); foreach (var c in s.Crimes) rc.Crime.Crimes.Add(new Crime { Kind = (CrimeKind)c.Kind, Where = c.Where, Day = c.Day, Witnesses = c.Witnesses, Known = c.Known, Tried = c.Tried });
                rc.Crime.SetOutlawed(s.Outlawed); rc.Crime.SetSanctuary(s.SanctuaryDaysLeft, s.SanctuaryExpired);
                rc.ActiveJob = s.ActiveJob != null ? RestoreJob(s.ActiveJob) : null; rc.NextJobId = s.NextJobId; rc.HomeSettlementId = s.HomeSettlementId;
            }
            else if (rc.Settlements.Count == 0)
            {
                // Older save: build England fresh from the seed.
                var coast = DataFiles.LoadCoast(); rc.MapSideKm = coast.mapSideKm;
                rc.Settlements = EnglandSetup.Generate(_balance, boot.World, coast, rc.Items, rc.Baronage, new NameGenerator(rc.Names, boot.Rng.Fork(41)), boot.Rng.Fork(45));
                rc.HomeSettlementId = string.IsNullOrEmpty(rc.SpawnLocationId) ? "ashby_village" : rc.SpawnLocationId;
            }
            var home = rc.Home;
            if (home != null && home.Market != null) rc.Market = home.Market;
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
            if (spawner != null) spawner.Build(RunSession.Context.Settlements, GameBootstrap.Instance.World.overworld.terrainSizeMetres, GameBootstrap.Instance.Rng.Fork(46));
            SaveNow(AffairsChoice.None); // a run exists from its first frame
        }

        private void OnCharacterChosen(CharacterFactory.Choice choice)
        {
            RunContext ctx;
            try { ctx = RunFactory.Create(choice, vitals); }
            catch (Exception e) { Debug.LogError("[run] could not create the character: " + e.Message); creation.Show(DataFiles.LoadBackgrounds(), OnCharacterChosen); return; }
            RunSession.Context = ctx;
            if (appearance != null) appearance.Apply(ctx.Character);
            if (spawner != null) spawner.Build(ctx.Settlements, GameBootstrap.Instance.World.overworld.terrainSizeMetres, GameBootstrap.Instance.Rng.Fork(46));
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
            hud.ShowDeath(vitals.CauseOfDeath ?? "unknown", days, melee != null ? melee.Kills : 0, director != null ? director.PlayerLosses : 0, RunSession.ReturnChronicle, neverWoke: false);
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
