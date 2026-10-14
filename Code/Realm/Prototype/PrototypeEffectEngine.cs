// ============================================================
// 异能效果引擎（v0.17.0 新增系统）
// 单一职责：按单位异能（ds_prototype）的主/副标签执行真实战斗效果。
// 由 AbilityStatusPatch（Actor.getHit）调用：
//   - OnAttack：攻击命中后触发攻击标签（Burn/Slow/Stun/Poison/Chain/Lifesteal/Crit/Heal）
//   - OnDefend：受击时生效防守标签（Shield/Dodge）
// 效果载体优先复用原版状态（burning/poisoned/frozen/stunned——原版自带持续伤害/行为），
// Shield/Dodge 无原版行为，由引擎实现。全部带上限与概率控制（延续 v0.16.0 限幅原则）。
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Core;
using Code.Data;

namespace Code.Realm.Prototype
{
    public static class PrototypeEffectEngine
    {
        // Chain 溅射防递归：同一攻击者的溅射伤害不再触发标签（防无限递归/爆炸扩散）
        private static readonly HashSet<long> _chainProcessing = new HashSet<long>();

        // 溅射半径（格）与伤害比例
        private const float CHAIN_RADIUS = 3f;
        private const float CHAIN_MAX_TARGETS = 4;

        // 减伤/闪避/吸血等上限（延续 v0.16.0 限幅原则）
        private const float MAX_SHIELD_REDUCTION = 0.4f;
        private const float MAX_DODGE_CHANCE = 0.4f;
        private const float MAX_LIFESTEAL = 0.3f;
        private const float MAX_CRIT_CHANCE = 0.4f;

        /// <summary>
        /// 攻击标签触发入口（在 AbilityStatusPatch 攻击加成段末尾调用）。
        /// </summary>
        public static void OnAttack(Actor attacker, Actor target, ref float damage)
        {
            if (attacker == null || target == null || !attacker.isAlive() || !target.isAlive()) return;
            if (damage <= 0f) return;
            // 溅射伤害不触发标签（防递归 + 防爆炸扩散：溅射跳伤害只继承本体伤害比例）
            if (_chainProcessing.Contains(attacker.getID())) return;
            var proto = PrototypeRegistry.GetForActor(attacker);
            if (proto == null) return;

            try
            {
                // 主标签与副标签都触发（主标签概率更高）
                TryApplyAttackTag(attacker, target, proto.MainTag, ref damage, 1.0f);
                TryApplyAttackTag(attacker, target, proto.SubTag, ref damage, 0.6f);
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Verbose("[GeneCultivation] 异能攻击效果异常: " + dsEx.Message);
            }
        }

