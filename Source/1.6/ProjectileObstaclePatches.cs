using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    [HarmonyPatch(typeof(Bullet), "Impact")]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(bool) })]
    public static class BulletImpactPatch
    {
        public static void Prefix(Bullet __instance, Thing hitThing, Vector3 ___origin)
        {
            if (!Utility.IsSupportedBulletProjectile(__instance))
            {
                return;
            }

            SuppressionCompatibility.ApplyAlongTrajectory(__instance, __instance.Launcher, ___origin);

            if (Utility.IsIndirectFireRaidReactionEnabled
                && hitThing is Pawn victim
                && __instance.Launcher is Pawn shooter)
            {
                Utility.TryWakeStagedRaidFromIndirectFire(shooter, victim, __instance.def, __instance.EquipmentDef);
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    [HarmonyPatch(new Type[] { typeof(DestroyMode) })]
    public static class BulletDestroyPatch
    {
        public static void Prefix(Thing __instance)
        {
            if (__instance is Bullet bullet && Utility.IsSupportedBulletProjectile(bullet))
            {
                SuppressionCompatibility.ApplyAlongTrajectoryToMapBoundary(bullet);
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
                || !BulletTrajectoryUtility.TryGetImpactGeometry(
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
            if (BulletTrajectoryUtility.IsFullObstacleAtCell(hitThing.Map, cellBehindObstacle))
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
                || !BulletTrajectoryUtility.TryGetImpactNormal(
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
            float weaponRange = BulletTrajectoryUtility.FindWeaponRange(
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
