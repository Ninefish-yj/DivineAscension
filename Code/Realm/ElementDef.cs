using System.Collections.Generic;
using Code.Realm;

namespace Code
{
	/// <summary>
	/// 基因序列定义 - 《基因序列图谱》
	/// 完全重构：基础生物学基因系统
	/// 
	/// 8条染色体（行）= 基因功能分类：
	///   0力量染色体  1敏捷染色体  2体质染色体  3智力染色体
	///   4感知染色体  5意志染色体  6异能染色体  7潜能染色体
	/// 
	/// 7表达层级（列）= 基因表达深度：
	///   第1层（基础表达） 第2层（进阶表达） 第3层（掌控表达）
	///   第4层（超常表达） 第5层（规则表达） 第6层（至高表达）
	///   第7层（本源表达·未知）
	/// 
	/// 基因序数 = (层级-1)×8 + 染色体 + 1
	/// 
	/// </summary>
	public static class ElementDef
	{
		/// <summary>
		/// 基因序列信息
		/// </summary>
		public class ElementInfo
		{
			public string Id;          // 基因ID（保持elem_前缀以兼容存档）
			public string NameZh;      // 中文名
			public string NameEn;      // 英文名
			public string Symbol;      // 符号（单字）
			public string DescZh;      // 中文描述
			public string DescEn;      // 英文描述
			public GeneUnlockType UnlockType;   // 基因突变条件类型
			public float UnlockParam;           // 突变参数
			public string UnlockHintZh;         // 突变提示（中文）
			public string UnlockHintEn;         // 突变提示（英文）
			public GeneEffectType EffectType;   // 基因表达效果类型
			public float EffectValue;           // 效果值
			public GeneNodeType NodeType = GeneNodeType.Main; // 基因图谱节点类型
			public int Profile;          // 内部计算分组（0-7）
			public int Period;           // 表达层级（列，1起）
			public int Group;            // 染色体（行，0-7）
			public bool UseAutoStats = true; // true=按层级×染色体公式自动生成数值
		}

