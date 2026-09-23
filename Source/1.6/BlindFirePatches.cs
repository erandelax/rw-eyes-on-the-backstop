using System;
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
            if (!Utility.IsBlindFireEnabled || !Utility.IsBlindFireJob(__instance))
            {
                return;
            }

            Utility.TryGetBlindFireTarget(__instance, targ, out targ);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
    public static class BlindFireShootLinePatch
    {
        public static bool Prefix(
            Verb __instance,
            LocalTargetInfo targ,
            ref ShootLine resultingLine,
            ref bool __result)
        {
            Pawn casterPawn = __instance.CasterPawn;
            if (Utility.IsBlindFireEnabled
                && __instance is Verb_LaunchProjectile
                && casterPawn != null
                && Utility.IsBlindFireJob(casterPawn)
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
        private const int MissingBlindFireDefErrorKey = 827364105;

        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!CanUseBlindFireCommand(__instance))
            {
                return;
            }

            BlindFireGizmoDef gizmoDef = DefDatabase<BlindFireGizmoDef>.GetNamedSilentFail("EyesOnTheBackstop_BlindFire");
            if (gizmoDef == null)
            {
                Log.ErrorOnce(
                    "Eyes On The Backstop could not find the BlindFireGizmoDef 'EyesOnTheBackstop_BlindFire'; the Blind Fire gizmo will be unavailable.",
                    MissingBlindFireDefErrorKey);
                return;
            }

            __result = (__result ?? Enumerable.Empty<Gizmo>()).Concat(new[] { CreateBlindFireCommand(__instance, gizmoDef) });
        }

        private static bool CanUseBlindFireCommand(Pawn pawn)
        {
            return Utility.CanUseBlindFire(pawn);
        }

        private static Command_Target CreateBlindFireCommand(Pawn pawn, BlindFireGizmoDef gizmoDef)
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
                    Action attackAction = GetBlindFireAttackAction(selectedPawn, target, out string targetFailReason);
                    if (attackAction != null)
                    {
                        attackAction();
                    }
                    else if (!targetFailReason.NullOrEmpty())
                    {
                        Messages.Message(targetFailReason, target.Thing, MessageTypeDefOf.RejectInput, historical: false);
                    }
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

        private static Action GetBlindFireAttackAction(Pawn pawn, LocalTargetInfo target, out string failReason)
        {
            failReason = "";
            return delegate
            {
                pawn.jobs.TryTakeOrderedJob(Utility.MakeBlindFireJob(pawn, target), JobTag.Misc);
            };
        }

        private static IEnumerable<Pawn> SelectedControllableDraftedPawns()
        {
            return Find.Selector.SelectedObjects
                .Where(obj => obj is Pawn)
                .Cast<Pawn>()
                .Where(pawn => CanUseBlindFireCommand(pawn) && pawn.Drafted);
        }
    }
}
