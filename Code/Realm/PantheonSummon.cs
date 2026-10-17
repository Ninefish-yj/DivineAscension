// ============================================================
// 神名念诵·召唤回归（v0.18.86，v0.18.96 本世界留痕语义）
// 世界观：登神级单位（11阶+）每年有概率"念诵神名"沟通上代强者，
// 将回归池中的神应召降临当前世界（回归即出池，席位轮换）。
//
// v0.18.96 本世界留痕（修复"跨存档神在本世界无信息却被念诵"的逻辑漏洞）：
//   念诵的前提是"本世界知道这个神"。神在本世界留下过痕迹才可被念诵：
//     ① 本世界诞生的登神（OriginWorld == 当前世界）；
//     ② 曾降临过本世界的神（SummonedWorlds 记录，念诵/召回成功时写入）。
//   满足任一条件的，本世界的任意登神者都可能念诵其神名（传说已在世界流传）。
//   无痕迹的跨存档神：本世界从未听说过它——无人能念诵（修复语义漏洞）。
//
// 约束：无留痕的神不触发；世界内该段位名额已满不召唤（防爆满）。
// ============================================================

using System.Collections.Generic;
using Code.Core;
using Code.Data;
using UnityEngine;

namespace Code.Realm
{
    /// <summary>神名念诵：登神级单位年度仪式，召唤回归池神降临</summary>
    public static class PantheonSummon
    {
        /// <summary>每年触发念诵的基础概率</summary>
        private const float SUMMON_CHANCE = 0.35f;

        /// <summary>世界内段位名额上限（对应世界登神/真神/超神名额）</summary>
        private const int CAP_T13 = 3;
        private const int CAP_T12 = 7;
        private const int CAP_T11 = 16;

        /// <summary>
        /// 年度念诵检查（挂全局年度结算）：遍历回归池，只考虑"在本世界留下过痕迹"的池神，
        /// 随机选一位有痕迹的神，由世界内任意登神者按概率念诵其神名召唤降临。
        /// </summary>
        public static void AnnualSummonCheck()
        {
            if (World.world == null) return;

            try
            {
                // v0.18.96 本世界留痕：只有本世界"听说过"的神才可被念诵（本世界诞生 / 曾降临过）
                List<TrueGodManager.TrueGodData> invocable = CollectInvocableGods();
                if (invocable == null || invocable.Count == 0) return; // 本世界没有留痕的神：神名无人知晓

                // 概率触发（避免每年轻松爆满）
                if (Random.value >= SUMMON_CHANCE) return;

                // 随机挑一位本世界有留痕的神
                TrueGodManager.TrueGodData target = invocable[Random.Range(0, invocable.Count)];

                // 名额约束：世界内该段位已达上限则不召唤（保持世界平衡）
                Actor summoner = FindSummonerForGod(target);
                if (summoner == null) return;
                if (WorldSlotFull(summoner)) return;

                // 念诵的神名：神名优先（与 TrySummonGodByName 匹配顺序一致），回退西式神名/单位名
                string invokedGodName = !string.IsNullOrEmpty(target.DivineName) ? target.DivineName
                    : (!string.IsNullOrEmpty(target.DivineNameWest) ? target.DivineNameWest : target.Name);
                string displayTitle = !string.IsNullOrEmpty(target.DivineName) ? target.DivineName : target.Name;
                if (invokedGodName != null)
                {
                    // 已在世间判定按单位名（IsNamePresentInWorld 按单位名查世界存活单位）
                    bool alreadyPresent = TrueGodManager.IsNamePresentInWorld(target.Name);
                    TrueGodManager.TrueGodData returned = TrueGodManager.TrySummonGodByName(invokedGodName);
                    if (returned != null)
                    {
                        DSNotificationManager.NotifyInfo("登神者 " + summoner.getName() + " 念诵传说神名「" + displayTitle + "」，上代强者「" + returned.Name + "」应召降临！");
                    }
                    else if (alreadyPresent)
                    {
                        DSNotificationManager.NotifyInfo("登神者 " + summoner.getName() + " 念诵神名「" + displayTitle + "」，但该神已在世间，无需重复降临。");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DSDebug.Warning("[GeneCultivation] 神名念诵年度检查异常: " + ex.Message);
            }
        }

        /// <summary>收集"本世界有留痕（诞生/曾降临）且有登神者可念诵"的回归池神</summary>
        private static List<TrueGodManager.TrueGodData> CollectInvocableGods()
        {
            var pool = TrueGodManager.GetReincarnations();
            if (pool == null || pool.Count == 0) return null;

            List<TrueGodManager.TrueGodData> invocable = new List<TrueGodManager.TrueGodData>();
            foreach (var god in pool)
            {
                if (god == null) continue;
                if (FindSummonerForGod(god) != null) invocable.Add(god);
            }
            return invocable;
        }

        /// <summary>
        /// 找一位可念诵该神的登神级（11-13阶）单位：
        /// ①该神必须在本世界留有痕迹（本世界诞生 / 曾降临过本世界）——否则本世界"没听说过它"，无人能念诵；
        /// ②世界内任意登神者都可念诵（传说已在世界流传，不限族裔）。
        /// </summary>
        private static Actor FindSummonerForGod(TrueGodManager.TrueGodData god)
        {
            if (god == null) return null;

            // 本世界留痕判定
            string worldName = World.world?.name;
            if (string.IsNullOrEmpty(worldName)) return null;
            bool bornHere = string.Equals(god.OriginWorld, worldName, System.StringComparison.OrdinalIgnoreCase);
            bool visitedHere = god.SummonedWorlds != null && god.SummonedWorlds.Contains(worldName);
            if (!bornHere && !visitedHere) return null; // 本世界从未听说过此神：神名失传

            var alive = World.world.units?.units_only_alive;
            if (alive == null) return null;

            List<Actor> candidates = new List<Actor>();
            foreach (var actor in alive)
            {
                if (actor == null || !actor.isAlive()) continue;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 11 || tier > 13) continue;
                candidates.Add(actor);
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>世界内该段位名额是否已满（超神3/真神7/登神16）</summary>
        private static bool WorldSlotFull(Actor summoner)
        {
            int tier = CultivationData.GetRealmTier(summoner);
            int cap = tier == 13 ? CAP_T13 : (tier == 12 ? CAP_T12 : CAP_T11);
            int count = 0;
            var alive = World.world.units?.units_only_alive;
            if (alive == null) return true;
            foreach (var actor in alive)
            {
                if (actor == null || !actor.isAlive()) continue;
                if (CultivationData.GetRealmTier(actor) == tier) count++;
            }
            return count >= cap;
        }
    }
}