		// 56个基础生物学基因（48已知 + 8未知）
		// 染色体序：0力量 1敏捷 2体质 3智力 4感知 5意志 6异能 7潜能
		private static readonly ElementInfo[] _elements = new ElementInfo[]
		{
			// === 第1层：基础表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_muscle_growth", NameZh = "肌肉生长", NameEn = "Muscle Growth", Symbol = "肌", DescZh = "控制肌肉纤维生长，提升基础力量", DescEn = "Controls muscle fiber growth, boosting base strength", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 10f, UnlockHintZh = "在力量训练中激活肌肉生长基因", UnlockHintEn = "Activate muscle growth genes through strength training", EffectType = GeneEffectType.AttackBonus, EffectValue = 2.5f, Profile = 1, Period = 1, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_nerve_conduction", NameZh = "神经传导", NameEn = "Nerve Conduction", Symbol = "神", DescZh = "加速神经信号传导，提升反应速度", DescEn = "Accelerates nerve signal conduction, boosting reaction speed", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 12f, UnlockHintZh = "在高速反应中激活神经传导基因", UnlockHintEn = "Activate nerve conduction genes in high-speed reactions", EffectType = GeneEffectType.SpeedBonus, EffectValue = 3f, Profile = 2, Period = 1, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_cell_division", NameZh = "细胞分裂", NameEn = "Cell Division", Symbol = "胞", DescZh = "控制细胞分裂速率，提升生命恢复", DescEn = "Controls cell division rate, boosting health recovery", UnlockType = GeneUnlockType.EnergyThreshold, UnlockParam = 8f, UnlockHintZh = "觉醒的瞬间，细胞分裂基因被激活", UnlockHintEn = "At the moment of awakening, cell division genes are activated", EffectType = GeneEffectType.HealthBonus, EffectValue = 2f, Profile = 0, Period = 1, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_synaptic_connection", NameZh = "突触连接", NameEn = "Synaptic Connection", Symbol = "突", DescZh = "增强神经元突触连接，提升思维速度", DescEn = "Enhances neuronal synaptic connections, boosting thinking speed", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 18f, UnlockHintZh = "在持续思考中激活突触连接基因", UnlockHintEn = "Activate synaptic connection genes through continuous thinking", EffectType = GeneEffectType.CultivationBonus, EffectValue = 4.5f, Profile = 3, Period = 1, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_visual_receptor", NameZh = "视觉受体", NameEn = "Visual Receptor", Symbol = "视", DescZh = "增强视觉受体灵敏度，提升视觉感知", DescEn = "Enhances visual receptor sensitivity, boosting visual perception", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 16f, UnlockHintZh = "在视觉训练中激活视觉受体基因", UnlockHintEn = "Activate visual receptor genes through visual training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 4f, Profile = 4, Period = 1, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_mental_focus", NameZh = "精神集中", NameEn = "Mental Focus", Symbol = "集", DescZh = "增强精神集中能力，提升专注度", DescEn = "Enhances mental focus ability, boosting concentration", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 14f, UnlockHintZh = "在专注训练中激活精神集中基因", UnlockHintEn = "Activate mental focus genes through concentration training", EffectType = GeneEffectType.DamageReduction, EffectValue = 3.5f, Profile = 5, Period = 1, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_ability_perception", NameZh = "异能感知", NameEn = "Ability Perception", Symbol = "异", DescZh = "感知异能能量流动，提升异能操控", DescEn = "Senses ability energy flow, improving ability control", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 20f, UnlockHintZh = "在异能感知中激活异能感知基因", UnlockHintEn = "Activate ability perception genes through ability sensing", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 5f, Profile = 6, Period = 1, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_potential_seal", NameZh = "潜能封印", NameEn = "Potential Seal", Symbol = "潜", DescZh = "控制潜能封印状态，可逐步释放潜能", DescEn = "Controls potential seal state, allowing gradual potential release", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 22f, UnlockHintZh = "在极限压力下激活潜能封印基因", UnlockHintEn = "Activate potential seal genes under extreme pressure", EffectType = GeneEffectType.SpecialAbility, EffectValue = 5.5f, Profile = 7, Period = 1, Group = 7 },

			// === 第2层：进阶表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_muscle_fiber", NameZh = "肌肉纤维", NameEn = "Muscle Fiber", Symbol = "纤", DescZh = "强化肌肉纤维密度，提升爆发力", DescEn = "Strengthens muscle fiber density, boosting explosive power", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 16f, UnlockHintZh = "在爆发力训练中激活肌肉纤维基因", UnlockHintEn = "Activate muscle fiber genes through explosive power training", EffectType = GeneEffectType.AttackBonus, EffectValue = 3.5f, Profile = 1, Period = 2, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_reflex_arc", NameZh = "反射弧", NameEn = "Reflex Arc", Symbol = "反", DescZh = "缩短反射弧路径，提升本能反应", DescEn = "Shortens reflex arc pathway, boosting instinctive reaction", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 18f, UnlockHintZh = "在反应训练中激活反射弧基因", UnlockHintEn = "Activate reflex arc genes through reaction training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 4f, Profile = 2, Period = 2, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_immune_response", NameZh = "免疫应答", NameEn = "Immune Response", Symbol = "免", DescZh = "增强免疫应答能力，提升抗病能力", DescEn = "Enhances immune response ability, boosting disease resistance", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 14f, UnlockHintZh = "在持续修炼中激活免疫应答基因", UnlockHintEn = "Activate immune response genes through continuous cultivation", EffectType = GeneEffectType.HealthBonus, EffectValue = 3f, Profile = 0, Period = 2, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_brain_region_development", NameZh = "脑区发育", NameEn = "Brain Region Development", Symbol = "脑", DescZh = "促进脑区发育，提升整体智力", DescEn = "Promotes brain region development, boosting overall intelligence", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 24f, UnlockHintZh = "在智力训练中激活脑区发育基因", UnlockHintEn = "Activate brain region development genes through intelligence training", EffectType = GeneEffectType.CultivationBonus, EffectValue = 5.5f, Profile = 3, Period = 2, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_auditory_receptor", NameZh = "听觉受体", NameEn = "Auditory Receptor", Symbol = "听", DescZh = "增强听觉受体灵敏度，提升听觉感知", DescEn = "Enhances auditory receptor sensitivity, boosting auditory perception", UnlockType = GeneUnlockType.AgeRequirement, UnlockParam = 22f, UnlockHintZh = "在听觉训练中激活听觉受体基因", UnlockHintEn = "Activate auditory receptor genes through auditory training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 5f, Profile = 4, Period = 2, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_emotion_control", NameZh = "情绪控制", NameEn = "Emotion Control", Symbol = "情", DescZh = "增强情绪控制能力，提升精神稳定", DescEn = "Enhances emotion control ability, boosting mental stability", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 20f, UnlockHintZh = "在情绪管理中激活情绪控制基因", UnlockHintEn = "Activate emotion control genes through emotion management", EffectType = GeneEffectType.DamageReduction, EffectValue = 4.5f, Profile = 5, Period = 2, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_energy_absorption", NameZh = "能量吸收", NameEn = "Energy Absorption", Symbol = "吸", DescZh = "增强能量吸收能力，提升能量获取", DescEn = "Enhances energy absorption ability, boosting energy acquisition", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 26f, UnlockHintZh = "在能量吸收中激活能量吸收基因", UnlockHintEn = "Activate energy absorption genes through energy absorption", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 6f, Profile = 6, Period = 2, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_potential_excitation", NameZh = "潜能激发", NameEn = "Potential Excitation", Symbol = "激", DescZh = "激发沉睡潜能，提升整体素质", DescEn = "Excites dormant potential, boosting overall qualities", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 28f, UnlockHintZh = "在极限状态下激活潜能激发基因", UnlockHintEn = "Activate potential excitation genes in extreme states", EffectType = GeneEffectType.SpecialAbility, EffectValue = 6.5f, Profile = 7, Period = 2, Group = 7 },

			// === 第3层：掌控表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_muscle_density", NameZh = "肌肉密度", NameEn = "Muscle Density", Symbol = "密", DescZh = "提升肌肉密度，增强力量输出", DescEn = "Increases muscle density, enhancing strength output", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 22f, UnlockHintZh = "在力量训练中激活肌肉密度基因", UnlockHintEn = "Activate muscle density genes through strength training", EffectType = GeneEffectType.AttackBonus, EffectValue = 4.5f, Profile = 1, Period = 3, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_muscle_contraction", NameZh = "肌肉收缩", NameEn = "Muscle Contraction", Symbol = "缩", DescZh = "加速肌肉收缩速率，提升移动速度", DescEn = "Accelerates muscle contraction rate, boosting movement speed", UnlockType = GeneUnlockType.AbilityUses, UnlockParam = 24f, UnlockHintZh = "在速度训练中激活肌肉收缩基因", UnlockHintEn = "Activate muscle contraction genes through speed training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 5f, Profile = 2, Period = 3, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_metabolic_rate", NameZh = "代谢速率", NameEn = "Metabolic Rate", Symbol = "代", DescZh = "提升代谢速率，加速能量转换", DescEn = "Increases metabolic rate, accelerating energy conversion", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 20f, UnlockHintZh = "在代谢训练中激活代谢速率基因", UnlockHintEn = "Activate metabolic rate genes through metabolic training", EffectType = GeneEffectType.HealthBonus, EffectValue = 4f, Profile = 0, Period = 3, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_memory_formation", NameZh = "记忆形成", NameEn = "Memory Formation", Symbol = "忆", DescZh = "增强记忆形成能力，提升学习效率", DescEn = "Enhances memory formation ability, boosting learning efficiency", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 30f, UnlockHintZh = "在持续学习中激活记忆形成基因", UnlockHintEn = "Activate memory formation genes through continuous learning", EffectType = GeneEffectType.CultivationBonus, EffectValue = 6.5f, Profile = 3, Period = 3, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_olfactory_receptor", NameZh = "嗅觉受体", NameEn = "Olfactory Receptor", Symbol = "嗅", DescZh = "增强嗅觉受体灵敏度，提升嗅觉感知", DescEn = "Enhances olfactory receptor sensitivity, boosting olfactory perception", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 28f, UnlockHintZh = "在嗅觉训练中激活嗅觉受体基因", UnlockHintEn = "Activate olfactory receptor genes through olfactory training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 6f, Profile = 4, Period = 3, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_stress_tolerance", NameZh = "压力耐受", NameEn = "Stress Tolerance", Symbol = "压", DescZh = "增强压力耐受能力，提升抗压能力", DescEn = "Enhances stress tolerance ability, boosting pressure resistance", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 26f, UnlockHintZh = "在压力训练中激活压力耐受基因", UnlockHintEn = "Activate stress tolerance genes through stress training", EffectType = GeneEffectType.DamageReduction, EffectValue = 5.5f, Profile = 5, Period = 3, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_energy_conversion", NameZh = "能量转化", NameEn = "Energy Conversion", Symbol = "转", DescZh = "增强能量转化能力，提升能量利用效率", DescEn = "Enhances energy conversion ability, boosting energy utilization efficiency", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 32f, UnlockHintZh = "在能量转化中激活能量转化基因", UnlockHintEn = "Activate energy conversion genes through energy conversion", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 7f, Profile = 6, Period = 3, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_limit_break", NameZh = "极限突破", NameEn = "Limit Break", Symbol = "极", DescZh = "突破身体极限，短暂提升全部能力", DescEn = "Breaks body limits, temporarily boosting all abilities", UnlockType = GeneUnlockType.ContinuousCultivation, UnlockParam = 34f, UnlockHintZh = "在极限突破中激活极限突破基因", UnlockHintEn = "Activate limit break genes through limit breaking", EffectType = GeneEffectType.SpecialAbility, EffectValue = 7.5f, Profile = 7, Period = 3, Group = 7 },

			// === 第4层：超常表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_skeletal_support", NameZh = "骨骼支撑", NameEn = "Skeletal Support", Symbol = "骨", DescZh = "强化骨骼支撑结构，提升身体硬度", DescEn = "Strengthens skeletal support structure, boosting body hardness", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 28f, UnlockHintZh = "在骨骼训练中激活骨骼支撑基因", UnlockHintEn = "Activate skeletal support genes through skeletal training", EffectType = GeneEffectType.DefenseBonus, EffectValue = 5.5f, Profile = 1, Period = 4, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_sense_of_balance", NameZh = "平衡感", NameEn = "Sense of Balance", Symbol = "平", DescZh = "增强平衡感，提升身体协调性", DescEn = "Enhances sense of balance, boosting body coordination", UnlockType = GeneUnlockType.DomainMaintain, UnlockParam = 30f, UnlockHintZh = "在平衡训练中激活平衡感基因", UnlockHintEn = "Activate balance sense genes through balance training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 6f, Profile = 2, Period = 4, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_bone_density", NameZh = "骨骼密度", NameEn = "Bone Density", Symbol = "密", DescZh = "提升骨骼密度，增强骨骼硬度", DescEn = "Increases bone density, enhancing bone hardness", UnlockType = GeneUnlockType.AgeRequirement, UnlockParam = 26f, UnlockHintZh = "在岁月淬炼中激活骨骼密度基因", UnlockHintEn = "Activate bone density genes through years of tempering", EffectType = GeneEffectType.HealthBonus, EffectValue = 5f, Profile = 0, Period = 4, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_information_processing", NameZh = "信息处理", NameEn = "Information Processing", Symbol = "信", DescZh = "增强信息处理能力，提升分析速度", DescEn = "Enhances information processing ability, boosting analysis speed", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 32f, UnlockHintZh = "在信息处理中激活信息处理基因", UnlockHintEn = "Activate information processing genes through information processing", EffectType = GeneEffectType.CultivationBonus, EffectValue = 6.5f, Profile = 3, Period = 4, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_tactile_receptor", NameZh = "触觉受体", NameEn = "Tactile Receptor", Symbol = "触", DescZh = "增强触觉受体灵敏度，提升触觉感知", DescEn = "Enhances tactile receptor sensitivity, boosting tactile perception", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 34f, UnlockHintZh = "在触觉训练中激活触觉受体基因", UnlockHintEn = "Activate tactile receptor genes through tactile training", EffectType = GeneEffectType.SpeedBonus, EffectValue = 7f, Profile = 4, Period = 4, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_pain_tolerance", NameZh = "疼痛耐受", NameEn = "Pain Tolerance", Symbol = "痛", DescZh = "增强疼痛耐受能力，提升战斗持久力", DescEn = "Enhances pain tolerance ability, boosting combat endurance", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 36f, UnlockHintZh = "在疼痛训练中激活疼痛耐受基因", UnlockHintEn = "Activate pain tolerance genes through pain training", EffectType = GeneEffectType.DamageReduction, EffectValue = 7.5f, Profile = 5, Period = 4, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_energy_condensation", NameZh = "能量凝聚", NameEn = "Energy Condensation", Symbol = "凝", DescZh = "增强能量凝聚能力，提升异能强度", DescEn = "Enhances energy condensation ability, boosting ability intensity", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 38f, UnlockHintZh = "在能量凝聚中激活能量凝聚基因", UnlockHintEn = "Activate energy condensation genes through energy condensation", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 8f, Profile = 6, Period = 4, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_cell_dormancy", NameZh = "细胞休眠", NameEn = "Cell Dormancy", Symbol = "眠", DescZh = "控制细胞休眠状态，降低能量消耗", DescEn = "Controls cell dormancy state, reducing energy consumption", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 40f, UnlockHintZh = "在休眠状态中激活细胞休眠基因", UnlockHintEn = "Activate cell dormancy genes in dormant states", EffectType = GeneEffectType.SpecialAbility, EffectValue = 8.5f, Profile = 7, Period = 4, Group = 7 },

			// === 第5层：规则表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_tendon_strength", NameZh = "肌腱强度", NameEn = "Tendon Strength", Symbol = "腱", DescZh = "强化肌腱强度，提升力量传递效率", DescEn = "Strengthens tendon strength, boosting force transmission efficiency", UnlockType = GeneUnlockType.AgeRequirement, UnlockParam = 34f, UnlockHintZh = "在岁月淬炼中激活肌腱强度基因", UnlockHintEn = "Activate tendon strength genes through years of tempering", EffectType = GeneEffectType.DefenseBonus, EffectValue = 6.5f, Profile = 1, Period = 5, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_spatial_sense", NameZh = "空间感", NameEn = "Spatial Sense", Symbol = "空", DescZh = "增强空间感知能力，提升空间定位", DescEn = "Enhances spatial perception ability, boosting spatial positioning", UnlockType = GeneUnlockType.CombatKills, UnlockParam = 36f, UnlockHintZh = "在空间感知中激活空间感基因", UnlockHintEn = "Activate spatial sense genes through spatial perception", EffectType = GeneEffectType.AttackBonus, EffectValue = 7f, Profile = 2, Period = 5, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_skin_thickness", NameZh = "皮肤厚度", NameEn = "Skin Thickness", Symbol = "皮", DescZh = "提升皮肤厚度，增强物理防护", DescEn = "Increases skin thickness, enhancing physical protection", UnlockType = GeneUnlockType.LowCorruption, UnlockParam = 32f, UnlockHintZh = "在防护训练中激活皮肤厚度基因", UnlockHintEn = "Activate skin thickness genes through protection training", EffectType = GeneEffectType.HealthBonus, EffectValue = 6f, Profile = 0, Period = 5, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_logical_reasoning", NameZh = "逻辑推理", NameEn = "Logical Reasoning", Symbol = "逻", DescZh = "增强逻辑推理能力，提升决策质量", DescEn = "Enhances logical reasoning ability, boosting decision quality", UnlockType = GeneUnlockType.CombatDefense, UnlockParam = 38f, UnlockHintZh = "在逻辑推理中激活逻辑推理基因", UnlockHintEn = "Activate logical reasoning genes through logical reasoning", EffectType = GeneEffectType.CultivationBonus, EffectValue = 7.5f, Profile = 3, Period = 5, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_gustatory_receptor", NameZh = "味觉受体", NameEn = "Gustatory Receptor", Symbol = "味", DescZh = "增强味觉受体灵敏度，提升味觉感知", DescEn = "Enhances gustatory receptor sensitivity, boosting gustatory perception", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 40f, UnlockHintZh = "在深度觉醒中激活味觉受体基因", UnlockHintEn = "Activate gustatory receptor genes in deep awakening", EffectType = GeneEffectType.SpeedBonus, EffectValue = 8f, Profile = 4, Period = 5, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_concentration", NameZh = "专注力", NameEn = "Concentration", Symbol = "专", DescZh = "增强专注力，提升精神集中程度", DescEn = "Enhances concentration, boosting mental focus level", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 44f, UnlockHintZh = "在深度专注中激活专注力基因", UnlockHintEn = "Activate concentration genes through deep concentration", EffectType = GeneEffectType.DamageReduction, EffectValue = 9f, Profile = 5, Period = 5, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_energy_release", NameZh = "能量释放", NameEn = "Energy Release", Symbol = "释", DescZh = "增强能量释放能力，提升异能输出", DescEn = "Enhances energy release ability, boosting ability output", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 42f, UnlockHintZh = "在能量释放中激活能量释放基因", UnlockHintEn = "Activate energy release genes through energy release", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 8.5f, Profile = 6, Period = 5, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_telomere_repair", NameZh = "端粒修复", NameEn = "Telomere Repair", Symbol = "端", DescZh = "修复染色体端粒，延缓细胞衰老", DescEn = "Repairs chromosome telomeres, delaying cellular aging", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 46f, UnlockHintZh = "在深度觉醒中激活端粒修复基因", UnlockHintEn = "Activate telomere repair genes in deep awakening", EffectType = GeneEffectType.SpecialAbility, EffectValue = 9.5f, Profile = 7, Period = 5, Group = 7 },

			// === 第6层：至高表达 ===
			// 力量染色体
			new ElementInfo { Id = "gene_energy_supply", NameZh = "能量供给", NameEn = "Energy Supply", Symbol = "供", DescZh = "增强能量供给能力，提升持续输出", DescEn = "Enhances energy supply ability, boosting sustained output", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 42f, UnlockHintZh = "在深度觉醒中激活能量供给基因", UnlockHintEn = "Activate energy supply genes in deep awakening", EffectType = GeneEffectType.DefenseBonus, EffectValue = 8.5f, NodeType = GeneNodeType.Branch, Profile = 1, Period = 6, Group = 0 },
			// 敏捷染色体
			new ElementInfo { Id = "gene_reaction_speed", NameZh = "反应速度", NameEn = "Reaction Speed", Symbol = "反", DescZh = "极致提升反应速度，超越常人极限", DescEn = "Extremely boosts reaction speed, surpassing normal limits", UnlockType = GeneUnlockType.EnergyThreshold, UnlockParam = 44f, UnlockHintZh = "在能量极致中激活反应速度基因", UnlockHintEn = "Activate reaction speed genes in energy extremes", EffectType = GeneEffectType.AttackBonus, EffectValue = 9f, NodeType = GeneNodeType.Branch, Profile = 2, Period = 6, Group = 1 },
			// 体质染色体
			new ElementInfo { Id = "gene_organ_function", NameZh = "器官功能", NameEn = "Organ Function", Symbol = "器", DescZh = "极致提升器官功能，接近真神形态", DescEn = "Extremely boosts organ function, approaching transcendent form", UnlockType = GeneUnlockType.Ritual, UnlockParam = 40f, UnlockHintZh = "通过至高仪式激活器官功能基因", UnlockHintEn = "Activate organ function genes through supreme ritual", EffectType = GeneEffectType.HealthBonus, EffectValue = 8f, NodeType = GeneNodeType.Branch, Profile = 0, Period = 6, Group = 2 },
			// 智力染色体
			new ElementInfo { Id = "gene_creativity", NameZh = "创造力", NameEn = "Creativity", Symbol = "创", DescZh = "极致提升创造力，产生全新想法", DescEn = "Extremely boosts creativity, generating entirely new ideas", UnlockType = GeneUnlockType.DeepEnlightenment, UnlockParam = 46f, UnlockHintZh = "在深度觉醒中激活创造力基因", UnlockHintEn = "Activate creativity genes in deep awakening", EffectType = GeneEffectType.DamageReduction, EffectValue = 9.5f, NodeType = GeneNodeType.Branch, Profile = 3, Period = 6, Group = 3 },
			// 感知染色体
			new ElementInfo { Id = "gene_proprioception", NameZh = "本体感觉", NameEn = "Proprioception", Symbol = "本", DescZh = "极致提升本体感觉，完美控制身体", DescEn = "Extremely boosts proprioception, perfectly controlling the body", UnlockType = GeneUnlockType.Ritual, UnlockParam = 48f, UnlockHintZh = "通过至高仪式激活本体感觉基因", UnlockHintEn = "Activate proprioception genes through supreme ritual", EffectType = GeneEffectType.SpeedBonus, EffectValue = 10f, NodeType = GeneNodeType.Branch, Profile = 4, Period = 6, Group = 4 },
			// 意志染色体
			new ElementInfo { Id = "gene_willpower", NameZh = "意志力", NameEn = "Willpower", Symbol = "意", DescZh = "极致提升意志力，精神不可动摇", DescEn = "Extremely boosts willpower, spirit unshakable", UnlockType = GeneUnlockType.Ritual, UnlockParam = 52f, UnlockHintZh = "通过至高仪式激活意志力基因", UnlockHintEn = "Activate willpower genes through supreme ritual", EffectType = GeneEffectType.CultivationBonus, EffectValue = 11f, NodeType = GeneNodeType.Branch, Profile = 5, Period = 6, Group = 5 },
			// 异能染色体
			new ElementInfo { Id = "gene_energy_manipulation", NameZh = "能量操控", NameEn = "Energy Manipulation", Symbol = "操", DescZh = "极致提升能量操控，进入量子态", DescEn = "Extremely boosts energy manipulation, entering quantum state", UnlockType = GeneUnlockType.Ritual, UnlockParam = 50f, UnlockHintZh = "通过至高仪式激活能量操控基因", UnlockHintEn = "Activate energy manipulation genes through supreme ritual", EffectType = GeneEffectType.EnergyMaxBonus, EffectValue = 10.5f, NodeType = GeneNodeType.Branch, Profile = 6, Period = 6, Group = 6 },
			// 潜能染色体
			new ElementInfo { Id = "gene_life_sublimation", NameZh = "生命升华", NameEn = "Life Sublimation", Symbol = "升", DescZh = "生命升华至极致，接近不朽", DescEn = "Life sublimates to the extreme, approaching immortality", UnlockType = GeneUnlockType.Ritual, UnlockParam = 54f, UnlockHintZh = "通过至高仪式激活生命升华基因", UnlockHintEn = "Activate life sublimation genes through supreme ritual", EffectType = GeneEffectType.SpecialAbility, EffectValue = 11.5f, NodeType = GeneNodeType.Branch, Profile = 7, Period = 6, Group = 7 },

			// === 第7层：本源表达（未知，无法主动获得）===
			new ElementInfo { Id = "gene_structural_source", NameZh = "结构本源", NameEn = "Structural Source", Symbol = "结", DescZh = "身体结构的本源形态（未知）", DescEn = "Source form of body structure (Unknown)", Profile = 1, Period = 7, Group = 0 },
			new ElementInfo { Id = "gene_neural_source", NameZh = "神经本源", NameEn = "Neural Source", Symbol = "神", DescZh = "神经系统的本源形态（未知）", DescEn = "Source form of nervous system (Unknown)", Profile = 2, Period = 7, Group = 1 },
			new ElementInfo { Id = "gene_life_source", NameZh = "生命本源", NameEn = "Life Source", Symbol = "生", DescZh = "生命系统的本源形态（未知）", DescEn = "Source form of life system (Unknown)", Profile = 0, Period = 7, Group = 2 },
			new ElementInfo { Id = "gene_intelligence_source", NameZh = "智力本源", NameEn = "Intelligence Source", Symbol = "智", DescZh = "智力系统的本源形态（未知）", DescEn = "Source form of intelligence system (Unknown)", Profile = 3, Period = 7, Group = 3 },
			new ElementInfo { Id = "gene_perception_source", NameZh = "感知本源", NameEn = "Perception Source", Symbol = "感", DescZh = "感知系统的本源形态（未知）", DescEn = "Source form of perception system (Unknown)", Profile = 4, Period = 7, Group = 4 },
			new ElementInfo { Id = "gene_will_source", NameZh = "意志本源", NameEn = "Will Source", Symbol = "意", DescZh = "意志系统的本源形态（未知）", DescEn = "Source form of will system (Unknown)", Profile = 5, Period = 7, Group = 5 },
			new ElementInfo { Id = "gene_ability_source", NameZh = "异能本源", NameEn = "Ability Source", Symbol = "异", DescZh = "异能系统的本源形态（未知）", DescEn = "Source form of ability system (Unknown)", Profile = 6, Period = 7, Group = 6 },
			new ElementInfo { Id = "gene_potential_source", NameZh = "潜能本源", NameEn = "Potential Source", Symbol = "潜", DescZh = "潜能系统的本源形态（未知）", DescEn = "Source form of potential system (Unknown)", Profile = 7, Period = 7, Group = 7 },
			GetDivineGeneDefault(), // 第57位神创基因
		};

