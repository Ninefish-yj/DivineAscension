// ============================================================

using System;
using System.Collections.Generic;
using Code.Data;
using Code.Realm;
using UnityEngine;

namespace Code.Core
{
    /// <summary>全局统计快照（用于调试面板一次性读取，避免多次遍历）</summary>
    public struct GlobalStatsSnapshot
    {
        public int TotalAwakened;           // 已觉醒异能者总数（tier>=1）
        public int TotalPopulation;          // 全球存活总人口
        public float AwakenedRatio;          // 异能者占总人口比例
        public int[] TierDistribution;       // 各境界人数 [0..13]
        public Dictionary<string, int> GeneDistribution; // 各基因掌握人数
        public int TotalSects;               // 组织总数
        public int TotalDisasters;           // 累计灾变次数
        public int TotalBreakthroughs;       // 累计突破次数
        public int TotalAwakenings;          // 累计觉醒次数
        public float AverageTurbulence;      // 平均失控指数
        public int TrueGodCount;             // 真神数量
        public int TotalAbilityUses;         // 累计异能使用次数
        public int TotalKills;               // 累计击杀数
        public int TotalAbilityKills;        // 累计异能击杀数
        public int HighestTierReached;       // 历史最高境界
        public int TotalGeneLinkages;       // 全局基因连锁激活总数
                        public int ActiveRifts;              // 当前活跃虚空裂隙数
        public float BarrierIntegrity;       // 天地屏障完整度
    }

    /// <summary>
    /// 全局统计管理器
    /// 统一提供异能体系全局统计查询与累计计数
    /// </summary>
    public static class DSGlobalStats
    {
        // ============================================================
        //  性能优化：统计结果缓存（避免每帧多次遍历所有单位）
        // ============================================================
        private static GlobalStatsSnapshot _cachedSnapshot;
        private static bool _cacheValid = false;
        private static float _lastSnapshotTime = -100f;
        private const float SNAPSHOT_CACHE_INTERVAL = 1.0f; // 缓存1秒
        // ===== 累计计数器（运行时静态存储，世界加载时重置，纯随档不跨存档）=====
        private static readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        // 预定义计数器键
        public const string COUNTER_BREAKTHROUGHS = "total_breakthroughs";
        public const string COUNTER_DISASTERS = "total_disasters";
        public const string COUNTER_AWAKENINGS = "total_awakenings";
        public const string COUNTER_ABILITY_USES = "total_ability_uses";

        /// <summary>重置所有累计计数器（世界加载时调用）</summary>
        public static void Reset()
        {
            // 清除缓存
            _cacheValid = false;
            _lastSnapshotTime = -100f;
            _counters.Clear();
        }

        // ============================================================
        //  累计计数器接口
        // ============================================================

        /// <summary>获取指定计数器值</summary>
        public static int GetCounter(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            _counters.TryGetValue(key, out int val);
            return val;
        }

        /// <summary>突破计数+1（突破时调用）</summary>
        public static void IncrementBreakthroughCount()
        {
            _counters.TryGetValue(COUNTER_BREAKTHROUGHS, out int val);
            _counters[COUNTER_BREAKTHROUGHS] = val + 1;
        }

        /// <summary>灾变计数+1（灾变发生时调用）</summary>
        public static void IncrementDisasterCount()
        {
            _counters.TryGetValue(COUNTER_DISASTERS, out int val);
            _counters[COUNTER_DISASTERS] = val + 1;
        }

        /// <summary>觉醒计数+1（凡人觉醒时调用）</summary>
        public static void IncrementAwakeningCount()
        {
            _counters.TryGetValue(COUNTER_AWAKENINGS, out int val);
            _counters[COUNTER_AWAKENINGS] = val + 1;
        }

        /// <summary>异能使用计数+1（异能技能使用时调用）</summary>
        public static void IncrementAbilityUseCount()
        {
            _counters.TryGetValue(COUNTER_ABILITY_USES, out int val);
            _counters[COUNTER_ABILITY_USES] = val + 1;
        }

        // ============================================================
        //  实时统计（每次查询遍历计算）
        // ============================================================

        /// <summary>各基因掌握人数（key=基因ID, value=掌握该基因的人数）</summary>

        /// <summary>组织总数</summary>
        public static int GetTotalSects()
        {
            try { return Sect.SectManager.GetAllSects().Count; }
            catch { return 0; }
        }

        /// <summary>累计灾变次数</summary>
        public static int GetTotalDisasters()
        {
            return GetCounter(COUNTER_DISASTERS);
        }

        /// <summary>累计突破次数</summary>
        public static int GetTotalBreakthroughs()
        {
            return GetCounter(COUNTER_BREAKTHROUGHS);
        }

        /// <summary>累计觉醒次数</summary>
        public static int GetTotalAwakenings()
        {
            return GetCounter(COUNTER_AWAKENINGS);
        }

        /// <summary>真神数量（跨存档神殿中的总数）</summary>
        public static int GetTrueGodCount()
        {
            try { return TrueGodManager.GetTrueGodCount(); }
            catch { return 0; }
        }

        /// <summary>累计异能使用次数</summary>
        public static int GetTotalAbilityUses()
        {
            return GetCounter(COUNTER_ABILITY_USES);
        }

