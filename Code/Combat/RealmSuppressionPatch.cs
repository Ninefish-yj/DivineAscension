// ============================================================
// 境界压制（即时版，v0.18.3 新增）——三系统桥接"战斗实感"B
//
//  参考天人武道 MartialRealmSuppressionPatches：getHit 当场结算跨境界压制，
//  取代"年度回溯补血"的延迟式实现（玩家感受不到位格鸿沟）。
//
//  规则（数值与 CombatRank.GetDamageReduction 对齐，避免两套口径）：
//    攻击者阶 > 防御者阶：每差1阶伤害放大15%（上限+85%），并绕过原版护甲（真伤）
//    防御者阶 > 攻击者阶：每差1阶伤害衰减15%（上限85%）
//    只有双方都有境界（tier>0）才结算；至少一方无境界则完全放行原版逻辑
// ============================================================

using System.Reflection;
using HarmonyLib;
using Code.Data;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>即时境界压制：getHit 前缀当场改写伤害</summary>
    [HarmonyPatch]
    public static class RealmSuppressionPatch
    {
        public const float REDUCTION_PER_TIER = 0.15f;   // 每差1阶 15%
        public const float MAX_SUPPRESSION = 0.85f;      // 上限 85%

        public static bool Prepare()
        {
            return ResolveGetHit7() != null || ResolveGetHit5() != null;
        }

        public static MethodBase TargetMethod()
        {
            return ResolveGetHit7() ?? ResolveGetHit5();
        }

        // 与天人武道同款签名解析：优先 7 参 getHit（0.51.x），回退 5 参
        private static MethodBase ResolveGetHit7()
        {
            return AccessTools.Method(typeof(Actor), "getHit", new[]
            {
                typeof(float), typeof(bool), typeof(AttackType), typeof(BaseSimObject),
                typeof(bool), typeof(bool), typeof(bool)
            });
        }

        private static MethodBase ResolveGetHit5()
        {
            return AccessTools.Method(typeof(Actor), "getHit", new[]
            {
                typeof(float), typeof(bool), typeof(AttackType), typeof(BaseSimObject), typeof(bool)
            });
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal)]
        public static bool Prefix(Actor __instance, ref float pDamage, BaseSimObject pAttacker, ref bool pCheckDamageReduction)
        {
            try
            {
                if (__instance == null || pDamage <= 0f) return true;
                if (!Code.Core.DSConfigManager.EnableRealmSuppression) return true;

                Actor defender = __instance;
                Actor attacker = pAttacker != null && pAttacker.isActor() ? pAttacker.a : null;
                if (attacker == null) return true;

                int defTier = CultivationData.GetRealmTier(defender);
                int atkTier = CultivationData.GetRealmTier(attacker);
                // 双方都有境界才触发位格结算；凡俗攻击/被攻击完全放行原版
                if (defTier <= 0 || atkTier <= 0) return true;

                int diff = atkTier - defTier;
                if (diff > 0)
                {
                    // 高境打低境：真伤放大 + 绕过原版护甲
                    float boost = Mathf.Min(MAX_SUPPRESSION, diff * REDUCTION_PER_TIER);
                    pDamage *= (1f + boost);
                    pCheckDamageReduction = false; // 真伤：跳过凡俗护甲减免
                    Code.Core.DSDebug.Verbose(string.Format("[GeneCultivation] 境界压制: {0}({1}阶) → {2}({3}阶)，高境真伤 +{4:F0}%",
                        attacker.getName(), atkTier, defender.getName(), defTier, boost * 100f));
                }
                else if (diff < 0)
                {
                    // 低境打高境：当场减免（取代回溯补血）
                    float reduction = Mathf.Min(MAX_SUPPRESSION, -diff * REDUCTION_PER_TIER);
                    pDamage *= (1f - reduction);
                    Code.Core.DSDebug.Verbose(string.Format("[GeneCultivation] 境界压制: {0}({1}阶) → {2}({3}阶)，低境衰减 -{4:F0}%",
                        attacker.getName(), atkTier, defender.getName(), defTier, reduction * 100f));
                }
            }
            catch (System.Exception dsEx)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] RealmSuppressionPatch 异常: " + dsEx.Message);
            }
            return true;
        }
    }
}