		/// <summary>
		/// 获取所有基因序列（数组顺序 = 编号顺序）
		/// </summary>
		public static IReadOnlyList<ElementInfo> AllElements
		{
			get { EnsureResolved(); return _elements; }
		}

		/// <summary>
		/// 基因序列总数
		/// </summary>
		public static int Count => _elements.Length;

		/// <summary>
		/// 
		/// </summary>

		/// <summary>
		/// 
		/// </summary>

		/// <summary>
		/// 
		/// </summary>

		/// <summary>
		/// 
		/// </summary>

		/// <summary>
		/// 
		/// </summary>

		/// <summary>
		/// 基因序数 = 层级优先坐标：(层级-1)×8 + 染色体 + 1
		/// </summary>
		public static int GetNumber(ElementInfo elem)
		{
			return (elem.Period - 1) * 8 + elem.Group + 1;
		}

		/// <summary>
		/// 根据ID获取基因序列
		/// </summary>
		private static readonly GeneUnlockType[] PERIOD_UNLOCK = {
			GeneUnlockType.CombatDefense, GeneUnlockType.CombatKills, GeneUnlockType.ContinuousCultivation,
			GeneUnlockType.LowCorruption, GeneUnlockType.DeepEnlightenment, GeneUnlockType.Ritual };
		private static readonly float[] PERIOD_PARAM_BASE = { 8f, 14f, 20f, 26f, 32f, 40f };
		private static readonly float[] PERIOD_VALUE_BASE = { 2f, 3f, 4f, 5f, 6f, 8f };
		private static readonly GeneEffectType[] GROUP_EFFECT = {
			GeneEffectType.HealthBonus, GeneEffectType.DefenseBonus, GeneEffectType.AttackBonus,
			GeneEffectType.DamageReduction, GeneEffectType.SpeedBonus, GeneEffectType.EnergyMaxBonus,
			GeneEffectType.CultivationBonus, GeneEffectType.SpecialAbility };

