// ============================================================

using System;
using Code;
using Code.UI;
using Code.Core;
using System.Collections.Generic;
using Code.Data;
using Code.Realm;
using Code.Traits;
using UnityEngine;
using HarmonyLib;

namespace Code.Core
{
    public static class AnnualTickManager
    {
        /// <summary>获取所有活跃异能者（供其他系统遍历用，避免遍历所有单位）</summary>
        public static List<Actor> GetActiveCultivators()
        {
            var result = new List<Actor>();
            try
            {
                if (_activeCultivatorIds == null) return result;
                var snapshot = new List<long>(_activeCultivatorIds);
                foreach (long id in snapshot)
                {
                    Actor actor = FindActorById(id);
                    if (actor != null && actor.isAlive() && CultivationData.IsAscended(actor))
                    {
                        result.Add(actor);
                    }
                }
            }
            catch { }
            return result;
        }

        // 全局年度结算标记（每年只执行一次全局结算：战乱失控指数、灾变、维度投影、持久化）
        private static int _globalSettledYear = -1;

        // 活跃异能者ID列表（运行时缓存，读档时重建）
        private static readonly List<long> _activeCultivatorIds = new List<long>();

        // 性能统计
        public static int LastSettledCount { get; private set; }
        public static float LastSettleDurationMs { get; private set; }
        public static bool PerformanceDiagnosticsEnabled = false;

        // ============================================================
        //  活跃异能者列表维护
        // ============================================================

        /// <summary>注册异能者到活跃列表（凡人觉醒、玩家手动授予时调用）</summary>
        public static void RegisterCultivator(Actor actor)
        {
            if (actor == null) return;
            long id = actor.getID();
            if (!_activeCultivatorIds.Contains(id))
            {
                _activeCultivatorIds.Add(id);
            }
        }

        /// <summary>
        /// 重建活跃异能者列表（读档、新世界加载后调用）。
        /// 遍历全部存活单位一次（加载时级别，非逐帧），找出异能者。
        /// 同时从特质同步数据，解决玩家手动赐予特质后统计不到的问题。
        /// </summary>
        public static void RebuildActiveList()
        {
            _activeCultivatorIds.Clear();
            if (World.world == null || World.world.units == null) return;

            int syncedCount = 0;
            foreach (var actor in World.world.units.units_only_alive)
            {
                if (actor == null) continue;

                // 先从特质同步数据（手动赐予特质的单位会被初始化）
                if (CultivationData.HasAnyModTrait(actor))
                {
                    if (CultivationData.SyncFromTraits(actor))
                    {
                        syncedCount++;
                    }
                }

                // 然后检查是否为异能者（动物也可以觉醒异能，达到3阶后获得高级脑功能区·文明化）
                // 资格过滤：船/树/石/建筑等非生物单位即使有旧数据也不进入修炼结算
                if (CultivationData.IsAscended(actor) && Code.Realm.RealmJudge.CanAwaken(actor))
                {
                    _activeCultivatorIds.Add(actor.getID());
                }
            }

            if (syncedCount > 0)
            {
                DSDebug.Verbose($"RebuildActiveList: 从特质同步了{syncedCount}个单位，活跃异能者={_activeCultivatorIds.Count}");
            }
        }

        /// <summary>获取活跃异能者数量</summary>
        public static int GetActiveCultivatorCount()
        {
            return _activeCultivatorIds.Count;
        }

        private static float _lastRebuildTime = 0f;
        private const float REBUILD_COOLDOWN = 5f; // 5秒冷却，避免频繁重建

        /// <summary>获取活跃异能者ID列表（如果列表为空，自动重建）</summary>
        public static List<long> GetActiveCultivatorIds()
        {
            // 保护机制：如果列表为空，可能是世界加载时RebuildActiveList失败了
            // 加冷却时间，避免频繁重建卡
            if (_activeCultivatorIds.Count == 0 && World.world != null && World.world.units != null)
            {
                if (Time.time - _lastRebuildTime > REBUILD_COOLDOWN)
                {
                    DSDebug.Verbose("GetActiveCultivatorIds: 活跃列表为空，自动重建");
                    _lastRebuildTime = Time.time;
                    RebuildActiveList();
                }
            }
            return _activeCultivatorIds;
        }