        /// <summary>
        /// 防守标签入口（在 AbilityStatusPatch 防御减伤段调用）。
        /// v0.18.0 扩展：传入攻击者，支持反击型防守标签（磁力/震动/声波麻痹攻击者）。
        /// </summary>
        public static void OnDefend(Actor defender, Actor attacker, ref float damage)
        {
            if (defender == null || !defender.isAlive() || damage <= 0f) return;
            var proto = PrototypeRegistry.GetForActor(defender);
            if (proto == null) return;

            try
            {
                // 护盾：减伤
                if (proto.MainTag == PrototypeTag.Shield || proto.SubTag == PrototypeTag.Shield)
                {
                    float shieldReduction = Mathf.Min(MAX_SHIELD_REDUCTION, PrototypeTags.Get(PrototypeTag.Shield).Strength + GeneAffinityBridge.GetTagAffinity(defender, PrototypeTag.Shield) * 0.03f);
                    damage *= Mathf.Max(0.05f, 1f - shieldReduction);
                }
                // 闪避组：Dodge/Illusion/Invisibility 概率完全免伤（强度递增）
                if (proto.MainTag == PrototypeTag.Dodge || proto.SubTag == PrototypeTag.Dodge ||
                    proto.MainTag == PrototypeTag.Illusion || proto.SubTag == PrototypeTag.Illusion ||
                    proto.MainTag == PrototypeTag.Invisibility || proto.SubTag == PrototypeTag.Invisibility)
                {
                    PrototypeTag dodgeTag = proto.MainTag == PrototypeTag.Dodge || proto.MainTag == PrototypeTag.Illusion || proto.MainTag == PrototypeTag.Invisibility
                        ? proto.MainTag : proto.SubTag;
                    float dodgeChance = Mathf.Min(MAX_DODGE_CHANCE, PrototypeTags.Get(dodgeTag).Strength + GeneAffinityBridge.GetTagAffinity(defender, dodgeTag) * 0.05f);
                    if (UnityEngine.Random.value < dodgeChance)
                    {
                        damage = 0f;
                        DSDebug.Verbose("[GeneCultivation] " + defender.getName() + " 异能闪避！");
                    }
                }
                // 防守回复组：Blessing/Regeneration 受击概率回血
                if (proto.MainTag == PrototypeTag.Blessing || proto.SubTag == PrototypeTag.Blessing ||
                    proto.MainTag == PrototypeTag.Regeneration || proto.SubTag == PrototypeTag.Regeneration)
                {
                    PrototypeTag healDefTag = proto.MainTag == PrototypeTag.Blessing || proto.MainTag == PrototypeTag.Regeneration
                        ? proto.MainTag : proto.SubTag;
                    var def = PrototypeTags.Get(healDefTag);
                    float chance = Mathf.Min(def.MaxChance, def.BaseChance + GeneAffinityBridge.GetTagAffinity(defender, healDefTag) * 0.05f);
                    if (UnityEngine.Random.value < chance)
                    {
                        float healAmount = damage * def.Strength;
                        if (healAmount > 1f) TryHeal(defender, healAmount);
                    }
                }
                // 反击组：Magnetism/Vibration/Sound 受击概率麻痹攻击者
                if (attacker != null && attacker.isAlive() &&
                    (proto.MainTag == PrototypeTag.Magnetism || proto.SubTag == PrototypeTag.Magnetism ||
                     proto.MainTag == PrototypeTag.Vibration || proto.SubTag == PrototypeTag.Vibration ||
                     proto.MainTag == PrototypeTag.Sound || proto.SubTag == PrototypeTag.Sound))
                {
                    PrototypeTag counterTag = proto.MainTag == PrototypeTag.Magnetism || proto.MainTag == PrototypeTag.Vibration || proto.MainTag == PrototypeTag.Sound
                        ? proto.MainTag : proto.SubTag;
                    var def = PrototypeTags.Get(counterTag);
                    float chance = Mathf.Min(def.MaxChance, def.BaseChance + GeneAffinityBridge.GetTagAffinity(defender, counterTag) * 0.04f);
                    if (UnityEngine.Random.value < chance)
                    {
                        try { DSStatusManager.SafeAddStatusEffect(attacker, "stunned", 2f); }
                        catch (System.Exception dsEx) { DSDebug.Verbose("[GeneCultivation] 异能反击附加状态异常: " + dsEx.Message); }
                    }
                }
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Verbose("[GeneCultivation] 异能防御效果异常: " + dsEx.Message);
            }
        }