        /// <summary>历史最高境界（所有存活单位中最高）</summary>
        public static int GetHighestTierReached()
        {
            int highest = 0;
            try
            {
                var units = World.world?.units?.units_only_alive;
                if (units == null) return 0;
                foreach (var a in units)
                {
                    try
                    {
                        if (a == null || !a.isAlive()) continue;
                        int t = CultivationData.GetHighestTierReached(a);
                        if (t > highest) highest = t;
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetHighestTierReached 异常: " + dsEx.Message); }
                }
            }
            catch (Exception e) { DSDebug.Warning("GetHighestTierReached异常: " + e.Message); }
            return highest;
        }

        // ============================================================
        //  统计快照（一次性计算所有数据，避免UI多次遍历）
        // ============================================================

        /// <summary>
        /// 获取全局统计快照（一次性遍历计算所有统计数据）
        /// 调试面板调用此方法，避免多次全图遍历
        /// </summary>
        public static GlobalStatsSnapshot GetStatsSnapshot()
        {
            // 性能优化：使用缓存，避免每帧多次遍历
            if (_cacheValid && Time.realtimeSinceStartup - _lastSnapshotTime < SNAPSHOT_CACHE_INTERVAL)
            {
                return _cachedSnapshot;
            }

            var snap = new GlobalStatsSnapshot();
            snap.TierDistribution = new int[14];
            snap.GeneDistribution = new Dictionary<string, int>();

            try
            {
                var units = World.world?.units?.units_only_alive;
                if (units != null)
                {
                    float turbSum = 0f;
                    int turbCount = 0;

                    foreach (var a in units)
                    {
                        try
                        {
                            if (a == null || !a.isAlive()) continue;
                            snap.TotalPopulation++;

                            int tier = CultivationData.GetRealmTier(a);
                            if (tier >= 0 && tier <= 13)
                                snap.TierDistribution[tier]++;

                            if (tier >= 1)
                            {
                                snap.TotalAwakened++;
                                turbSum += CultivationData.GetTurbulence(a);
                                turbCount++;
                                snap.TotalKills += CultivationData.GetKillsTotal(a);
                                snap.TotalAbilityKills += CultivationData.GetAbilityKills(a);
                                snap.TotalGeneLinkages += CultivationData.GetGeneLinkageCount(a);
                                int ht = CultivationData.GetHighestTierReached(a);
                                if (ht > snap.HighestTierReached) snap.HighestTierReached = ht;
                            }

                            // 基因分布（所有单位，不限于异能者但实际只有异能者有基因）
                            var unlocked = CultivationData.GetUnlockedElements(a);
                            if (unlocked != null && unlocked.Count > 0)
                            {
                                foreach (var eid in unlocked)
                                {
                                    if (string.IsNullOrEmpty(eid)) continue;
                                    if (snap.GeneDistribution.ContainsKey(eid))
                                        snap.GeneDistribution[eid]++;
                                    else
                                        snap.GeneDistribution[eid] = 1;

                                }
                            }
                        }
                        catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetStatsSnapshot 异常: " + dsEx.Message); }
                    }

                    if (turbCount > 0)
                        snap.AverageTurbulence = turbSum / turbCount;
                }
            }
            catch (Exception e) { DSDebug.Warning("GetStatsSnapshot遍历异常: " + e.Message); }

            // 非遍历类统计
            snap.AwakenedRatio = snap.TotalPopulation > 0
                ? (float)snap.TotalAwakened / snap.TotalPopulation
                : 0f;
            snap.TotalSects = GetTotalSects();
            snap.TotalDisasters = GetTotalDisasters();
            snap.TotalBreakthroughs = GetTotalBreakthroughs();
            snap.TotalAwakenings = GetTotalAwakenings();
            snap.TrueGodCount = GetTrueGodCount();
            snap.TotalAbilityUses = GetTotalAbilityUses();

            // 灾变系统实时状态
            try
            {
                snap.ActiveRifts = Disaster.DisasterManager.GetRiftCount();
                snap.BarrierIntegrity = Disaster.DisasterManager.GetBarrierIntegrity();
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetStatsSnapshot 异常: " + dsEx.Message); }

            // 性能优化：更新缓存
            _cachedSnapshot = snap;
            _cacheValid = true;
            _lastSnapshotTime = Time.realtimeSinceStartup;

            return snap;
        }

        // ============================================================
        //  境界分组统计辅助
        // ============================================================

        /// <summary>低阶(1-3)人数</summary>
        public static int GetLowTierCount(int[] tierDist)
        {
            return tierDist[1] + tierDist[2] + tierDist[3];
        }

        /// <summary>中阶(4-6)人数</summary>
        public static int GetMidTierCount(int[] tierDist)
        {
            return tierDist[4] + tierDist[5] + tierDist[6];
        }

        /// <summary>高阶(7-9)人数</summary>
        public static int GetHighTierCount(int[] tierDist)
        {
            return tierDist[7] + tierDist[8] + tierDist[9];
        }

        /// <summary>超高阶(10-12)人数</summary>
        public static int GetUltraTierCount(int[] tierDist)
        {
            return tierDist[10] + tierDist[11] + tierDist[12];
        }

        /// <summary>至高(13)人数</summary>
        public static int GetTrueTierCount(int[] tierDist)
        {
            return tierDist[13];
        }
    }
}

