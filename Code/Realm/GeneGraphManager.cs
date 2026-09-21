// ============================================================

using System;
using System.Collections.Generic;
using Code.UI;
using UnityEngine;
using Code.Data;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 基因节点解锁条件类型
    /// </summary>
    public enum GeneUnlockType
    {
        EnergyThreshold,        // 异能能量达到阈值
        ContinuousCultivation,  // 连续修炼年数
        CombatKills,            // 战斗击杀数
        CombatDefense,          // 战斗承受攻击数
        AbilityUses,            // 异能使用次数
        LowCorruption,          // 低失控指数持续年数
        AgeRequirement,         // 年龄要求
        DeepEnlightenment,      // 深度觉醒次数
        DomainMaintain,         // 场域维持天数
        Ritual,                 // 晋升仪式（特殊条件）
        Special,                // 其他特殊条件
        SourceUnknown,          // 未知（表达层级>6：本源未展开，无法主动获得）
    }

    /// <summary>
    /// 基因节点效果类型
    /// </summary>
    public enum GeneEffectType
    {
        AttackBonus,        // 攻击加成（百分比）
        DefenseBonus,       // 防御加成（百分比）
        SpeedBonus,         // 速度加成（百分比）
        HealthBonus,        // 生命加成（百分比）
        EnergyMaxBonus,     // 能量上限加成（百分比）
        EnergyRegenBonus,   // 能量恢复加成（百分比）
        CultivationBonus,   // 修炼效率加成（百分比）
        CorruptionReduction, // 失控指数减少（固定值）
        CooldownReduction,   // 技能冷却减少（百分比）
        DamageReduction,     // 伤害减免（百分比）
        CriticalChance,      // 暴击率（百分比）
        Lifesteal,           // 吸血（百分比）
        HealBonus,           // 治疗效果加成（百分比）
        SpecialAbility       // 特殊能力（解锁被动能力）
    }

    /// <summary>
    /// 基因节点类型
    /// </summary>
    public enum GeneNodeType
    {
        Core,      // 核心节点（必须解锁才能晋升）
        Main,      // 普通主干节点（非核心，但是主干）
        Branch     // 分支节点（可选，提供额外效果）
    }

    /// <summary>
    /// 基因节点定义
    /// </summary>
    [Serializable]
    public class GeneNode
    {
        public string Id;              // 节点ID
        public int Tier;               // 所属境界（1-13）
        public string NameZh;          // 中文名称
        public string NameEn;          // 英文名称
        public string DescZh;          // 中文描述
        public string DescEn;          // 英文描述
        public GeneUnlockType UnlockType;  // 解锁条件类型
        public float UnlockParam1;     // 解锁参数1
        public float UnlockParam2;     // 解锁参数2
        public string UnlockHintZh;    // 解锁提示（中文）
        public string UnlockHintEn;    // 解锁提示（英文）

        // 基因节点效果（解锁后永久生效）
        public GeneEffectType EffectType;  // 效果类型
        public float EffectValue;          // 效果值
        public string EffectDescZh;        // 效果描述（中文）
        public string EffectDescEn;        // 效果描述（英文）

        // 节点类型
        public GeneNodeType NodeType;      // 核心节点/分支节点
    }

    /// <summary>
    /// 基因图谱管理器
    /// </summary>
    public static class GeneGraphManager
    {
        // 所有基因节点定义
        private static readonly Dictionary<string, GeneNode> _allNodes = new Dictionary<string, GeneNode>();
        private static readonly Dictionary<int, List<GeneNode>> _nodesByTier = new Dictionary<int, List<GeneNode>>();
        private static bool _initialized = false;

        // ============================================================
        //  初始化
        // ============================================================

        /// <summary>
        /// 初始化基因图谱，定义13阶境界的所有基因节点
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _allNodes.Clear();
            _nodesByTier.Clear();

            // 
            //  48个基础基因基因图谱
            // 共56个基因（8条染色体×7层表达）
            //  每个基因自带解锁条件与效果配置（ElementDef）
            //  基因组合可以产生各种异能效果（ElementCombinationSystem）
            // 

            // 
            //  中心节点：基因觉醒（所有异能的起点）
            // 
            AddNode(MakeNode("gene_center_awakening", 0, UILocalization.Get("graph_gene_awakening_title"), "Gene Awakening",
                UILocalization.Get("graph_gene_awakening_desc"), "The dormant ability gene begins to awaken, the origin of all abilities",
                GeneUnlockType.EnergyThreshold, 50f, GeneEffectType.EnergyMaxBonus, 5f, GeneNodeType.Core));

            // 
            //  48个基因节点：遍历ElementDef，为每个基因创建节点
            //  解锁条件/效果直接取自元素自身配置
            // 
            var allElements = ElementDef.AllElements;
            foreach (var elem in allElements)
            {
                string nodeId = "gene_elem_" + elem.Id.Replace("elem_", "");
                string descZh = elem.DescZh + UILocalization.Get("graph_element_unlock_desc");
                string descEn = elem.DescEn + " Can combine with other elements to produce abilities.";
                
                AddNode(MakeNode(nodeId, 0, elem.NameZh, elem.NameEn,
                    descZh, descEn,
                    elem.UnlockType, elem.UnlockParam, elem.EffectType, elem.EffectValue, elem.NodeType,
                    elem.UnlockHintZh, elem.UnlockHintEn));
            }

            DSDebug.Verbose("基因图谱初始化完成：1中心+56基因=57个节点");
        }

