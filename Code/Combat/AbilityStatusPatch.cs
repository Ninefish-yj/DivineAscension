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
        private static FieldInfo _maxHpField;
        private static FieldInfo _hpField;
        private static FieldInfo _statsField;

        private static FieldInfo GetMaxHpField()
        {
            if (_maxHpField == null)
                _maxHpField = typeof(Actor).GetField("maxHp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return _maxHpField;
        }

        private static FieldInfo GetHpField()
        {
            if (_hpField == null)
                _hpField = typeof(Actor).GetField("hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return _hpField;
        }

        private static FieldInfo GetStatsField()
        {
            if (_statsField == null)
                _statsField = typeof(BaseSimObject).GetField("stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return _statsField;
        }

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
                            var maxHpField = GetMaxHpField();
                            if (maxHpField != null)
                            {
                                float maxHp = (float)maxHpField.GetValue(__instance);
                                if (damage > maxHp * 0.005f) damage = maxHp * 0.005f;
                            }
                        } catch { }
                    }
                }

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
                }

                // 神创基因暴击率：独立判定（默认0=不暴击）
                if (from != null && CultivationData.IsAscended(from))
                {
                    float divineCrit = GeneEnergyCalculator.GetDivineCritBonus(from);
                    if (divineCrit > 0f && UnityEngine.Random.value < divineCrit)
                    {
                        damage *= 2f;
                        Code.Core.DSDebug.Verbose($"[DivineAscension] {from.getName()} 神创暴击！伤害×2");
                    }
                }

                // 神创基因吸血：对目标造成实际伤害后，攻击者恢复生命（按最终伤害比例）
                if (from != null && CultivationData.IsAscended(from))
                {
                    float divineLifesteal = GeneEnergyCalculator.GetDivineLifestealBonus(from);
                    if (divineLifesteal > 0f)
                    {
                        float healAmount = damage * divineLifesteal;
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
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] 神创吸血异常: " + dsEx.Message); }
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

                pDamage = damage;
            }
            catch (Exception dsEx)
            {
                DSDebug.Warning("[DivineAscension] GetHit_Prefix 异常: " + dsEx.Message);
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
