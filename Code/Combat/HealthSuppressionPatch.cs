
using HarmonyLib;
using Code.Core;
using Code.Data;

namespace Code.Combat
{
    /// <summary>拦截整数恢复</summary>
    [HarmonyPatch(typeof(Actor), "restoreHealth")]
    public static class HealthSuppressionIntPatch
    {
        static bool Prefix(Actor __instance, int pVal)
        {
            // 总闸开关：关了就完全放行
            if (!DSConfigManager.EnableGlobalHealthSuppression) return true;
            // 只拦加血方向，不拦扣血
            if (pVal <= 0) return true;
            if (__instance == null || !__instance.isAlive()) return true;

            long id = __instance.id;
            // 全压：所有单位战斗中（10秒内受伤/挑战战斗）都不能回血，公平
            if (AnnualTickDriver.IsInCombatSuppression(id))
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>拦截百分比恢复</summary>
    [HarmonyPatch(typeof(Actor), "restoreHealthPercent")]
    public static class HealthSuppressionPercentPatch
    {
        static bool Prefix(Actor __instance, float pVal)
        {
            if (!DSConfigManager.EnableGlobalHealthSuppression) return true;
            if (pVal <= 0f) return true;
            if (__instance == null || !__instance.isAlive()) return true;

            long id = __instance.id;
            // 全压：所有单位战斗中都不能回血
            if (AnnualTickDriver.IsInCombatSuppression(id))
            {
                return false;
            }
            return true;
        }
    }
}
