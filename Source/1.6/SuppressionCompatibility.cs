using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    public static class SuppressionCompatibility
    {
        private const string SuppressionPackageId = "Mlie.Suppression";
        private const string SuppressionUtilTypeName = "SuppressionMod.SuppressionUtil";
        private const string SuppressionModTypeName = "SuppressionMod.SuppressionMod";
        private const string SuppressionImpactPatchTypeName = "SuppressionMod.HarmonyPatches.Bullet_Impact";
        private const string SuppressedDefName = "Suppressed";
        public const string SuppressionModWorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=2559826227";
        private const int MinDistanceFromLauncherSquared = 25;

        private static MethodInfo calcImpactSeverityMethod;
        private static MethodInfo suppressionImpactPrefixMethod;
        private static FieldInfo suppressionModInstanceField;
        private static FieldInfo suppressionSettingsField;
        private static FieldInfo onlyRangedPawnsField;
        private static FieldInfo moodAffectsChanceField;
        private static FieldInfo projectileOriginField;
        private static HediffDef suppressedDef;
        private static GeneDef unstoppableGene;

        public static bool PatchAvailable { get; private set; }

        public static bool SuppressionModActive => ModLister.GetActiveModWithIdentifier(SuppressionPackageId) != null;

        private static bool UserEnabled => EyesOnTheBackstopMod.Settings?.enableSuppressionCompatibility ?? true;

        private static bool IsEnabled => PatchAvailable && UserEnabled;

        public static void Initialize()
        {
            PatchAvailable = false;

            if (!SuppressionModActive)
            {
                return;
            }

            try
            {
                if (!TryLoadSuppressionApi())
                {
                    ForceDisable("Eyes On The Backstop found Suppression, but its required compatibility API or Suppressed hediff is unavailable. Suppression compatibility is disabled.");
                    return;
                }

                PatchAvailable = true;
            }
            catch (Exception exception)
            {
                ForceDisable("Eyes On The Backstop could not initialize Suppression compatibility. Suppression compatibility is disabled. " + exception.Message);
            }
        }

        private static bool TryLoadSuppressionApi()
        {
            Type suppressionUtilType = AccessTools.TypeByName(SuppressionUtilTypeName);
            calcImpactSeverityMethod = suppressionUtilType?.GetMethod(
                "CalcImpactSeverity",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(float) },
                null);
            Type suppressionImpactPatchType = AccessTools.TypeByName(SuppressionImpactPatchTypeName);
            suppressionImpactPrefixMethod = suppressionImpactPatchType?.GetMethod(
                "Prefix",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Bullet), typeof(Thing) },
                null);
            suppressedDef = DefDatabase<HediffDef>.GetNamedSilentFail(SuppressedDefName);

            Type suppressionModType = AccessTools.TypeByName(SuppressionModTypeName);
            suppressionModInstanceField = suppressionModType?.GetField(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            suppressionSettingsField = suppressionModType?.GetField(
                "Settings",
                BindingFlags.Public | BindingFlags.Instance);
            onlyRangedPawnsField = suppressionSettingsField?.FieldType.GetField(
                "OnlyRangedPawns",
                BindingFlags.Public | BindingFlags.Instance);
            moodAffectsChanceField = suppressionSettingsField?.FieldType.GetField(
                "MoodAffectsChance",
                BindingFlags.Public | BindingFlags.Instance);
            projectileOriginField = AccessTools.Field(typeof(Projectile), "origin");
            unstoppableGene = DefDatabase<GeneDef>.GetNamedSilentFail("Unstoppable");

            return calcImpactSeverityMethod != null
                && suppressionImpactPrefixMethod != null
                && suppressionModInstanceField != null
                && suppressionSettingsField != null
                && onlyRangedPawnsField != null
                && moodAffectsChanceField != null
                && projectileOriginField != null
                && suppressedDef != null
                && suppressedDef.hediffClass != null
                && suppressedDef.hediffClass.FullName == "RimWorld.Hediff_Suppressed";
        }

        public static void ApplyAlongTrajectory(Bullet bullet, Thing launcher, Vector3 origin)
        {
            if (!IsEnabled
                || bullet == null
                || launcher == null
                || bullet.Map == null)
            {
                return;
            }

            ApplyAlongTrajectory(
                bullet,
                launcher,
                origin,
                bullet.Position.ToVector3Shifted());
        }

        public static void ApplyAlongTrajectoryToMapBoundary(Bullet bullet)
        {
            if (!IsEnabled
                || bullet == null
                || bullet.Launcher == null
                || bullet.Map == null)
            {
                return;
            }

            try
            {
                Vector3 impactPosition = bullet.ExactPosition;
                if (impactPosition.InBounds(bullet.Map)
                    || !(projectileOriginField.GetValue(bullet) is Vector3 origin)
                    || !TryGetMapBoundaryPoint(bullet.Map, origin, impactPosition, out Vector3 boundaryPosition))
                {
                    return;
                }

                ApplyAlongTrajectory(bullet, bullet.Launcher, origin, boundaryPosition);
            }
            catch (Exception exception)
            {
                ForceDisable("Eyes On The Backstop disabled Suppression compatibility after a runtime compatibility failure. " + exception.Message);
            }
        }

        private static void ApplyAlongTrajectory(
            Bullet bullet,
            Thing launcher,
            Vector3 origin,
            Vector3 impactPosition)
        {
            if (!IsEnabled
                || bullet == null
                || launcher == null
                || bullet.Map == null)
            {
                return;
            }

            try
            {
                origin.y = 0f;
                impactPosition.y = 0f;

                Vector3 trajectory = impactPosition - origin;
                float trajectoryLength = trajectory.magnitude;
                if (trajectoryLength <= 0f)
                {
                    return;
                }

                Vector3 direction = trajectory / trajectoryLength;
                float impactSeverity = CalculateImpactSeverity(bullet, launcher);
                if (impactSeverity <= 0f)
                {
                    return;
                }

                bool onlyRangedPawns = ReadSuppressionSetting(onlyRangedPawnsField);
                bool moodAffectsChance = ReadSuppressionSetting(moodAffectsChanceField);
                bool allowSameFaction = EyesOnTheBackstopMod.Settings?.enableSameFactionSuppression ?? true;
                IntVec3 launcherPosition = launcher.PositionHeld;
                float maxDistance = EyesOnTheBackstopMod.Settings?.SuppressionRadius
                    ?? EyesOnTheBackstopSettings.DefaultSuppressionRadius;
                float maxDistanceSquared = maxDistance * maxDistance;
                float minX = Mathf.Min(origin.x, impactPosition.x) - maxDistance;
                float maxX = Mathf.Max(origin.x, impactPosition.x) + maxDistance;
                float minZ = Mathf.Min(origin.z, impactPosition.z) - maxDistance;
                float maxZ = Mathf.Max(origin.z, impactPosition.z) + maxDistance;

                foreach (Pawn pawn in bullet.Map.mapPawns.AllPawnsSpawned)
                {
                    IntVec3 pawnCell = pawn.PositionHeld;
                    Vector3 pawnPosition = pawnCell.ToVector3Shifted();
                    if (pawnPosition.x < minX
                        || pawnPosition.x > maxX
                        || pawnPosition.z < minZ
                        || pawnPosition.z > maxZ)
                    {
                        continue;
                    }

                    float distanceSquared = DistanceToSegmentSquared(pawnPosition, origin, impactPosition);
                    if (distanceSquared > maxDistanceSquared
                        || !IsValidSuppressionTarget(
                            pawn,
                            launcher,
                            pawnCell,
                            launcherPosition,
                            onlyRangedPawns,
                            moodAffectsChance,
                            allowSameFaction))
                    {
                        continue;
                    }

                    float distanceFactor = Mathf.Max(0f, 1f - distanceSquared / maxDistanceSquared);
                    Hediff suppressed = HediffMaker.MakeHediff(suppressedDef, pawn, null);
                    suppressed.Severity = impactSeverity * distanceFactor;
                    pawn.health.AddHediff(suppressed, null, null, null);
                }
            }
            catch (Exception exception)
            {
                ForceDisable("Eyes On The Backstop disabled Suppression compatibility after a runtime compatibility failure. " + exception.Message);
            }
        }

        private static bool TryGetMapBoundaryPoint(
            Map map,
            Vector3 origin,
            Vector3 endpoint,
            out Vector3 boundaryPoint)
        {
            boundaryPoint = Vector3.zero;
            origin.y = 0f;
            endpoint.y = 0f;

            if (map == null
                || !origin.InBounds(map)
                || endpoint.InBounds(map))
            {
                return false;
            }

            Vector3 direction = endpoint - origin;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            float boundaryFraction = 1f;
            if (direction.x > 0f)
            {
                boundaryFraction = Mathf.Min(boundaryFraction, (map.Size.x - origin.x) / direction.x);
            }
            else if (direction.x < 0f)
            {
                boundaryFraction = Mathf.Min(boundaryFraction, -origin.x / direction.x);
            }

            if (direction.z > 0f)
            {
                boundaryFraction = Mathf.Min(boundaryFraction, (map.Size.z - origin.z) / direction.z);
            }
            else if (direction.z < 0f)
            {
                boundaryFraction = Mathf.Min(boundaryFraction, -origin.z / direction.z);
            }

            if (boundaryFraction <= 0f)
            {
                return false;
            }

            boundaryPoint = origin + direction * boundaryFraction;
            boundaryPoint.y = 0f;
            return true;
        }

        private static float CalculateImpactSeverity(Bullet bullet, Thing launcher)
        {
            try
            {
                int damage = bullet.def.projectile.GetDamageAmount(1f, launcher, null);
                object result = calcImpactSeverityMethod.Invoke(null, new object[] { (float)damage });
                return Convert.ToSingle(result);
            }
            catch (Exception exception)
            {
                ForceDisable("Eyes On The Backstop failed to calculate Suppression severity. Suppression compatibility is disabled. " + exception.Message);
                return 0f;
            }
        }

        private static void ForceDisable(string message)
        {
            PatchAvailable = false;
            if (EyesOnTheBackstopMod.Settings != null)
            {
                EyesOnTheBackstopMod.Settings.enableSuppressionCompatibility = false;
            }

            Log.Warning(message);
        }

        private static bool IsValidSuppressionTarget(
            Pawn pawn,
            Thing launcher,
            IntVec3 pawnPosition,
            IntVec3 launcherPosition,
            bool onlyRangedPawns,
            bool moodAffectsChance,
            bool allowSameFaction)
        {
            if (!IsEligibleSuppressionTarget(
                    pawn,
                    launcher,
                    pawnPosition,
                    launcherPosition,
                    onlyRangedPawns,
                    allowSameFaction))
            {
                return false;
            }

            return !ShouldAvoidSuppressionDueToMood(pawn, moodAffectsChance);
        }

        private static bool IsEligibleSuppressionTarget(
            Pawn pawn,
            Thing launcher,
            IntVec3 pawnPosition,
            IntVec3 launcherPosition,
            bool onlyRangedPawns,
            bool allowSameFaction)
        {
            if (pawn == null
                || pawn == launcher
                || pawn.IsSubhuman
                || pawn.IsShambler
                || pawn.RaceProps == null
                || !pawn.RaceProps.Humanlike
                || !pawn.RaceProps.IsFlesh
                || (!allowSameFaction && pawn.Faction == launcher.Faction)
                || IntVec3Utility.DistanceToSquared(pawnPosition, launcherPosition) < MinDistanceFromLauncherSquared)
            {
                return false;
            }

            if (onlyRangedPawns
                && (pawn.equipment?.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon))
            {
                return false;
            }

            if (ModLister.BiotechInstalled
                && unstoppableGene != null
                && pawn.genes != null
                && pawn.genes.HasActiveGene(unstoppableGene))
            {
                return false;
            }

            return true;
        }

        private static bool ShouldAvoidSuppressionDueToMood(Pawn pawn, bool moodAffectsChance)
        {
            return moodAffectsChance
                && pawn.needs?.mood != null
                && Rand.Value < pawn.needs.mood.CurLevelPercentage - 0.1f;
        }

        private static bool ReadSuppressionSetting(FieldInfo settingField)
        {
            object suppressionMod = suppressionModInstanceField.GetValue(null);
            object settings = suppressionSettingsField.GetValue(suppressionMod);
            return (bool)settingField.GetValue(settings);
        }

        private static float DistanceToSegmentSquared(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;
            if (segmentLengthSquared <= 0f)
            {
                return (point - start).sqrMagnitude;
            }

            float projection = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSquared);
            Vector3 nearestPoint = start + segment * projection;
            return (point - nearestPoint).sqrMagnitude;
        }
    }
}
