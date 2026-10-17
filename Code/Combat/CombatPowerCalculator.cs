// ============================================================
// 战力计算器（v0.18.82，参考西幻模组 CombatPowerCalculator 的开方压缩设计）
//
// 战力 = √(总输出 × 有效生命 / SCALE)
//   总输出 = 单次伤害 × 攻速归一化 × 战斗 10 秒
//   有效生命 = 生命 × (1 + 护甲/100) + 回血 × 10
//
// 开方压缩防膨胀：属性随境界近似平方级膨胀，开方后战力还原为真实倍数差距，
// 高阶不会出现千万/亿级夸张数字。
// SCALE 校准：原版普通人类战士（300血/30伤/70攻速/10甲）战力 ≈ 100。
// 本计算器为纯业务层（不依赖 UI），采用 stats 快照口径，动态境界压制不进入基础战力。
// ============================================================

using System;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>战力计算器：开方压缩的基础战力（显示口径）。</summary>
    public static class CombatPowerCalculator
    {
        /// <summary>攻速上限语义：每秒攻击次数</summary>
        private const float ATTACK_SPEED = 10f;

        /// <summary>模拟战斗时长（秒）</summary>
        private const float BATTLE_TIME = 10f;

        /// <summary>总攻击次数 = 攻速 × 时长</summary>
        private const float TOTAL_ATTACKS = ATTACK_SPEED * BATTLE_TIME;

        /// <summary>开方压缩校准常数：普通人类战士 raw≈6.93e5 → √(raw/SCALE)≈100</summary>
        private const double SCALE = 70.0;

        /// <summary>
        /// 计算单位战力（Actor 实例，纳入基因能量 GE 加成与神创基因加成）。
        /// 输出 = (伤害 + GE伤害) × (1+神创伤害) × 攻速(含 GE攻速) × 总攻击次数
        /// 有效生命 = (生命 + GE生命) × (1+神创生命) × 护甲乘区(含 GE护甲+神创防御) + 回血×时长×(1+神创治疗)
        /// </summary>
        public static float Calculate(Actor actor)
        {
            if (actor == null || actor.stats == null) return 0f;

            try
            {
                float baseDamage = GetStat(actor, "damage");
                float baseHealth = actor.getMaxHealth();
                float baseAttackSpeed = GetStat(actor, "attack_speed");
                float baseArmor = GetStat(actor, "armor");
                float baseHealing = GetStat(actor, "healing");

                // GE 基因能量加成（GeneEnergyCalculator）
                float geDamage = Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
                float geHealth = Code.Core.GeneEnergyCalculator.GetHealthBonus(actor);
                float geArmor = Code.Core.GeneEnergyCalculator.GetArmorBonus(actor);
                float geSpeed = Code.Core.GeneEnergyCalculator.GetAttackSpeedBonus(actor);

                // 神创基因加成（独立乘区，不混入 GE 稀释）
                float divineDmg = Code.Core.GeneEnergyCalculator.GetDivineDamageBonus(actor);
                float divineHp = Code.Core.GeneEnergyCalculator.GetDivineHpBonus(actor);
                float divineDef = Code.Core.GeneEnergyCalculator.GetDivineDefBonus(actor);
                float divineHeal = Code.Core.GeneEnergyCalculator.GetDivineHealBonus(actor);

                float damage = (baseDamage + geDamage) * (1f + divineDmg);
                float health = (baseHealth + geHealth) * (1f + divineHp);
                float armor = baseArmor + geArmor + divineDef * 100f;
                float healing = baseHealing * (1f + divineHeal);
                float attackSpeed = baseAttackSpeed + geSpeed;

                return CalculateCore(health, damage, attackSpeed, armor, healing);
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] 战力计算异常: " + ex.Message);
                return 0f;
            }
        }

        /// <summary>
        /// 计算单位战力（stats 快照，供存档/无实例场景复用；不含 GE——快照无 GE 数据）。
        /// </summary>
        public static float CalculateFromSnapshot(float maxHealth, float damage, float attackSpeed, float armor, float healing)
        {
            return CalculateCore(maxHealth, damage, attackSpeed, armor, healing);
        }

        /// <summary>核心计算（纯函数，开方压缩防膨胀）</summary>
        private static float CalculateCore(float maxHealth, float damage, float attackSpeed, float armor, float healing)
        {
            if (maxHealth <= 0f || damage <= 0f) return 0f;

            // 攻速归一化：原版 attack_speed 为相对值（人类 60~70），/100 后乘 10 次/秒
            float speedNorm = Mathf.Clamp(attackSpeed / 100f, 0f, 1.5f);

            // 总输出 = 单次伤害 × 总攻击次数
            float output = damage * (speedNorm * TOTAL_ATTACKS);

            // 有效生命 = 生命 × 护甲乘区 + 回血 × 战斗时长
            float armorMul = 1f + Mathf.Max(0f, armor) / 100f;
            float ehp = maxHealth * armorMul + Mathf.Max(0f, healing) * BATTLE_TIME;

            double raw = (double)output * ehp;
            if (raw <= 0d) return 0f;
            return (float)Math.Sqrt(raw / SCALE);
        }

        /// <summary>安全取 stats（不存在返回 0）</summary>
        private static float GetStat(Actor actor, string id)
        {
            try
            {
                if (actor.stats.hasStat(id)) return actor.stats[id];
            }
            catch { }
            return 0f;
        }
    }
}
