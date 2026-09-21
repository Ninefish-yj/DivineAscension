// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Core;
using Code.Data;
using Code.Traits;
using Code.UI;

namespace Code.Realm
{
    /// <summary>境界技能定义</summary>
    public class RealmAbility
    {
        public int RequiredTier;      // 需要的境界
        public string Id;              // 技能ID
        public string Name;            // 技能名称（中文）
        public string NameEn;          // 技能名称（英文）
        public string Description;     // 技能描述（中文）
        public string DescriptionEn;   // 技能描述（英文）
        public float Cooldown;         // 冷却时间（年）
        public float EnergyCost;       // 异能能量消耗
        public AbilityType Type;       // 技能类型

        /// <summary>获取当前语言的技能名称</summary>
        public string GetName() { return UILocalization.CurrentLanguage == "en" ? NameEn : Name; }
        /// <summary>获取当前语言的技能描述</summary>
        public string GetDescription() { return UILocalization.CurrentLanguage == "en" ? DescriptionEn : Description; }
    }

    public enum AbilityType
    {
        Damage,      // 范围伤害
        Heal,        // 治疗
        Buff,        // 增益
        Debuff,      // 减益
        Utility,     // 功能型
        Ultimate     // 终极技能
    }

    public static class RealmAbilities
    {
        private static readonly Dictionary<int, List<RealmAbility>> _abilitiesByTier = new Dictionary<int, List<RealmAbility>>();
        private static readonly Dictionary<string, RealmAbility> _abilitiesById = new Dictionary<string, RealmAbility>();
        private static bool _initialized = false;

        // 技能冷却记录（actorId -> (abilityId -> lastUseTime)）
        // 冷却时间按秒计算（参考西幻世界，使用 World.world.getCurWorldTime()）
        private static readonly Dictionary<long, Dictionary<string, float>> _cooldowns = new Dictionary<long, Dictionary<string, float>>();

        // 怒气值系统（终极技能需要积累怒气才能释放）
        // actorId -> 当前怒气值（0-100）
        private static readonly Dictionary<long, float> _rageValues = new Dictionary<long, float>();
        private const float MAX_RAGE = 100f;
        private const float RAGE_PER_ATTACK = 5f;    // 普通攻击获得怒气
        private const float RAGE_PER_HIT = 3f;         // 被攻击获得怒气
        private const float RAGE_PER_KILL = 20f;       // 击杀获得怒气
        private const float RAGE_PER_SECOND = 1f;      // 每秒自动获得怒气

