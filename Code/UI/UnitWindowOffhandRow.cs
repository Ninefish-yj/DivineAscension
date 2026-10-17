// ============================================================
// 单位窗·副手武器行（v0.18.78）
// 在单位窗统计列表追加"副手武器"行，显示模组自建副手武器槽内容。
// 使用 UnitOwnStatsRows.AddRow（自建行方案，规避原版 showRows 协程遍历竞态），
// 独立 Postfix（与原 UnitWindowIntegration 的 Postfix 共存，Harmony 允许多 patch）。
// ============================================================

using System;
using HarmonyLib;
using UnityEngine;
using Code.Combat;
using Code.Core;

namespace Code.UI
{
    /// <summary>单位窗副手武器行：Postfix 于 showStatsRows。</summary>
    [HarmonyPatch(typeof(UnitWindow), "showStatsRows")]
    public static class UnitWindowOffhandRow
    {
        /// <summary>副手能量刃显示名</summary>
        private const string OFFHAND_BLADE_NAME = "能量刃";

        [HarmonyPostfix]
        public static void Postfix(UnitWindow __instance)
        {
            try
            {
                Actor actor = UnitWindowIntegration.GetUnitActor(__instance);
                if (actor == null || !actor.isAlive()) return;

                string assetId = OffhandWeaponSystem.GetOffhandAssetId(actor);
                if (string.IsNullOrEmpty(assetId)) return;

                string weaponName = GetWeaponDisplayName(assetId);
                UnitOwnStatsRows.AddRow(__instance, "副手武器", weaponName);
            }
            catch (Exception ex)
            {
                DSDebug.Verbose("[GeneCultivation] 副手武器行异常: " + ex.Message);
            }
        }

        /// <summary>副手武器显示名（能量刃特判；其他武器用资产id兜底）</summary>
        private static string GetWeaponDisplayName(string assetId)
        {
            if (assetId == OffhandWeaponSystem.OFFHAND_ENERGY_BLADE_ID) return OFFHAND_BLADE_NAME;
            try
            {
                var asset = AssetManager.items.get(assetId);
                if (asset != null && !string.IsNullOrEmpty(asset.id)) return asset.id;
            }
            catch { }
            return assetId;
        }
    }
}
