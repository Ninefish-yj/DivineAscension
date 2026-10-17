// ============================================================
// 单位窗·战力行（v0.18.82）
// 在单位窗统计列表追加"战力"行，显示开方压缩的基础战力（参考西幻战力公式）。
// 使用 UnitOwnStatsRows.AddRow 自建行方案（与原版行视觉一致、规避竞态）。
// ============================================================

using System;
using HarmonyLib;
using Code.Combat;
using Code.Core;

namespace Code.UI
{
    /// <summary>单位窗战力行：Postfix 于 showStatsRows。</summary>
    [HarmonyPatch(typeof(UnitWindow), "showStatsRows")]
    public static class UnitWindowPowerRow
    {
        [HarmonyPostfix]
        public static void Postfix(UnitWindow __instance)
        {
            try
            {
                Actor actor = UnitWindowIntegration.GetUnitActor(__instance);
                if (actor == null || !actor.isAlive()) return;

                float power = CombatPowerCalculator.Calculate(actor);
                if (power <= 0f) return;

                string value = power >= 10000f
                    ? (power / 10000f).ToString("0.0") + "万"
                    : power.ToString("0");
                UnitOwnStatsRows.AddRow(__instance, "战力", value);
            }
            catch (Exception ex)
            {
                DSDebug.Verbose("[GeneCultivation] 战力行异常: " + ex.Message);
            }
        }
    }
}
