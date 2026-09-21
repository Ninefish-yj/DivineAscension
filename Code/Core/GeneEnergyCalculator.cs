// ============================================================

using System;
using Code.Data;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 基因能量统一计算器
    /// 基因能量 = √(异能能量) × 境界系数 × (1 + 基因加成)
    /// 所有属性加成均基于基因能量动态计算，不依赖原版固定属性
    /// </summary>
    public static class GeneEnergyCalculator
    {
        // ============================================================
        //  境界系数（每阶的基因能量倍率）
        // ============================================================
        private static readonly float[] _tierMultipliers =
        {
            0f,      // 0阶（未觉醒）
            1f,      // 1阶 觉醒者
            1.5f,    // 2阶 共振者
            2f,      // 3阶 强化者
            3f,      // 4阶 突变者
            4f,      // 5阶 调控者
            6f,      // 6阶 场域者
            8f,      // 7阶 具象者
            12f,     // 8阶 干涉者
            16f,     // 9阶 解析者
            25f,     // 10阶 使徒级
            35f,     // 11阶 登神级
            50f,     // 12阶 真神级
            100f     // 13阶 超神级
        };

        // ============================================================
        //  属性加成系数（基因能量→属性的转换率）
        // ============================================================
        private const float DAMAGE_COEFF = 0.1f;      // 伤害加成系数
        private const float HEALTH_COEFF = 5.0f;      // 生命加成系数（从1.0提高到5.0，高境界血量更高）
        private const float ARMOR_COEFF = 0.05f;      // 防御加成系数
        private const float ATTACK_SPEED_COEFF = 0.001f; // 攻速加成系数
        private const float RANGE_COEFF = 0.0001f;    // 射程加成系数
        private const float SPEED_COEFF = 0.0001f;    // 速度加成系数
        private const float BREAKTHROUGH_COEFF = 0.02f; // 突破成功率加成系数（基因能量越高，突破成功率越高）
        private const float GENE_COLLAPSE_COEFF = 0.01f; // 基因崩溃率系数（基因能量越高，突破失败时基因崩溃率越高）
        private const float LIFE_REGEN_COEFF = 0.005f;    // 生命恢复加成系数（基因能量越高，生命恢复越快）
        private const float ELEMENT_UNLOCK_COEFF = 0.001f; // 基因解锁概率加成系数（基因能量越高，基因突变概率越高）

        /// <summary>获取单位的基因能量值（优先读取缓存，减轻性能负担）</summary>
        /// <param name="actor">单位</param>
        /// <returns>基因能量值</returns>
        public static float GetGeneEnergy(Actor actor)
        {
            if (actor == null || !CultivationData.IsAscended(actor)) return 0f;

            try
            {
                // 优先读取缓存（年度Tick时已计算，战斗时直接读取）
                float cached = CultivationData.GetGeneEnergyCache(actor);
                if (cached > 0f) return cached;

                // 缓存不存在时实时计算并存储
                return CalculateAndCacheGeneEnergy(actor);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[基因能量获取] 失败: " + e.Message);
                return 0f;
            }
        }

        /// <summary>计算并缓存基因能量（年度Tick时调用，减轻战斗时性能负担）</summary>
        public static float CalculateAndCacheGeneEnergy(Actor actor)
        {
            if (actor == null || !CultivationData.IsAscended(actor)) return 0f;

            try
            {
                float energy = CultivationData.GetEnergy(actor);
                int tier = CultivationData.GetRealmTier(actor);

                if (energy <= 0 || tier <= 0)
                {
                    CultivationData.SetGeneEnergyCache(actor, 0f);
                    return 0f;
                }

                // 基因能量 = √(异能能量) × 境界系数 × (1 + 基因加成) × 纪元乘数
                float sqrtEnergy = Mathf.Sqrt(energy);
                float tierMult = GetTierMultiplier(tier);
                float elementBonus = GetElementBonus(actor);
                float eraMultiplier = GetEraMultiplier(); // 纪元效果乘数（统一缓存，避免性能问题）

                float geneEnergy = sqrtEnergy * tierMult * (1f + elementBonus) * eraMultiplier;
                geneEnergy = Mathf.Max(0f, geneEnergy);

                // 存储到缓存
                CultivationData.SetGeneEnergyCache(actor, geneEnergy);

                return geneEnergy;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[基因能量计算] 计算失败: " + e.Message);
                return 0f;
            }
        }

        /// <summary>获取境界系数</summary>
        public static float GetTierMultiplier(int tier)
        {
            if (tier < 0 || tier >= _tierMultipliers.Length) return 0f;
            return _tierMultipliers[tier];
        }

        /// <summary>获取基因表达加成（已激活基因的表达层级影响基因能量）</summary>
        private static float GetElementBonus(Actor actor)
        {
            try
            {
                var elements = CultivationData.GetUnlockedElements(actor);
                if (elements == null || elements.Count == 0) return 0f;
                
                float totalBonus = 0f;
                foreach (var geneId in elements)
                {
                    // 获取基因表达层级（0=沉默，1=低表达，2=中表达，3=高表达，4=过表达，5=突变）
                    int exprLevel = CultivationData.GetGeneExpressionLevel(actor, geneId);
                    // 表达层级越高，基因能量加成越多
                    // 低表达=1%，中表达=2%，高表达=4%，过表达=6%，突变=10%
                    float[] levelBonus = { 0f, 0.01f, 0.02f, 0.04f, 0.06f, 0.10f };
                    if (exprLevel >= 0 && exprLevel < levelBonus.Length)
                    {
                        totalBonus += levelBonus[exprLevel];
                    }
                }
                
                // 神创基因加成：效果值直接加到基因能量加成里（用户自定义）
                if (CultivationData.IsDivineGeneUnlocked(actor))
                {
                    float divineValue = CultivationData.GetDivineGeneValue(actor);
                    totalBonus += divineValue / 100f; // 效果值是百分比，转成小数
                }
                
                // 上限+150%（神创基因额外加50%上限）
                return Mathf.Min(1.5f, totalBonus);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>获取纪元效果乘数（统一缓存，避免性能问题）</summary>
        /// <remarks>
        /// 真神纪元效果根据主宰的基因染色体动态变化：
        /// - 物质纪元：修炼+5%，战斗+10%，生命恢复+15%
        /// - 生命纪元：修炼+10%，生命恢复+25%
        /// - 能量纪元：能量获取+20%，战斗+15%
        /// - 其他纪元：各有不同加成
        /// 没有真神时返回1.0（无加成）
        /// </remarks>
        private static float GetEraMultiplier()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                if (eraEffect == null) return 1f;

                // 纪元效果作为GE的乘数（影响所有GE相关属性）
                // 取主要加成的平均值，避免单一属性过强
                float avgBonus = (eraEffect.CultivationBonus + eraEffect.CombatBonus + eraEffect.LifeRegenBonus) / 3f;
                return 1f + avgBonus;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>获取神创基因伤害加成（读取用户自定义数值）</summary>
        public static float GetDivineDamageBonus(Actor actor)
        {
            // 读用户在编辑界面保存的攻击加成数值，单位是%，比如填10就是+10%
            return Code.Data.CultivationData.GetDivineFloat(actor, "divine_atk", 0f) / 100f;
        }
        // ========== 神创基因自定义加成读取 ==========
        public static float GetDivineDefBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_def", 0f) / 100f;
        public static float GetDivineSpeedBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_spd", 0f) / 100f;
        public static float GetDivineHpBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_hp", 0f) / 100f;
        public static float GetDivineMaxEnBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_maxen", 0f) / 100f;
        public static float GetDivineEnRecBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_enrec", 0f) / 100f;
        public static float GetDivineCultBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_cult", 0f) / 100f;
        public static float GetDivineStabBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_stab", 0f) / 100f;
        public static float GetDivineCdBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_cd", 0f) / 100f;
        public static float GetDivineDmgRedBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_dmgred", 0f) / 100f;
        public static float GetDivineCritBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_crit", 0f) / 100f;
        public static float GetDivineLifestealBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_lifesteal", 0f) / 100f;
        public static float GetDivineHealBonus(Actor actor) => Code.Data.CultivationData.GetDivineFloat(actor, "divine_heal", 0f) / 100f;


        /// <summary>获取伤害加成（基于基因能量）
        /// 注意：神创基因伤害加成不混入此值（消费点 /30 会稀释），由战斗入口独立乘 GetDivineDamageBonus</summary>
        public static float GetDamageBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * DAMAGE_COEFF;
        }

        /// <summary>获取生命加成（基于基因能量）
        /// 注意：神创基因生命加成不混入此值（消费点 /50 会稀释），由治疗入口独立乘 GetDivineHpBonus</summary>
        public static float GetHealthBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * HEALTH_COEFF;
        }

        /// <summary>获取防御加成（基于基因能量）
        /// 注意：神创基因防御加成不混入此值（消费点 /100 会稀释），由战斗入口独立乘 GetDivineDefBonus</summary>
        public static float GetArmorBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * ARMOR_COEFF;
        }

        /// <summary>获取攻速加成（基于基因能量）
        /// 注意：神创基因攻速加成不混入此值（消费点 ×0.1 会稀释），由 AttributeApplier 独立乘 GetDivineSpeedBonus</summary>
        public static float GetAttackSpeedBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * ATTACK_SPEED_COEFF;
        }

        /// <summary>获取射程加成（基于基因能量）</summary>
        public static float GetRangeBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * RANGE_COEFF;
        }

        /// <summary>获取速度加成（基于基因能量）</summary>
        public static float GetSpeedBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            return gene * SPEED_COEFF;
        }

        /// <summary>获取突破成功率加成（基于基因能量）</summary>
        public static float GetBreakthroughBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            // 基因能量越高，突破成功率越高，上限+20%
            return Mathf.Min(0.20f, gene * BREAKTHROUGH_COEFF);
        }

        /// <summary>获取基因崩溃率（基于基因能量，突破失败时触发，崩溃即死亡）</summary>
        public static float GetGeneCollapseRate(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            // 基因能量越高，突破失败时基因崩溃率越高，上限30%（和GE加成的突破成功率上限一致）
            // 高境界单位基因能量高，突破失败时更容易发生基因崩溃
            // 基因崩溃即死亡：基因序列彻底崩溃，单位无法存活
            return Mathf.Min(0.30f, gene * GENE_COLLAPSE_COEFF);
        }

        /// <summary>获取生命恢复加成（基于基因能量，每秒恢复最大血量的百分比）</summary>
        public static float GetLifeRegenBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            // 基因能量越高，生命恢复越快，上限+50%
            return Mathf.Min(0.50f, gene * LIFE_REGEN_COEFF);
        }

        /// <summary>获取基因解锁概率加成（基于基因能量，基因突变概率提升）</summary>
        public static float GetElementUnlockBonus(Actor actor)
        {
            float gene = GetGeneEnergy(actor);
            // 基因能量越高，基因突变概率越高，上限+20%
            return Mathf.Min(0.20f, gene * ELEMENT_UNLOCK_COEFF);
        }

        /// <summary>获取纪元修炼加成（直接读取纪元效果，不经过GE公式）</summary>
        public static float GetEraCultivationBonus()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                return eraEffect != null ? eraEffect.CultivationBonus : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>获取纪元突破加成（直接读取纪元效果，不经过GE公式）</summary>
        public static float GetEraBreakthroughBonus()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                return eraEffect != null ? eraEffect.BreakthroughBonus : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>获取纪元战斗加成（直接读取纪元效果，不经过GE公式）</summary>
        public static float GetEraCombatBonus()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                return eraEffect != null ? eraEffect.CombatBonus : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>获取纪元生命恢复加成（直接读取纪元效果，不经过GE公式）</summary>
        public static float GetEraLifeRegenBonus()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                return eraEffect != null ? eraEffect.LifeRegenBonus : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>获取纪元基因解锁加成（直接读取纪元效果，不经过GE公式）</summary>
        public static float GetEraElementUnlockBonus()
        {
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                return eraEffect != null ? eraEffect.ElementUnlockBonus : 0f;
            }
            catch { return 0f; }
        }

    }

    /// <summary>基因能量详细信息（用于UI显示）</summary>
    public struct GeneEnergyInfo
    {
        public float geneEnergy;          // 基因能量值
        public float Energy;           // 异能能量
        public float SqrtEnergy;       // 能量平方根
        public int Tier;               // 境界
        public float TierMultiplier;   // 境界系数
        public float ElementBonus;     // 基因加成
        public float DamageBonus;      // 伤害加成
        public float HealthBonus;      // 生命加成
        public float ArmorBonus;       // 防御加成
        public float AttackSpeedBonus; // 攻速加成
    }
}
