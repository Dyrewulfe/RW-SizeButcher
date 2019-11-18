using Harmony;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using System.Reflection.Emit;

namespace SizeButcher
{

    public class SizeAffectsWorkSpeed : DefModExtension { }

    public class SizeButcher
    {
        [StaticConstructorOnStartup]
        static class HarmonyPatches
        {

            static HarmonyInstance harmony = HarmonyInstance.Create("rimworld.dyrewulfe.sizebutcher");

            static HarmonyPatches()
            {
                //HarmonyInstance.DEBUG = true;
                harmony.PatchAll();
            }

            [HarmonyPatch(typeof(Toils_Recipe))]
            class Toils_Recipe_DoRecipeWork_Patch
            {
                
                static MethodBase TargetMethod()
                {
                    return AccessTools.Inner(typeof(Verse.AI.Toils_Recipe), "<DoRecipeWork>c__AnonStorey1").GetMethod("<>m__1", AccessTools.all);
                }

                static IEnumerable<CodeInstruction> Transpiler(ILGenerator gen, IEnumerable<CodeInstruction> instructions)
                {
                    List<CodeInstruction> targetPattern = new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(Job), "RecipeDef").GetGetMethod()),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RecipeDef), "workSpeedStat")),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatExtension), "GetStatValue"))
                    };

                    Queue<CodeInstruction> currentPattern = new Queue<CodeInstruction>();

                    foreach (var code in instructions)
                    {
                        yield return code;
                        currentPattern.Enqueue(code);
                        if (currentPattern.Count > targetPattern.Count)
                        {
                            currentPattern.Dequeue();
                            if (ComparePattern(currentPattern.ToList(), targetPattern))
                            {                                
                                yield return new CodeInstruction(OpCodes.Ldloc_1);
                                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Toils_Recipe_DoRecipeWork_Patch), "GetWorkSizeFactor"));
                                yield return new CodeInstruction(OpCodes.Mul);
                            }
                        }
                    }
                }

                static float GetWorkSizeFactor(Job job)
                {
                    float speed = 1f;
                    if (job.RecipeDef.HasModExtension<SizeAffectsWorkSpeed>())
                    {
                        var corpse = job.targetB.Thing as Corpse;
                        float size = UnityEngine.Mathf.Clamp(corpse?.InnerPawn?.BodySize ?? 1f, 0.25f, 4f);
                        speed = 1f / size;
                    }
                    return speed;
                }
            }
            static bool ComparePattern(List<CodeInstruction> curr, List<CodeInstruction> targ)
            {
                for (var i = 0; i < targ.Count; i++)
                {
                    if ((targ[i].opcode != curr[i].opcode) || (targ[i].operand != curr[i].operand))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
