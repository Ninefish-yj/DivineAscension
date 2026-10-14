// ============================================================
// 基因属性加成总览（v0.18.3 新增）——三系统桥接"战斗实感"A
//
//  让玩家直接看到当前单位从基因体系获得的全部真实加成。
//  数值口径与 GeneEnergyAttributeApplier 完全一致（它把哪些加成写进
//  stats，这里就把哪些展示出来），只读不写。
//
//  聚合五类来源：
//    ① 基因个体效果（56基因 × 表达层级 → 攻击/护甲/速度/生命/能量上限）
//    ② 基因能量加成（攻速/射程/速度/伤害/生命/护甲）
//    ③ 神创基因（divine_* 13项）
//    ④ 登神协调（ds_boost_* 8键，固定值）
//    ⑤ 异能共鸣（ds_link_* 连锁百分比）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Core;
using Code.Data;
using Code.Realm;

namespace Code.UI
{
    /// <summary>基因属性加成总览：聚合 + 绘制（静态，无状态）</summary>
    public static class GeneBoostSummary
    {
        /// <summary>单条加成行</summary>
        public struct BoostLine
        {
            public string Label;   // 加成名称（本地化文案）
            public string Value;   // 数值文本（+X% / +X）
            public bool Active;    // 是否实际生效（>0）
        }

        private static readonly string[] BoostKeys = { "health", "damage", "speed", "mana", "stamina", "intelligence", "armor", "warfare" };
        private static readonly string[] LinkKeys = { "damage", "health", "speed", "armor", "mana" };

