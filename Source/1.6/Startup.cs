using System.Reflection;
using HarmonyLib;
using Verse;

namespace EyesOnTheBackstop
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("erandelax.eyesonthebackstop");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
