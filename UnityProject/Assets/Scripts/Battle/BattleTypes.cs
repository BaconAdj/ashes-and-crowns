using AshesAndCrowns.Sim;
using Unity.Mathematics;

namespace AshesAndCrowns.Battle
{
    public enum Side : byte { Player = 0, Enemy = 1 }

    /// <summary>Special target indices used in SoldierTargets.</summary>
    public static class TargetIds
    {
        public const int None = -1;
        public const int PlayerCharacter = -2;
    }

    /// <summary>Per-unit state read by the jobs. Kept as a blittable struct in a NativeArray.</summary>
    public struct UnitState
    {
        public byte Side;
        public byte Formation;   // FormationType
        public byte Order;       // OrderType
        public byte Alive;       // 1 while any soldier lives
        public int SoldierStart;
        public int SoldierCount;
        public float3 Anchor;    // formation centre the soldiers try to hold
        public float Yaw;        // radians, facing of the formation
        public float3 Target;    // destination (Move/Charge)
        public int AliveCount;

        public FormationType FormationType => (FormationType)Formation;
        public OrderType OrderType => (OrderType)Order;
        public Side SideEnum => (Side)Side;
    }

    /// <summary>Blittable copy of the battle numbers from balance.json, passed by value into jobs.</summary>
    public struct BattleParams
    {
        public float FormationSpacing, ShieldwallSpacing;
        public float WalkSpeed, ChargeSpeed, TurnRate;
        public float AttackRange, AttackCooldown;
        public float EngageRangeHold, EngageRangeAdvance;
        public float BaseDamage, DamageVariance, Armour;
        public float CellSize;
        public float ArrivalRadius;
        public float PlayerEngageRange, PlayerReach;

        public static BattleParams From(BattleBalance b, MeleeBalance m) => new BattleParams
        {
            FormationSpacing = b.formationSpacing, ShieldwallSpacing = b.shieldwallSpacing,
            WalkSpeed = b.soldierWalkSpeed, ChargeSpeed = b.soldierChargeSpeed, TurnRate = b.soldierTurnRate,
            AttackRange = b.attackRange, AttackCooldown = b.attackCooldownSeconds,
            EngageRangeHold = b.engageRangeHold, EngageRangeAdvance = b.engageRangeAdvance,
            BaseDamage = b.soldierBaseDamage, DamageVariance = b.soldierDamageVariance, Armour = b.soldierArmour,
            CellSize = b.spatialCellSize, ArrivalRadius = b.unitArrivalRadius,
            PlayerEngageRange = m.enemyEngagePlayerRange, PlayerReach = m.enemyReach
        };
    }
}
