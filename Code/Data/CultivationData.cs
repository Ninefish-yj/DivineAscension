// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Core;

namespace Code.Data
{
    /// <summary>
    /// 异能数据静态访问层
    /// 所有数据存储于ActorData的custom_data容器，随存档自动持久化
    /// 注意：custom_data key字符串保持不变以兼容存档迁移
    /// </summary>
    public static class CultivationData
    {
        // ===== 数据键常量（C#常量名已现代化，custom_data key字符串保持不变以兼容存档迁移）=====
        private const string K_ENERGY = "ds_qi";              // 异能能量（原"灵气"，key不变）
        private const string K_TURB = "ds_turb";
        private const string K_TIER = "ds_tier";
        private const string K_ANCHOR_MIND = "ds_anchor";     // 道心锚点心神锚点（key不变）
        private const string K_DOMAIN = "ds_domain";
        private const string K_SANCTUARY_ACTIVE = "ds_sanctuary";  // 圣域激活状态（key不变）
        private const string K_IN_DIVINE_REALM = "ds_in_divine";    // 神域中（key不变）
        private const string K_PERMANENTLY_FALLEN = "ds_fallen";    // 永久堕落（key不变）
        private const string K_ENLIGHTENMENT_EXP = "ds_enlight";     // 深度觉醒经验（原"悟道经验"，key不变）
        private const string K_ENLIGHT_DURATION = "ds_enlight_dur"; // 深度觉醒状态持续时间（年）
        private const string K_LAST_YEAR = "ds_last_year";
        private const string K_COOLDOWN_PREFIX = "ds_cd_";
        private const string K_MASTERY = "ds_mastery"; // 能力掌控度 0~100（替代印记系统，符合现代异能体系）
        private const string K_GENE_ENERGY_CACHE = "ds_ge_cache"; // 基因能量缓存（年度Tick时计算，战斗时直接读取，减轻性能负担）
        private const string K_LINKAGE_DAMAGE = "ds_link_dmg";   // 基因连锁伤害加成缓存（年度Tick时计算）
        private const string K_LINKAGE_DEFENSE = "ds_link_def";  // 基因连锁防御加成缓存
        private const string K_LINKAGE_HEALTH = "ds_link_hp";    // 基因连锁生命加成缓存

        // ============================================================
        //  基础数值读写
        // ============================================================

        /// <summary>安全获取单位名字（getName可能因数据损坏抛异常，避免日志行拖垮主逻辑）</summary>
        private static string SafeGetActorName(Actor actor)
        {
            if (actor == null) return "null";
            try { return actor.getName(); }
            catch (System.Exception) { return "Actor#" + actor.getID(); }
        }

        /// <summary>确保custom_data容器被初始化（关键修复：某些单位的custom_data_*可能为null）</summary>
        public static void EnsureCustomDataInitialized(Actor actor)
        {
            if (actor == null) return;
            var data = ActorDataAccessor.GetData(actor);
            if (data == null) return;

            string actorName = SafeGetActorName(actor);

            try
            {
                if (data.custom_data_float == null)
                {
                    data.custom_data_float = new CustomDataContainer<float>();
                }
            }
            catch { }

            try
            {
                if (data.custom_data_int == null)
                {
                    data.custom_data_int = new CustomDataContainer<int>();
                }
            }
            catch { }

            try
            {
                if (data.custom_data_string == null)
                {
                    data.custom_data_string = new CustomDataContainer<string>();
                }
            }
            catch { }

            try
            {
                if (data.custom_data_bool == null)
                {
                    data.custom_data_bool = new CustomDataContainer<bool>();
                }
            }
            catch { }
        }

        /// <summary>性能优化：一次反射获取ActorData（基础读写统一入口，替代重复GetData）</summary>
        private static ActorData GetDataOnce(Actor actor)
        {
            if (actor == null) return null;
            return ActorDataAccessor.GetData(actor);
        }

        public static float GetEnergy(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return 0f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return 0f;
            data.custom_data_float.TryGetValue(K_ENERGY, out float val);
            return val;
        }
        private static void SetCustomFloat(Actor actor, string key, float value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_float == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[key] = value;
        }

        public static void SetEnergy(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_ENERGY] = Mathf.Max(0f, value);
        }

        /// <summary>获取基因能量缓存（年度Tick时计算，战斗时直接读取，减轻性能负担）</summary>
        public static float GetGeneEnergyCache(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return 0f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return 0f;
            data.custom_data_float.TryGetValue(K_GENE_ENERGY_CACHE, out float val);
            return val;
        }

