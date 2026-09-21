// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Code.Core;
using Code.Data;
using Code.Traits;
using Code.UI;
using UnityEngine;

namespace Code.Realm
{
    public enum BreakthroughResult
    {
        Success,
        Fail_Mastery,      // 能力掌控度不足（替代印记，符合现代异能体系）
        Fail_GeneGraph,    // 基因图谱未全部解锁（基因图谱晋升系统）
        Fail_Qi,           // 异能能量不足
        Fail_Turbulence,   // 失控指数过高
        Fail_Enlightenment,// 深度觉醒经验不足（高阶）
        Fail_Risk,         // 突破风险失败
        Fail_AscensionLocked, // 成神被锁：成神条件未满足（≥12个基因、失控<50、深度觉醒经验≥500000）
        Fail_GeneCollapse,    // 基因崩溃：突破失败时基因序列彻底崩溃，单位死亡
        AlreadyMax,
        Invalid
    }

    public static class RealmJudge
    {
        /// <summary>根据ID查找Actor</summary>
        private static Actor FindActorById(long id)
        {
            try
            {
                if (World.world == null || World.world.units == null) return null;
                foreach (Actor actor in World.world.units.units_only_alive)
                {
                    if (actor != null && actor.getID() == id) return actor;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] FindActorById 异常: " + dsEx.Message); }
            return null;
        }
        // ============================================================
        //  13阶异能能量门槛（对应文档8.1节）
        // ============================================================
        private static readonly float[] _qiThresholds =
        {
            0f, 100f, 300f, 800f, 2000f, 5000f, 12000f,
            30000f, 80000f, 200000f, 500000f, 1500000f,
            4000000f, 10000000f
        };

        public static float GetQiThreshold(int tier)
        {
            if (tier < 0 || tier > 13) return float.MaxValue;
            return _qiThresholds[tier];
        }

        // ============================================================
        //  先驱者记录系统：每个境界第一个突破的人（全图唯一）
        // ============================================================
        private static readonly Dictionary<int, string> _pioneers = new Dictionary<int, string>();
        private static string _pioneerWorldName = null;  // 记录当前加载的世界名称，用于检测新世界创建
        private static bool _loadedFromSave = false;     // 刚通过PioneerSavePatcher从存档恢复先驱数据

        // ============================================================
        //  存档内存储内部方法（供PioneerSavePatcher调用）
        // ============================================================

        /// <summary>清除所有先驱者数据（内部方法）</summary>
        public static void ClearPioneersInternal()
        {
            _pioneers.Clear();
            // 世界未加载完成时不更新世界名标记（loadWorld是异步流程，Postfix阶段World.world可能仍为null，
            // 此时置null会导致读档后CheckAndRecordPioneer误判"新世界"而清空刚恢复的先驱记录）
            if (World.world != null) _pioneerWorldName = World.world.name;
        }

        /// <summary>标记刚从存档恢复先驱数据：读档后首次记录时只对齐世界名，不触发新世界重置</summary>
        public static void MarkLoadedFromSave()
        {
            _loadedFromSave = true;
        }

        /// <summary>设置先驱者（内部方法，不触发通知）</summary>
        public static void SetPioneerInternal(int tier, string name)
        {
            _pioneers[tier] = name;
        }

        /// <summary>获取所有先驱者数据（内部方法）</summary>
        public static Dictionary<int, string> GetAllPioneersInternal()
        {
            return new Dictionary<int, string>(_pioneers);
        }

        /// <summary>重置先驱记录（新世界/读档时调用）</summary>
        public static void ResetPioneers()
        {
            if (_loadedFromSave)
            {
                // 刚通过PioneerSavePatcher从存档恢复：不清空，只消费标记并对齐世界名
                // （否则读档后OnWorldLoaded会把恢复的先驱记录冲掉，导致首位被重新记录）
                _loadedFromSave = false;
                if (World.world != null) _pioneerWorldName = World.world.name;
                return;
            }
            _pioneers.Clear();
            if (World.world != null) _pioneerWorldName = World.world.name;
        }

        /// <summary>检查是否是先驱并记录，返回true表示是先驱</summary>
        private static bool CheckAndRecordPioneer(Actor actor, int tier)
        {
            // 检查当前世界名称是否变化（新世界创建时自动重置先驱记录）
            string currentWorldName = World.world != null ? World.world.name : null;

            if (_loadedFromSave)
            {
                // 刚从存档恢复：只对齐世界名，绝不能重置（否则读档后首位会被重新记录）
                _pioneerWorldName = currentWorldName;
                _loadedFromSave = false;
            }
            else if (currentWorldName != null && _pioneerWorldName != null && currentWorldName != _pioneerWorldName)
            {
                // 两个有效世界名不同：确为新世界，重置先驱记录
                _pioneers.Clear();
                _pioneerWorldName = currentWorldName;
            }
            else if (currentWorldName != null && _pioneerWorldName == null)
            {
                // 世界已加载但标记为空（冷启动首次记录）：对齐标记，不重置
                _pioneerWorldName = currentWorldName;
            }

            if (_pioneers.ContainsKey(tier)) return false;
            _pioneers[tier] = actor.getName();
            return true;
        }

        /// <summary>重置先驱记录（新世界/读档时调用）</summary>

        /// <summary>跳阶首位突破通知：越级突破使用特殊文案</summary>
        /// <param name="actor">突破的单位</param>
        /// <param name="tier">突破到的境界</param>
        /// <param name="fromTier">突破前的境界</param>
        public static void OnSkipTierPioneer(Actor actor, int tier, int fromTier)
        {
            try
            {
                if (actor == null || !actor.isAlive()) return;
                if (tier <= 0 || tier > 13) return;
                if (tier - fromTier <= 1) return; // 不是跳阶

                // 跳阶同样触发对应境界的事件（天劫、异象、全局效果等），与正常突破一致
                RealmEvents.OnBreakthrough(actor, tier);

                // 检查是否是该境界的首位突破者
                bool isFirst = CheckAndRecordPioneer(actor, tier);

                string tierName = UILocalization.GetTierName(tier);
                string actorName = TitleSuffixManager.ExtractBaseName(actor.getName());

                if (isFirst)
                {
                    // 跳阶首位突破：使用越级突破特殊文案
                    DSNotificationManager.NotifyInfo(
                        DSEventTexts.GetSkipPioneerFullTitle(actorName, tier, fromTier));

                    // 收束合并进主记录：标题=越级描述（含单位名），描述=收束语（传入单位名字替换"他"）
                    string skipEpilogue = UILocalization.Get("Divine_" + tier + "_epilogue");
                    if (string.IsNullOrEmpty(skipEpilogue) || skipEpilogue == "Divine_" + tier + "_epilogue") skipEpilogue = null;
                    string formattedSkipEpilogue = skipEpilogue != null ? string.Format(skipEpilogue, actorName) : null;
                    DSEventManager.RecordEvent(
                        DSEventType.Breakthrough,
                        DSEventTexts.GetSkipPioneerHistoryDesc(actorName, tier, tierName, fromTier),
                        formattedSkipEpilogue,
                        actor.getName(),
                        ActorDataAccessor.GetData(actor).id, tier, actor, true);

                    // 播放突破音乐（如果安装了God's Gramophone模组）

                    DSDebug.Verbose($"[DivineAscension] {actorName} 跳阶首位突破：{fromTier}阶 -> {tier}阶{tierName}");
                }
                else
                {
                    // 非首位跳阶：11阶以上才弹通知，10阶及以下只记录历史不弹通知
                    if (tier >= 11)
                    {
                        DSNotificationManager.NotifyInfo(
                            string.Format(UILocalization.Get("notify_breakthrough"), actorName, tier, tierName));
                    }

                    string fromTierName = UILocalization.GetTierName(fromTier);
                    // 历史记录标题：单位名 突破到X阶 境界名
                    string skipHistoryTitle = string.Format(UILocalization.Get("notify_breakthrough"), actorName, tier, tierName);
                    // 历史记录描述：越级说明 + 收束语（如果有）
                    string skipDesc = string.Format(UILocalization.Get("skip_breakthrough_history_desc"), fromTier, fromTierName);
                    string skipEpilogue = UILocalization.Get("Divine_" + tier + "_epilogue");
                    if (string.IsNullOrEmpty(skipEpilogue) || skipEpilogue == "Divine_" + tier + "_epilogue") skipEpilogue = null;
                    string skipHistoryDesc = skipDesc;
                    if (!string.IsNullOrEmpty(skipEpilogue))
                    {
                        string formattedSkipEpilogue = string.Format(skipEpilogue, actorName);
                        skipHistoryDesc += "\n" + formattedSkipEpilogue;
                    }
                    DSEventManager.RecordEvent(
                        DSEventType.Breakthrough,
                        skipHistoryTitle,
                        skipHistoryDesc,
                        actor.getName(),
                        ActorDataAccessor.GetData(actor).id, tier, actor, true);

                    DSDebug.Verbose($"[DivineAscension] {actorName} 跳阶突破：{fromTier}阶 -> {tier}阶{tierName}");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"跳阶突破通知失败: {e.Message}");
            }
        }

        /// <summary>获取先驱者记录文件路径</summary>

        // ============================================================
        //  动态超神纪元系统：哪个真神主宰纪元，纪元就是他的
        //  新真神降世挑战当前主宰胜者主宰纪元（纪元名动态变更）
        //  主宰陨落纪元更迭（纪元数+1，进入新纪元等待新主宰）
        // ============================================================

        /// <summary>纪元主宰信息</summary>
        public class EraDominator
        {
            public string Name;        // 主宰名字
            public string ActorId;     // 主宰ActorID
            public float Energy;       // 降世时异能能量（用于实力比较）
            public int ElementCount;   // 掌握基因数
            public long AscendTime;    // 降世时间（世界年）
            public int ElementGroup;   // 觉醒第二基因的染色体（决定纪元效果）
        }

        // ============================================================
        // 纪元主宰全局效果（由纪元主宰的主要基因染色体决定）
        //  不同染色体的主宰有不同的纪元加成
        // ============================================================

        /// <summary>纪元效果数据</summary>
        public class EraEffect
        {
            public string Name;
            public string Description;
            public float CultivationBonus;
            public float EnergyGainBonus;
            public float BreakthroughBonus;
            public float CombatBonus;
            public float LifeRegenBonus;
            public float ElementUnlockBonus;
        }

        /// <summary>根据主要基因染色体获取纪元效果</summary>
        public static EraEffect GetEraEffect(int elementGroup)
        {
            switch (elementGroup)
            {
                case 0: return new EraEffect { Name=UILocalization.Get("era_matter"), Description=UILocalization.Get("era_matter_desc"), CultivationBonus=0.05f, EnergyGainBonus=0.05f, BreakthroughBonus=0.03f, CombatBonus=0.10f, LifeRegenBonus=0.15f, ElementUnlockBonus=0.02f };
                case 1: return new EraEffect { Name=UILocalization.Get("era_life"), Description=UILocalization.Get("era_life_desc"), CultivationBonus=0.10f, EnergyGainBonus=0.05f, BreakthroughBonus=0.05f, CombatBonus=0.05f, LifeRegenBonus=0.25f, ElementUnlockBonus=0.05f };
                case 2: return new EraEffect { Name=UILocalization.Get("era_energy"), Description=UILocalization.Get("era_energy_desc"), CultivationBonus=0.05f, EnergyGainBonus=0.20f, BreakthroughBonus=0.03f, CombatBonus=0.15f, LifeRegenBonus=0.05f, ElementUnlockBonus=0.03f };
                case 3: return new EraEffect { Name=UILocalization.Get("era_force"), Description=UILocalization.Get("era_force_desc"), CultivationBonus=0.05f, EnergyGainBonus=0.05f, BreakthroughBonus=0.03f, CombatBonus=0.12f, LifeRegenBonus=0.05f, ElementUnlockBonus=0.02f };
                case 4: return new EraEffect { Name=UILocalization.Get("era_spacetime"), Description=UILocalization.Get("era_spacetime_desc"), CultivationBonus=0.15f, EnergyGainBonus=0.10f, BreakthroughBonus=0.10f, CombatBonus=0.05f, LifeRegenBonus=0.05f, ElementUnlockBonus=0.05f };
                case 5: return new EraEffect { Name=UILocalization.Get("era_spirit"), Description=UILocalization.Get("era_spirit_desc"), CultivationBonus=0.20f, EnergyGainBonus=0.05f, BreakthroughBonus=0.08f, CombatBonus=0.08f, LifeRegenBonus=0.05f, ElementUnlockBonus=0.08f };
                case 6: return new EraEffect { Name=UILocalization.Get("era_info"), Description=UILocalization.Get("era_info_desc"), CultivationBonus=0.10f, EnergyGainBonus=0.05f, BreakthroughBonus=0.05f, CombatBonus=0.05f, LifeRegenBonus=0.05f, ElementUnlockBonus=0.15f };
                case 7: return new EraEffect { Name=UILocalization.Get("era_concept"), Description=UILocalization.Get("era_concept_desc"), CultivationBonus=0.10f, EnergyGainBonus=0.10f, BreakthroughBonus=0.08f, CombatBonus=0.20f, LifeRegenBonus=0.10f, ElementUnlockBonus=0.10f };
                default: return new EraEffect { Name=UILocalization.Get("era_chaos"), Description=UILocalization.Get("judge_chaos_era_desc"), CultivationBonus=0f, EnergyGainBonus=0f, BreakthroughBonus=0f, CombatBonus=0f, LifeRegenBonus=0f, ElementUnlockBonus=0f };
            }
        }

        /// <summary>获取当前纪元效果</summary>
        public static EraEffect GetCurrentEraEffect()
        {
            if (_currentDominator != null) return GetEraEffect(_currentDominator.ElementGroup);
            return GetEraEffect(-1);
        }

        private static int _currentEra = 1;
        private const int MAX_TRUEGOD_COUNT = 3; // 13阶超神级：一个世界最多3个
        private const int MAX_ASCENDED_COUNT = 7; // 12阶真神级：一个世界最多7个
        private const int MAX_APOSTLE_COUNT = 64; // 10阶使徒级：一个世界最多64个
        private const int MAX_DOMAIN_LORD_COUNT = 16; // 11阶登神级：一个世界最多16个
        
        // 境界计数缓存：key=境界，value=存活数量
        // 突破/死亡时更新，避免每次都遍历所有单位
        private static readonly Dictionary<int, int> _tierCountCache = new Dictionary<int, int>();
        private static bool _tierCacheDirty = true; // 标记缓存是否需要重建
        
        private static EraDominator _currentDominator = null;
        // 纪元历史：key=纪元数，value=该纪元所有主宰列表（按时间顺序）
        private static readonly Dictionary<int, List<EraDominator>> _eraHistory =
            new Dictionary<int, List<EraDominator>>();

        /// <summary>获取当前纪元数（从1开始）</summary>
        public static int GetCurrentEra() { return _currentEra; }

        /// <summary>获取当前纪元主宰（null表示无主宰）</summary>
        public static EraDominator GetCurrentDominator() { return _currentDominator; }

        /// <summary>获取当前世界的真神数量（用缓存）</summary>
        public static int GetWorldTrueGodCount()
        {
            return GetTierCount(13);
        }

        /// <summary>获取当前世界的真神级（12阶）数量（用缓存）</summary>
        public static int GetWorldAscendedCount()
        {
            return GetTierCount(12);
        }

        /// <summary>获取指定境界的存活数量上限</summary>
        public static int GetTierCap(int tier)
        {
            if (tier >= 13) return MAX_TRUEGOD_COUNT;
            if (tier == 12) return MAX_ASCENDED_COUNT;
            if (tier == 11) return MAX_DOMAIN_LORD_COUNT;
            if (tier == 10) return MAX_APOSTLE_COUNT;
            return int.MaxValue; // 9阶及以下无限制
        }

        /// <summary>
        /// 启动时修剪溢出的高阶单位（两者结合方案）
        /// 如果超过上限+5，杀掉最弱小的，降到上限+5，然后自然淘汰
        /// </summary>
        public static void TrimTierOverflow()
        {
            try
            {
                // 检查13阶超神级
                int trueGodCount = GetWorldTrueGodCount();
                int trueGodCap = MAX_TRUEGOD_COUNT;
                int trueGodSoftCap = trueGodCap + 3; // 软上限=上限+3
                if (trueGodCount > trueGodSoftCap)
                {
                    TrimTierToSoftCap(13, trueGodSoftCap);
                }

                // 检查12阶真神级
                int ascendedCount = GetWorldAscendedCount();
                int ascendedCap = MAX_ASCENDED_COUNT;
                int ascendedSoftCap = ascendedCap + 3; // 软上限=上限+3
                if (ascendedCount > ascendedSoftCap)
                {
                    TrimTierToSoftCap(12, ascendedSoftCap);
                }

                // 检查11阶登神级
                int domainLordCount = CountAliveActorsAtTier(11);
                int domainLordCap = MAX_DOMAIN_LORD_COUNT;
                int domainLordSoftCap = domainLordCap + 3; // 软上限=上限+3
                if (domainLordCount > domainLordSoftCap)
                {
                    TrimTierToSoftCap(11, domainLordSoftCap);
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 修剪溢出高阶单位失败: {e.Message}");
            }
        }

        /// <summary>将指定境界的单位数量修剪到软上限（杀掉最弱小的）</summary>
        private static void TrimTierToSoftCap(int tier, int softCap)
        {
            var actorsAtTier = new List<Actor>();
            foreach (var actor in World.world.units)
            {
                if (actor == null || !actor.isAlive()) continue;
                int actorTier = CultivationData.GetRealmTier(actor);
                if ((tier >= 13 && actorTier >= 13) || (tier == 12 && actorTier == 12) || (tier == 11 && actorTier == 11))
                {
                    actorsAtTier.Add(actor);
                }
            }

            if (actorsAtTier.Count <= softCap) return;

            // 按战力排序（升序，最弱的在前）
            actorsAtTier.Sort((a, b) => CalculateCombatPower(a).CompareTo(CalculateCombatPower(b)));

            // 杀掉最弱小的，直到降到软上限
            int toKill = actorsAtTier.Count - softCap;
            string tierName = UILocalization.GetTierName(tier);
            DSDebug.Warning($"[DivineAscension] {tierName}数量溢出（{actorsAtTier.Count}人），修剪最弱的{toKill}人，降到{softCap}人");

            for (int i = 0; i < toKill; i++)
            {
                var weakActor = actorsAtTier[i];
                if (weakActor != null && weakActor.isAlive())
                {
                    // 直接杀死（不触发战斗，因为是启动时的清理）
                    try
                    {
                        var killMethod = weakActor.GetType().GetMethod("kill",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (killMethod != null)
                        {
                            killMethod.Invoke(weakActor, new object[] { null, null });
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>计算单位的战力（用于挑战上位的胜率计算）</summary>
        /// <remarks>
        /// 战力 = 最大生命值 × (1 + 攻击力/100) × (1 + 境界加成) × (1 + 基因能量加成)
        /// 强者更容易获胜，但保留一定随机性（弱者也有逆袭的可能）
        /// </remarks>
        private static float CalculateCombatPower(Actor actor)
        {
            if (actor == null) return 1f;
            try
            {
                // 基础属性
                float maxHealth = actor.getMaxHealth();
                float attack = 0f;
                float defense = 0f;
                try
                {
                    // 通过反射获取攻击力和防御力（stats字典）
                    var statsField = actor.GetType().GetField("stats",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (statsField != null)
                    {
                        var stats = statsField.GetValue(actor);
                        if (stats != null)
                        {
                            var attackProp = stats.GetType().GetProperty("attack");
                            var defenseProp = stats.GetType().GetProperty("defense");
                            if (attackProp != null) attack = (float)attackProp.GetValue(stats);
                            if (defenseProp != null) defense = (float)defenseProp.GetValue(stats);
                        }
                    }
                }
                catch { /* 反射失败时使用默认值 */ }

                // 境界加成：每阶+10%
                int tier = CultivationData.GetRealmTier(actor);
                float tierBonus = 1f + tier * 0.10f;

                // 基因能量加成（GE公式）
                float geBonus = 1f;
                try
                {
                    geBonus += Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
                }
                catch { /* GE计算失败时使用默认值 */ }

                // 战力 = 最大生命值 × (1 + 攻击力/100) × (1 + 防御力/200) × 境界加成 × 基因能量加成
                float power = maxHealth * (1f + attack / 100f) * (1f + defense / 200f) * tierBonus * geBonus;
                return Mathf.Max(1f, power);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>通过反射获取单位属性值（stats字典）</summary>
        private static float GetActorStat(Actor actor, string statName, float defaultValue)
        {
            if (actor == null) return defaultValue;
            try
            {
                // 通过反射获取 stats 字典
                var statsField = actor.GetType().GetField("stats",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (statsField == null) return defaultValue;
                var stats = statsField.GetValue(actor);
                if (stats == null) return defaultValue;

                // 尝试按字典取值
                var statsType = stats.GetType();
                var indexer = statsType.GetProperty("Item");
                if (indexer != null)
                {
                    try
                    {
                        object val = indexer.GetValue(stats, new object[] { statName });
                        if (val != null) return System.Convert.ToSingle(val);
                    }
                    catch { /* 键不存在 */ }
                }

                // 回退：尝试属性访问
                var prop = statsType.GetProperty(statName);
                if (prop != null)
                {
                    object val = prop.GetValue(stats);
                    if (val != null) return System.Convert.ToSingle(val);
                }
            }
            catch { /* 反射失败 */ }
            return defaultValue;
        }

        /// <summary>
        /// 确保指定境界有空位。如果达到上限，随机杀死一个同境界单位腾出位置。
        /// 弱肉强食：高位阶名额有限，后来者要上位，旧人就得陨落。
        /// </summary>
        /// <param name="tier">目标境界</param>
        /// <param name="excludeActor">要突破的单位（不杀自己）</param>
        /// <returns>true表示有空位（或已杀出人空位），false表示无法腾出空位</returns>
        public static bool EnsureTierSlot(int tier, Actor excludeActor)
        {
            int cap = GetTierCap(tier);
            if (cap == int.MaxValue) return true; // 无限制境界

            // 计算当前境界的存活数量
            int currentCount = 0;
            if (tier >= 13)
                currentCount = GetWorldTrueGodCount();
            else if (tier == 12)
                currentCount = GetWorldAscendedCount();
            else if (tier == 11)
                currentCount = CountAliveActorsAtTier(11);
            else if (tier == 10)
                currentCount = CountAliveActorsAtTier(10);
            if (currentCount < cap) return true; // 还有空位

            // 年度挑战次数限制：每年最多触发MaxChallengesPerYear次挑战上位
            int currentYear = (int)(World.world.getCurWorldTime() / 365.0);
            if (currentYear != _lastChallengeYear)
            {
                _lastChallengeYear = currentYear;
                _yearlyChallengeCount = 0;
            }
            if (_yearlyChallengeCount >= MaxChallengesPerYear)
            {
                DSDebug.Verbose($"[DivineAscension] {UILocalization.GetTierName(tier)}阶年度挑战次数已用完（{MaxChallengesPerYear}次），{excludeActor?.getName()} 今年无法挑战");
                return false;
            }
            _yearlyChallengeCount++;

            // 达到上限，突破者必须主动挑战一个同境界单位，胜者上位
            var candidates = new System.Collections.Generic.List<Actor>();
            foreach (var actor in World.world.units)
            {
                if (actor == null || !actor.isAlive()) continue;
                if (actor == excludeActor) continue;
                int actorTier = CultivationData.GetRealmTier(actor);
                if ((tier >= 13 && actorTier >= 13) || 
                    (tier == 12 && actorTier == 12) || 
                    (tier == 11 && actorTier == 11) ||
                    (tier == 10 && actorTier == 10))
                {
                    candidates.Add(actor);
                }
            }

            if (candidates.Count == 0)
            {
                DSDebug.Verbose($"[DivineAscension] {tier}阶名额已满（{cap}人），但找不到可挑战的同阶单位，{excludeActor?.getName()} 无法突破");
                return false;
            }

            // 突破者选择一个同境界单位挑战（随机选择目标）
            Actor victim = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            string tierName = UILocalization.GetTierName(tier);
            string victimName = victim.getName();
            string challengerName = excludeActor?.getName() ?? "未知";

            // 挑战者进入"破境之势"状态（临时属性加成，战斗结束后消失）
            // 11阶挑战12阶需要巨大加成，否则永远打不过
            try { excludeActor.addTrait(TraitManager.STATUS_BREAKTHROUGH_MOMENTUM); } catch { }

            // 直接触发原版战斗，让两个单位真的打，原版战斗系统自己决定胜负
            // 不再离线模拟算胜负
            StartChallengeDuelCoroutineReal(excludeActor, victim, tier);
            return true; // 暂时返回true，战斗结果在协程里处理
        }

        /// <summary>启动真正的原版战斗协程（不提前算胜负，让原版战斗系统自己打）</summary>
        private static void StartChallengeDuelCoroutineReal(Actor challenger, Actor defender, int tier)
        {
            try
            {
                // 找到AnnualTickDriver实例
                var driver = GameObject.FindObjectOfType<Code.Core.AnnualTickDriver>();
                if (driver != null)
                {
                    driver.StartCoroutine(driver.ChallengeDuelRealCoroutine(challenger, defender, tier));
                }
                else
                {
                    DSDebug.Warning("[DivineAscension] 找不到AnnualTickDriver，挑战战斗无法启动");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 启动战斗协程失败: {e.Message}");
            }
        }

        /// <summary>延迟移除破境之势状态</summary>
        private static System.Collections.IEnumerator RemoveBreakthroughMomentumDelayed(Actor challenger, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            try { challenger?.removeTrait(TraitManager.STATUS_BREAKTHROUGH_MOMENTUM); } catch { }
        }

        /// <summary>通过挑战杀死单位（触发正常死亡流程）</summary>
        private static void KillActorByChallenge(Actor victim, Actor killer)
        {
            try
            {
                if (victim == null || !victim.isAlive()) return;

                try
                {
                    // 直接调用 getHit 方法（publicized环境下直接访问）
                    victim.getHit(99999f, false, AttackType.Other, killer, false, false, true);
                }
                catch
                {
                    // 备用方案：直接设置血量为0
                    var actorData = ActorDataAccessor.GetData(victim);
                    if (actorData != null)
                    {
                        actorData.health = 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 挑战击杀 {victim?.getName()} 失败: {e.Message}");
            }
        }

        /// <summary>获取纪元名称（动态：第N纪元·当前主宰名；无主宰则第N纪元·混沌）</summary>
        public static string GetEraName(int era)
        {
            if (era == _currentEra && _currentDominator != null)
                return string.Format(UILocalization.Get("judge_era_name_format"), era, _currentDominator.Name);
            // 历史纪元：取该纪元最后一个主宰
            if (_eraHistory.TryGetValue(era, out var list) && list.Count > 0)
                return string.Format(UILocalization.Get("judge_era_name_format"), era, list[list.Count - 1].Name);
            return string.Format(UILocalization.Get("judge_era_chaos_format"), era);
        }

        /// <summary>新真神降世：挑战当前主宰，胜者成为纪元主宰。返回true表示挑战成功成为新主宰</summary>
        public static bool ChallengeDominator(Actor actor)
        {
            float actorEnergy = CultivationData.GetEnergy(actor);
            int actorElements = CultivationData.GetUnlockedElements(actor)?.Count ?? 0;

            // 无当前主宰  直接成为主宰
            if (_currentDominator == null)
            {
                SetDominator(actor, actorEnergy, actorElements);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 成为第{_currentEra}纪元首位主宰！");
                return true;
            }

            // 挑战当前主宰：用原版战斗系统真打一架
            Actor dominator = SystemManagerExtensions.FindActorById(long.Parse(_currentDominator.ActorId));
            if (dominator == null || !dominator.isAlive())
            {
                // 旧主宰死了，直接上位
                SetDominator(actor, actorEnergy, actorElements);
                DSDebug.Verbose($"[DivineAscension] 旧主宰已死，{actor.getName()} 直接成为新主宰！");
                return true;
            }

            // 调用原版战斗系统：挑战者和主宰真刀真枪打一架
            try
            {
                actor.startFightingWith(dominator);
                dominator.startFightingWith(actor);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 挑战 {dominator.getName()}，原版战斗开始！");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[DivineAscension] 触发原版战斗失败，回退到数值对比: {e.Message}");
            }

            // 胜负判定：谁先死或谁跑了谁输
            // 简单版：等几秒看谁死了（后面改成真正监听战斗结束事件）
            // 现在先检查：如果旧主宰死了，挑战者直接赢
            if (dominator == null || !dominator.isAlive())
            {
                // 旧主宰死了，挑战者赢
                string oldName = _currentDominator.Name;
                SetDominator(actor, actorEnergy, actorElements);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 击杀 {oldName}，成为第{_currentEra}纪元新主宰！");
                return true;
            }

            // 如果挑战者死了，挑战者输
            if (!actor.isAlive())
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 战斗中死亡，挑战失败");
                return false;
            }

            // 都没死的话，异步等待战斗结束，用原版战斗结果判定
            WaitForCombatAndDecide(actor, dominator, actorEnergy, actorElements);
            return true; // 先返回true，等战斗结束再真正判定
        }
        /// <summary>异步等待战斗结束，然后判定胜负</summary>
        public static void WaitForCombatAndDecide(Actor challenger, Actor dominator, float challengerEnergy, int challengerElements)
        {
            // 简单版：等10秒，然后检查谁死了
            // 后面改成真正监听战斗结束事件
            DSDebug.Verbose($"[DivineAscension] 战斗开始，等待10秒后判定胜负...");

            // 用AnnualTickManager的每年更新来检查（游戏内的时间）
            // 现在先简单版：直接判定
            System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ =>
            {
                try
                {
                    if (dominator == null || !dominator.isAlive())
                    {
                        // 旧主宰死了，挑战者赢
                        SetDominator(challenger, challengerEnergy, challengerElements);
                        DSDebug.Verbose($"[DivineAscension] {challenger.getName()} 击杀主宰，成为新主宰！");
                    }
                    else if (!challenger.isAlive())
                    {
                        // 挑战者死了
                        DSDebug.Verbose($"[DivineAscension] {challenger.getName()} 战斗中死亡，挑战失败");
                    }
                    else
                    {
                        // 检查谁跑了（原版逃亡任务id是run_away）
                        bool challengerFled = false;
                        try { challengerFled = challenger.hasTask() && challenger.ai.task.id == "run_away"; } catch { }
                        bool dominatorFled = false;
                        try { dominatorFled = dominator.hasTask() && dominator.ai.task.id == "run_away"; } catch { }

                        if (challengerFled)
                        {
                            // 挑战者跑了 → 终身不能突破，阶位回退
                            DSDebug.Verbose($"[DivineAscension] 挑战者 {challenger.getName()} 逃跑了！终身不能突破，阶位回退");
                            try { DemoteActor(challenger, CultivationData.GetRealmTier(challenger) - 1); } catch { }
                        }
                        else if (dominatorFled)
                        {
                            // 主宰跑了 → 主宰输，阶位回退，挑战者上位
                            DSDebug.Verbose($"[DivineAscension] 主宰 {dominator.getName()} 逃跑了！阶位回退");
                            try { DemoteActor(dominator, CultivationData.GetRealmTier(dominator) - 1); } catch { }
                            SetDominator(challenger, challengerEnergy, challengerElements);
                            DSDebug.Verbose($"[DivineAscension] {challenger.getName()} 击败逃跑的主宰，成为新主宰！");
                        }
                        else
                        {
                            // 都没死也没跑，先结束战斗，按数值判定
                            challenger.clearAttackTarget();
                            dominator.clearAttackTarget();
                            DSDebug.Verbose($"[DivineAscension] 战斗结束，双方都活着，按数值判定");
                        }
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Verbose($"[DivineAscension] 异步判定失败: {e.Message}");
                }
            });
        }

        /// <summary>挑战失败进入虚弱期：全属性降低50%，能量恢复降低，持续20年，期间无法再次挑战主宰</summary>
        private static void ExileChallenger(Actor actor)
        {
            try
            {
                // 添加虚弱期状态效果（持续20年，期间无法再次挑战主宰）
                try
                {
                    DSReflectionHelper.SafeAddStatusEffect(actor, "ds_era_weakened", 1200f, true);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 挑战主宰失败，进入虚弱期（20年）");
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExileChallenger 异常: " + dsEx.Message); }

                // 通知
                try
                {
                    Code.Core.DSNotificationManager.NotifyMajor(UILocalization.Get("judge_challenge_failed_title"),
                        string.Format(UILocalization.Get("judge_challenge_failed_desc"), actor.getName(), _currentDominator.Name));
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExileChallenger 异常: " + dsEx.Message); }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"虚弱期处理失败: {e.Message}");
            }
        }

        /// <summary>设置当前主宰并记录到历史</summary>
        private static void SetDominator(Actor actor, float energy, int elements)
        {            // 移除旧主宰的加成状态和专属特质（如果有）
            if (_currentDominator != null)
            {
                try
                {
                    long oldActorId = long.Parse(_currentDominator.ActorId);
                    Actor oldActor = FindActorById(oldActorId);
                    if (oldActor != null && oldActor.isAlive())
                    {
                        Code.Core.DSStatusManager.RemoveEraDominatorBuff(oldActor);
                        // 移除旧主宰的专属特质
                        try
                        {
                            oldActor.removeTrait("ds_era_dominator_trait");
                        }
                        catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetDominator 异常: " + dsEx.Message); }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetDominator 异常: " + dsEx.Message); }
            }

            // 获取主宰的主要基因染色体（从已解锁基因中选择出现最多的染色体）
            int dominatorGroup = 0;
            try {
                var unlocked = CultivationData.GetUnlockedElements(actor);
                if (unlocked != null && unlocked.Count > 0) {
                    int[] groupCount = new int[8];
                    foreach (string eid in unlocked) {
                        var elem = ElementDef.GetById(eid);
                        if (elem != null) groupCount[elem.Group]++;
                    }
                    int maxCount = 0;
                    for (int i = 0; i < 8; i++) {
                        if (groupCount[i] > maxCount) { maxCount = groupCount[i]; dominatorGroup = i; }
                    }
                }
            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetDominator 异常: " + dsEx.Message); }

            _currentDominator = new EraDominator
            {
                Name = actor.getName(),
                ActorId = ActorDataAccessor.GetData(actor).id.ToString(),
                Energy = energy,
                ElementCount = elements,
                AscendTime = (long)Code.Core.DSReflectionHelper.GetWorldTime(),
                ElementGroup = dominatorGroup
            };
            // 记录到当前纪元历史
            if (!_eraHistory.ContainsKey(_currentEra))
                _eraHistory[_currentEra] = new List<EraDominator>();
            _eraHistory[_currentEra].Add(_currentDominator);
            // 给新主宰添加纪元主宰加成状态
            Code.Core.DSStatusManager.AddEraDominatorBuff(actor);

            // 给新主宰添加纪元主宰专属唯一特质
            try
            {
                actor.addTrait("ds_era_dominator_trait");
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 获得纪元主宰专属特质");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"添加纪元主宰特质失败: {e.Message}");
            }
        }

                /// <summary>统一处理超神诞生（所有突破到13阶的入口都调这个）</summary>
        public static void HandleTrueGodBirth(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            try
            {
                // 跨存档保存
                TrueGodManager.OnTrueGodAscended(actor);
                RealmMechanics.OnTrueGodAscended(actor);
                // 对接原版时代系统：突破超神级时自动切换到超神纪元
                Code.Realm.EraBridge.OnTranscendenceBreakthrough(13);

                // 突破通知
                DSNotificationManager.NotifyMajor(UILocalization.Get("judge_true_god_ultimate"),
                    string.Format(UILocalization.Get("judge_true_god_ultimate_desc"), actor.getName()));
                DSEventManager.RecordEvent(
                    DSEventType.Divine,
                    UILocalization.Get("judge_true_god_birth_title"),
                    string.Format(UILocalization.Get("judge_true_god_birth_desc"), actor.getName()),
                    actor.getName(),
                    ActorDataAccessor.GetData(actor).id, 13, actor);
            }
            catch (Exception e)
            {
                DSDebug.Warning($"超神诞生处理失败: {e.Message}");
            }
        }

        /// <summary>主宰陨落：纪元更迭（纪元数+1，进入新纪元等待新主宰）</summary>
        public static void OnDominatorDeath(string actorId)
        {            // 移除陨落主宰的加成状态
            try
            {
                long deadActorId = long.Parse(actorId);
                Actor deadActor = FindActorById(deadActorId);
                if (deadActor != null)
                {
                    Code.Core.DSStatusManager.RemoveEraDominatorBuff(deadActor);
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnDominatorDeath 异常: " + dsEx.Message); }

            if (_currentDominator == null || _currentDominator.ActorId != actorId) return;

            string deadName = _currentDominator.Name;
            _currentDominator = null;
            _currentEra++;
            DSDebug.Verbose($"[DivineAscension] {deadName} 陨落，第{_currentEra - 1}纪元终结，第{_currentEra}纪元开启（混沌无主）");
        }

        /// <summary>获取某纪元的主宰历史列表</summary>
        public static List<EraDominator> GetEraHistory(int era)
        {
            return _eraHistory.TryGetValue(era, out var list) ? list : new List<EraDominator>();
        }

        /// <summary>重置纪元（新世界/读档时调用）</summary>
        public static void ResetEras()
        {
            _currentEra = 1;
            _currentDominator = null;
            _eraHistory.Clear();
        }

        // ============================================================
        //  超神纪元注册（参考Avb超级特质包虚无纪元实现）
        //  创建WorldAgeAsset  设置属性  添加到AssetManager.era_library
        //  真神降世/主宰变更时切换到此纪元
        // ============================================================

        private static bool _trueGodEraRegistered = false;

        // 年度挑战次数限制：每年最多触发N次挑战上位，避免一年死一堆人
        private static int _yearlyChallengeCount = 0;
        private static int _lastChallengeYear = -1;
        private const int MaxChallengesPerYear = 3; // 每年最多3次挑战上位

        /// <summary>注册超神纪元到游戏原生era_library（参考Avb虚无纪元实现）</summary>
        public static void RegisterTranscendentEra()
        {
            if (_trueGodEraRegistered) return;
            try
            {
                WorldAgeAsset trueGodEra = new WorldAgeAsset();
                trueGodEra.id = "age_ds_transcendent";
                trueGodEra.path_icon = "otherAssets/iconAgeTrueGod";
                trueGodEra.path_background = "otherAssets/ageTrueGodBackground";
                trueGodEra.title_color = Toolbox.makeColor("#6B8EFF");
                trueGodEra.rate = 2;
                trueGodEra.overlay_darkness = false;
                trueGodEra.era_effect_overlay_alpha = 0.3f;
                trueGodEra.cloud_interval = 5f; // 云生成间隔从1.5秒改为5秒，减少乌云密度
                trueGodEra.bonus_biomes_growth = 1;
                trueGodEra.special_effect_interval = 8f; // 粒子特效间隔8秒
                trueGodEra.clouds = Toolbox.splitStringIntoList("cloud_magic#1"); // 魔法云从3个减为1个，去掉闪电云
                // 添加超神纪元粒子特效（神圣光芒+附魔闪光）
                trueGodEra.special_effect_action = (WorldAgeAction)Delegate.Combine(
                    trueGodEra.special_effect_action,
                    new WorldAgeAction(TrySpawnDivineParticles));
                AssetManager.era_library.add(trueGodEra);
                _trueGodEraRegistered = true;
                DSDebug.Verbose("[DivineAscension] 超神纪元已注册到era_library（id=age_ds_transcendent，紫蓝冷色调，1.5倍速，神圣粒子特效）");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 超神纪元注册失败: " + e.Message);
            }
        }

        /// <summary>超神纪元粒子特效：随机在地图上生成神圣光芒/附魔闪光粒子</summary>
        /// <remarks>参考Avb虚无纪元的trySpawnThunder实现，使用EffectsLibrary.spawn生成原版粒子特效</remarks>
        private static void TrySpawnDivineParticles()
        {
            try
            {
                if (!MapBox.isRenderGameplay()) return;
                // 30%概率生成粒子特效
                if (UnityEngine.Random.value >= 0.3f) return;

                // 在地图随机位置生成粒子特效
                int x = UnityEngine.Random.Range(0, Mathf.Max(1, MapBox.width - 1));
                int y = UnityEngine.Random.Range(0, Mathf.Max(1, MapBox.height - 1));

                // 用反射获取tile
                object tile = null;
                try
                {
                    var getTileMethod = World.world.GetType().GetMethod("GetTile",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getTileMethod != null)
                    {
                        tile = getTileMethod.Invoke(World.world, new object[] { x, y });
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] TrySpawnDivineParticles 异常: " + dsEx.Message); }

                if (tile == null) return;

                // 随机选择粒子特效类型
                int effectType = UnityEngine.Random.Range(0, 3);
                string effectName = "fx_enchanted_sparkle";
                switch (effectType)
                {
                    case 0: effectName = "fx_enchanted_sparkle"; break;
                    case 1: effectName = "fx_cast_top_blue"; break;
                    case 2: effectName = "fx_building_sparkle"; break;
                }

                // 用反射调用EffectsLibrary.spawn（避免dynamic类型编译问题）
                try
                {
                    var spawnMethods = typeof(EffectsLibrary).GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    foreach (var m in spawnMethods)
                    {
                        if (m.Name == "spawn" && m.GetParameters().Length == 8)
                        {
                            m.Invoke(null, new object[] { effectName, tile, null, null, 0f, -1f, -1f, null });
                            break;
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] TrySpawnDivineParticles 异常: " + dsEx.Message); }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[DivineAscension] 超神纪元粒子特效生成失败: " + e.Message);
            }
        }

        /// <summary>切换到超神纪元（真神降世/主宰变更时调用）</summary>
        public static void SwitchToTrueGodEra()
        {            // 没有真神则锁住超神纪元，不能切换
            if (!RealmFeatures.HasTrueGodInWorld())
            {
                DSDebug.Verbose("[DivineAscension] 世界上没有真神，超神纪元已锁定，无法切换");
                return;
            }

            try
            {
                var eraManager = Code.Core.DSReflectionHelper.GetEraManager();
                if (eraManager == null) return;
                var setAgeMethod = eraManager.GetType().GetMethod("setCurrentAge",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setAgeMethod != null)
                {
                    var trueGodAge = AssetManager.era_library.get("age_ds_transcendent");
                    if (trueGodAge != null)
                    {
                        setAgeMethod.Invoke(eraManager, new object[] { trueGodAge, true });
                        DSDebug.Verbose("[DivineAscension] 已切换到超神纪元");
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[DivineAscension] 切换超神纪元失败（不影响动态纪元显示）: " + e.Message);
            }
        }

        // ============================================================
        //  原版纪元系统接入
        //  原版API：era_manager.prepare() + era_manager.getCurrentAge().id
        // ============================================================

        // ============================================================
        //  凡人觉醒
        // ============================================================

        /// <summary>
        /// 觉醒资格判定：仅智慧生物（isSapient）或（开启动物觉醒时）动物可修炼。
        /// 船、树、石、建筑等非生物单位（unit_other / 非动物非智慧）一律排除，
        /// 防止出现"船也能修炼突破获得境界称号"的异常。
        /// </summary>
        public static bool CanAwaken(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            if (actor.isSapient()) return true; // 智慧生物（人类/精灵/矮人等文明种族）
            if (actor.isAnimal()) return Code.Core.DengShenConfig.EnableAnimalAwakening; // 动物受配置控制
            return false; // 船/树/石/建筑等非生物单位
        }

        /// <summary>
        /// 凡人觉醒为觉醒异能者。由年度Tick按概率触发。
        /// 觉醒时随机锁定异能体系（终身不可更换），初始化1阶能力掌控度。
        /// </summary>
        public static bool AwakenMortal(Actor actor)
        {
            if (actor == null || !actor.isAlive())
            {
                DSDebug.Warning("[DivineAscension] AwakenMortal: actor为null或已死亡");
                return false;
            }
            if (!CanAwaken(actor))
            {
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: {actor.getName()}不是可修炼生物（船/树/石/建筑等），拒绝觉醒");
                return false;
            }
            if (CultivationData.IsAscended(actor))
            {
                DSDebug.Warning($"[DivineAscension] AwakenMortal: {actor.getName()}已经是异能者");
                return false;
            }

            try
            {
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 开始觉醒 {actor.getName()}");

                CultivationData.SetRealmTier(actor, 1);
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 设置境界=1成功");

                CultivationData.SetEnergy(actor, _qiThresholds[1]);
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 设置异能能量={_qiThresholds[1]}成功");

                // 天赋异禀：极低概率（5%）额外随机获得一个基因（觉醒第二基因）
                // 该基因所属染色体决定超神纪元效果（若此人日后成神）

                // 同步Trait（境界特质，体系只存在元数据中，不授予特质）
                TraitManager.SetRealmTrait(actor, 1);
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 设置境界特质成功");

                // 异能体系已由基因系统取代（无独立体系概念与特质）

                //  现代异能体系：觉醒时初始化能力掌控度为0
                //  替代原来的"掌握1阶全套印记"逻辑
                //  掌控度通过使用境界技能和年度修炼提升，达到100才能突破
                CultivationData.SetMastery(actor, 0f);
                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 初始化能力掌控度成功");
                //  设定：基因觉醒必须是修炼的起点，100%获得基因基因（elem_gene，生命染色体第1表达层级，一切异能的根源）
                // 基因系统：觉醒时获得主控基因，后续通过修炼获得更多基因
                CultivationData.UnlockElement(actor, "gene_cell_division");
                CultivationData.SetAwakened(actor, true);

                // 极低概率额外获得随机基因（天赋异禀，5%）
                if (UnityEngine.Random.value <= 0.05f)
                {
                    var bonusElements = ElementDef.AllElements.Where(e => e.Period <= 6 && e.Id != "gene_cell_division" && !CultivationData.IsElementUnlocked(actor, e.Id)).ToList();
                    if (bonusElements != null && bonusElements.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, bonusElements.Count);
                        var bonusElement = bonusElements[randomIndex];
                        CultivationData.UnlockElement(actor, bonusElement.Id);
                        DSDebug.Verbose($"[DivineAscension] AwakenMortal: 天赋异禀！额外获得基因={bonusElement.NameZh}({bonusElement.Id})");
                    }
                }

                DSDebug.Verbose($"[DivineAscension] AwakenMortal: 基因觉醒，基因觉醒成功");

                // 检查基因连锁效果
                GeneLinkageSystem.CheckAllLinkages(actor);

                DSDebug.Verbose($"[DivineAscension] AwakenMortal: {actor.getName()}觉醒成功！");

                //  统计钩子：记录觉醒年份 + 全局觉醒计数
                try
                {
                    int awakenYear = World.world != null ? (int)(World.world.getCurWorldTime() / 365.0) : 0;
                    CultivationData.SetAwakeningYear(actor, awakenYear);
                    DSGlobalStats.IncrementAwakeningCount();
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] AwakenMortal 异常: " + dsEx.Message); }

                //  先驱者记录：第一个觉醒者（1阶）也要记录
                try
                {
                    bool isFirstAwakened = CheckAndRecordPioneer(actor, 1);
                    if (isFirstAwakened)
                    {
                        string actorName = TitleSuffixManager.ExtractBaseName(actor.getName());
                        string tierName = UILocalization.GetTierName(1);
                        DSNotificationManager.NotifyInfo(
                            DSEventTexts.GetPioneerFullTitle(actorName, 1));
                        DSEventManager.RecordEvent(
                            DSEventType.Breakthrough,
                            DSEventTexts.GetPioneerHistoryTitle(actorName, 1),
                            DSEventTexts.GetPioneerHistoryDesc(actorName, 1, tierName),
                            actor.getName(),
                            ActorDataAccessor.GetData(actor).id, 1, actor);
                        DSDebug.Verbose("[DivineAscension] " + actorName + " 成为世界首位觉醒者！");
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Warning("首位觉醒者记录失败: " + e.Message);
                }

                return true;
            }
            catch (Exception e)
            {
                DSDebug.Error($"[DivineAscension] AwakenMortal异常: {actor.getName()}, {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // ============================================================
        //  晋升条件判定（三大硬性条件，文档9.4节）
        // ============================================================

        /// <summary>
        /// 检查是否满足晋升到下一阶的六大条件（现代异能体系）：
        /// 1. 异能能量达到该阶门槛（指数增长）
        /// 2. 能力掌控度达到100%（通过使用境界技能和年度修炼提升）
        /// 3. 基因图谱当前境界核心节点全部解锁（分支节点为可选强化）
        /// 4. 失控指数 < 70（失控过高无法稳定突破）
        /// 5. 8阶以上需要深度觉醒经验 >= 100
        /// 额外限制：13阶超神级基于基因系统判定
        /// </summary>
        public static bool CanBreakthrough(Actor actor, out BreakthroughResult failReason)
        {
            failReason = BreakthroughResult.Invalid;
            if (actor == null || !actor.isAlive()) return false;

            int currentTier = CultivationData.GetRealmTier(actor);
            if (currentTier <= 0)
            {
                failReason = BreakthroughResult.Invalid;
                return false;
            }

            int nextTier = currentTier + 1;
            if (nextTier > 13)
            {
                failReason = BreakthroughResult.AlreadyMax;
                return false;
            }

            // 条件：突破到12阶（真神级）必须有足够信仰（控制至少5个城市）
            if (nextTier == 12 && !VanillaIntegration.HasEnoughFaithForGodhood(actor))
            {
                failReason = BreakthroughResult.Fail_AscensionLocked;
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 信仰不足（需控制5个城市），无法突破真神级");
                return false;
            }

            //  成神可能性判定：突破到13阶（超神级）需要满足基因成神条件
            // 每个基因同级，不需要真理基因
            if (nextTier == 13 && !TraitManager.CanAscendToGod(actor))
            {
                failReason = BreakthroughResult.Fail_AscensionLocked;
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 成神条件未满足（需≥12个基因、失控<50、深度觉醒经验≥500000），无法成神");
                return false;
            }

            // 高位阶名额限制：使徒级(10阶)最多64人，登神级(11阶)最多16人，真神级(12阶)最多7人，超神级(13阶)最多3人
            // 弱肉强食：名额已满时，后来者挑战上位，随机击杀一个同阶单位腾出位置
            if (nextTier >= 10 && !EnsureTierSlot(nextTier, actor))
            {
                failReason = BreakthroughResult.Fail_AscensionLocked;
                DSDebug.Verbose($"[DivineAscension] {UILocalization.GetTierName(nextTier)}名额已满且无法腾出位置，{actor.getName()} 无法突破");
                return false;
            }

            // 条件1：异能能量达标
            if (CultivationData.GetEnergy(actor) < _qiThresholds[nextTier])
            {
                failReason = BreakthroughResult.Fail_Qi;
                return false;
            }

            // 条件2：能力掌控度达到100（现代异能体系，替代印记）
            //  通过使用境界技能和年度修炼提升掌控度
            if (!CultivationData.HasCompleteMastery(actor))
            {
                failReason = BreakthroughResult.Fail_Mastery;
                return false;
            }

            // 条件2.5：基因图谱核心节点解锁（基因图谱晋升系统，避免套娃式晋升）
            //  只需要当前境界的核心节点解锁即可晋升，分支节点为可选强化
            if (!GeneGraphManager.AreAllCoreNodesUnlocked(actor, currentTier))
            {
                failReason = BreakthroughResult.Fail_GeneGraph;
                return false;
            }

            // 条件3：失控指数 < 70（失控过高无法稳定突破）
            if (CultivationData.GetTurbulence(actor) >= 70f)
            {
                failReason = BreakthroughResult.Fail_Turbulence;
                return false;
            }

            // 条件4：高阶突破需要深度觉醒经验（8阶以上）
            if (nextTier >= 8 && CultivationData.GetEnlightenmentExp(actor) < 5000f)
            {
                failReason = BreakthroughResult.Fail_Enlightenment;
                return false;
            }

            //  条件5：高境界突破门槛增加（10阶以上）
            // 10阶以上：深度觉醒>=30000
            if (nextTier >= 10 && CultivationData.GetEnlightenmentExp(actor) < 30000f)
            {
                failReason = BreakthroughResult.Fail_Enlightenment;
                return false;
            }
            // 12阶以上：深度觉醒>=200000
            if (nextTier >= 12 && CultivationData.GetEnlightenmentExp(actor) < 200000f)
            {
                failReason = BreakthroughResult.Fail_Enlightenment;
                return false;
            }

            //  条件6：基因染色体突破条件（不同基因染色体有不同的突破要求）
            // 专精者（只掌握2个以下基因）可以豁免此条件
            var unlockedElems = CultivationData.GetUnlockedElements(actor);
            bool isSpecialist = unlockedElems != null && unlockedElems.Count <= 2;
            if (!isSpecialist && nextTier >= 6 && unlockedElems != null && unlockedElems.Count > 0)
            {
                // 获取主要基因染色体（从已解锁基因中选择出现最多的染色体）
                int[] groupCount = new int[8];
                foreach (string eid in unlockedElems)
                {
                    var elem = ElementDef.GetById(eid);
                    if (elem != null) groupCount[elem.Group]++;
                }
                int dominantGroup = 0, maxCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (groupCount[i] > maxCount) { maxCount = groupCount[i]; dominantGroup = i; }
                }

                // 按主要基因染色体判断突破条件
                switch (dominantGroup)
                {
                    case 0: // 力量染色体：需要年龄达标（力量积累）
                        if (actor.getAge() < nextTier * 10f) return false;
                        break;
                    case 1: // 敏捷染色体：需要连续修炼年数达标
                        if (CultivationData.GetGeneContinuousYears(actor) < nextTier * 5f) return false;
                        break;
                    case 2: // 体质染色体：需要能量上限达标
                        if (CultivationData.GetEnergy(actor) < nextTier * 1000f) return false;
                        break;
                    case 3: // 智力染色体：需要击杀数达标
                        if (CultivationData.GetGeneCombatKills(actor) < nextTier * 3f) return false;
                        break;
                    case 4: // 感知染色体：需要低失控年数达标
                        if (CultivationData.GetGeneLowCorrYears(actor) < nextTier * 3f) return false;
                        break;
                    case 5: // 意志染色体：需要战斗防御次数达标（意志磨砺）
                        if (CultivationData.GetGeneCombatDefense(actor) < nextTier * 5f) return false;
                        break;
                    case 6: // 异能染色体：需要基因掌握数量达标
                        if (unlockedElems == null || unlockedElems.Count < nextTier / 2) return false;
                        break;
                    case 7: // 潜能染色体：需要深度觉醒经验达标（潜能领悟）
                        if (CultivationData.GetEnlightenmentExp(actor) < nextTier * 50f) return false;
                        break;
                }
            }

            // 条件7：基因表达突破条件（完全重构）
            // 低境界关注单个基因表达层级，中境界关注染色体整体表达，高境界关注多染色体协同表达
            if (!GeneBreakthroughSystem.CheckGeneBreakthroughCondition(actor, nextTier))
            {
                failReason = BreakthroughResult.Fail_GeneGraph;
                return false;
            }

            failReason = BreakthroughResult.Success;
            return true;
        }

        // ============================================================
        //  突破执行
        // ============================================================

        /// <summary>执行正规突破（满足三大硬性条件后调用）</summary>
        public static BreakthroughResult DoBreakthrough(Actor actor)
        {
            if (!CanBreakthrough(actor, out var reason))
                return reason;

            int nextTier = CultivationData.GetRealmTier(actor) + 1;

            //  突破风险判定（现代异能体系特色）
            // 境界越高，突破风险越大；失控指数越高，失败概率越大
            float riskChance = GetBreakthroughRiskChance(nextTier, CultivationData.GetTurbulence(actor));

            //  境界特色突破修正
            // 4阶突变者：基因稳定，突破成功率+10%（风险-10%），仅对4-6阶生效（7阶以上基因稳定性递减）
            int currentTier = CultivationData.GetRealmTier(actor);
            if (currentTier >= 4 && currentTier <= 6)
            {
                riskChance -= 0.10f;
            }

            //  13阶超神级全局影响：全球异能者突破成功率+5%（风险-5%），仅对9阶以下生效（高阶突破不应被真神光环大幅稀释难度）
            if (RealmFeatures.HasTrueGodInWorld() && currentTier <= 9)
            {
                riskChance -= 0.05f;
            }

            riskChance = Mathf.Clamp(riskChance, 0.01f, 0.8f); // 最低1%失败率，最高80%

            if (UnityEngine.Random.value < riskChance)
            {
                //  突破失败惩罚（按境界阶梯式加重）
                float currentTurb = CultivationData.GetTurbulence(actor);
                float currentEnergy = CultivationData.GetEnergy(actor);
                float currentMastery = CultivationData.GetMastery(actor);

                if (nextTier <= 5)
                {
                    // 低阶（1-5）：失控+10，能量-20%
                    CultivationData.SetTurbulence(actor, currentTurb + 10f);
                    CultivationData.SetEnergy(actor, currentEnergy * 0.8f);
                }
                else if (nextTier <= 9)
                {
                    // 中阶（6-9）：失控+20，能量-30%，掌控度-10
                    CultivationData.SetTurbulence(actor, currentTurb + 20f);
                    CultivationData.SetEnergy(actor, currentEnergy * 0.7f);
                    CultivationData.SetMastery(actor, Mathf.Max(0f, currentMastery - 10f));
                }
                else if (nextTier <= 12)
                {
                    // 高阶（10-12）：失控+30，能量-50%，掌控度-20，10%概率境界跌落
                    CultivationData.SetTurbulence(actor, currentTurb + 30f);
                    CultivationData.SetEnergy(actor, currentEnergy * 0.5f);
                    CultivationData.SetMastery(actor, Mathf.Max(0f, currentMastery - 20f));
                    if (UnityEngine.Random.value <= 0.1f && nextTier > 1)
                    {
                        // 10%概率境界跌落
                        int fallenTier = nextTier - 2;
                        CultivationData.SetRealmTier(actor, fallenTier);
                        TraitManager.SetRealmTrait(actor, fallenTier);
                        DSDebug.Verbose($"[DivineAscension] {actor.getName()} 突破失败导致境界跌落至{fallenTier}阶！");
                    }
                }
                else
                {
                    // 13阶（超神级）：失控+50，能量-80%，深度觉醒-50%
                    CultivationData.SetTurbulence(actor, currentTurb + 50f);
                    CultivationData.SetEnergy(actor, currentEnergy * 0.2f);
                    float enlightExp = CultivationData.GetEnlightenmentExp(actor);
                    CultivationData.SetEnlightenmentExp(actor, enlightExp * 0.5f);
                }

                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 突破到{nextTier}阶失败！惩罚按境界阶梯式加重");
                return BreakthroughResult.Fail_Risk;
            }

            // 突破成功
            CultivationData.SetRealmTier(actor, nextTier);
            TraitManager.SetRealmTrait(actor, nextTier);
            // 立即应用境界特性：补全从1阶到新阶位的所有机制（跳阶突破不遗漏中间阶位）
            RealmFeatures.ApplyAllRealmFeaturesUpTo(actor, nextTier);

            //  组织贡献：突破境界额外+100
            try { if (CultivationData.GetSectRank(actor) > 0) CultivationData.AddSectContribution(actor, 100f); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoBreakthrough 组织贡献异常: " + dsEx.Message); }

            //  触发境界突破事件（天劫、异象、全局效果等）
            RealmEvents.OnBreakthrough(actor, nextTier);

            //  基因突变：突破境界时大概率发生基因突变（消耗能量）
            try { Code.Core.GeneMutationSystem.MutateOnBreakthrough(actor, nextTier); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoBreakthrough 异常: " + dsEx.Message); }

            //  原版深度交互：突破成功后授予对应境界的原版特质（strong/fast/smart/blessed等）
            VanillaInteractionManager.GrantVanillaTraitsForRealm(actor, nextTier);

            //  寿命加成：高境界异能者寿命更长（基因优化带来的寿命延长）
            VanillaInteractionManager.ApplyLifespanBonus(actor, nextTier);

            //  现代异能体系：突破后重置能力掌控度为0（开始掌握新境界的能力）
            //  替代原来的"自动掌握印记"逻辑
            CultivationData.ResetMasteryOnBreakthrough(actor);

            //  境界挂钩状态特质自动授予（关键修复：之前缺少这个互动）
            GrantTierLinkedStatusTraits(actor, nextTier);

            //  境界技能授予：每阶突破掌握对应异能状态（之前技能注册了但无人授予，形同虚设）
            DSStatusManager.AddTierAbility(actor, nextTier);

            //  动物开启智慧：非智慧生物达到3阶（强化者）后自动获得高级脑功能区·文明化特质
            // 现代异能体系特色：异能可以让动物开启高级脑功能区，变成智慧生物
            GrantAnimalIntelligence(actor, nextTier);

            //  真神诞生：统一处理（跨存档+立教+纪元+通知）
            if (nextTier == 13) HandleTrueGodBirth(actor);

            //  先驱记录 + 突破通知（13阶超神级已单独处理，此处跳过13阶避免双重记录；重大境界分支已删除，10/11阶走普通突破格式）
            if (nextTier < 13)
            {
                try
                {
                bool isFirst = CheckAndRecordPioneer(actor, nextTier);
                string tierName = UILocalization.GetTierName(nextTier);

                if (isFirst)
                {
                    // 先驱（任意阶首个突破者，使用DSEventTexts文案管理器）
                    DSNotificationManager.NotifyInfo(
                        DSEventTexts.GetPioneerFullTitle(TitleSuffixManager.ExtractBaseName(actor.getName()), nextTier));
                    DSEventManager.RecordEvent(
                        DSEventType.Breakthrough,
                        DSEventTexts.GetPioneerHistoryTitle(TitleSuffixManager.ExtractBaseName(actor.getName()), nextTier),
                        DSEventTexts.GetPioneerHistoryDesc(TitleSuffixManager.ExtractBaseName(actor.getName()), nextTier, tierName),
                        actor.getName(),
                        ActorDataAccessor.GetData(actor).id, nextTier, actor, true); // suppressNotification：已手动弹过短通知，避免RecordEvent用历史标题补发长中央大字
                    // 播放突破音乐（如果安装了God's Gramophone模组）
                }
                else if (nextTier >= 8)
                {
                    // 普通突破（非先驱）：只有8阶及以上才有全局通知和历史记录
                    // 历史记录格式：标题=单位名+突破境界，描述=突破功能描述+收束语（合并显示）
                    string actorName = TitleSuffixManager.ExtractBaseName(actor.getName());
                    string breakthroughDesc = DSEventTexts.GetBreakthroughDesc(nextTier);
                    string epilogue = UILocalization.Get("Divine_" + nextTier + "_epilogue");
                    if (string.IsNullOrEmpty(epilogue) || epilogue == "Divine_" + nextTier + "_epilogue") epilogue = null;
                    // 历史记录标题：单位名 突破到X阶 境界名
                    string historyTitle = string.Format(UILocalization.Get("notify_breakthrough"), actorName, nextTier, tierName);
                    // 历史记录描述：突破功能描述 + 收束语（收束句独立一行，传入单位名字替换"他"）
                    string historyDesc = breakthroughDesc;
                    if (!string.IsNullOrEmpty(epilogue))
                    {
                        string formattedEpilogue = string.Format(epilogue, actorName);
                        historyDesc += "\n" + formattedEpilogue;
                    }
                    // 全局通知：称号-名字 突破到X阶 境界名（不含功能描述）
                    DSNotificationManager.NotifyInfo(
                        string.Format(UILocalization.Get("notify_breakthrough"), actorName, nextTier, tierName));
                    // 历史记录
                    DSEventManager.RecordEvent(
                        DSEventType.Breakthrough,
                        historyTitle,
                        historyDesc,
                        actor.getName(),
                        ActorDataAccessor.GetData(actor).id, nextTier, actor, true); // suppressNotification：已手动弹过短通知，避免RecordEvent用合并长标题补发中央大字
                }
                // 8阶以下的普通突破不做全局通知和历史记录
                }
                catch (Exception e)
                {
                    DSDebug.Warning($"先驱/突破通知失败: {e.Message}");
                }
            }

            //  种族升华（4阶方碑进化/11阶登神升华）统一在 CultivationData.SetTier 中处理，
            //   覆盖正常突破、调试设阶等所有境界变化路径，避免重复调用

            DSDebug.Verbose($"[DivineAscension] {actor.getName()} 成功突破到{nextTier}阶 {UILocalization.GetTierName(nextTier)}！");
            return BreakthroughResult.Success;
        }

        /// <summary>
        /// 阶位回退（席位争夺机制）：只有登神级（11阶）及以上才允许回退，
        /// 回退一阶释放席位。挑战失败逃跑等场景由战斗协程调用。
        /// </summary>
        public static void DemoteActor(Actor actor, int targetTier)
        {
            if (actor == null) return;
            try
            {
                int currentTier = CultivationData.GetRealmTier(actor);
                // 只有登神级及以上才能回退，且回退目标不能低于10阶（使徒级）
                if (currentTier < 11) return;
                if (targetTier < 10) targetTier = 10;

                Code.Core.DSDebug.Verbose($"[DivineAscension] {actor.getName()} 阶位回退：{currentTier}阶 -> {targetTier}阶");

                // 设置境界 + 更新特质
                CultivationData.SetRealmTier(actor, targetTier, allowBigJump: true);
                TraitManager.SetRealmTrait(actor, targetTier);

                // 移除高阶技能状态
                try { DSStatusManager.RemoveTierAbilities(actor, currentTier); } catch (Exception e) { DSDebug.Warning($"[DivineAscension] DemoteActor 移除技能异常: {e.Message}"); }

                // 更新称号
                try { TitleSuffixManager.UpdateTitleSuffix(actor); } catch { }

                // 记录事件
                try
                {
                    string tierName = UILocalization.GetTierName(targetTier);
                    string actorName = TitleSuffixManager.ExtractBaseName(actor.getName());
                    DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("judge_demote_notify"), actorName, targetTier, tierName));
                    DSEventManager.RecordEvent(
                        DSEventType.Warning,
                        string.Format(UILocalization.Get("judge_demote_title"), actorName, targetTier, tierName),
                        string.Format(UILocalization.Get("judge_demote_desc"), actorName, targetTier, tierName),
                        actor.getName(), ActorDataAccessor.GetData(actor).id, targetTier, actor, true);
                }
                catch (Exception e) { DSDebug.Warning($"[DivineAscension] DemoteActor 事件记录异常: {e.Message}"); }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] DemoteActor 异常: {e.Message}");
            }
        }


        /// <summary>动物开启智慧：非智慧生物达到3阶后通过异能获得高级脑功能区（civilized文明化特质）</summary>
        /// <remarks>
        /// 现代异能体系特色：异能可以让动物开启高级脑功能区，变成智慧生物。
        /// 注意：intelligent/smart/genius 是智力程度特质，不是脑功能区特质；
        /// 真正的高级脑功能区是 civilized（文明化）特质，让非智慧种族能使用工具、建立文明。
        /// </remarks>
        private static void GrantAnimalIntelligence(Actor actor, int tier)
        {
            if (actor == null) return;

            try
            {
                // 已经是智慧生物（种族有脑属性或已有civilized特质）
                if (AnnualTickManager.IsHumanoidCivilized(actor)) return;

                // 3阶（强化者）：异能开启高级脑功能区，获得civilized（文明化）特质
                if (tier >= 3 && !actor.hasTrait("civilized"))
                {
                    actor.addTrait("civilized", false);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到{tier}阶，异能开启高级脑功能区，获得文明化特质！");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 动物开启智慧失败: {e.Message}");
            }
        }

        /// <summary>高境界（8阶+）突破后追加史诗收束记录（已改为合并进主记录描述，本方法已删除）</summary>

        /// <summary>计算突破失败概率（现代异能体系特色）</summary>
        public static float GetBreakthroughRiskChance(int targetTier, float turbulence)
        {
            // 基础风险：1-3阶5%，4-6阶10%，7-9阶15%，10-13阶20%
            float baseRisk = 0.05f;
            if (targetTier >= 4) baseRisk = 0.10f;
            if (targetTier >= 7) baseRisk = 0.15f;
            if (targetTier >= 10) baseRisk = 0.20f;

            // 失控指数加成：每10点失控增加2%失败概率
            float turbulenceBonus = (turbulence / 10f) * 0.02f;

            // 后期制约：同境界存活者越多，突破到该境界的失败概率越大（只对高阶8阶+生效，抑制高阶泛滥与突破刷屏）
            // 突破到X阶时看当前存活X阶单位数，每多一个失败概率+2%（比"8阶+总量"更精确：10阶饱和不影响8阶突破）
            int crowdingBonus = 0;
            if (targetTier >= 8)
            {
                int sameTierCount = CountAliveActorsAtTier(targetTier);
                crowdingBonus = sameTierCount * 2;
            }

            return Mathf.Clamp(baseRisk + turbulenceBonus + crowdingBonus / 100f, 0f, 0.8f);
        }

        /// <summary>统计世界上存活且境界恰好为指定境界的异能者数量</summary>
        private static int CountAliveActorsAtTier(int tier)
        {
            if (World.world == null || World.world.units == null) return 0;
            int count = 0;
            foreach (Actor a in World.world.units)
            {
                if (a == null || !a.isAlive()) continue;
                if (CultivationData.GetRealmTier(a) == tier) count++;
            }
            return count;
        }

        /// <summary>根据境界授予挂钩的状态特质（登神化身、维度投影等）</summary>
        private static void GrantTierLinkedStatusTraits(Actor actor, int tier)
        {
            if (actor == null) return;

            try
            {
                // === Buff技能改为被动特质（达到对应境界自动获得，永久生效）===
                // 2阶共振者：能量护盾（防御大增）
                if (tier >= 2 && !actor.hasTrait(TraitManager.STATUS_ENERGY_SHIELD))
                {
                    actor.addTrait(TraitManager.STATUS_ENERGY_SHIELD, false);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到2阶，获得永久特质：能量护盾");
                }

                // 3阶强化者：身体强化（攻击速度大增）
                if (tier >= 3 && !actor.hasTrait(TraitManager.STATUS_BODY_BOOST))
                {
                    actor.addTrait(TraitManager.STATUS_BODY_BOOST, false);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到3阶，获得永久特质：身体强化");
                }

                // 7阶具象者：能量武器（攻击射程大增）
                if (tier >= 7 && !actor.hasTrait(TraitManager.STATUS_ENERGY_WEAPON))
                {
                    actor.addTrait(TraitManager.STATUS_ENERGY_WEAPON, false);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到7阶，获得永久特质：能量武器");
                }

                // 11阶登神级：登神化身标记存储在元数据中（境界特质已有属性加成）
                if (tier >= 11)
                {
                    ActorDataAccessor.GetData(actor).custom_data_string["ds_sanctuary_avatar"] = "true";
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到{tier}阶，登神化身标记");
                }

                // 12阶真神级：维度投影标记存储在元数据中（境界特质已有属性加成）
                if (tier >= 12)
                {
                    ActorDataAccessor.GetData(actor).custom_data_string["ds_divine_projection"] = "true";
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到{tier}阶，维度投影标记");
                }

                // 13阶超神级：神眷状态特质（特殊，真神可以孕育神眷，这里只标记能力）
                if (tier >= 13 && !actor.hasTrait(TraitManager.STATUS_DIVINE_BLESSING))
                {
                    // 注意：神眷是真神孕育的从属者，不是真神自己的状态
                    // 这里不授予真神神眷特质，只记录日志
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到真神，可孕育神眷从属者");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 授予境界挂钩状态特质失败: {e.Message}");
            }
        }

        // ============================================================
        //  深度觉醒突破（无完整能力掌控度时的低成功率突破）
        // ============================================================

        /// <summary>
        /// 深度觉醒突破：无完整能力掌控度时可尝试，阶位越高成功率越低。
        /// 前置：异能能量达标 + 失控指数<70（能力掌控度条件豁免）。
        /// 成功：境界+1，自动提升能力掌控度。失败：失控指数大幅上涨，随机能力掌控度下降。
        /// </summary>
        /// <summary>
        /// 统一突破入口
        /// </summary>
        /// <param name="actor">突破单位</param>
        /// <param name="enlightenment">是否顿悟突破（false=正规突破）</param>
        public static BreakthroughResult TryBreakthrough(Actor actor, bool enlightenment = false)
        {
            if (enlightenment)
                return DoEnlightenmentBreakthrough(actor);
            else
                return DoBreakthrough(actor);
        }

        public static BreakthroughResult DoEnlightenmentBreakthrough(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return BreakthroughResult.Invalid;

            int currentTier = CultivationData.GetRealmTier(actor);
            if (currentTier <= 0) return BreakthroughResult.Invalid;

            int nextTier = currentTier + 1;
            if (nextTier > 13) return BreakthroughResult.AlreadyMax;

            // 11阶以上不支持顿悟突破（必须正规突破）
            if (nextTier >= 11) return BreakthroughResult.Fail_Enlightenment;

            //  成神可能性判定：深度觉醒突破到13阶也需要满足基因成神条件
            if (nextTier == 13 && !TraitManager.CanAscendToGod(actor))
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 成神条件未满足（需≥12个基因、失控<50、深度觉醒经验≥500000），无法成神");
                return BreakthroughResult.Fail_AscensionLocked;
            }

            // 仍需异能能量达标和失控指数<70
            if (CultivationData.GetEnergy(actor) < _qiThresholds[nextTier]) return BreakthroughResult.Fail_Qi;
            if (CultivationData.GetTurbulence(actor) >= 70f) return BreakthroughResult.Fail_Turbulence;

            // 高位阶名额限制（与正常突破一致）：登神级16人、真神级7人、超神级3人
            // 深度觉醒突破也必须遵守名额限制，否则会导致高阶单位泛滥
            if (nextTier >= 10 && !EnsureTierSlot(nextTier, actor))
            {
                DSDebug.Verbose($"[DivineAscension] {UILocalization.GetTierName(nextTier)}名额已满且无法腾出位置，{actor.getName()} 无法通过深度觉醒突破");
                return BreakthroughResult.Fail_AscensionLocked;
            }

            // 成功率：阶位越高越低（文档9.4节）
            // 突破成功率：阶梯式设计，高境界有难度但不是不可能
            // 低境界有一定失败率，高境界难度大但不是不可能
            // 加上深度觉醒+20%、专精+10%、幸运+10%、祝福+15%、天才+20%等加成
            // 接入基因能量统一公式（GE公式）：基因能量越高，突破成功率越高
float[] successRates = { 0f, 0.70f, 0.60f, 0.50f, 0.40f, 0.32f, 0.25f, 0.20f, 0.15f, 0.10f, 0.07f, 0.05f, 0.03f, 0.02f };
float successRate = (nextTier >= 0 && nextTier < successRates.Length) ? successRates[nextTier] : 0.02f;
            successRate += Mathf.Min(0.15f, CultivationData.GetEnlightenmentExp(actor) / 10000f);
            
            // 基因能量统一公式（GE公式）加成：基因能量越高，突破成功率越高（最多+30%）
            try
            {
                float geBreakthroughBonus = Code.Core.GeneEnergyCalculator.GetBreakthroughBonus(actor);
                successRate += geBreakthroughBonus;
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
            
            // 专精加成：只掌握2个基因（初始基因+觉醒第二基因）时，突破成功率+5%
            try
            {
                int elemCount = CultivationData.GetUnlockedElements(actor)?.Count ?? 0;
                if (elemCount <= 2) successRate += 0.05f;
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
            
            // 失控指数越低，突破成功率越高（失控指数<30时+5%）
            try
            {
                float turbulence = CultivationData.GetTurbulence(actor);
                if (turbulence < 30f) successRate += 0.05f;
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
            
            // 拥挤惩罚：同境界存活者越多，突破成功率越低（8阶+生效，与正规突破机制一致）
            // 突破到X阶时看当前存活X阶单位数，每多一个成功率-2%（抑制高阶泛滥）
            if (nextTier >= 8)
            {
                try
                {
                    int sameTierCount = CountAliveActorsAtTier(nextTier);
                    float crowdingPenalty = sameTierCount * 0.02f;
                    successRate -= crowdingPenalty;
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 拥挤惩罚异常: " + dsEx.Message); }
            }
            
            // 限制成功率范围（最低1%，最高90%）
            successRate = Mathf.Clamp(successRate, 0.01f, 0.90f);

            if (UnityEngine.Random.value <= successRate)
            {
                // 成功
                CultivationData.SetRealmTier(actor, nextTier);
                TraitManager.SetRealmTrait(actor, nextTier);
                // 立即应用境界特性：补全从1阶到新阶位的所有机制
                RealmFeatures.ApplyAllRealmFeaturesUpTo(actor, nextTier);
                CultivationData.SetEnlightenmentExp(actor, 0f);
                // 深度觉醒状态：修炼速度+100%，持续3年
                CultivationData.SetEnlightenmentDuration(actor, 10f);
                // 组织贡献：突破境界额外+100
                try { if (CultivationData.GetSectRank(actor) > 0) CultivationData.AddSectContribution(actor, 100f); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 组织贡献异常: " + dsEx.Message); }

                //  修复：深度觉醒突破成功后触发历史记录和全局通知（之前遗漏了）
                try
                {
                    string tierName = UILocalization.GetTierName(nextTier);
                    string actorName = TitleSuffixManager.ExtractBaseName(actor.getName());

                    // 先驱记录检查
                    bool isFirst = CheckAndRecordPioneer(actor, nextTier);

                    if (nextTier == 13) HandleTrueGodBirth(actor);
                    else if (isFirst)
                    {
                        // 先驱突破
                        DSNotificationManager.NotifyInfo(
                            DSEventTexts.GetPioneerFullTitle(actorName, nextTier));
                        DSEventManager.RecordEvent(
                            DSEventType.Breakthrough,
                            DSEventTexts.GetPioneerHistoryTitle(actorName, nextTier),
                            DSEventTexts.GetPioneerHistoryDesc(actorName, nextTier, tierName),
                            actor.getName(),
                            ActorDataAccessor.GetData(actor).id, nextTier, actor, true);
                    }
                    else if (nextTier >= 8)
                    {
                        // 普通突破（8阶及以上）
                        string breakthroughDesc = DSEventTexts.GetBreakthroughDesc(nextTier);
                        string epilogue = UILocalization.Get("Divine_" + nextTier + "_epilogue");
                        if (string.IsNullOrEmpty(epilogue) || epilogue == "Divine_" + nextTier + "_epilogue") epilogue = null;

                        string historyTitle = string.Format(UILocalization.Get("notify_breakthrough"), actorName, nextTier, tierName);
                        string historyDesc = breakthroughDesc;
                        if (!string.IsNullOrEmpty(epilogue))
                        {
                            string formattedEpilogue = string.Format(epilogue, actorName);
                            historyDesc += "\n" + formattedEpilogue;
                        }

                        DSNotificationManager.NotifyInfo(
                            string.Format(UILocalization.Get("notify_breakthrough"), actorName, nextTier, tierName));
                        DSEventManager.RecordEvent(
                            DSEventType.Breakthrough,
                            historyTitle,
                            historyDesc,
                            actor.getName(),
                            ActorDataAccessor.GetData(actor).id, nextTier, actor, true);
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Warning($"深度觉醒突破通知失败: {e.Message}");
                }

                return BreakthroughResult.Success;
            }
            else
            {
                // ============================================================
                //  突破失败惩罚（高境界惩罚更重）
                // ============================================================
                
                // 1. 失控指数大幅上涨
                CultivationData.SetTurbulence(actor,
                    CultivationData.GetTurbulence(actor) + 15f + nextTier * 2f);
                
                // 2. 触发能力失控（攻击+50%，防御-50%，持续3年）
                DSStatusManager.AddOutOfControl(actor);
                
                // 3. 深度觉醒经验清零
                CultivationData.SetEnlightenmentExp(actor, 0f);
                
                // 4. 寿命减少（高境界减少更多）
                try
                {
                    float lifespanLoss = nextTier * 2f; // 每阶减少2年寿命
                    // 使用反射访问actor.data字段（因为data字段是private/protected）
                    var dataField = actor.GetType().GetField("data",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (dataField != null)
                    {
                        object actorData = dataField.GetValue(actor);
                        if (actorData != null)
                        {
                            // 尝试通过反射减少寿命
                            try
                            {
                                var lifespanField = actorData.GetType().GetField("lifespan",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (lifespanField != null)
                                {
                                    float currentLifespan = (float)lifespanField.GetValue(actorData);
                                    lifespanField.SetValue(actorData, Mathf.Max(1f, currentLifespan - lifespanLoss));
                                }
                            }
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                
                // 5. 基因损伤状态（修炼速度-50%，进阶成功率-50%，持续10年）
                // 高境界突破失败时添加基因损伤状态
                if (nextTier >= 5)
                {
                    DSStatusManager.AddFoundationDamaged(actor);
                }
                
                // 6. 能量损失（损失一定比例的异能能量）
                try
                {
                    float currentEnergy = CultivationData.GetEnergy(actor);
                    float energyLossRate = Mathf.Min(0.5f, 0.1f + nextTier * 0.03f); // 1阶损失13%，12阶损失46%
                    CultivationData.SetEnergy(actor, currentEnergy * (1f - energyLossRate));
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                
                // 7. 基因亲和降低（临时降低基因亲和，影响修炼速度）
                try
                {
                    // 基因亲和降低通过基因损伤状态间接实现
                    // 这里可以添加额外的基因亲和降低逻辑
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                
                // 8. 基因崩溃率（接入GE公式，基因能量越高，崩溃率越高，崩溃即死亡）
                try
                {
                    float geneCollapseRate = Code.Core.GeneEnergyCalculator.GetGeneCollapseRate(actor);
                    if (UnityEngine.Random.value <= geneCollapseRate)
                    {
                        // 基因崩溃：基因序列彻底崩溃，单位无法存活，直接死亡
                        DSDebug.Verbose($"[DivineAscension] {actor.getName()} 突破失败时发生基因崩溃，死亡！境界：{nextTier}，崩溃率：{geneCollapseRate:P1}");

                        //  11阶及以上突破失败陨落是重大事件：全局通知 + 历史记录（灾变分类，写历史+中央通知）
                        if (nextTier >= 11)
                        {
                            try
                            {
                                // 基因崩溃记录：标题=崩溃描述（含单位名），描述=收束语（合并，不再独立青色记录）
                                Code.Core.DSEventManager.RecordEvent(
                                    Code.Core.DSEventType.Disaster,
                                    string.Format(UILocalization.Get("judge_gene_collapse_desc"), actor.getName(), nextTier),
                                    UILocalization.Get("gene_collapse_epilogue"),
                                    actor.getName(),
                                    Code.Data.ActorDataAccessor.GetData(actor) != null ? Code.Data.ActorDataAccessor.GetData(actor).id : 0L,
                                    nextTier, actor);
                            }
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] 基因崩溃记录异常: " + dsEx.Message); }
                        }
                        
                        // 直接导致单位死亡
                        try
                        {
                            // WorldBox中单位死亡的方式：设置健康值为0或调用die方法
                            // 使用反射访问actor.data字段（因为data字段是private/protected）
                            var dataField = actor.GetType().GetField("data",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (dataField != null)
                            {
                                object actorData = dataField.GetValue(actor);
                                if (actorData != null)
                                {
                                    // 设置健康值为0
                                    try
                                    {
                                        var healthField = actorData.GetType().GetField("health",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (healthField != null)
                                        {
                                            healthField.SetValue(actorData, 0f);
                                        }
                                    }
                                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                                }
                            }
                            // 调用死亡方法（如果存在）
                            try
                            {
                                var dieMethod = actor.GetType().GetMethod("die", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (dieMethod != null)
                                {
                                    dieMethod.Invoke(actor, null);
                                }
                            }
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                        }
                        catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                        
                        // 基因崩溃后直接返回，不再执行其他惩罚
                        return BreakthroughResult.Fail_GeneCollapse;
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] DoEnlightenmentBreakthrough 异常: " + dsEx.Message); }
                
                return BreakthroughResult.Fail_Enlightenment;
            }
        }

        /// <summary>获取指定境界的存活数量（用缓存）</summary>
        public static int GetTierCount(int tier)
        {
            if (_tierCacheDirty)
            {
                RebuildTierCountCache();
            }
            
            int count;
            if (_tierCountCache.TryGetValue(tier, out count))
            {
                return count;
            }
            return 0;
        }

        /// <summary>重建境界计数缓存（游戏加载时调用一次）</summary>
        public static void RebuildTierCountCache()
        {
            try
            {
                _tierCountCache.Clear();
                
                foreach (var actor in World.world.units)
                {
                    if (actor != null && actor.isAlive())
                    {
                        int tier = CultivationData.GetRealmTier(actor);
                        if (tier > 0)
                        {
                            if (_tierCountCache.ContainsKey(tier))
                                _tierCountCache[tier]++;
                            else
                                _tierCountCache[tier] = 1;
                        }
                    }
                }
                
                _tierCacheDirty = false;
                DSDebug.Verbose($"[DivineAscension] 境界计数缓存已重建：11阶={GetTierCount(11)}, 12阶={GetTierCount(12)}, 13阶={GetTierCount(13)}");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 重建境界计数缓存失败: " + e.Message);
            }
        }

        /// <summary>标记缓存需要更新（突破/跌落/死亡时调用）</summary>
        public static void MarkTierCacheDirty()
        {
            _tierCacheDirty = true;
        }
    }
}










