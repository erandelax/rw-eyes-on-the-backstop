using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace EyesOnTheBackstop
{
    [HarmonyPatch(typeof(LordJob_StageThenAttack), nameof(LordJob_StageThenAttack.CreateGraph))]
    public static class StagedRaidIndirectFireGraphPatch
    {
        public static void Postfix(StateGraph __result)
        {
            if (__result?.transitions == null)
            {
                return;
            }

            Transition assaultTransition = __result.transitions.FirstOrDefault(transition =>
                transition != null
                && transition.sources != null
                && transition.sources.Any(source => source is LordToil_Stage)
                && transition.target is LordToil_AssaultColony);

            assaultTransition?.AddTrigger(new Trigger_Memo(Utility.StagedRaidIndirectFireMemo));
        }
    }
}