        /// <summary>触发单个攻击标签</summary>
        private static void TryApplyAttackTag(Actor attacker, Actor target, PrototypeTag tag, ref float damage, float multiplier)
        {
            var def = PrototypeTags.Get(tag);
            if (def == null || !def.IsAttackTag) return;

            switch (tag)
            {
                case PrototypeTag.Burn:
                    TryAddStatus(attacker, target, "burning", tag, 8f, multiplier, 0.10f);
                    break;
                case PrototypeTag.Slow:
                    TryAddStatus(attacker, target, "frozen", tag, 5f, multiplier, 0.08f);
                    break;
                case PrototypeTag.Stun:
                    TryAddStatus(attacker, target, "stunned", tag, 2f, multiplier, 0.06f);
                    break;
                case PrototypeTag.Poison:
                    TryAddStatus(attacker, target, "poisoned", tag, 6f, multiplier, 0.08f);
                    break;
                case PrototypeTag.Lifesteal:
                    TryLifesteal(attacker, tag, damage);
                    break;
                case PrototypeTag.Crit:
                    TryCrit(attacker, tag, ref damage);
                    break;
                case PrototypeTag.Heal:
                    {
                        float chance = Mathf.Min(1f, def.BaseChance + GeneAffinityBridge.GetTagAffinity(attacker, tag) * 0.05f);
                        if (UnityEngine.Random.value < chance)
                        {
                            float healAmount = damage * def.Strength;
                            if (healAmount > 1f) TryHeal(attacker, healAmount);
                        }
                    }
                    break;
                case PrototypeTag.Chain:
                    TryChainSplash(attacker, target, damage, def.Strength);
                    break;
                // ---- v0.18.0 新攻击标签（机制 = 已有标签的变体/组合）----
                case PrototypeTag.Frostbite:
                case PrototypeTag.TimeSlow:
                    TryAddStatus(attacker, target, "frozen", tag, 6f, multiplier, 0.08f);
                    break;
                case PrototypeTag.Knockback:
                case PrototypeTag.Gravity:
                case PrototypeTag.Fear:
                    TryAddStatus(attacker, target, "stunned", tag, 2.5f, multiplier, 0.06f);
                    break;
                case PrototypeTag.Frost:
                    damage *= 1.05f;                        // 寒霜微伤：当次伤害 +5%
                    TryAddStatus(attacker, target, "frozen", tag, 5f, multiplier, 0.08f);
                    break;
                case PrototypeTag.Berserk:
                    damage *= 1f + def.Strength;            // 狂暴：当次伤害 +25%
                    break;
                case PrototypeTag.Drain:
                case PrototypeTag.Blood:
                    TryLifesteal(attacker, tag, damage);
                    break;
                case PrototypeTag.Soul:
                    {
                        float chance = Mathf.Min(def.MaxChance, def.BaseChance + GeneAffinityBridge.GetTagAffinity(attacker, tag) * 0.05f);
                        if (UnityEngine.Random.value < chance)
                        {
                            float healAmount = damage * def.Strength;
                            if (healAmount > 1f) TryHeal(attacker, healAmount);
                        }
                    }
                    break;
                case PrototypeTag.Curse:
                    TryAddStatus(attacker, target, "poisoned", tag, 6f, multiplier, 0.08f);
                    break;
                case PrototypeTag.Plasma:
                    TryAddStatus(attacker, target, "burning", tag, 10f, multiplier, 0.10f);
                    break;
                case PrototypeTag.Telekinesis:
                    TryCrit(attacker, tag, ref damage);
                    break;
                default:
                    break;
            }
        }

        /// <summary>吸血组：Lifesteal/Drain/Blood（吸血比例按标签强度，受上限约束）</summary>
        private static void TryLifesteal(Actor attacker, PrototypeTag tag, float damage)
        {
            var def = PrototypeTags.Get(tag);
            if (def == null) return;
            float chance = Mathf.Min(1f, def.BaseChance + GeneAffinityBridge.GetTagAffinity(attacker, tag) * 0.05f);
            if (UnityEngine.Random.value < chance)
            {
                float lifestealRatio = Mathf.Min(MAX_LIFESTEAL, def.Strength + GeneAffinityBridge.GetTagAffinity(attacker, tag) * 0.03f);
                float healAmount = damage * lifestealRatio;
                if (healAmount > 1f) TryHeal(attacker, healAmount);
            }
        }

        /// <summary>暴击组：Crit/Telekinesis（双倍伤害，概率受亲和加成）</summary>
        private static void TryCrit(Actor attacker, PrototypeTag tag, ref float damage)
        {
            var def = PrototypeTags.Get(tag);
            if (def == null) return;
            float critChance = Mathf.Min(MAX_CRIT_CHANCE, def.BaseChance + GeneAffinityBridge.GetTagAffinity(attacker, tag) * 0.05f);
            if (UnityEngine.Random.value < critChance)
            {
                damage *= 2f;
                DSDebug.Verbose("[GeneCultivation] " + attacker.getName() + " 异能暴击！伤害×2");
            }
        }

