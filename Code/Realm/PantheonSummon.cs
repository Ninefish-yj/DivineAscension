// ============================================================
// 神名念诵·召唤回归（v0.18.86，v0.18.96 传承语义重构）
// 世界观：登神级单位（11阶+）每年有概率"念诵神名"沟通上代强者，
// 将回归池中的神应召降临当前世界（回归即出池，席位轮换）。
//
// v0.18.96 传承绑定（修复"跨存档神在本世界无信息却被念诵"的逻辑漏洞）：
//   念诵的前提是"知道神名"= 族裔传承记忆。只有与目标神同族（Race 相同）的
//   登神级单位才继承该族裔的上代强者传说，得以念诵其神名。
//   跨存档降临的神若在本世界没有同族登神者，其神名在本世界失传——无人能念诵。
//
// 约束：无传承念诵者不触发；世界内该段位名额已满不召唤（防爆满）。
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
        /// 年度念诵检查（挂全局年度结算）：遍历回归池，找"有同族传承念诵者"的池神，
        /// 随机选一位有传承者的神，由传承者按概率念诵其神名召唤降临。
        /// </summary>
        public static void AnnualSummonCheck()
        {
            if (World.world == null) return;

            try
            {
                // v0.18.96 传承念诵：只有同族登神者才"认识"该神的神名（族裔传说）
                List<TrueGodManager.TrueGodData> invocable = CollectInvocableGods();
                if (invocable == null || invocable.Count == 0) return; // 无传承者：神名在本世界失传

                // 概率触发（避免每年轻松爆满）
                if (Random.value >= SUMMON_CHANCE) return;

                // 随机挑一位有传承者的神
                TrueGodManager.TrueGodData target = invocable[Random.Range(0, invocable.Count)];

                // 名额约束：世界内该段位已达上限则不召唤（保持世界平衡）
                Actor summoner = FindSummonerForGod(target);
                if (summoner == null) return;
                if (WorldSlotFull(summoner)) return;

                // 指定神名念诵：传承者念其神名精确召回
                string invokedGodName = target.Name;
                if (invokedGodName != null)
                {
                    // v0.18.95 神已在世间：记录由 TrueGodManager 出池流转席位，念诵无应（不重复降临）
                    bool alreadyPresent = TrueGodManager.IsNamePresentInWorld(invokedGodName);
                    TrueGodManager.TrueGodData returned = TrueGodManager.TrySummonGodByName(invokedGodName);
                    if (returned != null)
                    {
                        string divineTitle = !string.IsNullOrEmpty(returned.DivineName)
                            ? returned.DivineName
                            : returned.Name;
                        DSNotificationManager.NotifyInfo("族裔传承者 " + summoner.getName() + " 念诵族神名「" + divineTitle + "」，上代强者「" + returned.Name + "」应召降临！");
                    }
                    else if (alreadyPresent)
                    {
                        DSNotificationManager.NotifyInfo("族裔传承者 " + summoner.getName() + " 念诵神名「" + invokedGodName + "」，但该神已在世间，无需重复降临。");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DSDebug.Warning("[GeneCultivation] 神名念诵年度检查异常: " + ex.Message);
            }
        }

        /// <summary>收集"本世界有同族传承念诵者"的回归池神（无传承者的神名在本世界失传）</summary>
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

        /// <summary>找一位与该神同族（Race 相同）的登神级（11-13阶）单位作为传承念诵者，无则返回 null</summary>
        private static Actor FindSummonerForGod(TrueGodManager.TrueGodData god)
        {
            if (god == null) return null;
            var alive = World.world.units?.units_only_alive;
            if (alive == null) return null;

            List<Actor> candidates = new List<Actor>();
            foreach (var actor in alive)
            {
                if (actor == null || !actor.isAlive()) continue;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 11 || tier > 13) continue;
                // 族裔传承：同族（asset_id 相同）才继承该神的神名传说
                string actorRace = null;
                try
                {
                    var d = actor.getData() as ActorData;
                    actorRace = (d != null) ? d.asset_id : null;
                }
                catch { }
                if (!string.IsNullOrEmpty(actorRace) && string.Equals(actorRace, god.Race, System.StringComparison.OrdinalIgnoreCase))
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
