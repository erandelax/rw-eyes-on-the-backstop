using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EyesOnTheBackstop
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class PawnTryStartAttackPatch
    {
        public static void Prefix(Pawn __instance, ref LocalTargetInfo targ)
        {
            if (!Utility.IsSuppressionFireEnabled || !Utility.IsSuppressionFireJob(__instance))
            {
                return;
            }

            Utility.TryGetSuppressionFireTarget(__instance, targ, out targ);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
    public static class SuppressionFireShootLinePatch
    {
        public static bool Prefix(
            Verb __instance,
            LocalTargetInfo targ,
            ref ShootLine resultingLine,
            ref bool __result)
        {
            Pawn casterPawn = __instance.CasterPawn;
            if (Utility.IsSuppressionFireEnabled
                && __instance is Verb_LaunchProjectile
                && casterPawn != null
                && Utility.IsSuppressionFireJob(casterPawn)
                && !targ.HasThing
                && targ.Cell.IsValid)
            {
                resultingLine = new ShootLine(casterPawn.Position, targ.Cell);
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class PawnGetGizmosPatch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!Utility.CanUseSuppressionFire(__instance))
            {
                return;
            }

            SuppressionFireGizmoDef gizmoDef = DefDatabase<SuppressionFireGizmoDef>.GetNamedSilentFail("EyesOnTheBackstop_SuppressionFire");
            if (gizmoDef == null)
            {
                Log.ErrorOnce(
                    "Eyes On The Backstop could not find the SuppressionFireGizmoDef 'EyesOnTheBackstop_SuppressionFire'; the Suppression Fire gizmo will be unavailable.",
                    LogKeys.MissingSuppressionFireDef);
                return;
            }

            __result = (__result ?? Enumerable.Empty<Gizmo>()).Concat(new[] { CreateSuppressionFireCommand(__instance, gizmoDef) });
        }

        private static Command_Target CreateSuppressionFireCommand(Pawn pawn, SuppressionFireGizmoDef gizmoDef)
        {
            Command_Target command = new Command_Target
            {
                defaultLabel = gizmoDef.LabelCap,
                defaultDesc = gizmoDef.description,
                icon = gizmoDef.CommandIcon,
                targetingParams = TargetingParameters.ForThing()
            };
            command.targetingParams.canTargetLocations = true;

            string failReason;
            if (FloatMenuUtility.GetRangedAttackAction(pawn, LocalTargetInfo.Invalid, out failReason) == null)
            {
                command.Disable(failReason.CapitalizeFirst() + ".");
            }

            command.action = delegate (LocalTargetInfo target)
            {
                foreach (Pawn selectedPawn in SelectedControllableDraftedPawns())
                {
                    selectedPawn.jobs.TryTakeOrderedJob(
                        Utility.MakeSuppressionFireJob(selectedPawn, target),
                        JobTag.Misc);
                }
            };
            command.onUpdate = delegate
            {
                foreach (Pawn selectedPawn in SelectedControllableDraftedPawns())
                {
                    Verb primaryVerb = selectedPawn.equipment?.PrimaryEq?.PrimaryVerb;
                    primaryVerb?.verbProps.DrawRadiusRing(selectedPawn.Position, primaryVerb);
                }
            };

            return command;
        }

        private static IEnumerable<Pawn> SelectedControllableDraftedPawns()
        {
            return Find.Selector.SelectedObjects
                .Where(obj => obj is Pawn)
                .Cast<Pawn>()
                .Where(pawn => Utility.CanUseSuppressionFire(pawn) && pawn.Drafted);
        }
    }
}