        /// <summary>附加原版状态（概率 = 基础 + 亲和加成，受 multiplier 与上限约束）</summary>
        private static void TryAddStatus(Actor attacker, Actor target, string statusId, PrototypeTag tag, float duration, float multiplier, float affinityPerPoint)
        {
            var def = PrototypeTags.Get(tag);
            if (def == null) return;
            float chance = Mathf.Min(def.MaxChance, (def.BaseChance + GeneAffinityBridge.GetTagAffinity(attacker, tag) * affinityPerPoint) * multiplier);
            if (UnityEngine.Random.value < chance)
            {
                try { DSStatusManager.SafeAddStatusEffect(target, statusId, duration); }
                catch (System.Exception dsEx) { DSDebug.Verbose("[GeneCultivation] 附加状态异常 " + statusId + ": " + dsEx.Message); }
            }
        }

        /// <summary>治疗（写入 ActorData.health，与现有吸血实现一致）</summary>
        private static void TryHeal(Actor actor, float amount)
        {
            try
            {
                var data = ActorDataAccessor.GetData(actor);
                if (data == null) return;
                float curHp = actor.getHealth();
                float maxHp = actor.getMaxHealth();
                if (maxHp <= 0f) return;
                float newHp = Mathf.Min(maxHp, curHp + amount);
                data.health = (int)newHp;
            }
            catch (System.Exception dsEx) { DSDebug.Verbose("[GeneCultivation] 异能治疗异常: " + dsEx.Message); }
        }

        /// <summary>
        /// Chain 溅射：对目标周围半径内敌方单位造成比例溅射伤害。
        /// 防递归：溅射伤害通过 getHit 会再次进入 AbilityStatusPatch → OnAttack，
        /// 故以攻击者 id 标记本轮溅射中，避免无限递归与标签重复触发。
        /// </summary>
        private static void TryChainSplash(Actor attacker, Actor target, float baseDamage, float ratio)
        {
            if (attacker == null || target == null) return;
            long attackerId = attacker.getID();
            if (_chainProcessing.Contains(attackerId)) return;   // 递归防护
            if (!World.world) return;

            _chainProcessing.Add(attackerId);
            try
            {
                float splashDamage = baseDamage * ratio;
                if (splashDamage < 1f) return;

                var units = World.world.units?.getSimpleList();
                if (units == null) return;

                Vector2 center = target.current_position;
                int hits = 0;
                foreach (var obj in units)
                {
                    if (hits >= CHAIN_MAX_TARGETS) break;
                    Actor other = obj as Actor;
                    if (other == null || !other.isAlive()) continue;
                    if (other == attacker || other == target) continue;
                    // 敌我判定（原版 API：Kingdom.isEnemy；保守策略——同王国绝不溅射）
                    bool hostile;
                    if (attacker.kingdom != null && other.kingdom != null)
                        hostile = attacker.kingdom.isEnemy(other.kingdom);
                    else
                        hostile = true;   // 野生/无王国单位视为敌对（溅射波及中立）
                    if (!hostile) continue;
                    float dist = Toolbox.DistVec2Float(center, other.current_position);
                    if (dist <= CHAIN_RADIUS)
                    {
                        try
                        {
                            other.getHit(splashDamage, false, AttackType.Other, attacker,
                                pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
                            hits++;
                        }
                        catch (System.Exception dsEx) { DSDebug.Verbose("[GeneCultivation] 溅射伤害异常: " + dsEx.Message); }
                    }
                }
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Verbose("[GeneCultivation] 溅射执行异常: " + dsEx.Message);
            }
            finally
            {
                _chainProcessing.Remove(attackerId);
            }
        }
    }
}