        /// <summary>通过ID查找Actor（使用反射访问units.dict字典，O(1)查找）</summary>
        private static Actor FindActorById(long id)
        {
            if (World.world == null || World.world.units == null) return null;
            try
            {
                // dict字段是private的，需要用反射访问
                var dictField = World.world.units.GetType().GetField("dict",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (dictField == null) return null;
                var dict = dictField.GetValue(World.world.units) as System.Collections.Generic.Dictionary<long, Actor>;
                if (dict == null) return null;
                if (dict.TryGetValue(id, out Actor actor))
                {
                    return actor;
                }
            }
            catch
            {
                // 反射访问失败，返回null
            }
            return null;
        }

        // ============================================================
        //  年度检测与触发
        // ============================================================

        /// <summary>
        /// 年龄驱动的年度Tick入口（由Harmony Patch Actor.updateAge Postfix调用）。
        /// 每个单位年龄增长时触发，检查该单位是否过了一年。
        /// 禁令4：不在Update中遍历任何单位，全部结算由年龄事件驱动。
        /// 返回true表示触发了年度结算，false表示未触发。
        /// </summary>
        public static bool OnActorAgeUpdated(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            if (World.world == null) return false;

            // 获取当前年龄整数（getAge()返回游戏内年龄，每年+1）
            int ageNow = Mathf.FloorToInt((float)actor.getAge());
            if (ageNow < 1) return false; // 1岁以下不结算

            // 读取该单位上一次结算年份
            int ageLast = CultivationData.GetLastSettledYear(actor);
            if (ageLast == 0) ageLast = ageNow; // 首次触发，初始化

            if (ageNow <= ageLast) return false; // 年龄未增长，不结算

            // 全局年度结算（每年只执行一次：凡人觉醒、活跃异能者修炼/突破、灾变、维度投影；内部按世界年防重）
            // 注意：不再执行单位级结算，因为全局结算已对所有活跃异能者执行SettleSingleCultivator
            // 之前的重复结算导致每个异能者每年被结算两次，能量积累速度是预期的两倍
            ExecuteGlobalAnnualSettlement();

            // 更新该单位的上一次结算年份（用于调试和追踪）
            CultivationData.SetLastSettledYear(actor, ageNow);
            return true;
        }

        /// <summary>重置年度检测状态（读档、新世界创建时调用）</summary>
        public static void ResetYearTracker()
        {
            _globalSettledYear = -1;
            RebuildActiveList();
        }

        /// <summary>当前世界时间年（1年=60秒游戏时间），用于全局结算防重标记（与AnnualTickDriver的SECONDS_PER_YEAR一致）</summary>
        private static int GetWorldYear()
        {
            if (World.world == null) return -1;
            try { return Mathf.FloorToInt((float)(World.world.getCurWorldTime() / 60.0)); }
            catch { return -1; }
        }

        /// <summary>强制触发一次全局年度结算</summary>
        public static void ForceAnnualSettlement()
        {
            ExecuteGlobalAnnualSettlement(true);
            // 对所有活跃异能者强制结算一次
            var snapshot = new List<long>(_activeCultivatorIds);
            foreach (long id in snapshot)
            {
                Actor actor = FindActorById(id);
                if (actor != null && actor.isAlive() && CultivationData.IsAscended(actor))
                {
                    ExecuteUnitAnnualSettlement(actor);
                }
            }
        }

        // ============================================================
        //  年度结算主流程（拆分为全局结算 + 单位结算）
        // ============================================================

        /// <summary>
        /// 公共入口：触发一次全局年度结算（由AnnualTickDriver全局时间驱动调用）。
        /// 不遍历单位，仅执行全局结算（凡人觉醒抽样、灾变、维度投影、持久化）。
        /// 单位级结算在AwakenMortals和活跃异能者列表中处理。
        /// </summary>
        public static void TriggerGlobalAnnualSettlement()
        {
            ExecuteGlobalAnnualSettlement();
        }

        /// <summary>全局年度结算（每年只执行一次：战乱失控指数、凡人觉醒、灾变、维度投影、持久化）</summary>
        private static void ExecuteGlobalAnnualSettlement(bool force = false)
        {
            try
            {
                // 每年只执行一次：时间驱动与年龄驱动共享世界时间年标记（防止双驱动重复结算）
                int year = GetWorldYear();
                if (!force && year >= 0 && _globalSettledYear == year) return;
                if (year >= 0) _globalSettledYear = year;

                DSDebug.Verbose("===== 全局年度结算开始 =====");

                // 步骤0：清理失效ID + 补充注册新单位的死亡回调
                CleanupInactiveCultivators();
                Events.EventHooks.CheckAndHookNewActors();

                // 步骤0.3：动态真神纪元检查当前主宰是否存活，陨落则纪元更迭
                try { CheckDominatorAlive(); } catch (Exception e) { DSDebug.Warning($"主宰存活检查异常: {e.Message}"); }

                // 步骤0.5：战乱+地形破坏失控指数结算
                Events.EventHooks.OnAnnualTick();

                // 步骤1：凡人觉醒（全局抽样，不遍历全部单位）
                AwakenMortals();

                // 步骤2：活跃异能者年度结算（修炼异能能量、失控指数、突破）
                var snapshot = new List<long>(_activeCultivatorIds);
                int settledCount = 0;
                foreach (long id in snapshot)
                {
                    Actor actor = FindActorById(id);
                    if (actor != null && actor.isAlive() && CultivationData.IsAscended(actor))
                    {
                        // 清理无存活成员的空组织（防"没人组织"残留）
                        try { Code.Sect.SectManager.CleanupEmptySects(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteGlobalAnnualSettlement 异常: " + dsEx.Message); }
                        // 兜底：组织自动归属 + 自动收藏（突破未触发时年度补齐）
                        try { Code.Sect.SectManager.EnsureSectMembership(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteGlobalAnnualSettlement 异常: " + dsEx.Message); }
                        try { Code.Core.AutoFavoriteManager.CheckAndAutoFavorite(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteGlobalAnnualSettlement 异常: " + dsEx.Message); }
                        ExecuteUnitAnnualSettlement(actor);
                        settledCount++;
                    }
                }
                DSDebug.Verbose($"活跃异能者年度结算: {settledCount}人");

                // 步骤9：灾变系统年度结算（天地屏障、虚空裂隙）
                if (Code.Core.DengShenConfig.EnableDisasterSystem) Disaster.DisasterManager.OnAnnualTick();

                // 步骤10：宏观维度空间年度结算
                Dimension.DimensionRealmManager.OnAnnualTick();

                // 步骤10.5：境界内容系统年度结算（技能冷却、事件、建筑效果）
                Realm.RealmAbilities.OnAnnualTick();
                Realm.RealmMechanics.OnAnnualTick(); // 基因化身回收 + 神眷清理
                if (Code.Core.DengShenConfig.EnableRealmEvents) Realm.RealmEvents.OnAnnualTick();
//                 if (Code.Core.DengShenConfig.EnableRealmBuildings) Realm.RealmBuildings.OnAnnualTick();

                // 步骤10.55：领域系统年度脉冲（对所有活跃异能者触发领域效果）
                try
                {
                    foreach (long id in _activeCultivatorIds)
                    {
                        Actor actor = FindActorById(id);
                        if (actor != null && actor.isAlive())
                        {
                            Realm.DSDomainSystem.Pulse(actor);
                        }
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Warning("[DivineAscension] 领域系统年度结算异常: " + e.Message);
                }

                // 步骤10.55：年度信仰结算（登神级收集信徒贡献的信仰能量）
                Realm.VanillaIntegration.AnnualFaithSettlement();

                // 步骤10.6：历史系统已简化（参考西幻世界实现），消息直接调用message.add()，不需要刷新队列

                // 步骤10.8：自动创办组织（高阶无组织异能者有概率自行组建组织）
                AutoFoundSects();

                DSDebug.Verbose($"===== 全局年度结算完成，活跃异能者={_activeCultivatorIds.Count} =====");
            }
            catch (Exception e)
            {
                DSDebug.Error("全局年度结算异常: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>动态真神纪元：检查当前主宰是否存活，陨落则纪元更迭</summary>
        private static void CheckDominatorAlive()
        {
            var dominator = Realm.RealmJudge.GetCurrentDominator();
            if (dominator == null) return;

            // 通过ID查找主宰Actor，检查是否存活
            long actorId;
            if (!long.TryParse(dominator.ActorId, out actorId)) return;

            Actor actor = FindActorById(actorId);
            if (actor == null || !actor.isAlive())
            {
                // 主宰陨落  纪元更迭
                string deadName = dominator.Name;
                int oldEra = Realm.RealmJudge.GetCurrentEra();
                Realm.RealmJudge.OnDominatorDeath(dominator.ActorId);
                int newEra = Realm.RealmJudge.GetCurrentEra();

                DSDebug.Verbose($"[DivineAscension] 纪元主宰 {deadName} 陨落，第{oldEra}纪元终结，第{newEra}纪元开启（混沌无主）");

                // 全局通知 + 历史记录
                try
                {
                    DSNotificationManager.NotifyMajor(UILocalization.Get("era_change_god_death_title"),
    string.Format(UILocalization.Get("era_change_god_death_desc"), deadName, oldEra, newEra));
                    DSEventManager.RecordEvent(
                        DSEventType.Divine,
                        UILocalization.Get("era_change_title"),
                        string.Format(UILocalization.Get("era_change_desc"), deadName, oldEra, newEra),
                        deadName,
                        long.TryParse(dominator.ActorId, out long aid) ? aid : 0,
                        13
                    );
                }
                catch (Exception e)
                {
                    DSDebug.Warning($"纪元更迭通知失败: {e.Message}");
                }
            }
        }

        /// <summary>单位年度结算（每个单位年龄增长时调用：异能者结算异能能量/失控指数/进阶，凡人检查觉醒）</summary>
        private static void ExecuteUnitAnnualSettlement(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;

            try
            {
                if (CultivationData.IsAscended(actor))
                {
                    // 异能者：执行异能能量/失控指数/深度觉醒/进阶/境界跌落/失控惩罚
                    SettleSingleCultivator(actor);
                }
                else
                {
                    // 凡人：单独检查觉醒（全局AwakenMortals已抽样，这里补漏）
                    TryAwakenSingleMortal(actor);
                }
            }
            catch (Exception e)
            {
                DSDebug.Error("[DivineAscension] 单位年度结算异常: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>对单个异能者执行步骤2~7的完整结算</summary>
        private static void SettleSingleCultivator(Actor actor)
        {
            // 神创基因旧存档适配：10阶以上单位自动解锁神创基因
            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                if (tier >= 10 && !CultivationData.IsDivineGeneUnlocked(actor))
                {
                    CultivationData.UnlockDivineGene(actor);
                }
            }
            catch { }
            
            //  年度统计钩子：修炼年数+1，更新历史失控峰值
            try { CultivationData.AddCultivationYear(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }
            try { CultivationData.UpdateMaxTurbulence(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }

            if (Code.Core.DengShenConfig.EnableAutoCultivation)
            {
                // 步骤2：异能能量结算
                EnergyTurbulenceCalculator.SettleAnnualQi(actor);

                // 步骤2.1：更新基因能量缓存（年度Tick时计算，战斗时直接读取，减轻性能负担）
                try { GeneEnergyCalculator.CalculateAndCacheGeneEnergy(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }
                // 步骤2.1.1：更新基因连锁加成缓存（年度Tick时计算，战斗时直接读取，减轻性能负担）
                try { GeneLinkageSystem.RefreshLinkageCaches(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }

                // 注：GE 攻速/射程/速度加成已改由 Actor.updateStats Postfix 应用（stats 每次重建自动带上），
                // 不在年度结算重复调用，避免双倍叠加（GeneEnergyAttributeApplier.ApplyAttributes）

                //  13阶超神级全局影响：全球异能者能量恢复+20%
                try
                {
                    if (Realm.RealmFeatures.HasTrueGodInWorld())
                    {
                        float currentQi = CultivationData.GetEnergy(actor);
                        float bonus = currentQi * 0.20f;
                        if (bonus > 1f)
                        {
                            CultivationData.SetEnergy(actor, currentQi + bonus);
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }

                // 步骤3：失控指数结算
                float externalGain = EnergyTurbulenceCalculator.ConsumeAnnualTurbulenceGain(actor);
                EnergyTurbulenceCalculator.SettleAnnualTurbulence(actor, externalGain);

                //  自动进入/排斥维度空间
                try
                {
                    int tier = CultivationData.GetRealmTier(actor);
                    bool isInDimension = CultivationData.IsInDimensionSpace(actor);

                    // 12阶以上自动进入维度空间修炼
                    if (tier >= 12 && !isInDimension)
                    {
                        Code.Dimension.DimensionRealmManager.EnterDimension(actor);
                    }
                    // 非12阶单位在维度空间里，自动排斥出去
                    else if (tier < 12 && isInDimension)
                    {
                        Code.Dimension.DimensionRealmManager.ExitDimension(actor);
                        // 扣血惩罚
                        // 扣血惩罚：当前血量存在 actor.data.health（int类型）
                        try
                        {
                            int currentHp = (int)actor.data.health;
                            actor.data.health = (int)(currentHp * 0.9f); // 扣10%血
                        }
                        catch { }
                        DSDebug.Verbose($"[DivineAscension] 非12阶单位 {actor.name} 被排斥出维度空间，扣10%血");
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Verbose($"[DivineAscension] 自动进入维度空间失败: {e.Message}");
                }

                //  维度空间效果：修炼加速+失控消解
                try
                {
                    if (CultivationData.IsInDimensionSpace(actor))
                    {
                        // 修炼加速：额外获得100%异能能量
                        float currentQi = CultivationData.GetEnergy(actor);
                        float spaceBonus = currentQi * 1.0f;
                        if (spaceBonus > 1f)
                        {
                            CultivationData.SetEnergy(actor, currentQi + spaceBonus);
                        }

                        // 失控消解：额外降低5点失控指数
                        float turb = CultivationData.GetTurbulence(actor);
                        if (turb > 0f)
                        {
                            CultivationData.SetTurbulence(actor, Mathf.Max(0f, turb - 5f));
                        }

                        // 空间能量自动恢复（每年+10）
                        float spaceEnergy = CultivationData.GetDimensionSpaceEnergy(actor);
                        CultivationData.SetDimensionSpaceEnergy(actor, spaceEnergy + 10f);
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }

                // 步骤4：深度觉醒经验积累
                EnergyTurbulenceCalculator.SettleEnlightenment(actor);

                // 步骤4.5：能力掌控度提升（现代异能体系，替代印记系统）
                //  掌控度通过年度修炼自动提升，达到100才能突破
                SettleAnnualMastery(actor);

                // 步骤4.8：基因图谱解锁检查（检查当前境界的基因节点是否满足解锁条件）
                SettleGeneGraph(actor);
            }

            if (Code.Core.DengShenConfig.EnableAutoBreakthrough)
            {
                // 步骤5：进阶判定
                TryBreakthrough(actor);
            }

            // 步骤6：失控惩罚应用
            EnergyTurbulenceCalculator.ApplyTurbulencePenalties(actor);

            // 步骤8：战斗位格防护（跨阶伤害衰减回溯）
            Combat.CombatRank.ApplyCombatRankProtection(actor);

            // 步骤9：状态持续时间递减（深度觉醒等）
            SettleStatusDurations(actor);

            // 步骤10：境界特色机制（每个境界独特的年度效果）
            Realm.RealmFeatures.ApplyAnnualRealmFeatures(actor);

            // 步骤11：年度刷新称号（组织职位/基因变化自动反映；基因称号已缓存不会变）
            try { Code.Realm.TitleSuffixManager.UpdateTitleSuffix(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 异常: " + dsEx.Message); }

            // 步骤12：组织贡献年度累积（按职位，供组织贡献榜/单位窗口显示）
            try
            {
                int sectRank = CultivationData.GetSectRank(actor);
                if (sectRank > 0)
                {
                    float contrib = Code.Sect.SectManager.GetAnnualContribution(sectRank);
                    if (contrib > 0f) CultivationData.AddSectContribution(actor, contrib);
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleSingleCultivator 组织贡献异常: " + dsEx.Message); }
        }

        /// <summary>年度能力掌控度提升（现代异能体系，替代印记系统）</summary>
        private static void SettleAnnualMastery(Actor actor)
        {
            if (actor == null || !CultivationData.IsAscended(actor)) return;

            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0 || tier >= 13) return; // 真神不需要再提升掌控度

                float currentMastery = CultivationData.GetMastery(actor);
                if (currentMastery >= 100f) return; // 已经完全掌控

                // 基础掌控度提升：低阶快，高阶慢
                // 1-3阶：每年15点，4-6阶：每年10点，7-9阶：每年7点，10-12阶：每年5点
                float baseGain = 15f;
                if (tier >= 4) baseGain = 10f;
                if (tier >= 7) baseGain = 7f;
                if (tier >= 10) baseGain = 5f;

                // 深度觉醒状态：×2.0
                float enlightMultiplier = CultivationData.GetEnlightenmentDuration(actor) > 0f ? 2.0f : 1.0f;

                // 修炼配置倍率
                float configMultiplier = CultivationConfig.CultivationRateMultiplier;

                // 基因节点修炼效率加成
                var geneEffects = GeneGraphManager.CalculateEffects(actor);
                float geneCultBonus = 1f + geneEffects.CultivationBonus / 100f;

                //  原版智力属性加成：smart/genius/blessed/scholar/mage等特质提升修炼速度
                float vanillaIntBonus = VanillaInteractionManager.GetCultivationSpeedMultiplier(actor);

                // 计算最终提升
                float finalGain = baseGain * 1.0f * enlightMultiplier * configMultiplier * geneCultBonus * vanillaIntBonus;

                // 随机波动（±20%）
                finalGain *= UnityEngine.Random.Range(0.8f, 1.2f);

                CultivationData.AddMastery(actor, finalGain);

                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 能力掌控度提升 {finalGain:F1}  {CultivationData.GetMastery(actor):F1}/100");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"SettleAnnualMastery异常: {actor.getName()}, {e.Message}");
            }
        }

        /// <summary>基因图谱解锁检查（检查当前境界的基因节点是否满足解锁条件）</summary>
        private static void SettleGeneGraph(Actor actor)
        {
            if (actor == null || !CultivationData.IsAscended(actor)) return;

            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0 || tier > 13) return;

                // 更新进度数据
                CultivationData.AddGeneContinuousYear(actor);

                float corruption = CultivationData.GetTurbulence(actor);
                if (corruption < 40f)
                {
                    CultivationData.AddGeneLowCorrYear(actor);
                }
                else
                {
                    CultivationData.ResetGeneLowCorrYears(actor);
                    CultivationData.ResetGeneContinuousYears(actor); // 修炼失衡，连续修炼中断
                }

                // 如果有场域，增加场域维持天数（简化处理，每年365天）
                if (tier >= 6)
                {
                    CultivationData.AddGeneDomainDay(actor);
                }

                // 如果深度觉醒状态激活，增加深度觉醒次数
                if (CultivationData.GetEnlightenmentDuration(actor) > 0f)
                {
                    CultivationData.AddGeneDeepEnlight(actor);
                }

                // 检查当前境界的所有基因节点
                var nodes = Realm.GeneGraphManager.GetNodesByTier(tier);
                int newlyUnlocked = 0;

                foreach (var node in nodes)
                {
                    if (CultivationData.IsGeneUnlocked(actor, node.Id)) continue;

                    bool unlocked = false;

                    switch (node.UnlockType)
                    {
                        case Realm.GeneUnlockType.EnergyThreshold:
                            float energy = CultivationData.GetEnergy(actor);
                            unlocked = energy >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.ContinuousCultivation:
                            unlocked = CultivationData.GetGeneContinuousYears(actor) >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.CombatKills:
                            unlocked = CultivationData.GetGeneCombatKills(actor) >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.CombatDefense:
                            unlocked = CultivationData.GetGeneCombatDefense(actor) >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.AbilityUses:
                            unlocked = CultivationData.GetGeneAbilityUses(actor) >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.LowCorruption:
                            unlocked = CultivationData.GetGeneLowCorrYears(actor) >= node.UnlockParam2 &&
                                       corruption < node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.AgeRequirement:
                            unlocked = actor.getAge() >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.DeepEnlightenment:
                            unlocked = CultivationData.GetGeneDeepEnlightCount(actor) >= node.UnlockParam1;
                            break;

                        case Realm.GeneUnlockType.DomainMaintain:
                            unlocked = CultivationData.GetGeneDomainDays(actor) >= node.UnlockParam1;
                            break;

                                                case Realm.GeneUnlockType.Ritual:
                            // 顶点基因（第6表达层级·离终极最近）：同染色体链式递进解锁
                            // 必须走完本染色体前5表达层级递进链（15）才能触及本染色体顶点（6）
                            unlocked = CanUnlockRitualElement(actor, node.Id);
                            break;

                                                case Realm.GeneUnlockType.Special:
                            // 特殊条件：达到境界自动解锁
                            unlocked = true;
                            break;

                        case Realm.GeneUnlockType.SourceUnknown:
                            // 未知：本源未展开，当前无法主动获得
                            unlocked = false;
                            break;
                    }

                    if (unlocked)
                    {
                        CultivationData.UnlockGene(actor, node.Id);
                        newlyUnlocked++;
                        DSDebug.Verbose($"[DivineAscension] {actor.getName()} 解锁基因节点: {node.NameZh} ({node.Id})");
                    }
                }

                if (newlyUnlocked > 0)
                {
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 年度基因图谱解锁: {newlyUnlocked}个新节点");
                }

                //  基因突变机制：完成基因任务后按概率获得，未获得可重复尝试（下一年度）
                try { GeneMutationSystem.AnnualMutationCheck(actor); } catch (Exception e) { DSDebug.Warning($"基因突变异常: {e.Message}"); }
                // 修炼时缓慢提升基因表达层级
                try { Code.Realm.GeneExpressionSystem.CultivationExpressionUpgrade(actor); } catch (Exception e) { DSDebug.Warning($"基因表达提升异常: {e.Message}"); }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"SettleGeneGraph异常: {actor.getName()}, {e.Message}");
            }
        }

        /// <summary>
        /// 顶点基因（第6表达层级·各染色体终点）解锁判定 = 同染色体链式递进 + 各基因差异化条件：
        /// 同染色体前5表达层级全部解锁走完本染色体递进链（15）才能触及本染色体顶点（6）。
        /// 在此基础上，每个第6表达层级基因有各自的额外解锁条件（利用UnlockParam作为阈值参数）：
        ///   超越=金色染色体/登神种族达成；暗物质=基因接近全解锁；
        ///   辐射=累计击杀达标；核力=连续修炼年数达标；折叠=低失控指数；
        ///   量子=深度觉醒经验达标；共鸣=组织高层；真理=全基因图鉴接近完成。
        /// 真理（48号·宇宙终极法则）成神判定在 TraitManager.CanAscendToGod 中单独把关。
        /// </summary>
        private static bool CanUnlockRitualElement(Actor actor, string elementId)
        {
            var elem = ElementDef.GetById(elementId);
            if (elem == null) return false;

            var unlocked = CultivationData.GetUnlockedElements(actor);
            if (unlocked == null || unlocked.Count == 0) return false;

            // 深度：同染色体前5表达层级全部解锁（所有第6表达层级基因的共同基础条件）
            for (int p = 1; p <= 5; p++)
            {
                var prev = ElementDef.GetByGroupPeriod(elem.Group, p);
                if (prev == null) continue;
                if (!unlocked.Contains(prev.Id)) return false;
            }

            // 差异化：各基因额外解锁条件（UnlockParam作为阈值参数，40~54）
            float param = elem.UnlockParam;
            switch (elementId)
            {
                case "gene_organ_function": // 超越（生命染色体顶点）：金色染色体/登神种族达成
                    if (!Code.Realm.TranscendentSpeciesSystem.IsTranscendent(actor))
                    {
                        // 未达登神种族则检查完美生命基因特质
                        if (!actor.hasTrait("ds_perfect_life")) return false;
                    }
                    break;

                case "gene_energy_supply": // 能量供给（异能染色体顶点）：解锁至少20个基因
                    if (unlocked.Count < 20) return false;
                    break;

                case "gene_reaction_speed": // 辐射（能量染色体顶点）：累计击杀数达标
                    if (CultivationData.GetGeneCombatKills(actor) < (int)param) return false;
                    break;

                case "gene_creativity": // 核力（力场染色体顶点）：连续修炼年数达标
                    if (CultivationData.GetGeneContinuousYears(actor) < (int)param) return false;
                    break;

                case "gene_proprioception": // 折叠（时空染色体顶点）：低失控指数（空间折叠需要精密控制）
                    if (CultivationData.GetTurbulence(actor) > param) return false;
                    break;

                case "gene_energy_manipulation": // 量子（信息染色体顶点）：深度觉醒经验达标（param×100）
                    if (CultivationData.GetEnlightenmentExp(actor) < param * 100f) return false;
                    break;

                case "gene_willpower": // 共鸣（意识染色体顶点）：组织高层（职位3，共鸣需要组织连接）
                    if (CultivationData.GetSectRank(actor) < 3) return false;
                    break;

                case "gene_life_sublimation": // 真理（概念染色体顶点）：全基因图鉴接近完成（40/48已知基因）
                    if (unlocked.Count < 40) return false;
                    break;

                default:
                    // 未知第6表达层级基因：仅需同染色体前5表达层级（向后兼容）
                    break;
            }

            return true;
        }

        /// <summary>状态持续时间递减（深度觉醒等有持续时间的状态）</summary>
        private static void SettleStatusDurations(Actor actor)
        {
            // 深度觉醒状态递减
            float enlightDur = CultivationData.GetEnlightenmentDuration(actor);
            if (enlightDur > 0f)
            {
                enlightDur -= 1f;
                if (enlightDur <= 0f)
                {
                    enlightDur = 0f;
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 深度觉醒状态结束");
                }
                CultivationData.SetEnlightenmentDuration(actor, enlightDur);
            }

            //  技能状态效果能量维持消耗
            // 活跃技能状态（ds_ability_*）每年消耗能量维持，能量不足则状态效果提前消失
            try
            {
                if (actor != null && actor.isAlive() && CultivationData.IsAscended(actor))
                {
                    var allAbilities = Realm.RealmAbilities.GetAllAbilities();
                    if (allAbilities != null)
                    {
                        float totalUpkeepCost = 0f;
                        var activeAbilities = new List<Realm.RealmAbility>();

                        foreach (var kvp in allAbilities)
                        {
                            var ability = kvp.Value;
                            if (ability == null) continue;
                            // 只对增益型/持续型技能收取维持消耗（伤害型是瞬时的，不维持）
                            if (ability.Type != Realm.AbilityType.Buff &&
                                ability.Type != Realm.AbilityType.Ultimate &&
                                ability.Type != Realm.AbilityType.Heal)
                                continue;

                            // 直接调用hasStatus方法
                            bool hasStatus = false;
                            try
                            {
                                hasStatus = actor.hasStatus(ability.Id);
                            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleStatusDurations 异常: " + dsEx.Message); }
                            
                            if (hasStatus)
                            {
                                activeAbilities.Add(ability);
                                // 维持消耗 = 技能释放消耗的10%/年
                                totalUpkeepCost += ability.EnergyCost * 0.10f;
                            }
                        }

                        if (activeAbilities.Count > 0 && totalUpkeepCost > 0f)
                        {
                            float currentEnergy = CultivationData.GetEnergy(actor);
                            if (currentEnergy >= totalUpkeepCost)
                            {
                                // 能量足够，扣除维持消耗
                                CultivationData.SetEnergy(actor, currentEnergy - totalUpkeepCost);
                            }
                            else
                            {
                                // 能量不足，移除所有活跃技能状态效果
                                foreach (var ability in activeAbilities)
                                {
                                    try { Code.Core.DSStatusManager.RemoveStatus(actor, ability.Id); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleStatusDurations 异常: " + dsEx.Message); }
                                }
                                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 异能能量不足，{activeAbilities.Count}个技能状态效果提前消散");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"技能维持消耗结算异常: {e.Message}");
            }
        }

        // ============================================================
        //  进阶判定
        // ============================================================

        private static void TryBreakthrough(Actor actor)
        {
            int tier = CultivationData.GetRealmTier(actor);
            if (tier >= 13) return;
            if (CultivationData.GetTurbulence(actor) >= CultivationConfig.BreakthroughTurbulenceLimit) return;

            // 优先正规突破
            if (RealmJudge.CanBreakthrough(actor, out _))
            {
                RealmJudge.DoBreakthrough(actor);
                return;
            }

            // 深度觉醒经验足够则尝试深度觉醒突破
            int nextTier = tier + 1;
            if (CultivationData.GetEnlightenmentExp(actor) >= 500f * nextTier)
            {
                RealmJudge.DoEnlightenmentBreakthrough(actor);
            }
        }

        // ============================================================
        //  凡人觉醒
        // ============================================================

        /// <summary>
        /// 年度凡人觉醒。每年抽样检查部分凡人，按概率觉醒。
        /// 年度级别的遍历是允许的（禁令4禁止的是逐帧遍历）。
        /// </summary>
        private static void AwakenMortals()
        {
            if (!Code.Core.DengShenConfig.EnableAutoAwakening)
            {
                DSDebug.Verbose("[DivineAscension] 自动觉醒已关闭，跳过凡人觉醒抽样");
                return;
            }
            if (World.world == null || World.world.units == null)
            {
                DSDebug.Warning("[DivineAscension] AwakenMortals: World或units为null");
                return;
            }

            int checkedCount = 0;
            int awakenedCount = 0;
            int maxCheckPerYear = 2000; // 每年最多检查2000个凡人，让更多异能者产生
            int totalUnits = 0;
            int errorCount = 0;

            try
            {
                var aliveUnits = World.world.units.units_only_alive;
                if (aliveUnits == null)
                {
                    DSDebug.Warning("[DivineAscension] AwakenMortals: units_only_alive为null");
                    return;
                }

                foreach (var actor in aliveUnits)
                {
                    totalUnits++;
                    if (checkedCount >= maxCheckPerYear) break;
                    if (actor == null || !actor.isAlive()) continue;

                    try
                    {
                        if (CultivationData.IsAscended(actor)) continue;

                        // 动物也可以觉醒异能（现代异能体系特色），达到3阶后自动获得高级脑功能区·文明化
                        if (actor.isAnimal() && !Code.Core.DengShenConfig.EnableAnimalAwakening) continue;
                        checkedCount++;

                        // 随机觉醒
                        if (UnityEngine.Random.value <= CultivationConfig.MortalAwakenChance)
                        {
                            if (RealmJudge.AwakenMortal(actor))
                            {
                                RegisterCultivator(actor);
                                awakenedCount++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        errorCount++;
                        if (errorCount <= 3) // 只输出前3个错误，避免刷屏
                        {
                            DSDebug.Error($"[DivineAscension] AwakenMortals处理单位异常: {e.Message}\n{e.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Error($"[DivineAscension] AwakenMortals遍历异常: {e.Message}\n{e.StackTrace}");
            }

            // 详细调试日志（仅在调试模式下输出）
            DSDebug.Verbose($"凡人觉醒结算: 总单位={totalUnits}, 抽样检查={checkedCount}, 觉醒成功={awakenedCount}, 错误={errorCount}, 觉醒概率={CultivationConfig.MortalAwakenChance}");
        }

        /// <summary>单个凡人觉醒检查（年龄增长时调用，补漏全局抽样）</summary>
        private static void TryAwakenSingleMortal(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            if (CultivationData.IsAscended(actor)) return;

            // 检查是否是智慧生物（有种族脑属性或高级脑功能区·文明化特质）
            bool isIntelligent = IsHumanoidCivilized(actor);

            // 年龄越大觉醒概率越高
            int age = Mathf.FloorToInt((float)actor.getAge());

            // 智慧生物：18岁以上才可能觉醒
            // 非智慧生物（动物）：1岁以上就可能觉醒（动物寿命短）
            int minAge = isIntelligent ? 18 : 1;
            if (age < minAge) return;

            // 动物觉醒概率更低（没有智慧，难以觉醒异能）
            float awakenChance = CultivationConfig.MortalAwakenChance * Mathf.Clamp(age / 30f, 0.5f, 2f);
            if (!isIntelligent) awakenChance *= 0.3f; // 动物觉醒概率为智慧生物的30%

            if (UnityEngine.Random.value <= awakenChance)
            {
                if (RealmJudge.AwakenMortal(actor))
                {
                    RegisterCultivator(actor);
                }
            }
        }

        /// <summary>
        /// 自动创立组织（年度Tick调用）
        /// 高阶无组织异能者有概率自动创立组织，不需要玩家手动操作
        /// </summary>
        private static void AutoFoundSects()
        {
            try
            {
                int foundedCount = 0;
                // 组织数量上限：全局最多10个，满了不再自动创立（防组织爆炸）
                if (Sect.SectManager.GetAllSects().Count >= 10) return;
                var snapshot = new List<long>(_activeCultivatorIds);

                foreach (long id in snapshot)
                {
                    Actor actor = FindActorById(id);
                    if (actor == null || !actor.isAlive()) continue;
                    if (!CultivationData.IsAscended(actor)) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    string sectId = CultivationData.GetSectId(actor);

                    // 只有3阶以上、无组织的异能者才可能自动创立组织
                    if (tier < 3 || !string.IsNullOrEmpty(sectId)) continue;

                    // 创立概率：境界越高概率越大
                    // 3阶: 2%, 4阶: 3%, 5阶: 5%, 6阶: 8%, 7阶: 12%, 8阶+: 15%
                    float foundChance = tier switch
                    {
                        3 => 0.02f,
                        4 => 0.03f,
                        5 => 0.05f,
                        6 => 0.08f,
                        7 => 0.12f,
                        _ => 0.15f
                    };

                    if (UnityEngine.Random.value <= foundChance)
                    {
                        // 自动生成组织名称
                        string sectName = Sect.SectManager.GenerateRandomSectName();

                        // 创立组织
                        var sect = Sect.SectManager.FoundSect(actor, sectName);
                        if (sect != null)
                        {
                            foundedCount++;
                            DSDebug.Verbose($"[DivineAscension] {actor.getName()}({tier}阶) 自动创立组织 [{sectName}]");

                            // 记录到事件管理器（个人史诗风格）
                            DSEventManager.RecordEvent(
                                DSEventType.Faction,
                                string.Format(UILocalization.Get("sect_founded_notify"), TitleSuffixManager.ExtractBaseName(actor.getName()), tier, sectName),
                                "",
                                actor.getName(),
                                actor.getID(), 0, actor);
                        }
                    }
                }

                if (foundedCount > 0)
                {
                    DSDebug.Verbose($"自动创立组织: {foundedCount}个");
                }
            }
            catch (Exception e)
            {
                DSDebug.Error("自动创立组织异常: " + e.Message);
            }
        }

        // ============================================================
        //  工具方法
        // ============================================================

        private static void CleanupInactiveCultivators()
        {
            _activeCultivatorIds.RemoveAll(id =>
            {
                Actor actor = FindActorById(id);
                return actor == null || !actor.isAlive();
            });
        }

        /// <summary>判断是否为智慧生物（凡人觉醒前置）</summary>
        /// <remarks>
        /// 判定标准（严格接轨原版游戏）：
        /// 1. 种族 brain 属性为 true（原版智慧种族：人类/精灵/矮人/兽人等）
        /// 2. 拥有 civilized（文明化）特质（非智慧种族通过异能/进化开智后）
        /// 注意：动物天生不是智慧生物，必须通过异能觉醒达到3阶后获得 civilized 特质才能修炼
        /// </remarks>
        public static bool IsHumanoidCivilized(Actor actor)
        {
            if (actor == null) return false;

            // 判断标准1：种族脑属性（原版智慧种族天生具有脑属性）
            try
            {
                // 有王国时通过 kingdom.asset.brain 判断
                if (actor.kingdom != null && actor.kingdom.asset != null)
                {
                    var brainField = actor.kingdom.asset.GetType().GetField("brain",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (brainField != null && (bool)brainField.GetValue(actor.kingdom.asset))
                        return true;
                }
                // 无王国时通过种族 asset 判断
                if (actor.asset != null)
                {
                    var brainField = actor.asset.GetType().GetField("brain",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (brainField != null && (bool)brainField.GetValue(actor.asset))
                        return true;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] IsHumanoidCivilized 异常: " + dsEx.Message); }

            // 判断标准2：civilized（文明化）特质非智慧种族通过异能或进化开智后获得
            if (actor.hasTrait("civilized"))
                return true;

            return false;
        }

        /// <summary>
        /// 获取全局修行统计快照（用于异能图鉴面板）。
        /// 面板读取快照，不实时全图遍历。
        /// </summary>
        public static CultivationStats GetStatsSnapshot()
        {
            var stats = new CultivationStats();
            var snapshot = new List<long>(_activeCultivatorIds);

            foreach (long id in snapshot)
            {
                Actor actor = FindActorById(id);
                if (actor == null) continue;

                stats.TotalCultivators++;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier >= 1 && tier <= 4) stats.MortalRealm++;
                else if (tier >= 5 && tier <= 9) stats.CultivatorRealm++;
                else if (tier == 10 || tier == 11) stats.SanctuaryCount++;
                else if (tier == 12) stats.Demigod++;
                else if (tier == 13) stats.TrueGod++;

                stats.TotalTurbulence += CultivationData.GetTurbulence(actor);
            }

            if (stats.TotalCultivators > 0)
                stats.AverageTurbulence = stats.TotalTurbulence / stats.TotalCultivators;

            return stats;
        }
    }

    /// <summary>修行统计快照</summary>
    public class CultivationStats
    {
        public int TotalCultivators;
        public int MortalRealm;
        public int CultivatorRealm;
        public int SanctuaryCount;
        public int Demigod;
        public int TrueGod;
        public float TotalTurbulence;
        public float AverageTurbulence;
    }

    /// <summary>
    /// Harmony Patch: Actor.updateAge Postfix
    /// 每个单位年龄增长时触发年度Tick（替代原来的getCurWorldTime/365帧检测）。
    /// getAge()每年+1，比世界时间换算更可靠。
    /// </summary>
    [HarmonyPatch(typeof(Actor), "updateAge")]
    public static class ActorUpdateAgePatch
    {
        private static int _patchCallCount = 0;
        private static int _settleCount = 0;
        private static int _patchExceptionCount = 0; // 异常限频，防止日志爆炸

        static void Postfix(object __instance)
        {
            try
            {
                _patchCallCount++;
                // 前10次调用输出日志，确认Patch生效
                if (_patchCallCount <= 10)
                {
                    DSDebug.Verbose($"[DivineAscension] updateAge Patch触发 #{_patchCallCount}");
                }

                Actor actor = __instance as Actor;
                if (actor != null)
                {
                    bool settled = AnnualTickManager.OnActorAgeUpdated(actor);
                    if (settled)
                    {
                        _settleCount++;
                        if (_settleCount <= 5)
                        {
                            DSDebug.Verbose($"[DivineAscension] 年度结算触发 #{_settleCount}，单位: {actor.getName()}，年龄: {actor.getAge()}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                // 静默失败，不影响游戏原生逻辑
                if (_patchExceptionCount < 20)
                {
                    _patchExceptionCount++;
                DSDebug.Warning("[DivineAscension] updateAge Patch异常: " + e.Message);
                }
            }
        }
    }
}



















