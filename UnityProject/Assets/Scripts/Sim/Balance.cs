using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    // Plain, serializable data mirrors of Assets/Data/balance.json.
    // Public fields (not properties) so Unity's JsonUtility and System.Text.Json can both fill them.
    // Rule: no gameplay number may live anywhere else.

    [Serializable] public sealed class SimulationBalance
    {
        public long defaultSeed = 1135;
        public float realHoursToGameHours = 2f;
        public int maxCatchUpGameDays = 10;
    }

    [Serializable] public sealed class TimeBalance
    {
        public int startYear = 1135, startMonth = 12, startDay = 22, startHour = 9;
        public float realSecondsPerGameHour = 90f;
        public int[] bellHours = { 6, 9, 12, 15, 18, 21 };
        public string[] bellNames = { "Prime", "Terce", "Sext", "None", "Vespers", "Compline" };
    }

    [Serializable] public sealed class PlayerBalance
    {
        public float walkSpeed = 4.2f, sprintSpeed = 7f, gravity = 20f;
        public float staminaMax = 100f, staminaRegenPerSecond = 10f, sprintStaminaPerSecond = 14f;
        public float mountRadius = 3f, height = 1.8f;
    }

    [Serializable] public sealed class HorseBalance
    {
        public float walkSpeed = 6.5f, gallopSpeed = 13f, acceleration = 6f, braking = 9f;
        public float turnDegreesPerSecond = 110f, gallopTurnDegreesPerSecond = 55f, seatHeight = 1.75f;
    }

    [Serializable] public sealed class CameraBalance
    {
        public float thirdPersonDistance = 5.5f, thirdPersonHeight = 1.9f, ridingDistance = 7.5f, ridingHeight = 2.6f;
        public float lookSensitivity = 0.12f, pitchMinDegrees = -25f, pitchMaxDegrees = 65f, followSmoothing = 12f;
        public float commandHeight = 55f, commandPitchDegrees = 58f, commandPanSpeed = 45f;
        public float commandZoomMin = 25f, commandZoomMax = 120f, commandZoomSpeed = 6f;
    }

    [Serializable] public sealed class CommandBalance
    {
        public float vantageRadius = 14f, unitSelectRadius = 10f;
    }

    [Serializable] public sealed class BattleBalance
    {
        public int soldiersPerUnit = 1;          // 1 rendered soldier = N men (escape hatch)
        public int soldiersPerSide = 1000, unitSize = 50;
        public float formationSpacing = 1.3f, shieldwallSpacing = 0.9f;
        public float soldierHealth = 30f, soldierArmour = 0.2f, soldierBaseDamage = 11f, soldierDamageVariance = 0.35f;
        public float soldierWalkSpeed = 2f, soldierChargeSpeed = 4.3f, soldierTurnRate = 6f;
        public float attackRange = 1.6f, attackCooldownSeconds = 1.4f;
        public float engageRangeHold = 6f, engageRangeAdvance = 14f, spatialCellSize = 3f;
        public float enemyAdvanceDelaySeconds = 8f, enemyChargeDistance = 30f, unitArrivalRadius = 1.5f;
    }

    [Serializable] public sealed class MeleeBalance
    {
        public float reach = 2.1f, arcDegrees = 110f;
        public float swingWindupSeconds = 0.28f, swingRecoverySeconds = 0.55f, swingStaminaCost = 12f;
        public float baseDamage = 24f, damageVariance = 0.3f;
        public float blockArcDegrees = 110f, blockStaminaCost = 16f;
        public float enemyReach = 1.7f, enemyAttackCooldownSeconds = 1.7f, enemyDamage = 0.16f, enemyEngagePlayerRange = 9f;
    }

    [Serializable] public sealed class WoundBalance
    {
        /// <summary>Hit probability per body part, in BodyPart enum order.</summary>
        public float[] bodyPartHitWeights = { 0.08f, 0.42f, 0.14f, 0.16f, 0.10f, 0.10f };
        /// <summary>How much a wound to this part drains the body, in BodyPart enum order.</summary>
        public float[] bodyPartLethality = { 2.6f, 1f, 0.35f, 0.35f, 0.45f, 0.45f };
        public float bleedPerSecondPerSeverity = 0.004f;
        public float clotSecondsPerSeverity = 25f;
        public float restHealPerHour = 0.02f;
        public float maimThreshold = 0.85f;
        public float infectionChancePerDayPerSeverity = 0.18f; // an untreated 0.5 wound: 9% a day
        public float treatedInfectionMultiplier = 0.25f;
        public float infectionBloodLossPerDay = 0.1f;
        public float infectionClearChancePerDay = 0.09f;
        public float infectionWindowDays = 6f;           // wounds older than this no longer infect
    }

    [Serializable] public sealed class LookBalance
    {
        public float fogDensity = 0.011f;
        public float[] fogColor = { 0.62f, 0.65f, 0.68f };
        public float[] ambientColor = { 0.30f, 0.33f, 0.38f };
        public float[] sunColor = { 0.80f, 0.84f, 0.92f };
        public float sunIntensity = 1.05f, sunPitchDegrees = 28f, sunYawDegrees = -140f;
        public float saturation = -42f, contrast = 14f, postExposure = -0.25f, vignette = 0.36f, filmGrain = 0.38f;
        // Day/night
        public float noonSunIntensity = 1.1f, dawnSunIntensity = 0.45f, moonIntensity = 0.08f;
        public float[] nightAmbientColor = { 0.05f, 0.06f, 0.09f };
        public float[] dawnSunColor = { 0.85f, 0.62f, 0.45f };
        public float[] nightFogColor = { 0.08f, 0.09f, 0.12f };
        public float cloudSunDimming = 0.55f;   // sun intensity multiplier at full cloud cover
        public float rainParticlesPerSecond = 2500f, snowParticlesPerSecond = 900f;
    }

    [Serializable] public sealed class SaveBalance
    {
        public float autosaveIntervalSeconds = 30f;
        public float dirtySaveDelaySeconds = 2f;   // a wound/kill/bell schedules a save this soon, instead of writing per event
    }

    /// <summary>Dangers while the game is closed, per game day, by how the player left their affairs.</summary>
    [Serializable] public sealed class AbsenceBalance
    {
        public float dangerNone = 0.09f;           // closed the game without choosing: careless
        public float dangerSleepAtHome = 0.012f;
        public float dangerPostGuards = 0.006f;
        public float dangerLeaveSteward = 0.010f;
        public float dangerFortify = 0.004f;
        public float dangerKeepTravelling = 0.07f;
        public float dangerHide = 0.035f;
        public float hostileCountryMultiplier = 3f;
        public float woundSeverityMin = 0.12f, woundSeverityMax = 0.55f;
        public float feverBloodLoss = 0.08f;
        public float restHealMultiplier = 1f;      // scales WoundBalance.restHealPerHour while resting safely
        public float battleStandHitsPerEnemy = 1.1f;
        public float battleFleeHitChance = 0.35f;
        public float attackShare = 0.45f, feverShare = 0.30f, robberyShare = 0.25f; // what a bad night turns out to be
        public float hostilityHome = 0f, hostilityCountry = 0.35f, hostilityBattlefield = 0.7f, homeRadiusMetres = 150f;
    }

    [Serializable] public sealed class VillageBalance
    {
        public int hutCount = 9;
        public float greenRadiusMetres = 22f, flattenRadiusMetres = 70f;
        public float[] hutFootprintMetres = { 5.5f, 4f };
        public float hutWallHeight = 2.2f, hutRoofHeight = 2.4f;
        public float[] churchNave = { 9f, 6f, 18f };   // width, wall height, length
        public float churchTowerHeight = 16f;
        public float[] alehouseFootprint = { 10f, 7f };
        public float alehouseWallHeight = 3f;
    }

    [Serializable] public sealed class Balance
    {
        public int version = 1;
        public SimulationBalance simulation = new SimulationBalance();
        public TimeBalance time = new TimeBalance();
        public PlayerBalance player = new PlayerBalance();
        public HorseBalance horse = new HorseBalance();
        public CameraBalance camera = new CameraBalance();
        public CommandBalance command = new CommandBalance();
        public BattleBalance battle = new BattleBalance();
        public MeleeBalance melee = new MeleeBalance();
        public WoundBalance wounds = new WoundBalance();
        public LookBalance look = new LookBalance();
        public WeatherBalance weather = new WeatherBalance();
        public VillageBalance village = new VillageBalance();
        public SaveBalance save = new SaveBalance();
        public CharacterBalance character = new CharacterBalance();
        public BaronageBalance baronage = new BaronageBalance();
        public SurvivalBalance survival = new SurvivalBalance();
        public DiseaseBalance disease = new DiseaseBalance();
        public EconomyBalance economy = new EconomyBalance();
        public StandingBalance standing = new StandingBalance();
        public SettlementBalance settlements = new SettlementBalance();
        public JobBalance jobs = new JobBalance();
        public ReputationBalance reputation = new ReputationBalance();
        public CrimeBalance crime = new CrimeBalance();
        public MeleeRulesBalance duel = new MeleeRulesBalance();
        public MoraleBalance morale = new MoraleBalance();
        public ServiceBalance service = new ServiceBalance();
        public SiegeBalance siege = new SiegeBalance();
        public LordshipBalance lordship = new LordshipBalance();
        public IntrigueBalance intrigue = new IntrigueBalance();
        public VillageEventBalance villageEvents = new VillageEventBalance();
        public AbsenceBalance absence = new AbsenceBalance();
    }

    public static class BalanceValidator
    {
        /// <summary>Returns a list of human-readable problems. Empty list means the balance file is usable.</summary>
        public static List<string> Validate(Balance b)
        {
            var errors = new List<string>();
            if (b == null) { errors.Add("balance is null"); return errors; }
            Positive(errors, b.player.walkSpeed, "player.walkSpeed");
            Positive(errors, b.player.sprintSpeed, "player.sprintSpeed");
            Positive(errors, b.player.staminaMax, "player.staminaMax");
            Positive(errors, b.horse.walkSpeed, "horse.walkSpeed");
            if (b.horse.gallopSpeed <= b.horse.walkSpeed) errors.Add("horse.gallopSpeed must exceed horse.walkSpeed");
            if (b.player.sprintSpeed <= b.player.walkSpeed) errors.Add("player.sprintSpeed must exceed player.walkSpeed");
            Positive(errors, b.battle.soldiersPerUnit, "battle.soldiersPerUnit");
            Positive(errors, b.battle.soldiersPerSide, "battle.soldiersPerSide");
            Positive(errors, b.battle.unitSize, "battle.unitSize");
            Positive(errors, b.battle.soldierHealth, "battle.soldierHealth");
            Positive(errors, b.battle.attackRange, "battle.attackRange");
            Positive(errors, b.battle.spatialCellSize, "battle.spatialCellSize");
            if (b.battle.spatialCellSize < b.battle.attackRange) errors.Add("battle.spatialCellSize must be >= battle.attackRange (3x3 cell search)");
            Unit(errors, b.battle.soldierArmour, "battle.soldierArmour");
            Positive(errors, b.melee.reach, "melee.reach");
            Positive(errors, b.melee.baseDamage, "melee.baseDamage");
            Unit(errors, b.melee.enemyDamage, "melee.enemyDamage");
            if (b.wounds.bodyPartHitWeights == null || b.wounds.bodyPartHitWeights.Length != BodyParts.Count)
                errors.Add("wounds.bodyPartHitWeights must have " + BodyParts.Count + " entries");
            if (b.wounds.bodyPartLethality == null || b.wounds.bodyPartLethality.Length != BodyParts.Count)
                errors.Add("wounds.bodyPartLethality must have " + BodyParts.Count + " entries");
            if (b.time.bellHours == null || b.time.bellNames == null || b.time.bellHours.Length != b.time.bellNames.Length)
                errors.Add("time.bellHours and time.bellNames must have the same length");
            if (b.simulation.realHoursToGameHours <= 0f) errors.Add("simulation.realHoursToGameHours must be positive");
            Positive(errors, b.time.realSecondsPerGameHour, "time.realSecondsPerGameHour");
            if (b.time.startMonth < 1 || b.time.startMonth > 12) errors.Add("time.startMonth must be 1..12");
            if (b.save == null || b.save.autosaveIntervalSeconds <= 0f) errors.Add("save.autosaveIntervalSeconds must be positive");
            if (b.absence == null) errors.Add("absence missing");
            else
            {
                Unit(errors, b.absence.dangerNone, "absence.dangerNone"); Unit(errors, b.absence.dangerKeepTravelling, "absence.dangerKeepTravelling");
                if (b.absence.woundSeverityMax < b.absence.woundSeverityMin) errors.Add("absence.woundSeverityMax must be >= woundSeverityMin");
                if (Math.Abs(b.absence.attackShare + b.absence.feverShare + b.absence.robberyShare - 1f) > 0.01f) errors.Add("absence shares must sum to 1");
            }
            if (b.simulation.maxCatchUpGameDays <= 0) errors.Add("simulation.maxCatchUpGameDays must be positive");
            if (b.character == null || b.character.skillCap <= 0f) errors.Add("character.skillCap must be positive");
            if (b.survival == null || b.survival.hungerPerGameHour <= 0f) errors.Add("survival.hungerPerGameHour must be positive");
            if (b.economy == null || b.economy.foodSeasonByMonth == null || b.economy.foodSeasonByMonth.Length != 12) errors.Add("economy.foodSeasonByMonth must have 12 entries");
            if (b.economy != null && (b.economy.sellSpread <= 0f || b.economy.sellSpread > 1f)) errors.Add("economy.sellSpread must be within 0..1");
            if (b.settlements == null || b.settlements.walkKmPerDay <= 0f || b.settlements.rideKmPerDay <= b.settlements.walkKmPerDay) errors.Add("settlements: rideKmPerDay must exceed walkKmPerDay > 0");
            if (b.crime == null || b.crime.arrestChancePerDayAtFullWanted < 0f || b.crime.arrestChancePerDayAtFullWanted > 1f) errors.Add("crime.arrestChancePerDayAtFullWanted must be 0..1");
            if (b.duel == null || b.duel.windupSeconds <= 0f || b.duel.parryWindowSeconds <= 0f) errors.Add("duel timings must be positive");
            if (b.morale == null || !(b.morale.routBelow < b.morale.shakenBelow && b.morale.shakenBelow < b.morale.rallyAbove)) errors.Add("morale thresholds must satisfy rout < shaken < rally");
            if (b.service == null || b.service.floggingAbove >= b.service.hangingAbove) errors.Add("service.floggingAbove must be below hangingAbove");
            if (b.siege == null || b.siege.surrenderMoraleBelow <= 0f || b.siege.surrenderMoraleBelow >= 1f) errors.Add("siege.surrenderMoraleBelow must be within (0,1)");
            if (b.intrigue == null || b.intrigue.plotDaysMin <= 0 || b.intrigue.plotDaysMax < b.intrigue.plotDaysMin || b.villageEvents == null || b.villageEvents.raiderCountMin < 1) errors.Add("intrigue/villageEvents: plot days and raider counts must be positive and ordered");
            if (b.lordship == null || b.lordship.revoltAbove <= b.lordship.revoltCalm || b.lordship.seasonDays <= 0 || b.lordship.courtEveryDays <= 0) errors.Add("lordship: revoltAbove must exceed revoltCalm; seasonDays and courtEveryDays positive");
            if (b.siege != null && (b.siege.ladderVulnerability < 1f || b.siege.wallHalfThickness <= 0f || b.siege.motteDays <= 0 || b.siege.garrisonMin <= 0)) errors.Add("siege layout and castle numbers must be positive");
            if (b.baronage == null || b.baronage.minorLordCount <= 0) errors.Add("baronage.minorLordCount must be positive");
            if (b.baronage != null && b.baronage.knightsMax < b.baronage.knightsMin) errors.Add("baronage.knightsMax must be >= knightsMin");
            if (b.weather == null) errors.Add("weather missing");
            else
            {
                Positive(errors, b.weather.changeIntervalHours, "weather.changeIntervalHours");
                Twelve(errors, b.weather.overcastByMonth, "weather.overcastByMonth");
                Twelve(errors, b.weather.rainByMonth, "weather.rainByMonth");
                Twelve(errors, b.weather.fogByMonth, "weather.fogByMonth");
                Twelve(errors, b.weather.snowByMonth, "weather.snowByMonth");
                if (b.weather.rainByMonth != null && b.weather.rainByMonth.Length == 12)
                    for (int m = 0; m < 12; m++)
                        if (b.weather.overcastByMonth[m] + b.weather.rainByMonth[m] + b.weather.fogByMonth[m] + b.weather.snowByMonth[m] > 1f)
                            errors.Add("weather probabilities for month " + (m + 1) + " exceed 1");
            }
            return errors;
        }

        private static void Positive(List<string> errors, float v, string name) { if (!(v > 0f)) errors.Add(name + " must be positive"); }
        private static void Positive(List<string> errors, int v, string name) { if (v <= 0) errors.Add(name + " must be positive"); }
        private static void Twelve(List<string> errors, float[] a, string name) { if (a == null || a.Length != 12) errors.Add(name + " must have 12 entries"); }
        private static void Unit(List<string> errors, float v, string name) { if (v < 0f || v > 1f) errors.Add(name + " must be within 0..1"); }
    }
}
