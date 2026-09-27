using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EyesOnTheBackstop
{
    public class EyesOnTheBackstopMod : Mod
    {
        private const float ResetButtonHeight = 30f;
        private const float TabHeaderTopPadding = 32f;
        private const float OvershootingContentHeight = 220f;
        private const float PenetrationContentHeight = 300f;
        private const float RicochetContentHeight = 140f;
        private const float SuppressionContentHeight = 220f;

        private enum SettingsTab
        {
            Overshooting,
            Penetration,
            Ricochet,
            Suppression
        }

        public static EyesOnTheBackstopSettings Settings { get; private set; }

        private string penetrableEffectiveHitPointsEditBuffer = "";
        private Vector2 settingsScrollPosition;
        private SettingsTab currentSettingsTab;

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

            Rect tabsRect = new Rect(
                inRect.x,
                inRect.y + TabHeaderTopPadding,
                inRect.width,
                TabDrawer.TabHeight);
            TabDrawer.DrawTabs(tabsRect, CreateSettingsTabs());

            Rect contentViewport = new Rect(
                inRect.x,
                inRect.y + TabHeaderTopPadding + TabDrawer.TabHeight,
                inRect.width,
                inRect.height - TabHeaderTopPadding - TabDrawer.TabHeight - ResetButtonHeight);
            Rect contentRect = new Rect(
                0f,
                0f,
                contentViewport.width - 24f,
                Mathf.Max(contentViewport.height, GetCurrentTabContentHeight()));
            Widgets.BeginScrollView(contentViewport, ref settingsScrollPosition, contentRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);
            switch (currentSettingsTab)
            {
                case SettingsTab.Overshooting:
                    DrawOvershootingSettings(listing);
                    break;
                case SettingsTab.Penetration:
                    DrawPenetrationSettings(listing);
                    break;
                case SettingsTab.Ricochet:
                    DrawRicochetSettings(listing);
                    break;
                case SettingsTab.Suppression:
                    DrawSuppressionSettings(listing);
                    break;
            }

            Settings.NormalizeSettings();
            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(
                inRect.x,
                inRect.y + inRect.height - ResetButtonHeight,
                inRect.width,
                ResetButtonHeight);
            Listing_Standard resetListing = new Listing_Standard();
            resetListing.Begin(resetRect);
            if (resetListing.ButtonText("EyesOnTheBackstop_RestoreDefaults".Translate()))
            {
                Settings.RestoreDefaults();
                if (SuppressionCompatibility.SuppressionModActive)
                {
                    Settings.RestoreSuppressionCompatibilityDefaults();
                }

                penetrableEffectiveHitPointsEditBuffer = Settings.PenetrableEffectiveHitPoints.ToString("D");
            }

            resetListing.End();
        }

        private List<TabRecord> CreateSettingsTabs()
        {
            return new List<TabRecord>
            {
                new TabRecord(
                    "EyesOnTheBackstop_OvershootingSection".Translate(),
                    () => SelectSettingsTab(SettingsTab.Overshooting),
                    () => currentSettingsTab == SettingsTab.Overshooting),
                new TabRecord(
                    "EyesOnTheBackstop_PenetrationSection".Translate(),
                    () => SelectSettingsTab(SettingsTab.Penetration),
                    () => currentSettingsTab == SettingsTab.Penetration),
                new TabRecord(
                    "EyesOnTheBackstop_RicochetSection".Translate(),
                    () => SelectSettingsTab(SettingsTab.Ricochet),
                    () => currentSettingsTab == SettingsTab.Ricochet),
                new TabRecord(
                    "EyesOnTheBackstop_SuppressionSection".Translate(),
                    () => SelectSettingsTab(SettingsTab.Suppression),
                    () => currentSettingsTab == SettingsTab.Suppression)
            };
        }

        private void SelectSettingsTab(SettingsTab tab)
        {
            currentSettingsTab = tab;
            settingsScrollPosition = Vector2.zero;
        }

        private float GetCurrentTabContentHeight()
        {
            switch (currentSettingsTab)
            {
                case SettingsTab.Overshooting:
                    return OvershootingContentHeight;
                case SettingsTab.Penetration:
                    return PenetrationContentHeight;
                case SettingsTab.Ricochet:
                    return RicochetContentHeight;
                case SettingsTab.Suppression:
                    return SuppressionContentHeight;
                default:
                    return OvershootingContentHeight;
            }
        }

        private static void DrawOvershootingSettings(Listing_Standard listing)
        {
            listing.Label("EyesOnTheBackstop_OvershootingSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableExtendedBulletTrajectories".Translate(),
                ref Settings.enableExtendedBulletTrajectories,
                "EyesOnTheBackstop_EnableExtendedBulletTrajectoriesDesc".Translate());
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableIndirectFireRaidReaction".Translate(),
                ref Settings.enableIndirectFireRaidReaction,
                "EyesOnTheBackstop_EnableIndirectFireRaidReactionDesc".Translate());
            Settings.minRangeMultiplier = listing.SliderLabeled(
                "EyesOnTheBackstop_MinRangeMultiplierLabel".Translate(Settings.MinRangeMultiplier.ToString("0.##")),
                Settings.MinRangeMultiplier,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMin,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMax);
            Settings.maxRangeMultiplier = listing.SliderLabeled(
                "EyesOnTheBackstop_MaxRangeMultiplierLabel".Translate(Settings.MaxRangeMultiplier.ToString("0.##")),
                Settings.MaxRangeMultiplier,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMin,
                EyesOnTheBackstopSettings.RangeMultiplierSliderMax);
        }

        private void DrawPenetrationSettings(Listing_Standard listing)
        {
            listing.Label("EyesOnTheBackstop_PenetrationSection".Translate());
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
            Settings.penetrationChance = listing.SliderLabeled(
                "EyesOnTheBackstop_PenetrationChanceLabel".Translate(Settings.PenetrationChance.ToString("0")),
                Settings.PenetrationChance,
                0f,
                100f,
                tooltip: "EyesOnTheBackstop_PenetrationChanceDesc".Translate());
            Settings.adjacentObstaclePenetrationChanceMultiplier = listing.SliderLabeled(
                "EyesOnTheBackstop_AdjacentObstaclePenetrationChanceMultiplierLabel".Translate(
                    (Settings.AdjacentObstaclePenetrationChanceMultiplier * 100f).ToString("0")),
                Settings.AdjacentObstaclePenetrationChanceMultiplier,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMin,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMax,
                tooltip: "EyesOnTheBackstop_AdjacentObstaclePenetrationChanceMultiplierDesc".Translate());
            Settings.penetrationContinuationDistanceMultiplier = listing.SliderLabeled(
                "EyesOnTheBackstop_PenetrationContinuationDistanceMultiplierLabel".Translate(
                    (Settings.PenetrationContinuationDistanceMultiplier * 100f).ToString("0")),
                Settings.PenetrationContinuationDistanceMultiplier,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMin,
                EyesOnTheBackstopSettings.PenetrationMultiplierSliderMax,
                tooltip: "EyesOnTheBackstop_PenetrationContinuationDistanceMultiplierDesc".Translate());
        }

        private static void DrawRicochetSettings(Listing_Standard listing)
        {
            listing.Label("EyesOnTheBackstop_RicochetSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableRicochets".Translate(),
                ref Settings.enableRicochets,
                "EyesOnTheBackstop_EnableRicochetsDesc".Translate());
            Settings.maxRicochetAngle = Mathf.RoundToInt(listing.SliderLabeled(
                "EyesOnTheBackstop_MaxRicochetAngleLabel".Translate(Settings.MaxRicochetAngle.ToString("D")),
                Settings.MaxRicochetAngle,
                EyesOnTheBackstopSettings.RicochetAngleSliderMin,
                EyesOnTheBackstopSettings.RicochetAngleSliderMax));
        }

        private static void DrawSuppressionSettings(Listing_Standard listing)
        {
            listing.Label("EyesOnTheBackstop_SuppressionSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableSuppressionFire".Translate(),
                ref Settings.enableSuppressionFire,
                "EyesOnTheBackstop_EnableSuppressionFireDesc".Translate());

            if (!SuppressionCompatibility.SuppressionModActive)
            {
                if (listing.ButtonText("EyesOnTheBackstop_SuppressionModNotInstalled".Translate()))
                {
                    SteamUtility.OpenUrl(SuppressionCompatibility.SuppressionModWorkshopUrl);
                }

                return;
            }

            if (!SuppressionCompatibility.PatchAvailable)
            {
                Settings.enableSuppressionCompatibility = false;
            }

            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableSuppressionCompatibility".Translate(),
                ref Settings.enableSuppressionCompatibility,
                "EyesOnTheBackstop_EnableSuppressionCompatibilityDesc".Translate());
            Settings.suppressionRadius = Mathf.RoundToInt(listing.SliderLabeled(
                "EyesOnTheBackstop_SuppressionRadiusLabel".Translate(Settings.SuppressionRadius.ToString("0")),
                Settings.SuppressionRadius,
                EyesOnTheBackstopSettings.SuppressionRadiusSliderMin,
                EyesOnTheBackstopSettings.SuppressionRadiusSliderMax,
                tooltip: "EyesOnTheBackstop_SuppressionRadiusDesc".Translate()));
            listing.CheckboxLabeled(
                "EyesOnTheBackstop_EnableSameFactionSuppression".Translate(),
                ref Settings.enableSameFactionSuppression,
                "EyesOnTheBackstop_EnableSameFactionSuppressionDesc".Translate());
        }
    }
}
