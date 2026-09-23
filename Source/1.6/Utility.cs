using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace EyesOnTheBackstop
{
    public static class Utility
    {
        public const string StagedRaidIndirectFireMemo = "EyesOnTheBackstop_IndirectFire";
        private const float StagedRaidIndirectFireWakeChance = 0.4f;
        private const int StagedRaidIndirectFireCheckCooldownTicks = 120;
        private static int lastIndirectFireCheckTick = -StagedRaidIndirectFireCheckCooldownTicks; // intentionally global

        public static bool IsBlindFireEnabled => EyesOnTheBackstopMod.Settings?.enableBlindFire == true;

        public static bool IsExtendedBulletTrajectoriesEnabled => EyesOnTheBackstopMod.Settings?.enableExtendedBulletTrajectories ?? true;

        public static bool IsIndirectFireRaidReactionEnabled => EyesOnTheBackstopMod.Settings?.enableIndirectFireRaidReaction == true;

        public static bool IsObstaclePenetrationEnabled => EyesOnTheBackstopMod.Settings?.enableObstaclePenetration == true;

        public static bool IsRicochetsEnabled => EyesOnTheBackstopMod.Settings?.enableRicochets == true;

        public static int MaxRicochetAngle => EyesOnTheBackstopMod.Settings?.MaxRicochetAngle
            ?? EyesOnTheBackstopSettings.DefaultMaxRicochetAngle;

        public static bool IsSupportedBulletProjectile(ThingDef projectileDef)
        {
            return projectileDef != null
                && projectileDef.thingClass != null
                && projectileDef.projectile != null
                && typeof(Bullet).IsAssignableFrom(projectileDef.thingClass);
        }

        public static bool IsSupportedBulletProjectile(Projectile projectile)
        {
            return projectile is Bullet && IsSupportedBulletProjectile(projectile.def);
        }

        public static bool CanPenetrateThing(Thing target, Projectile projectile, Thing weapon = null)
        {
            if (!IsObstaclePenetrationEnabled
                || target == null
                || projectile == null
                || !IsSupportedBulletProjectile(projectile)
                || target.def == null
                || !target.def.useHitPoints
                || target.def.Fillage != FillCategory.Full)
            {
                return false;
            }

            Building_Door door = target as Building_Door;
            if (door != null && door.Open)
            {
                return false;
            }

            int threshold = EyesOnTheBackstopMod.Settings?.PenetrableEffectiveHitPoints
                ?? EyesOnTheBackstopSettings.DefaultPenetrableEffectiveHitPoints;
            return GetEffectiveHitPoints(target, projectile, weapon) < threshold;
        }

        public static float GetEffectiveHitPoints(Thing target, Projectile projectile, Thing weapon = null)
        {
            if (target == null || projectile == null || projectile.def?.projectile == null)
            {
                return 0f;
            }

            float armorPenetration = weapon != null
                ? projectile.def.projectile.GetArmorPenetration(weapon)
                : projectile.ArmorPenetration;
            return target.HitPoints * (1f - armorPenetration);
        }

        public static bool CanUseBlindFire(Pawn pawn)
        {
            if (!IsBlindFireEnabled)
            {
                return false;
            }

            if (pawn == null || (!pawn.IsColonistPlayerControlled && !pawn.IsColonyMechPlayerControlled && !pawn.IsColonySubhumanPlayerControlled))
            {
                return false;
            }

            Verb primaryVerb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            if (!(primaryVerb is Verb_LaunchProjectile projectileVerb))
            {
                return false;
            }

            return IsSupportedBulletProjectile(projectileVerb.Projectile);
        }

        public static bool TryGetBlindFireTarget(Pawn pawn, LocalTargetInfo target, out LocalTargetInfo blindFireTarget)
        {
            blindFireTarget = target;
            if (!IsBlindFireEnabled
                || pawn == null
                || (!pawn.IsColonistPlayerControlled && !pawn.IsColonyMechPlayerControlled && !pawn.IsColonySubhumanPlayerControlled)
                || !pawn.Spawned
                || pawn.Map == null
                || !pawn.Drafted
                || !target.Cell.IsValid
                || (target.HasThing && (target.Thing == null || target.Thing.Destroyed || target.Thing.Map != pawn.Map)))
            {
                return false;
            }

            Verb_LaunchProjectile projectileVerb = pawn.TryGetAttackVerb(target.HasThing ? target.Thing : null, !pawn.IsColonist) as Verb_LaunchProjectile;
            if (projectileVerb == null || !IsSupportedBulletProjectile(projectileVerb.Projectile))
            {
                return false;
            }

            float targetDistanceSquared = target.HasThing
                ? target.Thing.OccupiedRect().ClosestDistSquaredTo(pawn.Position)
                : pawn.Position.DistanceToSquared(target.Cell);
            if (targetDistanceSquared <= projectileVerb.EffectiveRange * projectileVerb.EffectiveRange
                && (!target.HasThing || projectileVerb.CanHitTargetFrom(pawn.Position, target)))
            {
                return false;
            }

            Vector3 direction = target.Cell.ToVector3Shifted() - pawn.Position.ToVector3Shifted();
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            direction.Normalize();

            float furthestCandidateDistance = Mathf.Max(1f, projectileVerb.EffectiveRange - 0.5f);
            for (float distance = furthestCandidateDistance; distance >= 1f; distance -= 0.5f)
            {
                IntVec3 candidateCell = (pawn.Position.ToVector3Shifted() + direction * distance).ToIntVec3();
                if (!candidateCell.InBounds(pawn.Map) || candidateCell == pawn.Position)
                {
                    continue;
                }

                LocalTargetInfo candidateTarget = new LocalTargetInfo(candidateCell);
                if (!projectileVerb.OutOfRange(
                    pawn.Position,
                    candidateTarget,
                    CellRect.SingleCell(candidateCell)))
                {
                    blindFireTarget = candidateTarget;
                    return true;
                }
            }

            return false;
        }

        public static Job MakeBlindFireJob(Pawn pawn, LocalTargetInfo target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            job.targetB = new LocalTargetInfo(pawn);
            job.targetC = new LocalTargetInfo(pawn);
            return job;
        }

        public static bool IsBlindFireJob(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job != null
                && job.def == JobDefOf.AttackStatic
                && job.targetB.Thing == pawn
                && job.targetC.Thing == pawn;
        }

        public static void TryWakeStagedRaidFromIndirectFire(Pawn shooter, Pawn victim, ThingDef projectileDef, ThingDef equipmentDef)
        {
            if (!IsIndirectFireRaidReactionEnabled)
            {
                return;
            }

            Lord lord = victim?.GetLord();
            if (shooter == null || lord == null || !(lord.LordJob is LordJob_StageThenAttack) || !(lord.CurLordToil is LordToil_Stage))
            {
                return;
            }

            float weaponRange = FindWeaponRange(shooter, projectileDef, equipmentDef);
            if (weaponRange <= 0f || shooter.Position.InHorDistOf(victim.Position, weaponRange))
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastIndirectFireCheckTick < StagedRaidIndirectFireCheckCooldownTicks)
            {
                return;
            }

            lastIndirectFireCheckTick = currentTick;
            if (!Rand.Chance(StagedRaidIndirectFireWakeChance))
            {
                return;
            }

            lord.ReceiveMemo(StagedRaidIndirectFireMemo);
        }

        private static float FindWeaponRange(Pawn shooter, ThingDef projectileDef, ThingDef equipmentDef)
        {
            List<Verb> verbs = shooter.equipment?.PrimaryEq?.AllVerbs;
            Verb_LaunchProjectile fallbackProjectileVerb = null;
            if (verbs != null)
            {
                for (int i = 0; i < verbs.Count; i++)
                {
                    Verb_LaunchProjectile projectileVerb = verbs[i] as Verb_LaunchProjectile;
                    if (projectileVerb == null || projectileVerb.Projectile != projectileDef)
                    {
                        continue;
                    }

                    fallbackProjectileVerb = fallbackProjectileVerb ?? projectileVerb;
                    if (equipmentDef == null || projectileVerb.EquipmentSource?.def == equipmentDef)
                    {
                        return projectileVerb.EffectiveRange;
                    }
                }
            }

            if (fallbackProjectileVerb != null)
            {
                return fallbackProjectileVerb.EffectiveRange;
            }

            List<VerbProperties> verbProperties = equipmentDef?.Verbs;
            if (verbProperties != null)
            {
                for (int i = 0; i < verbProperties.Count; i++)
                {
                    VerbProperties verbProps = verbProperties[i];
                    if (verbProps != null
                        && verbProps.LaunchesProjectile
                        && verbProps.defaultProjectile == projectileDef)
                    {
                        return verbProps.AdjustedRange(null, shooter);
                    }
                }
            }

            return 0f;
        }
    }
}