        /// <summary>设置基因能量缓存（年度Tick时调用）</summary>
        public static void SetGeneEnergyCache(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_GENE_ENERGY_CACHE] = Mathf.Max(0f, value);
        }

        /// <summary>读取基因连锁伤害缓存（-1=未缓存）</summary>
        public static float GetLinkageDamageCache(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return -1f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return -1f;
            if (data.custom_data_float.TryGetValue(K_LINKAGE_DAMAGE, out float val)) return val;
            return -1f;
        }
        public static void SetLinkageDamageCache(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_LINKAGE_DAMAGE] = value;
        }

        /// <summary>读取基因连锁防御缓存（-1=未缓存）</summary>
        public static float GetLinkageDefenseCache(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return -1f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return -1f;
            if (data.custom_data_float.TryGetValue(K_LINKAGE_DEFENSE, out float val)) return val;
            return -1f;
        }
        public static void SetLinkageDefenseCache(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_LINKAGE_DEFENSE] = value;
        }

        /// <summary>读取基因连锁生命缓存（-1=未缓存）</summary>
        public static float GetLinkageHealthCache(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return -1f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return -1f;
            if (data.custom_data_float.TryGetValue(K_LINKAGE_HEALTH, out float val)) return val;
            return -1f;
        }
        public static void SetLinkageHealthCache(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_LINKAGE_HEALTH] = value;
        }

        public static float GetTurbulence(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return 0f;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return 0f;
            data.custom_data_float.TryGetValue(K_TURB, out float val);
            return val;
        }
        public static void SetTurbulence(Actor actor, float value)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;
            data.custom_data_float[K_TURB] = Mathf.Clamp(value, 0f, 100f);
        }

        public static int GetRealmTier(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (data == null) return 0;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_int == null) return 0;
            data.custom_data_int.TryGetValue(K_TIER, out int val);
            return val;
        }
        public static void SetRealmTier(Actor actor, int value, bool allowBigJump = false)
        {
            var data = GetDataOnce(actor);
            if (data == null) return;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_int == null) return;
            int old = data.custom_data_int.TryGetValue(K_TIER, out int oldVal) ? oldVal : 0;
            int clamped = Mathf.Clamp(value, 0, 13);

            // 跳阶限制（调试面板/特质同步可绕过）：
            // 1. 不能跳3阶及以上（最多跳2阶）
            // 2. 真神（13阶）不能跳阶到达（必须从12阶正常突破）
            if (!allowBigJump && clamped > old)
            {
                int jump = clamped - old;
                if (jump > 2) clamped = old + 2;
                if (clamped == 13 && old < 12) clamped = 12;
            }

            if (old == clamped) return;
            data.custom_data_int[K_TIER] = clamped;

            // 突破到13阶超神级，自动保存到跨存档圣所
            if (clamped >= 13 && clamped > old)
            {
                try { Realm.RealmJudge.HandleTrueGodBirth(actor); } catch { }
            }
            
            // 高位阶名额限制检查（跳阶突破/调试设阶也走这里）
            // 正常突破和深度觉醒突破在RealmJudge里已检查，这里兜底防止跳阶突破绕过
            if (clamped >= 11 && clamped > old)
            {
                try
                {
                    if (!Realm.RealmJudge.EnsureTierSlot(clamped, actor))
                    {
                        // 名额已满且无法腾出位置，回退到旧境界
                        data.custom_data_int[K_TIER] = old;
                        DSDebug.Warning($"[DivineAscension] {actor.getName()} 突破到{clamped}阶失败：名额已满且无法腾出位置");
                        return;
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 名额检查异常: " + dsEx.Message); }
            }
            
            // 境界提升时记录突破统计和历史最高境界（仅提升时，跌落不计）
            if (clamped > old)
            {
                try { AddBreakthrough(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
                try { UpdateHighestTierReached(actor, clamped); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
                try { DSGlobalStats.IncrementBreakthroughCount(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
                // 跳阶检测：如果一次提升超过1阶，记录跳阶信息
                if (clamped - old > 1)
                {
                    try
                    {
                        string skipRecord = old + "->" + clamped;
                        string existing = data.custom_data_string != null && data.custom_data_string.TryGetValue(K_SKIP_TIER_RECORD, out string ex) ? ex : "";
                        if (!string.IsNullOrEmpty(existing)) existing += ",";
                        existing += skipRecord;
                        if (data.custom_data_string != null)
                        {
                            data.custom_data_string[K_SKIP_TIER_RECORD] = existing;
                        }
                        DSDebug.Verbose($"[DivineAscension] 单位{actor.getName()}跳阶：{old}阶 -> {clamped}阶");
                        // 跳阶首位突破通知：使用特殊的越级突破文案
                        try { Code.Realm.RealmJudge.OnSkipTierPioneer(actor, clamped, old); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
                    } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
                }
            }
            // 境界变化自动刷新称号（觉醒/突破/调试设阶均触发）
            try { Code.Realm.TitleSuffixManager.UpdateTitleSuffix(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
            
            // 标记境界计数缓存为脏（突破/跌落时更新缓存）
            try { Code.Realm.RealmJudge.MarkTierCacheDirty(); } catch (System.Exception ex) { DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
            // 境界变化：自动收藏（高阶/突破/组织高层）+ 组织自动归属（5阶起）
            try { Code.Core.AutoFavoriteManager.CheckAndAutoFavorite(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
            try { Code.Sect.SectManager.EnsureSectMembership(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); }
            //  种族升华（正常突破/调试设阶/直接设境界统一生效）：
            //   4阶（突变者）黑色方碑影响+基因优化；11阶（登神级）脱离种族+完美生命
            if (clamped >= 4) {
                Code.Core.DSDebug.Verbose("[DivineAscension] 检查4阶方碑: tier=" + clamped + " monolithDone=" + GetMonolithDone(actor));
                if (!GetMonolithDone(actor)) {
                    try { Code.Realm.TranscendentSpeciesSystem.ApplyMonolithEvolution(actor); }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 方碑异常: " + dsEx.Message); }
                }
            }
            if (clamped >= 10) { try { Code.Realm.TranscendentSpeciesSystem.ApplyTranscendence(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 异常: " + dsEx.Message); } }
            
            // 调用阶位特性（所有阶位的属性加成/被动/技能等）
            if (clamped > old)
            {
                try { Code.Realm.RealmFeatures.ApplyRealmFeaturesForTier(actor, clamped); } 
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SetRealmTier 阶位特性异常: " + dsEx.Message); }
            }
        }
        public static float GetAnchorMind(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_ANCHOR_MIND, out float val);
            return val;
        }
        public static float GetEnlightenmentExp(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_ENLIGHTENMENT_EXP, out float val);
            return val;
        }
        public static void SetEnlightenmentExp(Actor actor, float value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_ENLIGHTENMENT_EXP] = Mathf.Max(0f, value);
        }

        // 深度觉醒状态持续时间（年）
        public static float GetEnlightenmentDuration(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_ENLIGHT_DURATION, out float val);
            return val;
        }
        public static void SetEnlightenmentDuration(Actor actor, float value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_ENLIGHT_DURATION] = Mathf.Max(0f, value);
        }
        public static bool IsEnlightenmentActive(Actor actor)
        {
            return GetEnlightenmentDuration(actor) > 0f;
        }

        // ============================================================
        //  基因图谱系统
        // ============================================================

        private const string K_GENE_UNLOCKED = "ds_gene_unlocked";      // 已解锁基因节点ID（逗号分隔）
        private const string K_GENE_COMBAT_KILLS = "ds_gene_kills";     // 战斗击杀数
        private const string K_GENE_COMBAT_DEFENSE = "ds_gene_defense"; // 战斗承受攻击数
        private const string K_GENE_ABILITY_USES = "ds_gene_uses";       // 异能使用次数
        private const string K_GENE_CONTINUOUS_YEARS = "ds_gene_continuous"; // 连续修炼年数
        private const string K_GENE_LOW_CORR_YEARS = "ds_gene_low_corr"; // 低失控持续年数
        private const string K_GENE_DOMAIN_DAYS = "ds_gene_domain_days";  // 场域维持天数
        private const string K_GENE_DEEP_ENLIGHT = "ds_gene_deep_enlight"; // 深度觉醒次数

        /// <summary>
        /// 获取已解锁的基因节点ID列表
        /// </summary>
        public static List<string> GetUnlockedGenes(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return new List<string>();
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_string == null) return new List<string>();
            ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(K_GENE_UNLOCKED, out string val);
            if (string.IsNullOrEmpty(val)) return new List<string>();
            return new List<string>(val.Split(','));
        }

        /// <summary>
        /// 检查基因节点是否已解锁
        /// </summary>
        public static bool IsGeneUnlocked(Actor actor, string geneId)
        {
            return GetUnlockedGenes(actor).Contains(geneId);
        }

        /// <summary>
        /// 解锁基因节点
        /// </summary>
        public static void UnlockGene(Actor actor, string geneId)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            if (IsGeneUnlocked(actor, geneId)) return;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_string == null) return;
            List<string> unlocked = GetUnlockedGenes(actor);
            unlocked.Add(geneId);
            ActorDataAccessor.GetData(actor).custom_data_string[K_GENE_UNLOCKED] = string.Join(",", unlocked);
        }

        // 基因图谱进度数据访问器
        public static int GetGeneCombatKills(Actor actor) { return GetCustomInt(actor, K_GENE_COMBAT_KILLS); }
        public static void AddGeneCombatKill(Actor actor) { SetCustomInt(actor, K_GENE_COMBAT_KILLS, GetGeneCombatKills(actor) + 1); }

        // 终身不能突破（逃跑惩罚）
        private const string K_LIFETIME_BANNED = "ds_lifetime_banned";
        public static void SetLifetimeBan(Actor actor) { SetCustomInt(actor, K_LIFETIME_BANNED, 1); }
        public static bool IsLifetimeBanned(Actor actor) { return GetCustomInt(actor, K_LIFETIME_BANNED) > 0; }
        public static int GetGeneCombatDefense(Actor actor) { return GetCustomInt(actor, K_GENE_COMBAT_DEFENSE); }
        public static void AddGeneCombatDefense(Actor actor) { SetCustomInt(actor, K_GENE_COMBAT_DEFENSE, GetGeneCombatDefense(actor) + 1); }
        public static int GetGeneAbilityUses(Actor actor) { return GetCustomInt(actor, K_GENE_ABILITY_USES); }
        public static void AddGeneAbilityUse(Actor actor)
        {
            SetCustomInt(actor, K_GENE_ABILITY_USES, GetGeneAbilityUses(actor) + 1);
            //  全局统计钩子：异能使用计数+1
            try { DSGlobalStats.IncrementAbilityUseCount(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] AddGeneAbilityUse 异常: " + dsEx.Message); }
        }
        public static int GetGeneContinuousYears(Actor actor) { return GetCustomInt(actor, K_GENE_CONTINUOUS_YEARS); }
        public static void AddGeneContinuousYear(Actor actor) { SetCustomInt(actor, K_GENE_CONTINUOUS_YEARS, GetGeneContinuousYears(actor) + 1); }
        public static void ResetGeneContinuousYears(Actor actor) { SetCustomInt(actor, K_GENE_CONTINUOUS_YEARS, 0); }
        public static int GetGeneLowCorrYears(Actor actor) { return GetCustomInt(actor, K_GENE_LOW_CORR_YEARS); }
        public static void AddGeneLowCorrYear(Actor actor) { SetCustomInt(actor, K_GENE_LOW_CORR_YEARS, GetGeneLowCorrYears(actor) + 1); }
        public static void ResetGeneLowCorrYears(Actor actor) { SetCustomInt(actor, K_GENE_LOW_CORR_YEARS, 0); }
        public static int GetGeneDomainDays(Actor actor) { return GetCustomInt(actor, K_GENE_DOMAIN_DAYS); }
        public static void AddGeneDomainDay(Actor actor) { SetCustomInt(actor, K_GENE_DOMAIN_DAYS, GetGeneDomainDays(actor) + 1); }
        public static int GetGeneDeepEnlightCount(Actor actor) { return GetCustomInt(actor, K_GENE_DEEP_ENLIGHT); }
        public static void AddGeneDeepEnlight(Actor actor) { SetCustomInt(actor, K_GENE_DEEP_ENLIGHT, GetGeneDeepEnlightCount(actor) + 1); }

        // ============================================================
        //  单位详细统计数据（全部存custom_data，自动持久化）
        // ============================================================
        private const string K_CULT_YEARS = "ds_stat_cult_years";       // 修炼总时长（年）
        private const string K_BREAKTHROUGH_CNT = "ds_stat_break_cnt";  // 突破次数
        private const string K_MAX_TURB = "ds_stat_max_turb";           // 历史失控峰值
        private const string K_KILLS_TOTAL = "ds_stat_kills";           // 总击杀数
        private const string K_KILLS_ABILITY = "ds_stat_ability_kills"; // 异能击杀数
        private const string K_HIGHEST_TIER = "ds_stat_highest_tier";   // 历史最高境界
        private const string K_AWAKEN_YEAR = "ds_stat_awaken_year";     // 觉醒年份
        private const string K_TOTAL_ENERGY = "ds_stat_total_energy";   // 累计获得能量
        private const string K_DISASTERS_SURVIVED = "ds_stat_disasters";// 经历灾变次数

        /// <summary>修炼总时长（年）</summary>
        public static int GetCultivationYears(Actor actor) { return GetCustomInt(actor, K_CULT_YEARS); }
        /// <summary>修炼年数+1（年度结算时调用）</summary>
        public static void AddCultivationYear(Actor actor) { SetCustomInt(actor, K_CULT_YEARS, GetCultivationYears(actor) + 1); }

        /// <summary>突破次数</summary>
        public static int GetBreakthroughCount(Actor actor) { return GetCustomInt(actor, K_BREAKTHROUGH_CNT); }
        /// <summary>突破次数+1（境界提升时调用）</summary>
        public static void AddBreakthrough(Actor actor) { SetCustomInt(actor, K_BREAKTHROUGH_CNT, GetBreakthroughCount(actor) + 1); }
        /// <summary>基因表达层级key前缀</summary>
        private const string K_GENE_EXPR_PREFIX = "gene_expr_";

        /// <summary>获取基因表达层级（0=沉默，1=低表达，2=中表达，3=高表达，4=过表达，5=突变）</summary>
        public static int GetGeneExpressionLevel(Actor actor, string geneId)
        {
            if (actor == null || string.IsNullOrEmpty(geneId)) return 0;
            if (!IsElementUnlocked(actor, geneId)) return 0;
            int level = GetCustomInt(actor, K_GENE_EXPR_PREFIX + geneId);
            if (level <= 0) level = 1;
            return Mathf.Clamp(level, 0, 5);
        }

        /// <summary>设置基因表达层级</summary>
        public static void SetGeneExpressionLevel(Actor actor, string geneId, int level)
        {
            if (actor == null || string.IsNullOrEmpty(geneId)) return;
            SetCustomInt(actor, K_GENE_EXPR_PREFIX + geneId, Mathf.Clamp(level, 0, 5));
        }

        /// <summary>历史失控峰值</summary>
        public static float GetMaxTurbulenceReached(Actor actor) { return GetCustomFloat(actor, K_MAX_TURB); }
        /// <summary>更新历史失控峰值（仅当当前值更高时更新，年度结算时调用）</summary>
        public static void UpdateMaxTurbulence(Actor actor)
        {
            float current = GetTurbulence(actor);
            float max = GetMaxTurbulenceReached(actor);
            if (current > max) SetCustomFloat(actor, K_MAX_TURB, current);
        }

        /// <summary>总击杀数（含异能击杀和普通击杀）</summary>
        public static int GetKillsTotal(Actor actor) { return GetCustomInt(actor, K_KILLS_TOTAL); }
        /// <summary>总击杀数+1（杀戮事件时调用）</summary>
        public static void AddKill(Actor actor) { SetCustomInt(actor, K_KILLS_TOTAL, GetKillsTotal(actor) + 1); }

        /// <summary>异能击杀数</summary>
        public static int GetAbilityKills(Actor actor) { return GetCustomInt(actor, K_KILLS_ABILITY); }
        /// <summary>异能击杀数+1（异能者杀戮时调用）</summary>
        public static void AddAbilityKill(Actor actor) { SetCustomInt(actor, K_KILLS_ABILITY, GetAbilityKills(actor) + 1); }

        /// <summary>已激活基因连锁数量</summary>
        public static int GetGeneLinkageCount(Actor actor)
        {
            var combos = GetActiveCombos(actor);
            return combos != null ? combos.Count : 0;
        }

        /// <summary>历史最高境界</summary>
        public static int GetHighestTierReached(Actor actor) { return GetCustomInt(actor, K_HIGHEST_TIER); }
        /// <summary>更新历史最高境界（境界提升时调用，仅当更高时更新）</summary>
        public static void UpdateHighestTierReached(Actor actor, int newTier)
        {
            int current = GetHighestTierReached(actor);
            if (newTier > current) SetCustomInt(actor, K_HIGHEST_TIER, newTier);
        }

        /// <summary>设置觉醒年份（凡人觉醒时调用）</summary>
        public static void SetAwakeningYear(Actor actor, int year) { SetCustomInt(actor, K_AWAKEN_YEAR, year); }

        /// <summary>累计获得能量</summary>
        public static float GetTotalEnergyGained(Actor actor) { return GetCustomFloat(actor, K_TOTAL_ENERGY); }
        /// <summary>增加累计获得能量（获得异能能量时调用）</summary>
        public static void AddTotalEnergyGained(Actor actor, float amount)
        {
            if (amount <= 0f) return;
            SetCustomFloat(actor, K_TOTAL_ENERGY, GetTotalEnergyGained(actor) + amount);
        }

        /// <summary>经历灾变次数</summary>
        public static int GetDisastersSurvived(Actor actor) { return GetCustomInt(actor, K_DISASTERS_SURVIVED); }
        /// <summary>经历灾变次数+1（灾变发生时调用）</summary>
        public static void AddDisasterSurvived(Actor actor) { SetCustomInt(actor, K_DISASTERS_SURVIVED, GetDisastersSurvived(actor) + 1); }

        // ===== 基因系统数据 =====
        private const string K_ELEMENTS_UNLOCKED = "ds_elem_unlocked";    // 已解锁基因ID（逗号分隔）
        private const string K_ELEMENTS_ATTEMPTED = "ds_elem_attempted";  // 已尝试获得的基因ID（逗号分隔，任务只能完成一次）
        private const string K_ACTIVE_COMBOS = "ds_combos_active";         // 已激活组合ID（逗号分隔）
        private const string K_COMBO_DAMAGE = "ds_combo_dmg";              // 组合伤害加成
        private const string K_COMBO_DEFENSE = "ds_combo_def";             // 组合防御加成
        private const string K_AWAKENED = "ds_awakened";               // 是否已基因觉醒

        // ===== 运行时缓存（纯内存，不随存档；世界加载时清空，防止跨存档串数据）=====
        private static readonly Dictionary<long, List<string>> _unlockedCache = new Dictionary<long, List<string>>();
        private static readonly List<string> _emptyGeneList = new List<string>();

        /// <summary>清空运行时缓存（世界加载时调用）</summary>
        public static void ClearRuntimeCaches()
        {
            _unlockedCache.Clear();
        }

        /// <summary>获取已解锁的基因列表（带运行时缓存，避免每次Split分配GC）</summary>
        public static List<string> GetUnlockedElements(Actor actor)
        {
            if (actor == null) return _emptyGeneList;
            long id = actor.getID();
            if (_unlockedCache.TryGetValue(id, out var cached)) return cached;

            var data = GetDataOnce(actor);
            if (data == null) return _emptyGeneList;
            EnsureCustomDataInitialized(actor);
            if (data.custom_data_string == null) return _emptyGeneList;

            var result = new List<string>();
            if (data.custom_data_string.TryGetValue(K_ELEMENTS_UNLOCKED, out string val) && !string.IsNullOrEmpty(val))
            {
                result.AddRange(val.Split(','));
            }
            _unlockedCache[id] = result;
            return result;
        }

        /// <summary>检查基因是否已解锁</summary>
        public static bool IsElementUnlocked(Actor actor, string elementId)
        {
			if (elementId == ElementDef.DivineGeneId) return true;
            return GetUnlockedElements(actor).Contains(elementId);
        }

        /// <summary>解锁基因</summary>
        public static bool UnlockElement(Actor actor, string elementId)
        {
            var data = GetDataOnce(actor);
            if (actor == null || data == null) return false;
            EnsureCustomDataInitialized(actor);
            var unlocked = GetUnlockedElements(actor);
            if (unlocked.Contains(elementId)) return false;
            unlocked.Add(elementId);
            data.custom_data_string[K_ELEMENTS_UNLOCKED] = string.Join(",", unlocked);

            // 获得新基因后刷新称号（首次觉醒时生成基因称号）
            try { Code.Realm.TitleSuffixManager.UpdateTitleSuffix(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] UnlockElement 异常: " + dsEx.Message); }
            return true;
        }
        /// <summary>获取已尝试获得的基因列表（任务只能完成一次，失败后不再尝试）</summary>

        /// <summary>清空所有已解锁基因</summary>
        public static void ClearElements(Actor actor)
        {
            var data = GetDataOnce(actor);
            if (actor == null || data == null) return;
            EnsureCustomDataInitialized(actor);
            if (actor != null) _unlockedCache.Remove(actor.getID());
            if (data.custom_data_string != null)
            {
                data.custom_data_string[K_ELEMENTS_UNLOCKED] = "";
                data.custom_data_string[K_ACTIVE_COMBOS] = "";
            }
        }

        /// <summary>检查是否已基因觉醒</summary>
        public static bool IsAwakened(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            EnsureCustomDataInitialized(actor);
            return GetCustomInt(actor, K_AWAKENED) > 0;
        }

        /// <summary>设置基因觉醒状态</summary>
        public static void SetAwakened(Actor actor, bool awakened)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            EnsureCustomDataInitialized(actor);
            SetCustomInt(actor, K_AWAKENED, awakened ? 1 : 0);
        }

        /// <summary>获取已激活的组合列表</summary>
        public static List<string> GetActiveCombos(Actor actor)
        {
            var result = new List<string>();
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return result;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_string == null) return result;
            if (ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(K_ACTIVE_COMBOS, out string val) && !string.IsNullOrEmpty(val))
            {
                result.AddRange(val.Split(','));
            }
            return result;
        }

        /// <summary>获取组合伤害加成</summary>
        public static float GetComboDamageBonus(Actor actor) { return GetCustomFloat(actor, K_COMBO_DAMAGE); }
        /// <summary>获取组合防御加成</summary>
        public static float GetComboDefenseBonus(Actor actor) { return GetCustomFloat(actor, K_COMBO_DEFENSE); }
        // 自定义int数据访问器
        private static int GetCustomInt(Actor actor, string key)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_int == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(key, out int val);
            return val;
        }
        private static void SetCustomInt(Actor actor, string key, int value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_int == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[key] = value;
        }

        // 自定义float数据访问器
        private static float GetCustomFloat(Actor actor, string key)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor).custom_data_float == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(key, out float val);
            return val;
        }

        public static int GetLastSettledYear(Actor actor)
        {
            var data = actor == null ? null : ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_int == null) return -1;
            data.custom_data_int.TryGetValue(K_LAST_YEAR, out int val);
            return val;
        }
        public static void SetLastSettledYear(Actor actor, int value)
        {
            var data = actor == null ? null : ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_int == null) return;
            data.custom_data_int[K_LAST_YEAR] = value;
        }

        private const string K_MONOLITH = "ds_monolith_done";
        private const string K_SKIP_TIER_RECORD = "ds_skip_tier";  // 跳阶记录（格式：fromTier-toTier，逗号分隔多次跳阶）
        public static bool GetMonolithDone(Actor actor)
        {
            var data = actor == null ? null : ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_bool == null) return false;
            data.custom_data_bool.TryGetValue(K_MONOLITH, out bool val);
            return val;
        }

        public static void SetMonolithDone(Actor actor, bool value)
        {
            var data = actor == null ? null : ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_bool == null) return;
            data.custom_data_bool[K_MONOLITH] = value;
        }

        // ============================================================
        //  布尔状态读写
        // ============================================================

        public static bool IsDomainActive(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            ActorDataAccessor.GetData(actor).custom_data_bool.TryGetValue(K_DOMAIN, out bool val);
            return val;
        }
        public static bool IsSanctuaryActive(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            ActorDataAccessor.GetData(actor).custom_data_bool.TryGetValue(K_SANCTUARY_ACTIVE, out bool val);
            return val;
        }
        public static bool IsInDivineRealm(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            ActorDataAccessor.GetData(actor).custom_data_bool.TryGetValue(K_IN_DIVINE_REALM, out bool val);
            return val;
        }
        // ============================================================
        //  能力掌控度系统（现代异能体系，替代印记系统）
        //  存储于custom_data_float，key = "ds_mastery"
        //  0=未掌握，100=完全掌控，达到100才能突破到下一阶
        //  通过使用境界技能和年度修炼提升
        // ============================================================

        /// <summary>获取当前境界的能力掌控度 0~100</summary>
        public static float GetMastery(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            EnsureCustomDataInitialized(actor);
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_MASTERY, out float val);
            return val;
        }

        /// <summary>设置能力掌控度</summary>
        public static void SetMastery(Actor actor, float value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            EnsureCustomDataInitialized(actor);
            ActorDataAccessor.GetData(actor).custom_data_float[K_MASTERY] = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>增加能力掌控度（使用技能或修炼时调用）</summary>
        public static void AddMastery(Actor actor, float amount)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            float current = GetMastery(actor);
            SetMastery(actor, current + amount);
        }

        /// <summary>是否完全掌控当前境界（掌控度>=100）</summary>
        public static bool HasCompleteMastery(Actor actor)
        {
            return GetMastery(actor) >= 100f;
        }

        /// <summary>突破后重置掌控度为0（开始掌握新境界的能力）</summary>
        public static void ResetMasteryOnBreakthrough(Actor actor)
        {
            SetMastery(actor, 0f);
        }

        // ============================================================
        //  冷却计时器
        // ============================================================

        public static int GetCooldown(Actor actor, string key)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_COOLDOWN_PREFIX + key, out int val);
            return val;
        }
        public static void SetCooldown(Actor actor, string key, int value)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_COOLDOWN_PREFIX + key] = Mathf.Max(0, value);
        }
        public static void DecrementCooldown(Actor actor, string key)
        {
            int cd = GetCooldown(actor, key);
            if (cd > 0) SetCooldown(actor, key, cd - 1);
        }

        // ============================================================
        //  组织系统数据
        // ============================================================
        private const string K_SECT_ID = "ds_sect_id";
        private const string K_SECT_RANK = "ds_sect_rank";
        private const string K_SECT_CONTRIB = "ds_sect_contrib";
        private const string K_HERITAGE_GEN = "ds_heritage_gen";
        private const string K_MASTER_ID = "ds_master_id";
        private const string K_SECT_NAME = "ds_sect_name";       // 组织名（随首领单位存档，纯随档不跨档）
        private const string K_SECT_FOUNDED = "ds_sect_founded"; // 组织创立年份
        private const string K_SECT_ANCESTOR = "ds_sect_ancestor"; // 组织创始人姓名
        private const string K_SECT_BREAKS = "ds_sect_breaks";       // 传承断裂次数
        private const string K_SECT_BREAK_YEAR = "ds_sect_break_year"; // 最近断裂年份（防重复计数）

        public static string GetSectId(Actor actor)
        {
            if (actor == null) return "";
            var data = ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_string == null) return "";
            data.custom_data_string.TryGetValue(K_SECT_ID, out string val);
            return val ?? "";
        }
        public static void SetSectId(Actor actor, string v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_string[K_SECT_ID] = v ?? "";
        }
        public static int GetSectRank(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_SECT_RANK, out int val);
            return val;
        }
        public static void SetSectRank(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_SECT_RANK] = v;
        }
        public static float GetSectContribution(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_SECT_CONTRIB, out float val);
            return val;
        }
        public static void SetSectContribution(Actor actor, float v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_SECT_CONTRIB] = v;
        }
        /// <summary>累加组织贡献（年度职位贡献 + 突破奖励）</summary>
        public static void AddSectContribution(Actor actor, float amount)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            SetSectContribution(actor, GetSectContribution(actor) + amount);
        }
        public static int GetHeritageGen(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_HERITAGE_GEN, out int val);
            return val;
        }
        public static void SetHeritageGen(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_HERITAGE_GEN] = v;
        }

        public static void SetMasterId(Actor actor, string v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_string[K_MASTER_ID] = v ?? "";
        }

        //  组织元数据（随首领单位存档  组织纯随档，绝不跨存档）
        public static string GetSectName(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return null;
            ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(K_SECT_NAME, out string val);
            return string.IsNullOrEmpty(val) ? null : val;
        }
        public static void SetSectName(Actor actor, string v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_string[K_SECT_NAME] = v ?? "";
        }
        public static int GetSectFoundedYear(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_SECT_FOUNDED, out int val);
            return val;
        }
        public static void SetSectFoundedYear(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_SECT_FOUNDED] = v;
        }
        public static string GetSectAncestorName(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return null;
            ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(K_SECT_ANCESTOR, out string val);
            return string.IsNullOrEmpty(val) ? null : val;
        }
        public static void SetSectAncestorName(Actor actor, string v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_string[K_SECT_ANCESTOR] = v ?? "";
        }
        public static int GetSectBreaks(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_SECT_BREAKS, out int val);
            return val;
        }
        public static void SetSectBreaks(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_SECT_BREAKS] = v;
        }
        public static int GetSectBreakYear(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_int.TryGetValue(K_SECT_BREAK_YEAR, out int val);
            return val;
        }
        public static void SetSectBreakYear(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_int[K_SECT_BREAK_YEAR] = v;
        }

        // ============================================================
        //  维度投影数据
        // ============================================================
        private const string K_DIVINE_PROJ = "ds_divine_proj";
        private const string K_DIVINE_X = "ds_divine_x";
        private const string K_DIVINE_Y = "ds_divine_y";
        private const string K_DIVINE_POWER = "ds_divine_power";

        public static bool IsDimensionProjectionActive(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            ActorDataAccessor.GetData(actor).custom_data_bool.TryGetValue(K_DIVINE_PROJ, out bool val);
            return val;
        }
        // ========== 维度空间系统 ==========
        private const string K_DIM_SPACE_ACTIVE = "ds_dim_space_active";
        private const string K_DIM_SPACE_ENERGY = "ds_dim_space_energy";
        private const string K_DIM_SPACE_LEVEL = "ds_dim_space_level";
        private const string K_DIM_SPACE_SIZE = "ds_dim_space_size";

        /// <summary>是否在维度空间中</summary>
        public static bool IsInDimensionSpace(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;
            ActorDataAccessor.GetData(actor).custom_data_bool.TryGetValue(K_DIM_SPACE_ACTIVE, out bool val);
            return val;
        }
        public static void SetInDimensionSpace(Actor actor, bool v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_bool[K_DIM_SPACE_ACTIVE] = v;
        }

        /// <summary>获取维度空间能量</summary>
        public static float GetDimensionSpaceEnergy(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_DIM_SPACE_ENERGY, out float val);
            return val;
        }
        public static void SetDimensionSpaceEnergy(Actor actor, float v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_DIM_SPACE_ENERGY] = v;
        }

        /// <summary>获取维度空间等级</summary>
        public static int GetDimensionSpaceLevel(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_DIM_SPACE_LEVEL, out float val);
            return (int)val;
        }
        public static void SetDimensionSpaceLevel(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_DIM_SPACE_LEVEL] = (float)v;
        }

        /// <summary>获取维度空间大小</summary>
        public static int GetDimensionSpaceSize(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_DIM_SPACE_SIZE, out float val);
            return (int)val;
        }
        public static void SetDimensionSpaceSize(Actor actor, int v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_DIM_SPACE_SIZE] = (float)v;
        }
        // ============================================================
        //  战斗位格数据（伤害回溯用）
        // ============================================================
        private const string K_LAST_HP = "ds_last_hp";
        private const string K_DAMAGE_REDUCE = "ds_damage_reduce";

        public static float GetLastHp(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return -1f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_LAST_HP, out float val);
            return val;
        }
        public static void SetLastHp(Actor actor, float v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_LAST_HP] = v;
        }
        public static float GetDamageReduction(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return 0f;
            ActorDataAccessor.GetData(actor).custom_data_float.TryGetValue(K_DAMAGE_REDUCE, out float val);
            return val;
        }
        public static void SetDamageReduction(Actor actor, float v)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            ActorDataAccessor.GetData(actor).custom_data_float[K_DAMAGE_REDUCE] = v;
        }

        // ============================================================
        //  工具方法
        // ============================================================

        /// <summary>旧特质ID到新特质ID的映射（兼容旧存档）</summary>
        private static readonly Dictionary<string, string> _oldToNewTraitIdMap = new Dictionary<string, string>
        {
            { "ds_tier_1_ganqi", "ds_tier_01_awakened" },
            { "ds_tier_2_ningqi", "ds_tier_02_initiate" },
            { "ds_tier_3_tongmai", "ds_tier_03_refined" },
            { "ds_tier_4_cuiti", "ds_tier_04_transformed" },
            { "ds_tier_5_hengyuan", "ds_tier_05_controller" },
            { "ds_tier_6_lingyu", "ds_tier_06_domain" },
            { "ds_tier_7_ningshen", "ds_tier_07_materialized" },
            { "ds_tier_8_mingdao", "ds_tier_08_lawbearer" },
            { "ds_tier_9_guiyuan", "ds_tier_09_originator" }
            // 10-13阶ID没有变化（已经是两位数）
        };

        /// <summary>
        /// 迁移旧特质ID到新特质ID（兼容旧存档）。
        /// 特质ID从ds_tier_1改为ds_tier_01，旧存档中的特质需要迁移。
        /// </summary>
        public static void MigrateOldTraitIds(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;

            foreach (var kvp in _oldToNewTraitIdMap)
            {
                string oldId = kvp.Key;
                string newId = kvp.Value;

                if (actor.hasTrait(oldId))
                {
                    // 移除旧特质，添加新特质
                    actor.removeTrait(oldId);
                    if (!actor.hasTrait(newId))
                    {
                        actor.addTrait(newId, false);
                    }
                    DSDebug.Verbose($"特质ID迁移: {actor.getName()} {oldId} -> {newId}");
                }
            }
        }

        /// <summary>是否为异能者（境界>0）</summary>
        public static bool IsAscended(Actor actor)
        {
            return GetRealmTier(actor) > 0;
        }

        /// <summary>
        /// 从特质同步数据到CultivationData。
        /// 解决玩家手动赐予特质后，图鉴UI统计不到的问题。
        /// 检查单位是否有境界特质、体系特质、异能特质，如果有但CultivationData没有数据，自动同步。
        /// 同时处理旧ID到新ID的迁移（特质ID从ds_tier_1改为ds_tier_01）。
        /// 返回true表示进行了同步，false表示不需要同步。
        /// </summary>
        public static bool SyncFromTraits(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return false;

            bool synced = false;
            int currentTier = GetRealmTier(actor);

            // 0. 确保custom_data容器被初始化
            try { EnsureCustomDataInitialized(actor); } catch { }

            // 0.5 旧ID到新ID的迁移（兼容旧存档）
            MigrateOldTraitIds(actor);

            // 1. 检查境界特质（从特质ID推断境界等级）
            string[] tierTraits = {
                "ds_tier_01_awakened", "ds_tier_02_initiate", "ds_tier_03_refined", "ds_tier_04_transformed",
                "ds_tier_05_controller", "ds_tier_06_domain", "ds_tier_07_materialized", "ds_tier_08_lawbearer",
                "ds_tier_09_originator", "ds_tier_10_realmbreaker", "ds_tier_11_sanctuary", "ds_tier_12_ascended",
                "ds_tier_13_truegod"
            };

            for (int i = 0; i < tierTraits.Length; i++)
            {
                if (actor.hasTrait(tierTraits[i]))
                {
                    int traitTier = i + 1;
                    if (currentTier != traitTier)
                    {
                        SetRealmTier(actor, traitTier, allowBigJump: true);
                        currentTier = traitTier;
                        synced = true;
                        DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}从特质同步境界={traitTier}");
                    }
                    break; // 境界特质互斥，只需要检查第一个
                }
            }

            // 2. 检查异能特质（如果有任何异能特质但没有境界，初始化为1阶觉醒者）
            if (currentTier == 0)
            {
                // 异能体系特质 + 能力状态特质：玩家通过调试赐予这些特质但未赐予境界特质时，自动初始化1阶
                string[] abilityTraits = {
                    "ds_status_demonic_obsession",   // 能力失控
                    "ds_status_enlightenment",        // 顿悟/深度觉醒
                    "ds_status_sanctuary_avatar",    // 登神化身
                    "ds_status_divine_projection",   // 维度投影
                    "ds_status_divine_blessing",     // 神眷
                    "ds_status_immortal_body",       // 不朽之躯
                };

                foreach (string abilityTrait in abilityTraits)
                {
                    if (actor.hasTrait(abilityTrait))
                    {
                        // 有异能特质但没有境界，初始化为1阶觉醒者
                        SetRealmTier(actor, 1);
                        currentTier = 1;
                        synced = true;
                        DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}有异能特质{abilityTrait}，初始化为1阶觉醒者");
                        break;
                    }
                }
            }

            // 4. 如果有境界但异能能量为0，初始化基础异能能量
            if (currentTier > 0 && GetEnergy(actor) <= 0f)
            {
                // 根据境界设置基础异能能量（1阶=100，每阶递增）
                float baseQi = 100f * currentTier;
                SetEnergy(actor, baseQi);
                synced = true;
                DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}初始化异能能量={baseQi}");
            }

            // 5. 数据完整性检查和恢复
            if (currentTier > 0)
            {
                // 5.1 失控指数检查：负数或异常大（>100）则重置为0
                float turb = GetTurbulence(actor);
                if (turb < 0f || turb > 100f)
                {
                    SetTurbulence(actor, 0f);
                    synced = true;
                    DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}失控指数异常({turb})，重置为0");
                }

                // 5.2 深度觉醒经验检查：负数则重置为0
                float enlightExp = GetEnlightenmentExp(actor);
                if (enlightExp < 0f)
                {
                    SetEnlightenmentExp(actor, 0f);
                    synced = true;
                    DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}深度觉醒经验异常({enlightExp})，重置为0");
                }

                // 5.3 能力掌控度检查：0或负数则按境界初始化（1阶=10，每阶递增10）
                float mastery = GetMastery(actor);
                if (mastery <= 0f)
                {
                    float baseMastery = 10f * currentTier;
                    SetMastery(actor, baseMastery);
                    synced = true;
                    DSDebug.Verbose($"SyncFromTraits: 单位{actor.getName()}能力掌控度丢失，初始化为{baseMastery}");
                }
            }

            return synced;
        }

        /// <summary>
        /// 检查单位是否有任何模组特质（境界/组织）
        /// </summary>
        public static bool HasAnyModTrait(Actor actor)
        {
            if (actor == null) return false;

            // 检查境界特质
            string[] tierTraits = {
                "ds_tier_01_awakened", "ds_tier_02_initiate", "ds_tier_03_refined", "ds_tier_04_transformed",
                "ds_tier_05_controller", "ds_tier_06_domain", "ds_tier_07_materialized", "ds_tier_08_lawbearer",
                "ds_tier_09_originator", "ds_tier_10_realmbreaker", "ds_tier_11_sanctuary", "ds_tier_12_ascended",
                "ds_tier_13_truegod"
            };
            foreach (string t in tierTraits) if (actor.hasTrait(t)) return true;

            // 检查组织特质
            string[] abilityTraits = {
                "ds_sect_leader", "ds_sect_deputy", "ds_sect_elder", "ds_sect_cadre",
                "ds_sect_elite", "ds_sect_member", "ds_sect_probation", "ds_sect_founder",
                "ds_sect_heritage", "ds_sect_guardian", "ds_sect_guest"
            };
            foreach (string t in abilityTraits) if (actor.hasTrait(t)) return true;

            return false;
        }


        // ============================================================
        //  神创基因系统（第57位，可自定义）
        // ============================================================

        private const string K_DIVINE_GENE_UNLOCKED = "ds_divine_gene_unlocked";   // 神创基因是否已解锁
        private const string K_DIVINE_GENE_NAME = "ds_divine_gene_name";           // 神创基因名字
        private const string K_DIVINE_GENE_GROUP = "ds_divine_gene_group";         // 神创基因染色体类型（0-7）
        private const string K_DIVINE_GENE_VALUE = "ds_divine_gene_value";         // 神创基因效果值

        /// <summary>检查神创基因是否已解锁</summary>
        public static bool IsDivineGeneUnlocked(Actor actor)
        {
                        var data = ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_bool == null) return false;
            data.custom_data_bool.TryGetValue(K_DIVINE_GENE_UNLOCKED, out bool val);
            return val;
        }

        /// <summary>解锁神创基因</summary>
        public static void UnlockDivineGene(Actor actor)
        {
                        EnsureCustomDataInitialized(actor);
            var data = ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_bool == null) return;
            data.custom_data_bool[K_DIVINE_GENE_UNLOCKED] = true;
            // 解锁时自动解锁基因节点
            UnlockGene(actor, ElementDef.DivineGeneId);
        }

        /// <summary>获取神创基因名字</summary>
        public static string GetDivineGeneName(Actor actor)
        {
            if (!IsDivineGeneUnlocked(actor)) return null;
            if (ActorDataAccessor.GetData(actor)?.custom_data_string == null) return "神创基因";
            ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(K_DIVINE_GENE_NAME, out string val);
            return string.IsNullOrEmpty(val) ? "神创基因" : val;
        }

        /// <summary>设置神创基因名字</summary>
        public static void SetDivineGeneName(Actor actor, string name)
        {
            EnsureCustomDataInitialized(actor);
            if (ActorDataAccessor.GetData(actor)?.custom_data_string == null) return;
            ActorDataAccessor.GetData(actor).custom_data_string[K_DIVINE_GENE_NAME] = name ?? "神创基因";
        }

        /// <summary>获取神创基因染色体类型（0-7）</summary>
        public static int GetDivineGeneGroup(Actor actor)
        {
            if (!IsDivineGeneUnlocked(actor)) return 7; // 默认潜能染色体
            return GetCustomInt(actor, K_DIVINE_GENE_GROUP);
        }

        /// <summary>设置神创基因染色体类型（0-7）</summary>
        public static void SetDivineGeneGroup(Actor actor, int group)
        {
            SetCustomInt(actor, K_DIVINE_GENE_GROUP, group);
        }

        /// <summary>获取神创基因效果值</summary>
        public static float GetDivineGeneValue(Actor actor)
        {
            if (!IsDivineGeneUnlocked(actor)) return 0f;
            return GetCustomFloat(actor, K_DIVINE_GENE_VALUE);
        }

        /// <summary>设置神创基因效果值</summary>
        public static void SetDivineGeneValue(Actor actor, float value)
        {
            SetCustomFloat(actor, K_DIVINE_GENE_VALUE, value);
        }

        // ========== 神创基因自定义数值存取（13个效果值，单位%） ==========
        private static readonly Dictionary<long, Dictionary<string, float>> divineValues = new Dictionary<long, Dictionary<string, float>>();
        public static float GetDivineFloat(Actor actor, string key, float def = 0f)
        {
            if (actor == null) return def;
            long id = actor.id;
            Dictionary<string, float> perActor;
            if (!divineValues.TryGetValue(id, out perActor)) return def;
            float val;
            return perActor.TryGetValue(key, out val) ? val : def;
        }
        public static void SetDivineFloat(Actor actor, string key, float value)
        {
            if (actor == null) return;
            long id = actor.id;
            Dictionary<string, float> perActor;
            if (!divineValues.TryGetValue(id, out perActor))
            {
                perActor = new Dictionary<string, float>();
                divineValues[id] = perActor;
            }
            perActor[key] = value;
        }
    }
}

