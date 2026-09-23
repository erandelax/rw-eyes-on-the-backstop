using System;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    public class EyesOnTheBackstopSettings : ModSettings
    {
        public const float RangeMultiplierSliderMin = 0.5f;
        public const float RangeMultiplierSliderMax = 5f;
        public const float DefaultMinRangeMultiplier = 0.75f;
        public const float DefaultMaxRangeMultiplier = 1.75f;
        public const float PenetrationMultiplierSliderMin = 0.01f;
        public const float PenetrationMultiplierSliderMax = 1f;
        public const float DefaultAdjacentObstaclePenetrationChanceMultiplier = 0.2f;
        public const float DefaultPenetrationContinuationDistanceMultiplier = 0.2f;
        public const int DefaultPenetrableEffectiveHitPoints = 150;
        public const float DefaultPenetrationChance = 66f;
        public const int RicochetAngleSliderMin = 1;
        public const int RicochetAngleSliderMax = 90;
        public const int DefaultMaxRicochetAngle = 30;

        public bool enableBlindFire = false;
        public bool enableExtendedBulletTrajectories = true;
        public bool enableIndirectFireRaidReaction = true;
        public bool enableObstaclePenetration = false;
        public bool enableRicochets = false;
        public int penetrableEffectiveHitPoints = DefaultPenetrableEffectiveHitPoints;
        public float penetrationChance = DefaultPenetrationChance;
        public float minRangeMultiplier = DefaultMinRangeMultiplier;
        public float maxRangeMultiplier = DefaultMaxRangeMultiplier;
        public float adjacentObstaclePenetrationChanceMultiplier = DefaultAdjacentObstaclePenetrationChanceMultiplier;
        public float penetrationContinuationDistanceMultiplier = DefaultPenetrationContinuationDistanceMultiplier;
        public int maxRicochetAngle = DefaultMaxRicochetAngle;

        public float MinRangeMultiplier => Mathf.Clamp(minRangeMultiplier, RangeMultiplierSliderMin, RangeMultiplierSliderMax);

        public float MaxRangeMultiplier => Mathf.Clamp(maxRangeMultiplier, RangeMultiplierSliderMin, RangeMultiplierSliderMax);

        public float AdjacentObstaclePenetrationChanceMultiplier => Mathf.Clamp(
            adjacentObstaclePenetrationChanceMultiplier,
            PenetrationMultiplierSliderMin,
            PenetrationMultiplierSliderMax);

        public float PenetrationContinuationDistanceMultiplier => Mathf.Clamp(
            penetrationContinuationDistanceMultiplier,
            PenetrationMultiplierSliderMin,
            PenetrationMultiplierSliderMax);

        public int PenetrableEffectiveHitPoints => Math.Max(0, penetrableEffectiveHitPoints);

        public float PenetrationChance => Mathf.Clamp(penetrationChance, 0f, 100f);

        public int MaxRicochetAngle => Mathf.Clamp(maxRicochetAngle, RicochetAngleSliderMin, RicochetAngleSliderMax);

        public void RestoreDefaults()
        {
            enableBlindFire = false;
            enableExtendedBulletTrajectories = true;
            enableIndirectFireRaidReaction = true;
            enableObstaclePenetration = false;
            enableRicochets = false;
            penetrableEffectiveHitPoints = DefaultPenetrableEffectiveHitPoints;
            penetrationChance = DefaultPenetrationChance;
            minRangeMultiplier = DefaultMinRangeMultiplier;
            maxRangeMultiplier = DefaultMaxRangeMultiplier;
            adjacentObstaclePenetrationChanceMultiplier = DefaultAdjacentObstaclePenetrationChanceMultiplier;
            penetrationContinuationDistanceMultiplier = DefaultPenetrationContinuationDistanceMultiplier;
            maxRicochetAngle = DefaultMaxRicochetAngle;
        }

        public void NormalizeSettings()
        {
            minRangeMultiplier = Mathf.Clamp(minRangeMultiplier, RangeMultiplierSliderMin, RangeMultiplierSliderMax);
            maxRangeMultiplier = Mathf.Clamp(maxRangeMultiplier, RangeMultiplierSliderMin, RangeMultiplierSliderMax);
            adjacentObstaclePenetrationChanceMultiplier = Mathf.Clamp(
                adjacentObstaclePenetrationChanceMultiplier,
                PenetrationMultiplierSliderMin,
                PenetrationMultiplierSliderMax);
            penetrationContinuationDistanceMultiplier = Mathf.Clamp(
                penetrationContinuationDistanceMultiplier,
                PenetrationMultiplierSliderMin,
                PenetrationMultiplierSliderMax);
            penetrableEffectiveHitPoints = Math.Max(0, penetrableEffectiveHitPoints);
            maxRicochetAngle = Mathf.Clamp(maxRicochetAngle, RicochetAngleSliderMin, RicochetAngleSliderMax);
            if (minRangeMultiplier > maxRangeMultiplier)
            {
                float temp = minRangeMultiplier;
                minRangeMultiplier = maxRangeMultiplier;
                maxRangeMultiplier = temp;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableBlindFire, "enableBlindFire", defaultValue: false);
            Scribe_Values.Look(ref enableExtendedBulletTrajectories, "enableExtendedBulletTrajectories", defaultValue: true);
            Scribe_Values.Look(ref enableIndirectFireRaidReaction, "enableIndirectFireRaidReaction", defaultValue: true);
            Scribe_Values.Look(ref enableObstaclePenetration, "enableObstaclePenetration", defaultValue: false);
            Scribe_Values.Look(ref enableRicochets, "enableRicochets", defaultValue: false);
            Scribe_Values.Look(ref penetrableEffectiveHitPoints, "penetrableEffectiveHitPoints", DefaultPenetrableEffectiveHitPoints);
            Scribe_Values.Look(ref penetrationChance, "penetrationChance", DefaultPenetrationChance);
            Scribe_Values.Look(ref minRangeMultiplier, "minRangeMultiplier", DefaultMinRangeMultiplier);
            Scribe_Values.Look(ref maxRangeMultiplier, "maxRangeMultiplier", DefaultMaxRangeMultiplier);
            Scribe_Values.Look(
                ref adjacentObstaclePenetrationChanceMultiplier,
                "adjacentObstaclePenetrationChanceMultiplier",
                DefaultAdjacentObstaclePenetrationChanceMultiplier);
            Scribe_Values.Look(
                ref penetrationContinuationDistanceMultiplier,
                "penetrationContinuationDistanceMultiplier",
                DefaultPenetrationContinuationDistanceMultiplier);
            Scribe_Values.Look(ref maxRicochetAngle, "maxRicochetAngle", DefaultMaxRicochetAngle);
        }
    }
}
