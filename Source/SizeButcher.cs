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

                    /* 0x001C00CC 6F453B0006  IL_0110: callvirt instance class Verse.RecipeDef Verse.AI.Job::get_RecipeDef()
                    /* 0x001C00D1 7BCA2B0004  IL_0115: ldfld class RimWorld.StatDef Verse.RecipeDef::workSpeedStat
                    /* 0x001C00D6 17          IL_011A: ldc.i4.1
                    /* 0x001C00D7 2892380006  IL_011B: call float32 RimWorld.StatExtension::GetStatValue(class Verse.Thing, class RimWorld.StatDef, bool)
                     */

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
                                /* 0x001BFFE7 6F463B0006   IL_002B: callvirt instance valuetype Verse.LocalTargetInfo Verse.AI.Job::GetTarget(valuetype Verse.AI.TargetIndex)
                                /* 0x001BFFEC 1304         IL_0030: stloc.s V_4
                                /* 0x001BFFEE 1204         IL_0032: ldloca.s V_4
                                /* 0x001BFFF0 281E5E0006   IL_0034: call instance class Verse.Thing Verse.LocalTargetInfo::get_Thing()
                                 */
                                
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
