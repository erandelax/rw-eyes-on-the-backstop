using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    public class EyesOnTheBackstopMod : Mod
    {
        public static EyesOnTheBackstopSettings Settings { get; private set; }

        private string penetrableEffectiveHitPointsEditBuffer = "";

        public EyesOnTheBackstopMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<EyesOnTheBackstopSettings>();
        }

        public override string SettingsCategory()
        {
            return "EyesOnTheBackstop_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.NormalizeSettings();
            if (penetrableEffectiveHitPointsEditBuffer.NullOrEmpty())
            {
                penetrableEffectiveHitPointsEditBuffer = Settings.PenetrableEffectiveHitPoints.ToString("D");
            }

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableBlindFire".Translate(),
                ref Settings.enableBlindFire,
                "EyesOnTheBackstop_EnableBlindFireDesc".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableExtendedBulletTrajectories".Translate(),
                ref Settings.enableExtendedBulletTrajectories,
                "EyesOnTheBackstop_EnableExtendedBulletTrajectoriesDesc".Translate());
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableIndirectFireRaidReaction".Translate(),
                ref Settings.enableIndirectFireRaidReaction,
                "EyesOnTheBackstop_EnableIndirectFireRaidReactionDesc".Translate());
            listing.Label("EyesOnTheBackstop_MinRangeMultiplierLabel".Translate(Settings.MinRangeMultiplier.ToString("0.##")));
            Settings.minRangeMultiplier = listing.Slider(
                Settings.MinRangeMultiplier,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMin,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMax);
            listing.Label("EyesOnTheBackstop_MaxRangeMultiplierLabel".Translate(Settings.MaxRangeMultiplier.ToString("0.##")));
            Settings.maxRangeMultiplier = listing.Slider(
                Settings.MaxRangeMultiplier,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMin,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMax);
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableObstaclePenetration".Translate(),
                ref Settings.enableObstaclePenetration,
                "EyesOnTheBackstop_EnableObstaclePenetrationDesc".Translate());
            listing.Label("EyesOnTheBackstop_PenetrableHitPointsThresholdLabel".Translate(Settings.PenetrableEffectiveHitPoints.ToString("D")));
            listing.IntEntry(
                ref Settings.penetrableEffectiveHitPoints,
                ref penetrableEffectiveHitPointsEditBuffer,
                min: 0);
            listing.Label(
                "EyesOnTheBackstop_PenetrationChanceLabel".Translate(Settings.PenetrationChance.ToString("0")),
                -1f,
                "EyesOnTheBackstop_PenetrationChanceDesc".Translate());
            Settings.penetrationChance = listing.Slider(Settings.PenetrationChance, 0f, 100f);
            listing.Label(
                "EyesOnTheBackstop_AdjacentObstaclePenetrationChanceMultiplierLabel".Translate(
                    (Settings.AdjacentObstaclePenetrationChanceMultiplier * 100f).ToString("0")),
                -1f,
                "EyesOnTheBackstop_AdjacentObstaclePenetrationChanceMultiplierDesc".Translate());
            Settings.adjacentObstaclePenetrationChanceMultiplier = listing.Slider(
                Settings.AdjacentObstaclePenetrationChanceMultiplier,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMin,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMax);
            listing.Label(
                "EyesOnTheBackstop_PenetrationContinuationDistanceMultiplierLabel".Translate(
                    (Settings.PenetrationContinuationDistanceMultiplier * 100f).ToString("0")),
                -1f,
                "EyesOnTheBackstop_PenetrationContinuationDistanceMultiplierDesc".Translate());
            Settings.penetrationContinuationDistanceMultiplier = listing.Slider(
                Settings.PenetrationContinuationDistanceMultiplier,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMin,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMax);
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableRicochets".Translate(),
                ref Settings.enableRicochets,
                "EyesOnTheBackstop_EnableRicochetsDesc".Translate());
            listing.Label("EyesOnTheBackstop_MaxRicochetAngleLabel".Translate(Settings.MaxRicochetAngle.ToString("D")));
            Settings.maxRicochetAngle = Mathf.RoundToInt(listing.Slider(
                Settings.MaxRicochetAngle,
                EyesOnTheBackstopSettings.RicochetAngleSliderMin,
                EyesOnTheBackstopSettings.RicochetAngleSliderMax));
            if (listing.ButtonText("EyesOnTheBackstop_RestoreDefaults".Translate()))
            {
                Settings.RestoreDefaults();
                penetrableEffectiveHitPointsEditBuffer = Settings.PenetrableEffectiveHitPoints.ToString("D");
            }

            Settings.NormalizeSettings();
            listing.End();
        }
    }
}
