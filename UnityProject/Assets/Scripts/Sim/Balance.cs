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
        public int soldiersPerSide = 50, unitSize = 25;
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
    }

    [Serializable] public sealed class LookBalance
    {
        public float fogDensity = 0.011f;
        public float[] fogColor = { 0.62f, 0.65f, 0.68f };
        public float[] ambientColor = { 0.30f, 0.33f, 0.38f };
        public float[] sunColor = { 0.80f, 0.84f, 0.92f };
        public float sunIntensity = 1.05f, sunPitchDegrees = 28f, sunYawDegrees = -140f;
        public float saturation = -42f, contrast = 14f, postExposure = -0.25f, vignette = 0.36f, filmGrain = 0.38f;
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
            return errors;
        }

        private static void Positive(List<string> errors, float v, string name) { if (!(v > 0f)) errors.Add(name + " must be positive"); }
        private static void Positive(List<string> errors, int v, string name) { if (v <= 0) errors.Add(name + " must be positive"); }
        private static void Unit(List<string> errors, float v, string name) { if (v < 0f || v > 1f) errors.Add(name + " must be within 0..1"); }
    }
}