        /// <summary>聚合全部加成行（数值与 AttributeApplier 同口径）</summary>
        public static List<BoostLine> Summarize(Actor actor)
        {
            var lines = new List<BoostLine>();
            if (actor == null) return lines;
            bool en = UILocalization.CurrentLanguage == "en";

            // ① 基因个体效果（cap 0.5，与 GeneExpressionSystem 默认一致）
            AddPct(lines, en ? "Gene: ATK" : UILocalization.Get("boost_gene_atk"),
                GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.AttackBonus));
            AddPct(lines, en ? "Gene: DEF" : UILocalization.Get("boost_gene_def"),
                GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.DefenseBonus));
            AddPct(lines, en ? "Gene: SPD" : UILocalization.Get("boost_gene_spd"),
                GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.SpeedBonus));
            AddPct(lines, en ? "Gene: HP" : UILocalization.Get("boost_gene_hp"),
                GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.HealthBonus));
            AddPct(lines, en ? "Gene: Energy Max" : UILocalization.Get("boost_gene_enmax"),
                GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.EnergyMaxBonus));

            // ② 基因能量加成（与应用处同 clamp）
            float atkSpd = Mathf.Min(0.5f, GeneEnergyCalculator.GetAttackSpeedBonus(actor) * 0.1f);
            float rng = Mathf.Min(0.3f, GeneEnergyCalculator.GetRangeBonus(actor) * 0.05f);
            float spd = Mathf.Min(0.3f, GeneEnergyCalculator.GetSpeedBonus(actor) * 0.05f);
            AddPct(lines, en ? "Energy: AtkSpd" : UILocalization.Get("boost_en_atkspd"), atkSpd);
            AddPct(lines, en ? "Energy: Range" : UILocalization.Get("boost_en_range"), rng);
            AddPct(lines, en ? "Energy: Speed" : UILocalization.Get("boost_en_speed"), spd);
            AddPct(lines, en ? "Energy: DMG" : UILocalization.Get("boost_en_dmg"), GeneEnergyCalculator.GetDamageBonus(actor));
            AddPct(lines, en ? "Energy: HP" : UILocalization.Get("boost_en_hp"), GeneEnergyCalculator.GetHealthBonus(actor));
            AddPct(lines, en ? "Energy: Armor" : UILocalization.Get("boost_en_armor"), GeneEnergyCalculator.GetArmorBonus(actor));

            // ③ 神创基因（divine_*，13 项非零才展示）
            AddPct(lines, en ? "Divine: DMG" : UILocalization.Get("boost_divine_dmg"), GeneEnergyCalculator.GetDivineDamageBonus(actor));
            AddPct(lines, en ? "Divine: Armor" : UILocalization.Get("boost_divine_def"), GeneEnergyCalculator.GetDivineDefBonus(actor));
            AddPct(lines, en ? "Divine: AtkSpd" : UILocalization.Get("boost_divine_spd"), GeneEnergyCalculator.GetDivineSpeedBonus(actor));
            AddPct(lines, en ? "Divine: HP" : UILocalization.Get("boost_divine_hp"), GeneEnergyCalculator.GetDivineHpBonus(actor));
            AddPct(lines, en ? "Divine: Energy Max" : UILocalization.Get("boost_divine_maxen"), GeneEnergyCalculator.GetDivineMaxEnBonus(actor));
            AddPct(lines, en ? "Divine: Energy Regen" : UILocalization.Get("boost_divine_enrec"), GeneEnergyCalculator.GetDivineEnRecBonus(actor));
            AddPct(lines, en ? "Divine: Cult" : UILocalization.Get("boost_divine_cult"), GeneEnergyCalculator.GetDivineCultBonus(actor));
            AddPct(lines, en ? "Divine: Stability" : UILocalization.Get("boost_divine_stab"), GeneEnergyCalculator.GetDivineStabBonus(actor));
            AddPct(lines, en ? "Divine: Cooldown" : UILocalization.Get("boost_divine_cd"), GeneEnergyCalculator.GetDivineCdBonus(actor));
            AddPct(lines, en ? "Divine: DMG Red." : UILocalization.Get("boost_divine_dmgred"), GeneEnergyCalculator.GetDivineDmgRedBonus(actor));
            AddPct(lines, en ? "Divine: Crit" : UILocalization.Get("boost_divine_crit"), GeneEnergyCalculator.GetDivineCritBonus(actor));
            AddPct(lines, en ? "Divine: Lifesteal" : UILocalization.Get("boost_divine_lifesteal"), GeneEnergyCalculator.GetDivineLifestealBonus(actor));
            AddPct(lines, en ? "Divine: Heal" : UILocalization.Get("boost_divine_heal"), GeneEnergyCalculator.GetDivineHealBonus(actor));

            // ④ 登神协调（固定值直接加成）
            var data = ActorDataAccessor.GetData(actor);
            if (data != null && data.custom_data_float != null)
            {
                for (int i = 0; i < BoostKeys.Length; i++)
                {
                    if (data.custom_data_float.TryGetValue("ds_boost_" + BoostKeys[i], out float bv) && bv > 0f)
                    {
                        AddValue(lines, (en ? "Synergy: " : UILocalization.Get("boost_synergy")) + BoostKeys[i], bv);
                    }
                }

                // ⑤ 异能共鸣（连锁百分比）
                float all = 0f;
                data.custom_data_float.TryGetValue("ds_link_all", out all);
                for (int i = 0; i < LinkKeys.Length; i++)
                {
                    float link = 0f;
                    data.custom_data_float.TryGetValue("ds_link_" + LinkKeys[i], out link);
                    float total = link + all;
                    if (total > 0f) AddPct(lines, (en ? "Link: " : UILocalization.Get("boost_link")) + LinkKeys[i], total);
                }
            }

            return lines;
        }

        /// <summary>百分比行（0.15 → "+15%"）</summary>
        private static void AddPct(List<BoostLine> lines, string label, float value)
        {
            if (value <= 0.0005f) return;
            lines.Add(new BoostLine { Label = label, Value = "+" + (value * 100f).ToString("F1") + "%", Active = true });
        }

        /// <summary>固定值行</summary>
        private static void AddValue(List<BoostLine> lines, string label, float value)
        {
            if (value <= 0.0005f) return;
            lines.Add(new BoostLine { Label = label, Value = "+" + value.ToString("F1"), Active = true });
        }

        /// <summary>绘制加成总览（供基因图谱/异能窗中栏调用）</summary>
        public static void Draw(Actor actor)
        {
            if (actor == null) return;
            bool en = UILocalization.CurrentLanguage == "en";

            var lines = Summarize(actor);
            if (lines.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label(en ? "No active gene boosts (awaken first)" : UILocalization.Get("boost_none"), DSGUIStylePool.Label(9));
                GUI.color = Color.white;
                return;
            }

            Rect sepRect = GUILayoutUtility.GetRect(1f, 1f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
            GUI.DrawTexture(sepRect, Texture2D.whiteTexture);
            GUILayout.Space(6f);

            GUI.color = new Color(0.95f, 0.85f, 0.5f);
            GUILayout.Label(en ? "GENE BOOST SUMMARY (live)" : UILocalization.Get("boost_title"), DSGUIStylePool.Label(10, FontStyle.Bold));
            GUI.color = Color.white;

            // 最多展示 14 条，超出折叠提示
            int show = Mathf.Min(14, lines.Count);
            for (int i = 0; i < show; i++)
            {
                var line = lines[i];
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUI.color = new Color(0.8f, 0.85f, 0.9f);
                GUILayout.Label(line.Label, DSGUIStylePool.Label(9), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                GUI.color = new Color(0.9f, 0.75f, 0.4f);
                GUILayout.Label(line.Value, DSGUIStylePool.Label(9, FontStyle.Bold), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(70f) });
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }
            if (lines.Count > show)
            {
                GUI.color = new Color(0.5f, 0.55f, 0.5f);
                GUILayout.Label(en ? "+" + (lines.Count - show) + " more" : "+" + (lines.Count - show) + UILocalization.Get("boost_more"), DSGUIStylePool.Label(8));
                GUI.color = Color.white;
            }
        }
    }
}