private static void AddNode(GeneNode node)
        {
            _allNodes[node.Id] = node;
            if (!_nodesByTier.ContainsKey(node.Tier))
                _nodesByTier[node.Tier] = new List<GeneNode>();
            _nodesByTier[node.Tier].Add(node);
        }

        /// <summary>
        /// 简化节点创建（带unlockParam2）
        /// </summary>
        private static GeneNode MakeNode(string id, int tier, string nameZh, string nameEn,
            string descZh, string descEn,
            GeneUnlockType unlockType, float unlockParam1, float unlockParam2,
            GeneEffectType effectType, float effectValue, GeneNodeType nodeType,
            string customUnlockHintZh = null, string customUnlockHintEn = null)
        {
            string effectDescZh = GetEffectDesc(effectType, effectValue, true);
            string effectDescEn = GetEffectDesc(effectType, effectValue, false);
            string unlockHintZh = customUnlockHintZh ?? GetUnlockHint(unlockType, unlockParam1, unlockParam2, true);
            string unlockHintEn = customUnlockHintEn ?? GetUnlockHint(unlockType, unlockParam1, unlockParam2, false);

            return new GeneNode {
                Id = id, Tier = tier,
                NameZh = nameZh, NameEn = nameEn,
                DescZh = descZh, DescEn = descEn,
                UnlockType = unlockType,
                UnlockParam1 = unlockParam1, UnlockParam2 = unlockParam2,
                UnlockHintZh = unlockHintZh, UnlockHintEn = unlockHintEn,
                EffectType = effectType, EffectValue = effectValue,
                EffectDescZh = effectDescZh, EffectDescEn = effectDescEn,
                NodeType = nodeType
            };
        }

        /// <summary>
        /// 简化节点创建（不带unlockParam2）
        /// </summary>
        private static GeneNode MakeNode(string id, int tier, string nameZh, string nameEn,
            string descZh, string descEn,
            GeneUnlockType unlockType, float unlockParam1,
            GeneEffectType effectType, float effectValue, GeneNodeType nodeType,
            string customUnlockHintZh = null, string customUnlockHintEn = null)
        {
            return MakeNode(id, tier, nameZh, nameEn, descZh, descEn,
                unlockType, unlockParam1, 0f, effectType, effectValue, nodeType,
                customUnlockHintZh, customUnlockHintEn);
        }

        /// <summary>获取效果描述</summary>
        private static string GetEffectDesc(GeneEffectType type, float value, bool zh)
        {
            switch (type)
            {
                case GeneEffectType.AttackBonus: return zh ? string.Format(UILocalization.Get("graph_attr_attack"), value) : $"Attack +{value}%";
                case GeneEffectType.DefenseBonus: return zh ? string.Format(UILocalization.Get("graph_attr_defense"), value) : $"Defense +{value}%";
                case GeneEffectType.SpeedBonus: return zh ? string.Format(UILocalization.Get("graph_attr_speed"), value) : $"Speed +{value}%";
                case GeneEffectType.HealthBonus: return zh ? string.Format(UILocalization.Get("graph_attr_health"), value) : $"Health +{value}%";
                case GeneEffectType.EnergyMaxBonus: return zh ? string.Format(UILocalization.Get("graph_attr_energy_max"), value) : $"Energy Max +{value}%";
                case GeneEffectType.EnergyRegenBonus: return zh ? string.Format(UILocalization.Get("graph_attr_energy_regen"), value) : $"Energy Regen +{value}%";
                case GeneEffectType.CultivationBonus: return zh ? string.Format(UILocalization.Get("graph_attr_cultivation"), value) : $"Cultivation +{value}%";
                case GeneEffectType.CorruptionReduction: return zh ? string.Format(UILocalization.Get("graph_attr_turbulence"), value) : $"Corruption -{value}/yr";
                case GeneEffectType.CooldownReduction: return zh ? string.Format(UILocalization.Get("graph_attr_cooldown"), value) : $"Cooldown -{value}%";
                case GeneEffectType.DamageReduction: return zh ? string.Format(UILocalization.Get("graph_attr_damage_reduction"), value) : $"Dmg Reduction +{value}%";
                case GeneEffectType.CriticalChance: return zh ? string.Format(UILocalization.Get("graph_attr_crit"), value) : $"Crit +{value}%";
                case GeneEffectType.Lifesteal: return zh ? string.Format(UILocalization.Get("graph_attr_lifesteal"), value) : $"Lifesteal +{value}%";
                case GeneEffectType.HealBonus: return zh ? string.Format(UILocalization.Get("graph_attr_healing"), value) : $"Heal +{value}%";
                default: return zh ? UILocalization.Get("graph_special_ability") : "Special Ability";
            }
        }

        /// <summary>获取解锁提示（基因系统版）</summary>
        private static string GetUnlockHint(GeneUnlockType type, float p1, float p2, bool zh)
        {
            // 基因节点：根据解锁类型返回更符合基因系统的描述
            switch (type)
            {
                case GeneUnlockType.CombatDefense: return zh ? UILocalization.Get("graph_unlock_defense") : "Unlock through defense combat";
                case GeneUnlockType.CombatKills: return zh ? UILocalization.Get("graph_unlock_kill") : "Unlock through combat kills";
                case GeneUnlockType.ContinuousCultivation: return zh ? UILocalization.Get("graph_unlock_cultivation") : "Unlock through continuous cultivation";
                case GeneUnlockType.AbilityUses: return zh ? UILocalization.Get("graph_unlock_ability") : "Unlock through ability usage";
                case GeneUnlockType.LowCorruption: return zh ? UILocalization.Get("graph_unlock_stability") : "Unlock through low corruption state";
                case GeneUnlockType.DeepEnlightenment: return zh ? UILocalization.Get("graph_unlock_deep_awakening") : "Unlock through deep enlightenment";
                case GeneUnlockType.Ritual: return zh ? UILocalization.Get("graph_unlock_ritual") : "Unlock through ascension ritual";
                case GeneUnlockType.EnergyThreshold: return zh ? UILocalization.Get("graph_unlock_energy") : "Unlock through energy accumulation";
                default: return zh ? UILocalization.Get("graph_unlock_cultivation") : "Unlock through cultivation and combat";
            }
        }

        /// <summary>
        /// 获取指定境界的所有基因节点
        /// </summary>
        public static List<GeneNode> GetNodesByTier(int tier)
        {
            if (!_initialized) Init();
            if (_nodesByTier.ContainsKey(tier))
                return _nodesByTier[tier];
            return new List<GeneNode>();
        }

        /// <summary>
        /// 获取指定ID的基因节点
        /// </summary>
        public static GeneNode GetNode(string id)
        {
            if (!_initialized) Init();
            if (_allNodes.ContainsKey(id))
                return _allNodes[id];
            return null;
        }

        /// <summary>
        /// 基因节点效果汇总
        /// </summary>
        public class GeneEffectSummary
        {
            public float AttackBonus = 0f;
            public float DefenseBonus = 0f;
            public float SpeedBonus = 0f;
            public float HealthBonus = 0f;
            public float EnergyMaxBonus = 0f;
            public float EnergyRegenBonus = 0f;
            public float CultivationBonus = 0f;
            public float CorruptionReduction = 0f;
            public float CooldownReduction = 0f;
            public float DamageReduction = 0f;
            public float CriticalChance = 0f;
            public float Lifesteal = 0f;
            public float HealBonus = 0f;
            public int UnlockedNodeCount = 0;
        }

        /// <summary>
        /// 计算单位所有已解锁基因节点的效果汇总
        /// </summary>
        public static GeneEffectSummary CalculateEffects(Actor actor)
        {
            var summary = new GeneEffectSummary();
            if (actor == null) return summary;

            var unlockedGenes = CultivationData.GetUnlockedGenes(actor);
            if (unlockedGenes == null) return summary;

            foreach (var geneId in unlockedGenes)
            {
                var node = GetNode(geneId);
                if (node == null) continue;

                summary.UnlockedNodeCount++;

                switch (node.EffectType)
                {
                    case GeneEffectType.AttackBonus:
                        summary.AttackBonus += node.EffectValue;
                        break;
                    case GeneEffectType.DefenseBonus:
                        summary.DefenseBonus += node.EffectValue;
                        break;
                    case GeneEffectType.SpeedBonus:
                        summary.SpeedBonus += node.EffectValue;
                        break;
                    case GeneEffectType.HealthBonus:
                        summary.HealthBonus += node.EffectValue;
                        break;
                    case GeneEffectType.EnergyMaxBonus:
                        summary.EnergyMaxBonus += node.EffectValue;
                        break;
                    case GeneEffectType.EnergyRegenBonus:
                        summary.EnergyRegenBonus += node.EffectValue;
                        break;
                    case GeneEffectType.CultivationBonus:
                        summary.CultivationBonus += node.EffectValue;
                        break;
                    case GeneEffectType.CorruptionReduction:
                        summary.CorruptionReduction += node.EffectValue;
                        break;
                    case GeneEffectType.CooldownReduction:
                        summary.CooldownReduction += node.EffectValue;
                        break;
                    case GeneEffectType.DamageReduction:
                        summary.DamageReduction += node.EffectValue;
                        break;
                    case GeneEffectType.CriticalChance:
                        summary.CriticalChance += node.EffectValue;
                        break;
                    case GeneEffectType.Lifesteal:
                        summary.Lifesteal += node.EffectValue;
                        break;
                    case GeneEffectType.HealBonus:
                        summary.HealBonus += node.EffectValue;
                        break;
                }
            }

            return summary;
        }

        /// <summary>
        /// 检查晋升条件：基于基因解锁数量和基因组合数量
        /// 修炼的一切起始就是基因觉醒和基因
        /// 低境界需要少量基因，高境界需要多个基因组合
        /// </summary>
        public static bool AreAllCoreNodesUnlocked(Actor actor, int tier)
        {
            // 方案B：完全移除基因数量限制，突破条件由深度觉醒/掌控度/能量/失控指数控制
            // 成神条件在TraitManager.CanAscendToGod中单独判断
            // 基因是同级的，不靠数量堆砌，靠对超越的深度理解
            return true;
        }
        
        /// <summary>
        /// 获取晋升所需的基因数量（用于UI显示）
        /// </summary>
        public static int GetRequiredElementsForTier(int tier)
        {
            // 方案B：不再限制基因数量，返回0
            return 0;
        }
        
        /// <summary>
        /// 获取晋升所需的组合数量（用于UI显示）
        /// </summary>
        public static int GetRequiredCombosForTier(int tier)
        {
            // 方案B：不再限制基因组合数量，返回0
            return 0;
        }

        /// <summary>
        /// 获取所有基因节点
        /// </summary>
        public static Dictionary<string, GeneNode> GetAllNodes()
        {
            if (!_initialized) Init();
            return _allNodes;
        }

    }
}




