using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch))]
    [HarmonyPatch(new Type[]
    {
        typeof(Thing),
        typeof(Vector3),
        typeof(LocalTargetInfo),
        typeof(LocalTargetInfo),
        typeof(ProjectileHitFlags),
        typeof(bool),
        typeof(Thing),
        typeof(ThingDef)
    })]
    public static class BulletLaunchPatch
    {
        public static void Prefix(
            Projectile __instance,
            ref LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            Vector3 origin,
            Thing equipment,
            Thing launcher)
        {
            if (!Utility.IsExtendedBulletTrajectoriesEnabled
                || !Utility.IsSupportedBulletProjectile(__instance))
            {
                return;
            }

            if (usedTarget.HasThing || IsFullObstacleAtDestination(__instance, usedTarget.Cell))
            {
                return;
            }

            Vector3 targetPosition = usedTarget.Cell.ToVector3Shifted();
            Vector3 direction = targetPosition - origin;
            direction.y = 0f;

            float weaponRange = FindWeaponRange(__instance.def, launcher, equipment);
            if (weaponRange <= 0f)
            {
                return;
            }

            float minRangeMultiplier = EyesOnTheBackstopMod.Settings?.MinRangeMultiplier ?? EyesOnTheBackstopSettings.DefaultMinRangeMultiplier;
            float maxRangeMultiplier = EyesOnTheBackstopMod.Settings?.MaxRangeMultiplier ?? EyesOnTheBackstopSettings.DefaultMaxRangeMultiplier;
            if (minRangeMultiplier > maxRangeMultiplier)
            {
                float temp = minRangeMultiplier;
                minRangeMultiplier = maxRangeMultiplier;
                maxRangeMultiplier = temp;
            }

            float rangeMultiplier = Rand.Range(minRangeMultiplier, maxRangeMultiplier);
            float extendedDistance = weaponRange * rangeMultiplier;
            if (extendedDistance <= 0)
            {
                return;
            }

            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            direction.Normalize();
            IntVec3 extendedTarget = (origin + direction * extendedDistance).ToIntVec3();
            if (!extendedTarget.IsValid || extendedTarget == usedTarget.Cell)
            {
                return;
            }

            usedTarget = new LocalTargetInfo(extendedTarget);
        }

        private static bool IsFullObstacleAtDestination(Projectile projectile, IntVec3 destination)
        {
            return IsFullObstacleAtCell(projectile.Map, destination);
        }

        public static bool IsFullObstacleAtCell(Map map, IntVec3 destination)
        {
            if (map == null || !destination.InBounds(map))
            {
                return false;
            }

            List<Thing> things = destination.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null
                    && thing.def != null
                    && thing.def.Fillage == FillCategory.Full
                    && (!(thing is Building_Door door) || !door.Open))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetImpactNormal(
            Thing target,
            Vector3 origin,
            Vector3 targetPosition,
            out Vector3 normal,
            out Vector3 impactPosition)
        {
            return TryGetImpactGeometry(
                target,
                origin,
                targetPosition,
                out normal,
                out impactPosition,
                out _);
        }

        public static bool TryGetImpactGeometry(
            Thing target,
            Vector3 origin,
            Vector3 targetPosition,
            out Vector3 normal,
            out Vector3 impactPosition,
            out Vector3 exitPosition)
        {
            normal = Vector3.zero;
            impactPosition = Vector3.zero;
            exitPosition = Vector3.zero;
            if (target == null)
            {
                return false;
            }

            Vector3 direction = targetPosition - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            CellRect occupiedRect = target.OccupiedRect();
            float minX = occupiedRect.minX;
            float maxX = occupiedRect.maxX + 1f;
            float minZ = occupiedRect.minZ;
            float maxZ = occupiedRect.maxZ + 1f;

            float xEntry;
            float xExit;
            if (direction.x == 0f)
            {
                if (origin.x < minX || origin.x > maxX)
                {
                    return false;
                }

                xEntry = float.NegativeInfinity;
                xExit = float.PositiveInfinity;
            }
            else if (direction.x > 0f)
            {
                xEntry = (minX - origin.x) / direction.x;
                xExit = (maxX - origin.x) / direction.x;
            }
            else
            {
                xEntry = (maxX - origin.x) / direction.x;
                xExit = (minX - origin.x) / direction.x;
            }

            float zEntry;
            float zExit;
            if (direction.z == 0f)
            {
                if (origin.z < minZ || origin.z > maxZ)
                {
                    return false;
                }

                zEntry = float.NegativeInfinity;
                zExit = float.PositiveInfinity;
            }
            else if (direction.z > 0f)
            {
                zEntry = (minZ - origin.z) / direction.z;
                zExit = (maxZ - origin.z) / direction.z;
            }
            else
            {
                zEntry = (maxZ - origin.z) / direction.z;
                zExit = (minZ - origin.z) / direction.z;
            }

            float entry = Mathf.Max(xEntry, zEntry);
            float exit = Mathf.Min(xExit, zExit);
            if (entry > exit || exit < 0f || entry > 1f)
            {
                return false;
            }

            if (xEntry > zEntry)
            {
                normal = direction.x > 0f ? Vector3.left : Vector3.right;
            }
            else
            {
                normal = direction.z > 0f ? Vector3.back : Vector3.forward;
            }

            impactPosition = origin + direction * Mathf.Max(0f, entry);
            impactPosition.y = 0f;
            exitPosition = origin + direction * Mathf.Max(0f, exit);
            exitPosition.y = 0f;

            return true;
        }

        public static float FindWeaponRange(ThingDef projectileDef, Thing launcher, Thing equipment)
        {
            List<Verb> candidateVerbs = FindCandidateVerbs(launcher, equipment);
            if (candidateVerbs == null)
            {
                return 0f;
            }

            for (int i = 0; i < candidateVerbs.Count; i++)
            {
                Verb_LaunchProjectile projectileVerb = candidateVerbs[i] as Verb_LaunchProjectile;
                if (projectileVerb != null && projectileVerb.Projectile == projectileDef)
                {
                    return projectileVerb.EffectiveRange;
                }
            }

            return 0f;
        }

        private static List<Verb> FindCandidateVerbs(Thing launcher, Thing equipment)
        {
            CompEquippable equipmentComp = equipment?.TryGetComp<CompEquippable>();
            if (equipmentComp != null)
            {
                return equipmentComp.AllVerbs;
            }

            if (equipment is Building_TurretGun buildingTurret && buildingTurret.GunCompEq != null)
            {
                return buildingTurret.GunCompEq.AllVerbs;
            }

            CompTurretGun turretComp = launcher?.TryGetComp<CompTurretGun>();
            return turretComp?.GunCompEq?.AllVerbs;
        }
    }

    [HarmonyPatch(typeof(Bullet), "Impact")]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(bool) })]
    public static class BulletImpactPatch
    {
        public static void Prefix(Bullet __instance, Thing hitThing)
        {
            if (Utility.IsIndirectFireRaidReactionEnabled
                && hitThing is Pawn victim
                && __instance.Launcher is Pawn shooter)
            {
                Utility.TryWakeStagedRaidFromIndirectFire(shooter, victim, __instance.def, __instance.EquipmentDef);
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(bool) })]
    public static class BulletPenetrationPatch
    {
        public static bool Prefix(
            Projectile __instance,
            Thing hitThing,
            bool blockedByShield,
            ref Vector3 ___origin,
            ref Vector3 ___destination,
            ref int ___ticksToImpact,
            ref int ___lifetime,
            ref bool ___landed,
            Thing ___equipment)
        {
            if (blockedByShield
                || hitThing == null)
            {
                return true;
            }

            bool ricochetsEnabled = Utility.IsRicochetsEnabled;
            bool obstaclePenetrationEnabled = Utility.IsObstaclePenetrationEnabled;
            if ((!ricochetsEnabled && !obstaclePenetrationEnabled)
                || !Utility.IsSupportedBulletProjectile(__instance))
            {
                return true;
            }

            if (ricochetsEnabled
                && TryRicochet(
                    __instance,
                    hitThing,
                    ref ___origin,
                    ref ___destination,
                    ref ___ticksToImpact,
                    ref ___lifetime,
                    ref ___landed))
            {
                return false;
            }

            if (TryPenetrate(
                __instance,
                hitThing,
                ref ___origin,
                ref ___destination,
                ref ___ticksToImpact,
                ref ___lifetime,
                ref ___landed,
                ___equipment))
            {
                return false;
            }

            return true;
        }

        private static bool TryPenetrate(
            Projectile projectile,
            Thing hitThing,
            ref Vector3 origin,
            ref Vector3 destination,
            ref int ticksToImpact,
            ref int lifetime,
            ref bool landed,
            Thing equipment)
        {
            if (!Utility.CanPenetrateThing(hitThing, projectile, equipment))
            {
                return false;
            }

            Vector3 incomingDirection = destination - origin;
            incomingDirection.y = 0f;
            if (incomingDirection.sqrMagnitude <= 0f
                || !BulletLaunchPatch.TryGetImpactGeometry(
                    hitThing,
                    origin,
                    destination,
                    out _,
                    out _,
                    out Vector3 exitPosition))
            {
                return false;
            }

            incomingDirection.Normalize();
            float penetrationChance = EyesOnTheBackstopMod.Settings?.PenetrationChance
                ?? EyesOnTheBackstopSettings.DefaultPenetrationChance;
            IntVec3 cellBehindObstacle = (exitPosition + incomingDirection * 0.51f).ToIntVec3();
            if (BulletLaunchPatch.IsFullObstacleAtCell(hitThing.Map, cellBehindObstacle))
            {
                penetrationChance *= EyesOnTheBackstopMod.Settings?.AdjacentObstaclePenetrationChanceMultiplier
                    ?? EyesOnTheBackstopSettings.DefaultAdjacentObstaclePenetrationChanceMultiplier;
            }

            if (!Rand.Chance(penetrationChance / 100f))
            {
                return false;
            }

            Vector3 continuationStart = exitPosition + incomingDirection * 0.01f;
            float travelledDistance = (continuationStart - origin).MagnitudeHorizontal();
            float remainingDistance = Vector3.Dot(destination - continuationStart, incomingDirection);
            if (remainingDistance <= 0.01f)
            {
                remainingDistance = GetContinuationDistance(projectile, equipment, destination - origin);
            }

            SetContinuationTrajectory(
                projectile,
                continuationStart,
                incomingDirection,
                travelledDistance,
                remainingDistance,
                ref origin,
                ref destination,
                ref ticksToImpact,
                ref lifetime,
                ref landed);
            return true;
        }

        private static bool TryRicochet(
            Projectile projectile,
            Thing hitThing,
            ref Vector3 origin,
            ref Vector3 destination,
            ref int ticksToImpact,
            ref int lifetime,
            ref bool landed)
        {
            Vector3 incomingDirection = destination - origin;
            incomingDirection.y = 0f;
            if (incomingDirection.sqrMagnitude <= 0f
                || !BulletLaunchPatch.TryGetImpactNormal(
                    hitThing,
                    origin,
                    destination,
                    out Vector3 surfaceNormal,
                    out Vector3 impactPosition))
            {
                return false;
            }

            incomingDirection.Normalize();
            float trajectoryAngle = Mathf.Asin(
                Mathf.Clamp01(Mathf.Abs(Vector3.Dot(incomingDirection, surfaceNormal.normalized)))) * Mathf.Rad2Deg;
            float remainingDistance = (destination - impactPosition).MagnitudeHorizontal();
            if (trajectoryAngle >= Utility.MaxRicochetAngle || remainingDistance <= 0f)
            {
                return false;
            }

            Vector3 reflectedDirection = Vector3.Reflect(incomingDirection, surfaceNormal.normalized).normalized;
            if (!IsRicochetExitClear(hitThing, impactPosition, surfaceNormal, reflectedDirection))
            {
                return false;
            }

            Vector3 continuationStart = impactPosition + surfaceNormal * 0.01f;
            float travelledDistance = (continuationStart - origin).MagnitudeHorizontal();
            remainingDistance = (destination - continuationStart).MagnitudeHorizontal();
            SetContinuationTrajectory(
                projectile,
                continuationStart,
                reflectedDirection,
                travelledDistance,
                remainingDistance,
                ref origin,
                ref destination,
                ref ticksToImpact,
                ref lifetime,
                ref landed);
            return true;
        }

        private static void SetContinuationTrajectory(
            Projectile projectile,
            Vector3 continuationStart,
            Vector3 direction,
            float travelledDistance,
            float remainingDistance,
            ref Vector3 origin,
            ref Vector3 destination,
            ref int ticksToImpact,
            ref int lifetime,
            ref bool landed)
        {
            direction.Normalize();
            float speed = Mathf.Max(0.01f, projectile.def.projectile.SpeedTilesPerTick);
            ticksToImpact = Mathf.Max(1, Mathf.CeilToInt(remainingDistance / speed));
            remainingDistance = ticksToImpact * speed;
            origin = continuationStart - direction * travelledDistance;
            destination = continuationStart + direction * remainingDistance;
            lifetime = ticksToImpact;
            landed = false;
            projectile.usedTarget = new LocalTargetInfo(destination.ToIntVec3());
        }

        private static float GetContinuationDistance(
            Projectile projectile,
            Thing equipment,
            Vector3 originalTrajectory)
        {
            float weaponRange = BulletLaunchPatch.FindWeaponRange(
                projectile.def,
                projectile.Launcher,
                equipment);
            if (weaponRange > 0f)
            {
                float minRangeMultiplier = EyesOnTheBackstopMod.Settings?.MinRangeMultiplier
                    ?? EyesOnTheBackstopSettings.DefaultMinRangeMultiplier;
                float maxRangeMultiplier = EyesOnTheBackstopMod.Settings?.MaxRangeMultiplier
                    ?? EyesOnTheBackstopSettings.DefaultMaxRangeMultiplier;
                if (minRangeMultiplier > maxRangeMultiplier)
                {
                    float temp = minRangeMultiplier;
                    minRangeMultiplier = maxRangeMultiplier;
                    maxRangeMultiplier = temp;
                }

                return Mathf.Max(
                    1f,
                    weaponRange
                    * Rand.Range(minRangeMultiplier, maxRangeMultiplier)
                    * (EyesOnTheBackstopMod.Settings?.PenetrationContinuationDistanceMultiplier
                        ?? EyesOnTheBackstopSettings.DefaultPenetrationContinuationDistanceMultiplier));
            }

            return Mathf.Max(1f, originalTrajectory.MagnitudeHorizontal());
        }

        private static bool IsRicochetExitClear(
            Thing hitThing,
            Vector3 impactPosition,
            Vector3 surfaceNormal,
            Vector3 reflectedDirection)
        {
            Map map = hitThing.Map;
            if (map == null)
            {
                return false;
            }

            IntVec3 exitCell = (impactPosition + surfaceNormal * 0.51f + reflectedDirection * 0.2f).ToIntVec3();
            if (!exitCell.InBounds(map))
            {
                return false;
            }

            List<Thing> things = exitCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null
                    && thing != hitThing
                    && thing.def != null
                    && thing.def.Fillage == FillCategory.Full
                    && (!(thing is Building_Door door) || !door.Open))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