		private static bool _statsResolved = false;

		private static void EnsureResolved()
		{
			if (_statsResolved) return;
			foreach (var e in _elements) ResolveAutoStats(e);
			_statsResolved = true;
		}

		/// <summary>
		/// 自动数值填充：按 层级×染色体 公式生成解锁/效果/节点类型/组合分组
		/// </summary>
		private static void ResolveAutoStats(ElementInfo e)
		{
			if (!e.UseAutoStats) return;
			int p = e.Period;
			e.EffectType = GROUP_EFFECT[e.Profile];
			e.UnlockType = (p <= 6) ? PERIOD_UNLOCK[p - 1] : GeneUnlockType.SourceUnknown;
			float pBase = (p <= 6) ? PERIOD_PARAM_BASE[p - 1] : 40f + (p - 6) * 6f;
			float vBase = (p <= 6) ? PERIOD_VALUE_BASE[p - 1] : 8f + (p - 6) * 2f;
			e.UnlockParam = pBase + e.Profile * 2f;
			e.EffectValue = vBase + e.Profile * 0.5f;
			e.NodeType = (p >= 6) ? GeneNodeType.Branch : GeneNodeType.Main;
		}

		public static ElementInfo GetById(string id)
		{
			foreach (var elem in _elements)
			{
				if (elem.Id == id) return elem;
			}
			return null;
		}

		public static ElementInfo GetByGroupPeriod(int group, int period)
		{
			foreach (var elem in _elements)
			{
				if (elem.Group == group && elem.Period == period) return elem;
			}
			return null;
		}

        // ============================================================
        //  神创基因（第57位，可自定义）
        // ============================================================

        /// <summary>神创基因ID（固定第57位）</summary>
        public const string DivineGeneId = "gene_divine";

        /// <summary>获取神创基因默认信息</summary>
        public static ElementInfo GetDivineGeneDefault()
        {
            return new ElementInfo
            {
                Id = DivineGeneId,
                NameZh = "神创基因",
                NameEn = "Divine Gene",
                Symbol = "神",
                DescZh = "由神之手创造的基因，拥有无限可能",
                DescEn = "A gene created by divine hand, with infinite possibilities",
                UnlockType = GeneUnlockType.Special,
                UnlockParam = 0f,
                UnlockHintZh = "神创基因，天生拥有",
                UnlockHintEn = "Unlock divine gene slot at Tier 10",
                EffectType = GeneEffectType.SpecialAbility,
                EffectValue = 5.0f,
                Profile = 0,
                Period = 8,  // 第8层（第57位神创基因）
                Group = 0,
                UseAutoStats = false
            };
        }
    }
}
