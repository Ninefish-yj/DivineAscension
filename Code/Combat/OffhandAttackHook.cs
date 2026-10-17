// ============================================================
// 副手武器·双持开火 Hook（v0.18.79）
//
// 原版远程/近战攻击动作（CombatActionLibrary.attackRangeAction / attackMeleeAction）
// 完成后，副手能量刃单位补发一发能量刃投射物（发射点偏移身侧 → 双持开火视觉）。
// 直接调 ProjectileManager.spawn（不经过攻击动作 → 无递归）。
// 伤害语义：副手投射物 = 真实第二发武器攻击（能量刃附魔），命中走原版伤害+特质加成，
// 双持单位每轮攻击 = 主手一击 + 副手一击（双倍能量伤害，符合双持强度预期）。
// ============================================================

using System;
using HarmonyLib;
using Code.Core;

namespace Code.Combat
{
    /// <summary>双持开火：远程攻击后副手补发投射物。</summary>
    [HarmonyPatch(typeof(CombatActionLibrary), "attackRangeAction")]
    public static class OffhandRangeFireHook
    {
        [HarmonyPostfix]
        public static void Postfix(AttackData pData)
        {
            TryFireOffhandFrom(pData);
        }

        private static void TryFireOffhandFrom(AttackData pData)
        {
            try
            {
                // AttackData 是 struct，不能与 null 比较（CS0019），以 initiator 判空
                if (pData.initiator == null || pData.initiator.a == null) return;
                Actor attacker = pData.initiator.a;
                if (!attacker.isAlive()) return;
                if (pData.target == null) return;
                OffhandWeaponSystem.TryFireOffhand(attacker, pData.target);
            }
            catch (Exception ex)
            {
                DSDebug.Verbose("[GeneCultivation] 副手远程开火异常: " + ex.Message);
            }
        }
    }

    /// <summary>双持开火：近战攻击后副手补发投射物（主手近战挥砍 + 副手能量刃甩出）。</summary>
    [HarmonyPatch(typeof(CombatActionLibrary), "attackMeleeAction")]
    public static class OffhandMeleeFireHook
    {
        [HarmonyPostfix]
        public static void Postfix(AttackData pData)
        {
            try
            {
                // AttackData 是 struct，不能与 null 比较（CS0019），以 initiator 判空
                if (pData.initiator == null || pData.initiator.a == null) return;
                Actor attacker = pData.initiator.a;
                if (!attacker.isAlive()) return;
                if (pData.target == null) return;
                OffhandWeaponSystem.TryFireOffhand(attacker, pData.target);
            }
            catch (Exception ex)
            {
                DSDebug.Verbose("[GeneCultivation] 副手近战开火异常: " + ex.Message);
            }
        }
    }
}
