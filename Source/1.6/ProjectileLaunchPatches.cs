using System;
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

            if (usedTarget.HasThing
                || BulletTrajectoryUtility.IsFullObstacleAtCell(__instance.Map, usedTarget.Cell))
            {
                return;
            }

            Vector3 targetPosition = usedTarget.Cell.ToVector3Shifted();
            Vector3 direction = targetPosition - origin;
            direction.y = 0f;

            float weaponRange = BulletTrajectoryUtility.FindWeaponRange(__instance.def, launcher, equipment);
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

    }

}
