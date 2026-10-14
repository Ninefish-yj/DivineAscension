// ============================================================
// 能量抵伤（v0.18.3 新增）——三系统桥接"战斗实感"C
//
//  参考天人武道 MartialNeiliAbsorbPatch：受伤扣血前，有境界者先消耗
//  修炼资源抵伤（护盾结算之后、真正掉血之前）。
//  本模组修炼资源为「异能能量」（CultivationData.GetEnergy/SetEnergy）。
//
//  规则：
//    防御者无境界（tier<=0）→ 完全放行原版
//    抵伤比例随境界提升：ratio = min(0.8, 0.10 + tier*0.05)
//    能量效率随境界提升：每抵1点伤害耗能 = max(0.5, 1.5 - tier*0.08)
//      低境界 1 点能量换 0.7 伤害，高境界 1 点能量换 2 点伤害
//    能量不足时按剩余能量部分抵伤，绝不透支
// ============================================================

using System.Reflection;
using HarmonyLib;
using Code.Data;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>能量抵伤：changeHealth 前缀，扣血前消耗异能能量吸收部分伤害</summary>
    [HarmonyPatch]
    public static class EnergyAbsorbPatch
    {
        public const float BASE_ABSORB_RATIO = 0.10f;   // 每阶+5%，1阶起
        public const float ABSORB_RATIO_PER_TIER = 0.05f;
        public const float MAX_ABSORB_RATIO = 0.80f;    // 上限 80%
        public const float BASE_ENERGY_COST = 1.5f;     // 每点抵伤基准耗能
        public const float ENERGY_COST_STEP = 0.08f;    // 每阶降 0.08
        public const float MIN_ENERGY_COST = 0.5f;      // 能耗下限（1点能量抵2伤害）

        public static bool Prepare()
        {
            return AccessTools.Method(typeof(Actor), "changeHealth", new[] { typeof(int) }) != null
                || AccessTools.Method(typeof(BaseSimObject), "changeHealth", new[] { typeof(int) }) != null;
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Actor), "changeHealth", new[] { typeof(int) })
                ?? AccessTools.Method(typeof(BaseSimObject), "changeHealth", new[] { typeof(int) });
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(BaseSimObject __instance, ref int pValue)
        {
            try
            {
                // 只拦扣血方向；护盾/无敌等先于本补丁已结算
                if (pValue >= 0) return;
                if (__instance == null || !__instance.isActor()) return;
                Actor actor = __instance.a;
                if (actor == null || !actor.isAlive()) return;
                if (!Code.Core.DSConfigManager.EnableEnergyAbsorb) return;

                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0) return; // 无境界不触发

                float energy = CultivationData.GetEnergy(actor);
                if (energy <= 0f) return;

                float damage = -pValue;
                if (damage <= 0f) return;

                // 抵伤比例与能量效率随境界提升
                float ratio = Mathf.Min(MAX_ABSORB_RATIO, BASE_ABSORB_RATIO + tier * ABSORB_RATIO_PER_TIER);
                float energyPerPoint = Mathf.Max(MIN_ENERGY_COST, BASE_ENERGY_COST - tier * ENERGY_COST_STEP);

                // 想抵的部分 vs 能量能换的部分，取小
                float wantAbsorb = damage * ratio;
                float canAbsorb = energy / energyPerPoint;
                float absorb = Mathf.Min(wantAbsorb, canAbsorb);
                if (absorb <= 0f) return;

                float cost = absorb * energyPerPoint;
                CultivationData.SetEnergy(actor, energy - cost);
                pValue += (int)absorb; // 少扣（pValue 为负，加上正值=减伤）

                Code.Core.DSDebug.Verbose(string.Format("[GeneCultivation] 能量抵伤: {0}({1}阶) 耗能量{2:F0} 抵伤{3:F0}",
                    actor.getName(), tier, cost, absorb));
            }
            catch (System.Exception dsEx)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] EnergyAbsorbPatch 异常: " + dsEx.Message);
            }
        }
    }
}
