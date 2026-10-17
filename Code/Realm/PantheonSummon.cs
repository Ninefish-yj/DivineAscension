// ============================================================
// 神名念诵·召唤回归（v0.18.86）
// 世界观：登神级单位（11阶+）每年有概率"念诵神名"沟通上代强者，
// 将回归池中的神应召降临当前世界（回归即出池，席位轮换）。
// 约束：无念诵者不触发；世界内该段位名额已满不召唤（防爆满）。
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
        /// 年度念诵检查（挂全局年度结算）：世界内有登神级单位时，按概率念诵神名召唤回归池之神。
        /// </summary>
        public static void AnnualSummonCheck()
        {
            if (World.world == null) return;

            try
            {
                // 念诵者：世界内随机一位 11 阶+ 单位（登神者才有沟通上代神的神格）
                Actor summoner = FindSummoner();
                if (summoner == null) return;

                // 概率触发（避免每年轻松爆满）
                if (Random.value >= SUMMON_CHANCE) return;

                // 名额约束：世界内该段位已达上限则不召唤（保持世界平衡）
                if (WorldSlotFull(summoner)) return;

                string godName = TrueGodManager.TrySummonRandomGod();
                if (godName != null)
                {
                    DSNotificationManager.NotifyInfo(summoner.getName() + " 念诵神名，上代强者「" + godName + "」应召降临！");
                }
            }
            catch (System.Exception ex)
            {
                DSDebug.Warning("[GeneCultivation] 神名念诵年度检查异常: " + ex.Message);
            }
        }

        /// <summary>随机找一位登神级（11阶+）单位作为念诵者，无则返回 null</summary>
        private static Actor FindSummoner()
        {
            var alive = World.world.units?.units_only_alive;
            if (alive == null) return null;

            List<Actor> candidates = new List<Actor>();
            foreach (var actor in alive)
            {
                if (actor == null || !actor.isAlive()) continue;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier >= 11 && tier <= 13) candidates.Add(actor);
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
