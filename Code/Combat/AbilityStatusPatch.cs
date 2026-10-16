// ============================================================

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Code.Core;
using Code.Data;
using Code.Realm;

namespace Code.Combat
{
    [HarmonyPatch(typeof(Actor), "getHit")]
    public static class AbilityStatusPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void GetHit_Prefix(
            Actor __instance,
            ref float pDamage,
            bool pFlash,
            AttackType pAttackType,
            BaseSimObject pAttacker,
            bool pSkipIfShake,
            bool pMetallicWeapon)
        {
            if (pDamage <= 0f) return;
            try
            {
                if (__instance == null)
                    return;
                
                // 用ActorDataAccessor访问data字段（避免直接访问private字段导致FieldAccessException）
                var actorData = ActorDataAccessor.GetData(__instance);
                // 用反射访问stats字段（避免直接访问private字段导致FieldAccessException）
                var stats = __instance.stats;
                if (actorData == null || stats == null)
                    return;

                float damage = pDamage;
                Actor from = pAttacker as Actor;

                if (from != null && CultivationData.IsAscended(from))
                    RealmAbilities.AddRage(from, 5f);
                if (__instance != null && CultivationData.IsAscended(__instance))
                    RealmAbilities.AddRage(__instance, 3f);

                if (from != null && CultivationData.IsAscended(from))
                    TryAutoCastAbilityInCombat(from);

                if (__instance != null && CultivationData.IsAscended(__instance))
                {
                    try { GeneMutationSystem.CombatMutationCheck(__instance); } catch { }
                }

                if (from != null && __instance != null && CultivationData.IsAscended(from) && CultivationData.IsAscended(__instance))
                {
                    int attackerTier = CultivationData.GetRealmTier(from);
                    int defenderTier = CultivationData.GetRealmTier(__instance);
                    int tierDiff = attackerTier - defenderTier;
                    if (tierDiff > 0)
                    {
                        damage *= Mathf.Pow(2f, tierDiff / 2f);
                        if (attackerTier >= 13 && defenderTier <= 12) damage *= 5f;
                    }
                    else if (tierDiff < 0)
                    {
                        damage *= Mathf.Max(0.01f, 1f - 0.2f * (-tierDiff));
                        if (defenderTier >= 13 && attackerTier <= 12) damage *= 0.05f;
                        try
                        {
                            float maxHp = __instance.getMaxHealth();
                            if (maxHp > 0f && damage > maxHp * 0.005f) damage = maxHp * 0.005f;
                        } catch { }
                    }
                }

                // 纪元战斗加成：真神/超神纪元的主宰染色体赋予全世界战斗增幅（对所有攻击者生效，无纪元时为0）
                float eraCombat = GeneEnergyCalculator.GetEraCombatBonus();
                if (eraCombat > 0f) damage *= (1f + eraCombat);

                if (from != null && CultivationData.IsAscended(from))
                {
                    damage *= RealmFeatures.GetRealmCombatMultiplier(CultivationData.GetRealmTier(from));
                    float geDamage = GeneEnergyCalculator.GetDamageBonus(from);
                    if (geDamage > 0f) damage *= (1f + geDamage / 30f);
                    float linkageDamage = GeneLinkageSystem.GetTotalDamageBonus(from);
                    if (linkageDamage > 0f) damage *= (1f + linkageDamage);
//                     float buildingDamage = CultivationData.GetBuildingDamageBoost(from);
//                     if (buildingDamage > 0f) damage *= (1f + buildingDamage);
                    // 神创基因攻击加成：独立百分比乘（不受/30稀释）
                    float divineAtk = GeneEnergyCalculator.GetDivineDamageBonus(from);
                    if (divineAtk > 0f) damage *= (1f + divineAtk);

                    // 基因个体效果·暴击（v0.16.0）：CriticalChance 基因×表达倍率聚合为暴击率，命中翻倍伤害
                    try
                    {
                        float critChance = Code.Realm.GeneExpressionSystem.GetIndividualEffectSum(from, Code.Realm.GeneEffectType.CriticalChance, 0.5f);
                        if (critChance > 0f && UnityEngine.Random.value < critChance) damage *= 2f;
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 暴击应用失败: " + dsEx.Message); }

                    // 基因个体效果·潜能暴走（SpecialAbility 基因，v0.16.0）：
                    // 每激活1个潜能基因，濒死（生命<30%）暴走伤害+10%（上限+50%）——特殊能力的真实落点
                    try
                    {
                        int specialGenes = Code.Realm.GeneExpressionSystem.CountGenesOfType(from, Code.Realm.GeneEffectType.SpecialAbility);
                        // 连锁·特殊能力（v0.16.0）：激活任意特殊能力连锁视为额外1个潜能基因（ds_link_special 此前无消费方）
                        var fromData2 = ActorDataAccessor.GetData(from);
                        if (fromData2 != null && fromData2.custom_data_float != null &&
                            fromData2.custom_data_float.TryGetValue("ds_link_special", out float linkSpecial) && linkSpecial > 0f)
                        {
                            specialGenes += 1;
                        }
                        if (specialGenes > 0 && from.data.health < from.getMaxHealth() * 0.3f)
                        {
                            float berserk = Mathf.Min(0.5f, specialGenes * 0.10f);
                            damage *= (1f + berserk);
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 潜能暴走应用失败: " + dsEx.Message); }

                    // 异能攻击标签（v0.17.0）：灼烧/减速/麻痹/毒蚀/溅射/吸血/暴击/治疗（读 ds_prototype 异能标签）
                    try { Code.Realm.Prototype.PrototypeEffectEngine.OnAttack(from, __instance, ref damage); }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 异能攻击效果失败: " + dsEx.Message); }
                }

                if (__instance != null && CultivationData.IsAscended(__instance))
                {
                    damage /= RealmFeatures.GetRealmDefenseMultiplier(CultivationData.GetRealmTier(__instance));
                    float geArmor = GeneEnergyCalculator.GetArmorBonus(__instance);
                    if (geArmor > 0f) damage *= Mathf.Max(0.05f, 1f - geArmor / 100f);
                    float linkageArmor = GeneLinkageSystem.GetTotalDefenseBonus(__instance);
                    if (linkageArmor > 0f) damage *= Mathf.Max(0.1f, 1f - linkageArmor);
                    // 神创基因防御加成：独立百分比减伤（不受/100稀释）
                    float divineDef = GeneEnergyCalculator.GetDivineDefBonus(__instance);
                    if (divineDef > 0f) damage *= Mathf.Max(0.05f, 1f - divineDef);
                    // 神创基因伤害减免：独立乘
                    float divineDmgRed = GeneEnergyCalculator.GetDivineDmgRedBonus(__instance);
                    if (divineDmgRed > 0f) damage *= Mathf.Max(0.05f, 1f - divineDmgRed);
                    // 基因个体效果·伤害减免（v0.16.0）：DamageReduction 基因×表达倍率聚合减伤（上限50%）
                    try
                    {
                        float geneDmgRed = Code.Realm.GeneExpressionSystem.GetIndividualEffectSum(__instance, Code.Realm.GeneEffectType.DamageReduction, 0.5f);
                        if (geneDmgRed > 0f) damage *= Mathf.Max(0.05f, 1f - geneDmgRed);
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 基因减伤应用失败: " + dsEx.Message); }

                    // 异能防守标签（v0.17.0/0.18.0）：护盾减伤 / 闪避免伤 / 祝福再生回血 / 磁力震动声波反击（读 ds_prototype 异能标签）
                    try { Code.Realm.Prototype.PrototypeEffectEngine.OnDefend(__instance, from, ref damage); }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 异能防守效果失败: " + dsEx.Message); }
                }

                // 神创基因暴击率：独立判定（默认0=不暴击）
                if (from != null && CultivationData.IsAscended(from))
                {
                    float divineCrit = GeneEnergyCalculator.GetDivineCritBonus(from);
                    if (divineCrit > 0f && UnityEngine.Random.value < divineCrit)
                    {
                        damage *= 2f;
                        Code.Core.DSDebug.Verbose($"[GeneCultivation] {from.getName()} 神创暴击！伤害×2");
                    }
                }

                // 神创基因吸血：对目标造成实际伤害后，攻击者恢复生命（按最终伤害比例）
                if (from != null && CultivationData.IsAscended(from))
                {
                    float divineLifesteal = GeneEnergyCalculator.GetDivineLifestealBonus(from);
                    // 基因个体效果·吸血（v0.16.0）：Lifesteal 基因×表达倍率聚合，与神创吸血叠加
                    float geneLifesteal = 0f;
                    try { geneLifesteal = Code.Realm.GeneExpressionSystem.GetIndividualEffectSum(from, Code.Realm.GeneEffectType.Lifesteal, 0.5f); }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 基因吸血聚合失败: " + dsEx.Message); }
                    float lifesteal = divineLifesteal + geneLifesteal;
                    if (lifesteal > 0f)
                    {
                        float healAmount = damage * lifesteal;
                        if (healAmount > 1f)
                        {
                            try
                            {
                                var fromData = ActorDataAccessor.GetData(from);
                                if (fromData != null)
                                {
                                    float curHp = from.getHealth();
                                    float maxHp = from.getMaxHealth();
                                    float newHp = Mathf.Min(maxHp, curHp + healAmount);
                                    fromData.health = (int)newHp;
                                }
                            }
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 神创吸血异常: " + dsEx.Message); }
                        }
                    }
                }

                if (from != null && VirtualWeaponSystem.HasVirtualEnergyWeapon(from))
                {
                    float virtualDamage = VirtualWeaponSystem.GetVirtualWeaponDamage(from);
                    if (virtualDamage > 0f)
                    {
                        virtualDamage *= (1f + GeneExpressionSystem.GetSkillDamageBonus(from));
                        if (CultivationData.IsAscended(from))
                        {
                            float geDamage = GeneEnergyCalculator.GetDamageBonus(from);
                            if (geDamage > 0f) virtualDamage *= (1f + geDamage / 30f);
                        }
                        damage *= (1f + VirtualWeaponSystem.GetVirtualWeaponArmorPenetration(from) * 0.5f);
                        damage += virtualDamage;
                        VirtualWeaponSystem.PlayWeaponAttackEffect(from, __instance);
                    }
                }

                // 战斗表现层（v0.18.47）：伤害命中演出——登神单位出手时触发星形/火花/碎片/音效，大伤害追加击停
                // （仅登神攻击者触发，避免原版生物互殴被演出刷屏）
                try
                {
                    if (from != null && CultivationData.IsAscended(from))
                    {
                        Code.Combat.Fx.CombatFxDirector.HitImpact(__instance, damage);
                    }
                }
                catch (System.Exception dsFxEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 命中演出失败: " + dsFxEx.Message); }

                pDamage = damage;
            }
            catch (Exception dsEx)
            {
                DSDebug.Warning("[GeneCultivation] GetHit_Prefix 异常: " + dsEx.Message);
            }
        }

        private static readonly Dictionary<long, float> _combatCooldowns = new Dictionary<long, float>();

        private static void TryAutoCastAbilityInCombat(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            long actorId = ActorDataAccessor.GetData(actor).id;
            float currentTime = Time.time;
            if (_combatCooldowns.TryGetValue(actorId, out float lastCast) && currentTime - lastCast < 0.5f) return;

            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0) return;
            var availableAbilities = RealmAbilities.GetAbilitiesForTier(tier);
            if (availableAbilities == null || availableAbilities.Count == 0) return;

            var normalAbilities = availableAbilities.FindAll(a => a.RequiredTier < 10);
            var ultimateAbilities = availableAbilities.FindAll(a => a.RequiredTier >= 10);
            RealmAbility chosen = null;

            if (ultimateAbilities.Count > 0 && RealmAbilities.GetRage(actor) >= 100f)
                chosen = ultimateAbilities[UnityEngine.Random.Range(0, ultimateAbilities.Count)];

            if (chosen == null && normalAbilities.Count > 0)
            {
                var damageAbilities = normalAbilities.FindAll(a => a.Type == Realm.AbilityType.Damage);
                var buffAbilities = normalAbilities.FindAll(a => a.Type == Realm.AbilityType.Buff);
                float roll = UnityEngine.Random.value;
                if (roll < 0.5f && damageAbilities.Count > 0)
                    chosen = damageAbilities[UnityEngine.Random.Range(0, damageAbilities.Count)];
                else if (roll < 0.8f && buffAbilities.Count > 0)
                    chosen = buffAbilities[UnityEngine.Random.Range(0, buffAbilities.Count)];
                else
                    chosen = normalAbilities[UnityEngine.Random.Range(0, normalAbilities.Count)];
            }

            if (chosen != null && RealmAbilities.UseAbility(actor, chosen.Id))
                _combatCooldowns[actorId] = currentTime;
        }
    }
}
