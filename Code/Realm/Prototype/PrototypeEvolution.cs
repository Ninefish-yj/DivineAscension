// ============================================================
// 异能进阶（v0.18.22 新增系统）——异能的"宝可梦式进化"（与境界/阶位系统相互独立）
//
// 设计（用户确认）：
//  1. 独立进化链：同一"系→子类"家族内的异能按品质（1凡阶/2觉醒阶/3进化阶/4升维阶/5概念级）
//     从低到高排布；每个异能唯一上级 = 家族内第一个严格更高品质的异能（固定链、宝可梦式）
//  2. 年度自动事件触发：境界达标 + 觉醒年限达标 + 低概率（难度极高，品质越高越难）
//  3. 天生高品：觉醒抽签稀有度加权（概念级约 1.5%），God 可从 UI 区分"觉醒"与"进阶"来源
// 单一职责：品质推导 / 进化链查询 / 觉醒来源记录 / 年度进阶判定 / 年度扫描
// ============================================================

using System.Collections.Generic;
using System;

namespace Code.Realm.Prototype
{
    public static class PrototypeEvolution
    {
        // ---- custom_data_string keys（与现有 ds_* 模式一致）----
        public const string KEY_ORIGIN = "ds_prototype_origin";             // "awakened"(天生觉醒) / "evolved"(后天进阶)
        public const string KEY_EVOLVE_COUNT = "ds_prototype_evolve_count"; // 进阶次数（int 字符串）
        public const string KEY_AWAKEN_YEAR = "ds_prototype_awaken_year";   // 觉醒年份（世界年，1年=60s，与年度结算同源）

        public const int TIER_MAX = 5;

        /// <summary>异能品质：同系同子类家族内按注册顺序分档（越靠后品越高，1凡~5概念级）</summary>
        public static int GetTier(PrototypeDef def)
        {
            if (def == null) return 1;
            if (!PrototypeRegistry.Categories.TryGetValue(def.Category, out var subs) ||
                !subs.TryGetValue(def.SubCategory, out var list) || list == null || list.Count == 0)
                return 1;
            int n = list.Count;
            if (n <= 1) return 1;
            for (int i = 0; i < n; i++)
            {
                if (list[i] != null && list[i].Id == def.Id)
                {
                    int tier = 1 + (i * 4) / (n - 1);
                    if (tier > TIER_MAX) tier = TIER_MAX;
                    return tier;
                }
            }
            return 1;
        }

        /// <summary>进化目标（唯一上级）：同家族内第一个严格更高品质的异能；概念级/家族顶点 → null</summary>
        public static PrototypeDef GetEvolveTarget(PrototypeDef def)
        {
            if (def == null) return null;
            int myTier = GetTier(def);
            if (myTier >= TIER_MAX) return null;
            if (!PrototypeRegistry.Categories.TryGetValue(def.Category, out var subs) ||
                !subs.TryGetValue(def.SubCategory, out var list) || list == null) return null;
            foreach (var cand in list)
                if (cand != null && GetTier(cand) > myTier) return cand;
            return null;
        }

        /// <summary>觉醒抽签稀有度权重（天生高品概率极低）</summary>
        public static float GetRarityWeight(int tier)
        {
            switch (tier)
            {
                case 2: return 0.35f;
                case 3: return 0.12f;
                case 4: return 0.04f;
                case 5: return 0.015f;
                default: return 1f;
            }
        }

        // ---- 来源记录（God 视角区分：天生觉醒 vs 后天进阶）----
        public static bool IsEvolved(Actor actor)
        {
            if (actor == null || actor.data == null || actor.data.custom_data_string == null) return false;
            return actor.data.custom_data_string.TryGetValue(KEY_ORIGIN, out string o) && o == "evolved";
        }

        public static int GetEvolveCount(Actor actor)
        {
            if (actor == null || actor.data == null || actor.data.custom_data_string == null) return 0;
            if (actor.data.custom_data_string.TryGetValue(KEY_EVOLVE_COUNT, out string v) && int.TryParse(v, out int c)) return c;
            return 0;
        }

        public static int GetAwakenYear(Actor actor)
        {
            if (actor == null || actor.data == null || actor.data.custom_data_string == null) return 0;
            if (actor.data.custom_data_string.TryGetValue(KEY_AWAKEN_YEAR, out string v) && int.TryParse(v, out int y)) return y;
            return 0;
        }

