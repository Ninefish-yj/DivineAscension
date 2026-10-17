// ============================================================
// 单位窗·模组自建统计行（v0.18.67 新增）
//
// 背景：原版 StatsRowsContainer.OnEnable 会启动 showRows 协程，
//   foreach stats_rows 每行 yield 一帧（手机 16FPS 下行一多协程
//   拖长到秒级）。若在此期间 stats_rows 被 Add/Clear（窗口重入、
//   关闭时原版 OnDisable 还会 stats_rows.Clear()），枚举器抛
//   "Collection was modified"。模组此前经 showStatRow 反射把行
//   追加进 stats_rows，正好踩中竞态。
//
// 方案：模组行不再进入 stats_rows 遍历集合——自建 KeyValueField
//   行对象挂到 StatsRowsContainer.transform 下，内容与原版行一致
//   （同 prefab、同奇偶底色），但生命周期由模组自理
//   （UnitGeneTabPatches.OnDisable_Postfix 统一清理）。
//   原版协程只遍历 stats_rows，永远碰不到模组行 → 竞态根治。
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Code.UI
{
    /// <summary>单位窗模组自建行：不进原版 stats_rows，规避 showRows 协程遍历竞态。</summary>
    public static class UnitOwnStatsRows
    {
        /// <summary>本窗口实例已创建的自建行（OnDisable 时统一销毁）</summary>
        private static readonly List<KeyValueField> _ownRows = new List<KeyValueField>();

        /// <summary>找当前单位窗下的 StatsRowsContainer（含未激活子对象）</summary>
        internal static StatsRowsContainer GetContainer(UnitWindow window)
        {
            if (window == null) return null;
            try { return window.GetComponentInChildren<StatsRowsContainer>(true); }
            catch { return null; }
        }

        /// <summary>
        /// 追加一行模组统计行（label/value 均为已本地化文本，图标隐藏）。
        /// 与原版 showStatRow 视觉一致（同 prefab、同奇偶底色），但独立于 stats_rows。
        /// </summary>
        internal static KeyValueField AddRow(UnitWindow window, string label, string value)
        {
            try
            {
                var container = GetContainer(window);
                if (container == null) return null;
                var prefab = Resources.Load<KeyValueField>("ui/KeyValueFieldStats");
                if (prefab == null) return null;
                var row = Object.Instantiate(prefab, container.transform, false);
                row.gameObject.name = "[GeneCultivation] " + label;
                row.icon.gameObject.SetActive(false);
                row.name_text.text = label;
                row.value.text = value;
                row.gameObject.SetActive(true); // 触发 OnEnable 奇偶底色（与原版行一致）
                _ownRows.Add(row);
                return row;
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] 自建统计行失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>销毁本窗口实例创建的全部自建行（原版 OnDisable 只清 stats_rows，不含我们）</summary>
        internal static void ClearOwnRows()
        {
            try
            {
                foreach (var row in _ownRows)
                {
                    if (row != null && row.gameObject != null)
                        Object.Destroy(row.gameObject);
                }
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] 清理自建统计行异常: " + ex.Message);
            }
            _ownRows.Clear();
        }
    }
}