        /// <summary>初始化所有境界技能</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // 1阶 觉醒者 - 基础异能冲击
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 1,
                Id = "ds_ability_energy_bolt",
                Name = UILocalization.Get("ability_energy_bolt"),
                NameEn = "Energy Bolt",
                Description = UILocalization.Get("ability_energy_bolt_desc"),
                DescriptionEn = "Release a bolt of ability energy, dealing minor damage to enemies in front.",
                Cooldown = 2f,       // 冷却2秒（参考西幻世界，按秒计算）
                EnergyCost = 10f,    // 能量消耗降低
                Type = AbilityType.Damage
            });

            // 4阶 突变者 - 治愈之光
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 4,
                Id = "ds_ability_healing_light",
                Name = UILocalization.Get("ability_healing_light"),
                NameEn = "Healing Light",
                Description = UILocalization.Get("ability_healing_light_desc"),
                DescriptionEn = "Release healing energy, restoring health to self and nearby allies.",
                Cooldown = 5f,       // 冷却5秒
                EnergyCost = 30f,    // 能量消耗降低
                Type = AbilityType.Heal
            });

            // 9阶天灾级 - 龙卷风风暴
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 9,
                Id = "ds_ability_energy_storm",
                Name = UILocalization.Get("ability_energy_storm"),
                NameEn = "Energy Storm",
                Description = UILocalization.Get("ability_energy_storm_desc"),
                DescriptionEn = "Create an energy storm at a target location, dealing continuous damage to enemies in range.",
                Cooldown = 5f,       // 冷却5秒
                EnergyCost = 50f,    // 能量消耗降低
                Type = AbilityType.Damage
            });

            // 6阶 场域者 - 场域展开
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 6,
                Id = "ds_ability_domain_expand",
                Name = UILocalization.Get("ability_field_expand"),
                NameEn = "Domain Expand",
                Description = UILocalization.Get("ability_field_expand_desc"),
                DescriptionEn = "Expand a personal ability domain, greatly boosting all stats and suppressing enemies within.",
                Cooldown = 10f,      // 冷却10秒
                EnergyCost = 80f,    // 能量消耗降低
                Type = AbilityType.Ultimate
            });

            // 8阶 干涉者 - 规则压制
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 8,
                Id = "ds_ability_law_suppress",
                Name = UILocalization.Get("ability_rule_suppress"),
                NameEn = "Law Suppress",
                Description = UILocalization.Get("ability_rule_suppress_desc"),
                DescriptionEn = "Invoke law authority to suppress enemies in range, preventing them from using skills.",
                Cooldown = 10f,      // 冷却10秒
                EnergyCost = 100f,   // 能量消耗降低
                Type = AbilityType.Debuff
            });

            // 11阶 登神级 - 超神级降临（终极技能，需要满怒气）
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 11,
                Id = "ds_ability_sanctuary",
                Name = UILocalization.Get("ability_superbody_descend"),
                NameEn = "Sanctuary",
                Description = UILocalization.Get("ability_superbody_descend_desc"),
                DescriptionEn = "Descend a sanctuary, making allies invulnerable and continuously healing while purifying enemies.",
                Cooldown = 25f,      // 冷却25秒
                EnergyCost = 300f,   // 能量消耗降低
                Type = AbilityType.Ultimate
            });

            // 13阶超神级 - 维度打击（终极技能，需要满怒气）
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 13,
                Id = "ds_ability_dimensional_strike",
                Name = UILocalization.Get("ability_dimension_strike"),
                NameEn = "Dimensional Strike",
                Description = UILocalization.Get("ability_dimension_strike_desc"),
                DescriptionEn = "Deliver a devastating strike from dimensional space, dealing massive damage to all enemies on screen.",
                Cooldown = 30f,      // 冷却30秒
                EnergyCost = 400f,   // 能量消耗降低
                Type = AbilityType.Ultimate
            });

            // 12阶真神级 - 神罚（终极技能，需要满怒气）
            RegisterAbility(new RealmAbility
            {
                RequiredTier = 12,
                Id = "ds_ability_divine_punishment",
                Name = UILocalization.Get("ability_divine_punishment"),
                NameEn = "Divine Punishment",
                Description = UILocalization.Get("ability_divine_punishment_desc"),
                DescriptionEn = "Descend divine punishment, destroying everything in a target area; only True Gods may wield this power.",
                Cooldown = 45f,      // 冷却45秒
                EnergyCost = 500f,   // 能量消耗降低
                Type = AbilityType.Ultimate
            });

            DSDebug.Verbose("境界专属技能初始化完成，共" + _abilitiesById.Count + "个技能");
        }

        private static void RegisterAbility(RealmAbility ability)
        {
            if (!_abilitiesByTier.ContainsKey(ability.RequiredTier))
                _abilitiesByTier[ability.RequiredTier] = new List<RealmAbility>();

            _abilitiesByTier[ability.RequiredTier].Add(ability);
            _abilitiesById[ability.Id] = ability;
        }

        /// <summary>获取指定境界可使用的所有技能</summary>
        public static List<RealmAbility> GetAbilitiesForTier(int tier)
        {
            var result = new List<RealmAbility>();
            for (int i = 1; i <= tier; i++)
            {
                if (_abilitiesByTier.ContainsKey(i))
                    result.AddRange(_abilitiesByTier[i]);
            }
            return result;
        }

        /// <summary>获取指定单位可使用的所有技能</summary>
        public static List<RealmAbility> GetAbilitiesForActor(Actor actor)
        {
            if (actor == null) return new List<RealmAbility>();
            int tier = CultivationData.GetRealmTier(actor);
            return GetAbilitiesForTier(tier);
        }

        /// <summary>检查技能是否在冷却中</summary>
        public static bool IsOnCooldown(Actor actor, string abilityId)
        {
            if (actor == null) return false;
            long actorId = ActorDataAccessor.GetData(actor).id;
            if (!_cooldowns.ContainsKey(actorId)) return false;
            if (!_cooldowns[actorId].ContainsKey(abilityId)) return false;

            // 冷却时间按秒计算（参考西幻世界，使用 Time.time）
            float currentTime = Time.time;
            float lastUseTime = _cooldowns[actorId][abilityId];
            RealmAbility ability = GetAbilityById(abilityId);
            if (ability == null) return false;

            // Cooldown 字段现在表示秒数，不是年数
            // 神创基因技能冷却缩减：独立百分比降低冷却
            float divineCd = Code.Core.GeneEnergyCalculator.GetDivineCdBonus(actor);
            float effectiveCooldown = ability.Cooldown * (1f - divineCd);
            return currentTime - lastUseTime < effectiveCooldown;
        }

        /// <summary>获取技能剩余冷却时间（秒）</summary>
        public static float GetRemainingCooldown(Actor actor, string abilityId)
        {
            if (actor == null) return 0f;
            long actorId = ActorDataAccessor.GetData(actor).id;
            if (!_cooldowns.ContainsKey(actorId)) return 0f;
            if (!_cooldowns[actorId].ContainsKey(abilityId)) return 0f;

            float currentTime = Time.time;
            float lastUseTime = _cooldowns[actorId][abilityId];
            RealmAbility ability = GetAbilityById(abilityId);
            if (ability == null) return 0f;

            // 神创基因技能冷却缩减：独立百分比降低冷却
            float divineCd = Code.Core.GeneEnergyCalculator.GetDivineCdBonus(actor);
            float effectiveCooldown = ability.Cooldown * (1f - divineCd);
            return Mathf.Max(0f, effectiveCooldown - (currentTime - lastUseTime));
        }

        /// <summary>使用技能</summary>
        public static bool UseAbility(Actor actor, string abilityId, Actor target = null)
        {
            if (actor == null || !actor.isAlive()) return false;

            //  8阶规则压制沉默检测：如果附近12格内有高于自身境界的8阶+干涉者，无法使用技能
            try
            {
                int actorTier = CultivationData.GetRealmTier(actor);
                if (actorTier < 8 && RealmFeatures.HasRuleSuppressorNearby(actor, 12f))
                {
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 被规则压制，无法使用技能！");
                    return false;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] UseAbility 异常: " + dsEx.Message); }

            // 技能释放后，添加状态效果到状态栏（有持续时间）
            // 注意：必须在所有检查通过后再添加，避免检查失败时状态已经加上

            RealmAbility ability = GetAbilityById(abilityId);
            if (ability == null) return false;

            int tier = CultivationData.GetRealmTier(actor);
            if (tier < ability.RequiredTier)
            {
                DSDebug.Warning($"[DivineAscension] {actor.getName()} 境界不足，无法使用 {ability.Name}（需要{ability.RequiredTier}阶）");
                return false;
            }

            if (IsOnCooldown(actor, abilityId))
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 的 {ability.Name} 正在冷却中");
                return false;
            }

            //  精细掌控：技能能量消耗减免（5阶-25%，9阶额外-20%，13阶额外-30%）
            float actualCost = ability.EnergyCost * RealmFeatures.GetAbilityCostMultiplier(actor);
            float currentEnergy = CultivationData.GetEnergy(actor);
            if (currentEnergy < actualCost)
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 异能能量不足，无法使用 {ability.Name}（需要{actualCost:F0}）");
                return false;
            }

            // 消耗能量（应用减免后的实际消耗）
            CultivationData.SetEnergy(actor, currentEnergy - actualCost);

            // 设置冷却（按秒计算，参考西幻世界）
            long actorId = ActorDataAccessor.GetData(actor).id;
            if (!_cooldowns.ContainsKey(actorId))
                _cooldowns[actorId] = new Dictionary<string, float>();
            _cooldowns[actorId][abilityId] = Time.time;

            // 终极技能（10阶+）释放后消耗怒气值
            if (ability.RequiredTier >= 10)
            {
                _rageValues[actorId] = 0f;
            }

            // 执行技能效果
            ExecuteAbilityEffect(actor, ability, target);

            //  技能成功释放后，添加限时状态效果到状态栏（Buff=30秒，Damage=5秒等）
            AddAbilityStatusEffect(actor, abilityId);

            //  6阶场域者：释放技能时自动展开个人场域（半径8格，自身+20%，敌人-10%）
            if (tier >= 6 && CultivationData.GetCooldown(actor, "domain_ready") > 0)
            {
                TriggerDomainExpansion(actor);
            }

            // 播放技能帧动画特效（如果有注册的动画）
            DSDebug.Verbose($"[技能释放] {actor.getName()}({tier}阶) 释放技能: {ability.Name} (ID={abilityId}), 能量消耗={actualCost:F0}, 剩余能量={currentEnergy - actualCost:F0}");
            try { DSSkillEffectManager.PlayEffect(actor, abilityId, target); }
            catch (System.Exception e) { DSDebug.Warning($"技能特效播放失败: {e.Message}"); }

            // 基因图谱战斗进度：记录异能使用次数
            try { CultivationData.AddGeneAbilityUse(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] UseAbility 异常: " + dsEx.Message); }

            DSDebug.Verbose($"[DivineAscension] {actor.getName()}({tier}阶) 使用技能: {ability.Name} (ID={abilityId})");
            return true;
        }

        // ============================================================
        //  怒气值系统（终极技能需要积累怒气才能释放）
        // ============================================================

        /// <summary>获取单位的当前怒气值（0-100）</summary>
        public static float GetRage(Actor actor)
        {
            if (actor == null) return 0f;
            long actorId = ActorDataAccessor.GetData(actor).id;
            if (!_rageValues.ContainsKey(actorId)) return 0f;
            return _rageValues[actorId];
        }

        /// <summary>增加单位的怒气值（攻击/被攻击/击杀时调用）</summary>
        public static void AddRage(Actor actor, float amount)
        {
            if (actor == null || amount <= 0f) return;
            long actorId = ActorDataAccessor.GetData(actor).id;
            if (!_rageValues.ContainsKey(actorId))
                _rageValues[actorId] = 0f;
            _rageValues[actorId] = Mathf.Min(MAX_RAGE, _rageValues[actorId] + amount);
        }

        /// <summary>检查单位是否有足够的怒气释放终极技能</summary>
        public static bool HasEnoughRage(Actor actor, string abilityId)
        {
            if (actor == null) return false;
            RealmAbility ability = GetAbilityById(abilityId);
            if (ability == null || ability.RequiredTier < 10) return true; // 非终极技能不需要怒气
            return GetRage(actor) >= MAX_RAGE; // 终极技能需要满怒气
        }

        /// <summary>每秒自动恢复怒气值（在Update中调用）</summary>
        public static void UpdateRageRegeneration()
        {
            if (World.world == null || World.world.units == null) return;
            try
            {
                // 只更新活跃异能者的怒气值（避免遍历全图单位）
                var activeIds = AnnualTickManager.GetActiveCultivatorIds();
                if (activeIds == null || activeIds.Count == 0) return;

                foreach (long id in activeIds)
                {
                    try
                    {
                        Actor actor = World.world.units.get(id);
                        if (actor == null || !actor.isAlive()) continue;
                        AddRage(actor, RAGE_PER_SECOND * Time.deltaTime);
                    }
                    catch { /* 忽略单个单位更新失败 */ }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] UpdateRageRegeneration 异常: " + dsEx.Message); }
        }

        /// <summary>执行技能效果</summary>
        private static void ExecuteAbilityEffect(Actor actor, RealmAbility ability, Actor target)
        {
            if (World.world == null || World.world.units == null) return;

            float tierMultiplier = 1f + CultivationData.GetRealmTier(actor) * 0.5f;
            int tier = CultivationData.GetRealmTier(actor);

            switch (ability.Type)
            {
                case AbilityType.Damage:
                    // 普通攻击技能
                    float damage = ability.EnergyCost * 1.5f * tierMultiplier;
                    // 基因表达层级技能伤害加成
                    float geneDamageBonus = Code.Realm.GeneExpressionSystem.GetSkillDamageBonus(actor);
                    if (geneDamageBonus > 0f) damage *= (1f + geneDamageBonus);
                    // GE公式（基因能量）技能伤害加成
                    float geDamageBonus = Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
                    if (geDamageBonus > 0f) damage *= (1f + geDamageBonus / 30f);
                    // 基因连锁系统技能伤害加成
                    float linkageSkillDamage = Code.GeneLinkageSystem.GetTotalSkillDamageBonus(actor);
                    if (linkageSkillDamage > 0f) damage *= (1f + linkageSkillDamage);
                    
                    if (ability.Id == "ds_ability_energy_storm")
                    {
                        // 能量风暴：找到最近的敌人作为目标，播放龙卷风特效 + 范围伤害
                        Actor stormTarget = null;
                        float stormNearestDist = float.MaxValue;
                        foreach (var enemy in World.world.units.units_only_alive)
                        {
                            if (enemy == null || !enemy.isAlive()) continue;
                            if (enemy == actor) continue;
                            float dist = Vector2.Distance(actor.current_position, enemy.current_position);
                            if (dist < stormNearestDist && dist < 20f)
                            {
                                stormNearestDist = dist;
                                stormTarget = enemy;
                            }
                        }
                        
                        Vector3 stormPos = stormTarget != null ? stormTarget.current_position : actor.current_position;
                        
                        // 播放龙卷风特效（用反射调用EffectsLibrary.spawn 8参数版本）
                        try
                        {
                            WorldTile stormTile = World.world.GetTileSimple((int)stormPos.x, (int)stormPos.y);
                            if (stormTile != null)
                            {
                                var spawnMethods = typeof(EffectsLibrary).GetMethods(
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                foreach (var method in spawnMethods)
                                {
                                    if (method.Name == "spawn" && method.GetParameters().Length == 8)
                                    {
                                        // 播放多个风/风暴相关特效
                                        string[] stormEffects = { "fx_storm", "fx_wind_trail_t", "fx_cast_top_purple", "fx_explosion" };
                                        foreach (var fx in stormEffects)
                                        {
                                            try {
                                                method.Invoke(null, new object[] { fx, stormTile, null, null, 2.0f, -1f, -1f, null });
                                            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteAbilityEffect 异常: " + dsEx.Message); }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Code.Core.DSDebug.Verbose($"[DivineAscension] 能量风暴特效播放失败: {e.Message}");
                        }
                        
                        // ★ 调用原版龙卷风灾难
                        try { Realm.VanillaIntegration.SpawnTornado(World.world.GetTileSimple((int)stormPos.x, (int)stormPos.y)); } catch { }

                        // ★ 使用统一AOE伤害管理器（修复：只伤害敌人 + 性能优化）
                        int stormDamaged = Code.Combat.DSAoEDamageManager.ApplyAreaDamage(
                            attacker: actor,
                            centerPosition: stormPos,
                            radius: 8f,
                            baseDamage: damage * 1.2f,
                            damageType: 1,  // 魔法伤害
                            distanceDecay: true,
                            hitAllies: false);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {ability.Name} 龙卷风风暴，命中{stormDamaged}个敌人，基础伤害{damage:F0}");
                    }
                    else if (ability.Id == "ds_ability_energy_bolt")
                    {
                        // 异能冲击：播放帧动画特效 + 范围伤害
                        try { Code.Realm.DSSkillEffectManager.PlayEffect(actor, ability.Id, null); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteAbilityEffect 异常: " + dsEx.Message); }
                        
                        // ★ 使用统一AOE伤害管理器（修复：只伤害敌人 + 性能优化）
                        int damaged = Code.Combat.DSAoEDamageManager.ApplyAreaDamageAtCaster(
                            attacker: actor,
                            radius: 5f,
                            baseDamage: damage,
                            damageType: 1,
                            distanceDecay: true,
                            hitAllies: false);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {ability.Name} 能量冲击，命中{damaged}个敌人，基础伤害{damage:F0}");
                    }
                    else if (ability.Id == "ds_ability_divine_punishment")
                    {
                        // 神罚：原版陨石灾难 + 闪电特效
                        try { Realm.VanillaIntegration.SpawnMeteorite(World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y)); } catch { }
                        // 额外播放闪电特效
                        try
                        {
                            WorldTile punishTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                            if (punishTile != null)
                            {
                                var spawnMethods = typeof(EffectsLibrary).GetMethods(
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                foreach (var method in spawnMethods)
                                {
                                    if (method.Name == "spawn" && method.GetParameters().Length == 8)
                                    {
                                        string[] lightningEffects = { "fx_lightning_big", "fx_lightning_medium" };
                                        foreach (var fx in lightningEffects)
                                        {
                                            try { method.Invoke(null, new object[] { fx, punishTile, null, null, 2.0f, -1f, -1f, null }); } catch { }
                                        }
                                        break;
                                    }
                                }
                            }
                        } catch { }

                        // 大范围AOE伤害
                        int punished = Code.Combat.DSAoEDamageManager.ApplyAreaDamage(
                            attacker: actor,
                            centerPosition: actor.current_position,
                            radius: 12f,
                            baseDamage: damage * 2.0f,
                            damageType: 1,
                            distanceDecay: true,
                            hitAllies: false);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {ability.Name} 神罚陨石，命中{punished}个敌人");
                    }
                    else if (ability.Id == "ds_ability_dimensional_strike")
                    {
                        // 维度打击：调用原版地震灾难
                        try { Realm.VanillaIntegration.SpawnEarthquake(World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y)); } catch { }

                        // 中范围AOE伤害
                        int stricken = Code.Combat.DSAoEDamageManager.ApplyAreaDamage(
                            attacker: actor,
                            centerPosition: actor.current_position,
                            radius: 24f,
                            baseDamage: damage * 1.5f,
                            damageType: 1,
                            distanceDecay: true,
                            hitAllies: false);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {ability.Name} 维度打击地震，命中{stricken}个敌人");
                    }
                    else
                    {
                        // 其他Damage类型技能：范围伤害（使用统一AOE管理器）
                        int damaged = Code.Combat.DSAoEDamageManager.ApplyAreaDamageAtCaster(
                            attacker: actor,
                            radius: 15f,
                            baseDamage: damage,
                            damageType: 0,  // 物理伤害
                            distanceDecay: true,
                            hitAllies: false);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {ability.Name} 造成伤害，命中{damaged}个敌人，基础伤害{damage:F0}");
                    }
                    break;

                case AbilityType.Heal:
                    // 治疗
                    float healAmount = ability.EnergyCost * 0.5f * tierMultiplier;
                    // 基因表达层级技能治疗加成
                    float geneHealBonus = Code.Realm.GeneExpressionSystem.GetSkillHealBonus(actor);
                    if (geneHealBonus > 0f) healAmount *= (1f + geneHealBonus);
                    // GE公式（基因能量）技能治疗加成
                    float geHealBonus = Code.Core.GeneEnergyCalculator.GetHealthBonus(actor);
                    if (geHealBonus > 0f) healAmount *= (1f + geHealBonus / 50f);
                    // 神创基因治疗加成：独立百分比乘（不受/50稀释）
                    float divineHeal = Code.Core.GeneEnergyCalculator.GetDivineHealBonus(actor);
                    if (divineHeal > 0f) healAmount *= (1f + divineHeal);
                    
                    // ★ 使用统一AOE治疗管理器（修复：只治疗友军 + 性能优化 + 直接恢复生命值）
                    int healed = Code.Combat.DSAoEDamageManager.ApplyAreaHeal(
                        healer: actor,
                        centerPosition: actor.current_position,
                        radius: 10f,
                        healAmount: healAmount,
                        includeSelf: true);
                    DSDebug.Verbose($"[DivineAscension] {ability.Name} 治疗{healed}个友军，治疗量{healAmount:F0}");
                    break;

                case AbilityType.Buff:
                    // 增益（添加strong/fast等特质模拟）
                    actor.addTrait("strong", false);
                    actor.addTrait("fast", false);
                    DSDebug.Verbose($"[DivineAscension] {ability.Name} 增益效果已激活");
                    break;

                case AbilityType.Debuff:
                    // 减益（使用空间分区查询 + 敌友判断）
                    int debuffed = 0;
                    try
                    {
                        WorldTile centerTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                        if (centerTile != null)
                        {
                            var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 16, 17f, false);
                            if (nearbyUnits != null)
                            {
                                foreach (var enemy in nearbyUnits)
                                {
                                    if (enemy == null || !enemy.isAlive() || enemy == actor) continue;
                                    // ★ 敌友判断：只减益敌人
                                    if (actor.kingdom != null && enemy.kingdom != null && !actor.kingdom.isEnemy(enemy.kingdom)) continue;
                                    float dist = Vector2.Distance(actor.current_position, enemy.current_position);
                                    if (dist < 12f)
                                    {
                                        enemy.addTrait("weak", false);
                                        enemy.addTrait("slow", false);
                                        debuffed++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) { DSDebug.Verbose($"[DivineAscension] Debuff异常: {e.Message}"); }
                    DSDebug.Verbose($"[DivineAscension] {ability.Name} 减益{debuffed}个敌人");
                    break;

                case AbilityType.Utility:
                    // 功能型（汲取能量，使用空间分区查询 + 敌友判断）
                    float drained = 0f;
                    try
                    {
                        WorldTile centerTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                        if (centerTile != null)
                        {
                            var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 16, 15f, false);
                            if (nearbyUnits != null)
                            {
                                foreach (var enemy in nearbyUnits)
                                {
                                    if (enemy == null || !enemy.isAlive() || enemy == actor) continue;
                                    // ★ 敌友判断：只从敌人汲取能量
                                    if (actor.kingdom != null && enemy.kingdom != null && !actor.kingdom.isEnemy(enemy.kingdom)) continue;
                                    float dist = Vector2.Distance(actor.current_position, enemy.current_position);
                                    if (dist < 10f)
                                    {
                                        float enemyEnergy = CultivationData.GetEnergy(enemy);
                                        float drain = Mathf.Min(enemyEnergy, 100f * tierMultiplier);
                                        CultivationData.SetEnergy(enemy, enemyEnergy - drain);
                                        drained += drain;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) { DSDebug.Verbose($"[DivineAscension] Utility异常: {e.Message}"); }
                    float currentEnergy = CultivationData.GetEnergy(actor);
                    CultivationData.SetEnergy(actor, currentEnergy + drained);
                    DSDebug.Verbose($"[DivineAscension] {ability.Name} 汲取能量{drained:F0}");
                    break;

                case AbilityType.Ultimate:
                    // ★ 按境界设置不同技能范围（方案A）
                    // 10阶使徒级:20格, 11阶登神级:30格, 12阶真神级:50格, 13阶超神级:全屏
                    float ultRange = tier >= 13 ? 9999f : (tier >= 12 ? 50f : (tier >= 11 ? 30f : 20f));

                    // 终极技能（范围伤害+特殊效果）
                    float ultDamage = ability.EnergyCost * 2.5f * tierMultiplier;
                    // ★ 基因表达层级技能伤害加成
                    float ultGeneBonus = Code.Realm.GeneExpressionSystem.GetSkillDamageBonus(actor);
                    if (ultGeneBonus > 0f) ultDamage *= (1f + ultGeneBonus);
                    // ★ GE公式（基因能量）技能伤害加成
                    float ultGeBonus = Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
                    if (ultGeBonus > 0f) ultDamage *= (1f + ultGeBonus / 30f);
                    // ★ 基因连锁系统技能伤害加成
                    float ultLinkageBonus = Code.GeneLinkageSystem.GetTotalSkillDamageBonus(actor);
                    if (ultLinkageBonus > 0f) ultDamage *= (1f + ultLinkageBonus);

                    int ultDamaged = 0;
                    
                    // ★ 非全屏技能使用空间分区查询（性能优化），全屏技能（真神）遍历所有单位
                    if (ultRange < 9999f)
                    {
                        // 空间分区查询（性能优化）
                        try
                        {
                            WorldTile centerTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                            if (centerTile != null)
                            {
                                int chunkRange = (int)(ultRange / 8f) + 2;
                                var nearbyUnits = Finder.getUnitsFromChunk(centerTile, chunkRange, ultRange + 5f, false);
                                if (nearbyUnits != null)
                                {
                                    foreach (var enemy in nearbyUnits)
                                    {
                                        if (enemy == null || !enemy.isAlive() || enemy == actor) continue;
                                        // ★ 敌友判断：只伤害敌人
                                        if (actor.kingdom != null && enemy.kingdom != null && !actor.kingdom.isEnemy(enemy.kingdom)) continue;
                                        float dist = Vector2.Distance(actor.current_position, enemy.current_position);
                                        if (dist < ultRange)
                                        {
                                            // ★ 距离衰减：中心100%，边缘50%
                                            float finalDmg = ultDamage * (1f - dist / ultRange * 0.5f);
                                            enemy.getHit(finalDmg, false, AttackType.Other, null, false, false, true);
                                            ultDamaged++;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e) { DSDebug.Verbose($"终极技能空间查询异常: {e.Message}"); }
                    }
                    else
                    {
                        // 真神全屏技能：遍历所有单位（释放频率低，可接受）
                        foreach (var enemy in World.world.units.units_only_alive)
                        {
                            if (enemy == null || !enemy.isAlive() || enemy == actor) continue;
                            // ★ 敌友判断：只伤害敌人
                            if (actor.kingdom != null && enemy.kingdom != null && !actor.kingdom.isEnemy(enemy.kingdom)) continue;
                            // 真神全屏无衰减
                            enemy.getHit(ultDamage, false, AttackType.Other, null, false, false, true);
                            ultDamaged++;
                        }
                    }

                    // 终极技能附带特殊效果
                    if (ability.Id == "ds_ability_sanctuary")
                    {
                        // 真神：友军获得无敌（用blessed模拟），使用空间分区查询
                        try
                        {
                            WorldTile centerTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                            if (centerTile != null)
                            {
                                var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 16, 25f, false);
                                if (nearbyUnits != null)
                                {
                                    foreach (var ally in nearbyUnits)
                                    {
                                        if (ally == null || !ally.isAlive()) continue;
                                        // 只增益友军
                                        if (actor.kingdom != null && ally.kingdom != null && actor.kingdom != ally.kingdom) continue;
                                        float dist = Vector2.Distance(actor.current_position, ally.current_position);
                                        if (dist < 20f)
                                        {
                                            ally.addTrait("blessed", false);
                                            ally.addTrait("regenerating", false);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e) { DSDebug.Verbose($"[DivineAscension] 圣域友军增益异常: {e.Message}"); }
                        //  11阶登神级：凝聚基因化身独立作战
                        try { RealmMechanics.SummonElementAvatar(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExecuteAbilityEffect 异常: " + dsEx.Message); }
                    }
                    else if (ability.Id == "ds_ability_space_tear")
                    {
                        // 空间撕裂：制造虚空裂隙
                        Disaster.DisasterManager.SpawnDebugRift(actor.current_position.x, actor.current_position.y);
                    }
                    else if (ability.Id == "ds_ability_divine_punishment")
                    {
                        //  13阶神罚：屏幕震动 + 全局通知
                        TriggerDivinePunishmentEffects(actor);
                    }
                    
                    // ★ 地形破坏：10阶以上技能释放时破坏范围内的地形
                    if (tier >= 10)
                    {
                        try
                        {
                            // 根据技能类型选择破坏类型（境界越高，破坏类型越强）
                            Code.Combat.TerrainDestructionType terrainType = tier switch
                            {
                                >= 13 => Code.Combat.TerrainDestructionType.DivinePunishment,      // 真神：神罚型
                                >= 12 => Code.Combat.TerrainDestructionType.DimensionalStrike,     // 真神级：维度打击型
                                >= 11 => Code.Combat.TerrainDestructionType.EnergyStorm,            // 登神级：能量风暴型
                                _ => Code.Combat.TerrainDestructionType.Generic                      // 使徒级：通用型
                            };
                            
                            // 根据境界设置破坏概率（境界越高，概率越高）
                            float terrainChance = tier switch
                            {
                                >= 13 => 0.50f,  // 真神：50%
                                >= 12 => 0.40f,  // 真神级：40%
                                >= 11 => 0.30f,  // 登神级：30%
                                _ => 0.20f        // 使徒级：20%
                            };
                            
                            // 根据境界设置破坏范围（境界越高，范围越大）
                            // 真神：60格（接近全屏，但不是完全全屏，避免性能问题）
                            float terrainRadius = tier switch
                            {
                                >= 13 => 60f,    // 超神级：60格
                                >= 12 => 45f,    // 真神级：45格
                                >= 11 => 35f,    // 登神级：35格
                                >= 10 => 25f,    // 使徒级：25格
                                >= 9 => 15f,     // 天灾级：15格
                                _ => 0f          // 8阶及以下：无地形破坏
                            };
                            
                            int destroyed = Code.Combat.DSTerrainDestruction.DestroyTerrain(
                                center: actor.current_position,
                                radius: terrainRadius,
                                destructionChance: terrainChance,
                                destructionType: terrainType,
                                caster: actor);
                            
                            if (destroyed > 0)
                            {
                                DSDebug.Verbose($"[DivineAscension] {ability.Name} 地形破坏！{destroyed}个地块被摧毁");
                            }
                        }
                        catch (Exception e) { DSDebug.Verbose($"地形破坏异常: {e.Message}"); }
                    }
                    
                    DSDebug.Verbose($"[DivineAscension]  终极技能 {ability.Name} 释放！命中{ultDamaged}个敌人，伤害{ultDamage:F0}");
                    break;
            }
        }

        /// <summary>
        /// 6阶场域者：释放技能时自动展开个人场域
        /// 半径8格，自身全属性+20%（strong+fast），敌人全属性-10%（weak+slow）
        /// 持续10秒（用特质模拟，年度Tick中高境界会自动维持）
        /// </summary>
        private static void TriggerDomainExpansion(Actor actor)
        {
            try
            {
                if (World.world == null || World.world.units == null) return;

                // 自身增益
                if (!actor.hasTrait("strong")) actor.addTrait("strong", false);
                if (!actor.hasTrait("fast")) actor.addTrait("fast", false);

                // 场域内敌人减益（半径8格，使用空间分区查询 + 敌友判断）
                int suppressed = 0;
                try
                {
                    WorldTile centerTile = World.world.GetTileSimple((int)actor.current_position.x, (int)actor.current_position.y);
                    if (centerTile != null)
                    {
                        var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 8, 13f, false);
                        if (nearbyUnits != null)
                        {
                            int actorTier = CultivationData.GetRealmTier(actor);
                            foreach (var enemy in nearbyUnits)
                            {
                                if (enemy == null || !enemy.isAlive() || enemy == actor) continue;
                                // ★ 敌友判断：只压制敌人
                                if (actor.kingdom != null && enemy.kingdom != null && !actor.kingdom.isEnemy(enemy.kingdom)) continue;
                                float dist = Vector2.Distance(actor.current_position, enemy.current_position);
                                if (dist < 8f)
                                {
                                    int enemyTier = CultivationData.GetRealmTier(enemy);
                                    // 只压制低于场域者境界的敌人
                                    if (enemyTier < actorTier)
                                    {
                                        if (!enemy.hasTrait("weak")) enemy.addTrait("weak", false);
                                        if (!enemy.hasTrait("slow")) enemy.addTrait("slow", false);
                                        suppressed++;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) { DSDebug.Verbose($"场域展开减益异常: {e.Message}"); }

                // 场域展开通知（宏大叙事风格）—— 至少压制1个敌人才弹全局通知，0个不刷屏
                if (suppressed >= 1)
                {
                    DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("ability_domain_effect"), actor.getName(), suppressed));
                }
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 场域展开！半径8格，压制{suppressed}个敌人");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"场域展开失败: {e.Message}");
            }
        }

        /// <summary>
        /// 13阶神罚特效：屏幕震动 + 全局通知 + 历史记录
        /// </summary>
        private static void TriggerDivinePunishmentEffects(Actor actor)
        {
            try
            {
                // 屏幕震动（通过相机震动组件）
                if (Camera.main != null)
                {
                    var shaker = Camera.main.gameObject.GetComponent<DivinePunishmentShaker>();
                    if (shaker == null)
                    {
                        shaker = Camera.main.gameObject.AddComponent<DivinePunishmentShaker>();
                    }
                    shaker.Shake(1.0f, 0.5f);
                }

                // 全局通知（宏大叙事风格）
                DSNotificationManager.NotifyMajor(UILocalization.Get("ability_truegod_storm_name"), string.Format(UILocalization.Get("ability_truegod_storm_desc"), actor.getName()));

                // 历史记录（个人史诗风格）
                DSEventManager.RecordEvent(
                    DSEventType.Divine,
                    UILocalization.Get("ability_divine_punishment_name"),
                    string.Format(UILocalization.Get("ability_divine_punishment_desc"), actor.getName()),
                    actor.getName(),
                    ActorDataAccessor.GetData(actor).id, 0, actor);
            }
            catch (Exception e)
            {
                DSDebug.Warning($"神罚特效失败: {e.Message}");
            }
        }

        /// <summary>神罚屏幕震动组件（释放后自动移除，防止Camera.main组件累积）</summary>
        private class DivinePunishmentShaker : MonoBehaviour
        {
            private float _shakeDuration = 0f;
            private float _shakeIntensity = 0f;
            private Vector3 _originalPosition;

            public void Shake(float duration, float intensity)
            {
                _shakeDuration = duration;
                _shakeIntensity = intensity;
                _originalPosition = transform.position;
            }

            private void Update()
            {
                if (_shakeDuration > 0)
                {
                    transform.position = _originalPosition + UnityEngine.Random.insideUnitSphere * _shakeIntensity;
                    _shakeDuration -= Time.deltaTime;
                    if (_shakeDuration <= 0)
                    {
                        transform.position = _originalPosition;
                        Destroy(this);
                    }
                }
            }
        }

        /// <summary>根据ID获取技能</summary>
        public static RealmAbility GetAbilityById(string abilityId)
        {
            if (_abilitiesById.TryGetValue(abilityId, out RealmAbility ability))
                return ability;
            return null;
        }

        /// <summary>获取所有技能</summary>
        public static Dictionary<string, RealmAbility> GetAllAbilities()
        {
            return new Dictionary<string, RealmAbility>(_abilitiesById);
        }

        /// <summary>冷却清理（定期调用，清理过期的冷却记录）</summary>
        public static void OnAnnualTick()
        {
            // 清理过期的冷却记录（超过最大冷却时间+60秒的）
            float currentTime = Time.time;
            var toRemove = new List<long>();
            foreach (var kvp in _cooldowns)
            {
                bool allExpired = true;
                foreach (var cd in kvp.Value)
                {
                    RealmAbility ability = GetAbilityById(cd.Key);
                    // 冷却时间现在按秒计算，超过冷却时间+60秒的记录可以清理
                    if (ability != null && currentTime - cd.Value < ability.Cooldown + 60f)
                    {
                        allExpired = false;
                        break;
                    }
                }
                if (allExpired) toRemove.Add(kvp.Key);
            }
            foreach (long id in toRemove)
                _cooldowns.Remove(id);

            // 清理死亡单位的怒气值
            var rageToRemove = new List<long>();
            foreach (var kvp in _rageValues)
            {
                try
                {
                    Actor actor = World.world.units.get(kvp.Key);
                    if (actor == null || !actor.isAlive())
                    {
                        rageToRemove.Add(kvp.Key);
                    }
                }
                catch { rageToRemove.Add(kvp.Key); }
            }
            foreach (long id in rageToRemove)
                _rageValues.Remove(id);
        }

        /// <summary>
        /// 技能释放后，添加对应的状态效果到状态栏（有持续时间）
        /// 状态效果显示在单位窗口的状态栏（buff/debuff区域）
        /// 持续时间结束后状态效果自动消失
        /// </summary>
        public static void AddAbilityStatusEffect(Actor actor, string abilityId)
        {
            if (actor == null || !actor.isAlive()) return;

            try
            {
                var ability = GetAbilityById(abilityId);
                if (ability == null) return;

                // 状态效果持续时间 = 技能持续时间（暂时用冷却时间×10，即冷却1年=持续10秒）
                // 不同类型技能有不同的默认持续时间
                float duration = ability.Type switch
                {
                    AbilityType.Buff => 30f,      // 增益：30秒（0.5年）
                    AbilityType.Debuff => 20f,    // 减益：20秒
                    AbilityType.Heal => 10f,      // 治疗：10秒
                    AbilityType.Damage => 5f,     // 伤害：5秒（瞬间伤害后的短暂效果）
                    AbilityType.Utility => 15f,   // 功能：15秒
                    AbilityType.Ultimate => 60f,  // 终极：60秒（1年）
                    _ => 10f
                };

                // 使用技能ID作为状态效果ID，添加到状态栏
                DSReflectionHelper.SafeAddStatusEffect(actor, abilityId, duration, true);

                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 释放技能【{ability.Name}】，状态效果已添加到状态栏（持续{duration}秒）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加技能状态效果失败: {e.Message}");
            }
        }
    }
}
