        /// <summary>觉醒定相成功时记录来源（RollPrototype 内调用）</summary>
        public static void RecordAwakened(Actor actor)
        {
            if (actor == null || actor.data == null) return;
            if (actor.data.custom_data_string == null) actor.data.custom_data_string = new Dictionary<string, string>();
            actor.data.custom_data_string[KEY_ORIGIN] = "awakened";
            actor.data.custom_data_string[KEY_AWAKEN_YEAR] = GetWorldYear().ToString();
        }

        // ---- 年度进阶判定（难度非常高）----
        public static bool TryEvolve(Actor actor)
        {
            if (actor == null || actor.data == null) return false;
            var proto = PrototypeRegistry.GetForActor(actor);
            if (proto == null) return false;
            var target = GetEvolveTarget(proto);
            if (target == null) return false;   // 已达家族顶点（概念级）不再进化

            int targetTier = GetTier(target);

            // 1) 境界门槛（异能进化需要对应境界支撑；仅作门槛，与境界系统本身相互独立）
            int realmTier = Code.Data.CultivationData.GetRealmTier(actor);
            int realmNeed = 99;
            switch (targetTier)
            {
                case 2: realmNeed = 3; break;   // →觉醒阶：需 3 阶境界
                case 3: realmNeed = 5; break;   // →进化阶：需 5 阶
                case 4: realmNeed = 7; break;   // →升维阶：需 7 阶
                case 5: realmNeed = 9; break;   // →概念级：需 9 阶
            }
            if (realmTier < realmNeed) return false;

            // 2) 觉醒年限门槛（异能需要时间沉淀，防止觉醒即连跳）
            int year = GetWorldYear();
            int awakenYear = GetAwakenYear(actor);
            if (awakenYear <= 0) awakenYear = year;   // 旧档无记录：按当年觉醒计（不白送年限）
            int years = year - awakenYear;
            int yearsNeed = 99;
            switch (targetTier)
            {
                case 2: yearsNeed = 2; break;
                case 3: yearsNeed = 5; break;
                case 4: yearsNeed = 10; break;
                case 5: yearsNeed = 20; break;
            }
            if (years < yearsNeed) return false;

            // 3) 概率（难度极高，品质越高越难）：基础 2%/年 × 品质系数
            float chance = 0.02f;
            switch (targetTier)
            {
                case 2: chance *= 1f; break;
                case 3: chance *= 0.6f; break;
                case 4: chance *= 0.35f; break;
                case 5: chance *= 0.15f; break;
            }
            if (UnityEngine.Random.value > chance) return false;

            // 成功：写新异能 + 来源=进阶 + 次数
            var data = actor.data;
            if (data.custom_data_string == null) data.custom_data_string = new Dictionary<string, string>();
            data.custom_data_string[GeneAffinityBridge.KEY_PROTOTYPE] = target.Id;
            data.custom_data_string[KEY_ORIGIN] = "evolved";
            data.custom_data_string[KEY_EVOLVE_COUNT] = (GetEvolveCount(actor) + 1).ToString();
            Code.Core.DSDebug.Log("[GeneCultivation] 异能进化: " + actor.getName() + " " + proto.NameZh +
                " → " + target.NameZh + "（" + TierName(targetTier) + "），来源=进阶×" + GetEvolveCount(actor));
            return true;
        }

        /// <summary>年度扫描（AnnualTickManager 每年代调）：抽样遍历有异能单位做进阶判定</summary>
        public static void AnnualSweep()
        {
            if (World.world == null || World.world.units == null) return;
            if (!Code.Core.GeneCultivationConfig.EnableAutoAwakening) return;   // 与觉醒同开关（异能事件统一控制）
            int checkedCount = 0;
            const int maxCheckPerYear = 2000;   // 抽样上限（防全图遍历卡顿；异能者本就稀少）
            try
            {
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (actor == null) continue;
                    if (!PrototypeRegistry.HasPrototype(actor)) continue;
                    if (++checkedCount > maxCheckPerYear) break;
                    try { TryEvolve(actor); }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] 异能进化处理异常: " + dsEx.Message); }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] 异能进化遍历异常: " + dsEx.Message); }
        }

        /// <summary>当前世界年（1年=60s 游戏时间，与年度结算同源）</summary>
        public static int GetWorldYear()
        {
            if (World.world == null) return 0;
            try { return UnityEngine.Mathf.FloorToInt((float)(World.world.getCurWorldTime() / 60.0)); }
            catch { return 0; }
        }

        /// <summary>品质名（UI 展示默认名；汉化由 UI 侧按语言覆盖）</summary>
        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return "凡阶";
                case 2: return "觉醒阶";
                case 3: return "进化阶";
                case 4: return "升维阶";
                case 5: return "概念级";
                default: return "";
            }
        }
    }
}
