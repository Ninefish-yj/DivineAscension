
using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Data;

namespace Code
{
	/// <summary>
	/// 基因连锁系统（动态连锁）
	/// 不预设固定组合，根据单位拥有的基因动态计算连锁效果和名字
	/// </summary>
	public static class GeneLinkageSystem
	{
		/// <summary>
		/// 连锁效果类型
		/// </summary>
		public enum LinkageEffectType
		{
			DamageBonus,     // 伤害加成
			DefenseBonus,    // 防御加成
			HealthBonus,     // 生命加成
			SpeedBonus,      // 速度加成
			EnergyBonus,     // 能量加成
			CultivationBonus, // 修炼加成
			SkillDamageBonus, // 技能伤害加成
			AllAttributeBonus, // 全属性加成
			SpecialAbility,  // 特殊能力
		}

		/// <summary>
		/// 动态连锁结果
		/// </summary>
		public class DynamicLinkage
		{
			public string Id;              // 连锁ID
			public string NameZh;          // 中文名
			public string NameEn;          // 英文名
			public LinkageEffectType EffectType; // 效果类型
			public float EffectValue;      // 效果数值
			public string DescZh;          // 中文描述
			public string DescEn;          // 英文描述
			public int Tier;               // 连锁等级（1=初级, 2=中级, 3=高级, 4=本源）
			public string Category;        // 连锁类别（同染色体/跨染色体/表达层级）
		}

		// 染色体名称
		private static readonly string[] ChromosomeNamesZh = { "力量", "敏捷", "体质", "智力", "感知", "意志", "异能", "潜能" };
		private static readonly string[] ChromosomeNamesEn = { "Strength", "Agility", "Constitution", "Intelligence", "Perception", "Will", "Ability", "Potential" };

		// 染色体对应的主效果类型
		private static readonly LinkageEffectType[] ChromosomeEffectTypes = {
			LinkageEffectType.DamageBonus,      // 力量 -> 伤害
			LinkageEffectType.SpeedBonus,       // 敏捷 -> 速度
			LinkageEffectType.HealthBonus,      // 体质 -> 生命
			LinkageEffectType.CultivationBonus, // 智力 -> 修炼
			LinkageEffectType.SpeedBonus,       // 感知 -> 速度
			LinkageEffectType.DefenseBonus,     // 意志 -> 防御
			LinkageEffectType.EnergyBonus,      // 异能 -> 能量
			LinkageEffectType.SpecialAbility,   // 潜能 -> 特殊能力
		};

		// 同染色体连锁等级配置
		private static readonly (int MinGenes, string SuffixZh, string SuffixEn, float Bonus, int Tier)[] ChromosomeLinkageLevels = {
			(2, "觉醒者", "Awakened", 0.05f, 1),
			(4, "掌控者", "Master", 0.15f, 2),
			(6, "主宰者", "Dominator", 0.30f, 3),
			(7, "本源觉醒", "Source Awakening", 0.50f, 4),
		};

		// 跨染色体连锁等级配置
		private static readonly (int MinChromosomes, int MinGenesPerChromosome, string NameZh, string NameEn, float Bonus, int Tier)[] CrossChromosomeLinkageLevels = {
			(2, 3, "双染色体协同", "Dual Chromosome Synergy", 0.10f, 2),
			(4, 3, "四象掌控者", "Four Symbols Master", 0.25f, 3),
			(8, 3, "全知全能者", "Omniscient Almighty", 0.50f, 4),
		};

		// 表达层级连锁等级配置
		private static readonly (int MinGenes, int MinExpressionLevel, string NameZh, string NameEn, float Bonus, int Tier)[] ExpressionLinkageLevels = {
			(3, 3, "基因高表达者", "Gene High Expressor", 0.15f, 2),
			(3, 4, "基因过表达者", "Gene Over Expressor", 0.30f, 3),
			(1, 5, "基因突变者", "Gene Mutant", 0.50f, 4),
		};

		/// <summary>
		/// 检查单位的所有动态基因连锁
		/// </summary>
		public static List<DynamicLinkage> CheckAllLinkages(Actor actor)
		{
			var result = new List<DynamicLinkage>();
			if (actor == null || !actor.isAlive()) return result;
			if (!CultivationData.IsAscended(actor)) return result;

			var unlockedGenes = CultivationData.GetUnlockedElements(actor);
			if (unlockedGenes == null || unlockedGenes.Count == 0) return result;

			// 1. 检查同染色体连锁
			result.AddRange(CheckChromosomeLinkages(actor, unlockedGenes));

			// 2. 检查跨染色体连锁
			result.AddRange(CheckCrossChromosomeLinkages(actor, unlockedGenes));

			// 3. 检查表达层级连锁
			result.AddRange(CheckExpressionLinkages(actor, unlockedGenes));

			return result;
		}

		/// <summary>
		/// 检查同染色体连锁
		/// </summary>
		private static List<DynamicLinkage> CheckChromosomeLinkages(Actor actor, List<string> unlockedGenes)
		{
			var result = new List<DynamicLinkage>();

			// 按染色体分组统计基因数量
			var chromosomeGeneCounts = new int[8];
			foreach (var geneId in unlockedGenes)
			{
				var geneInfo = ElementDef.GetById(geneId);
				if (geneInfo != null)
				{
					chromosomeGeneCounts[geneInfo.Group]++;
				}
			}

			// 检查每条染色体的连锁等级
			for (int chromosome = 0; chromosome < 8; chromosome++)
			{
				int geneCount = chromosomeGeneCounts[chromosome];
				if (geneCount < 2) continue;

				// 找到最高的连锁等级
				for (int i = ChromosomeLinkageLevels.Length - 1; i >= 0; i--)
				{
					var level = ChromosomeLinkageLevels[i];
					if (geneCount >= level.MinGenes)
					{
						var linkage = new DynamicLinkage
						{
							Id = $"linkage_chromosome_{chromosome}_{level.MinGenes}",
							NameZh = $"{ChromosomeNamesZh[chromosome]}{level.SuffixZh}",
							NameEn = $"{ChromosomeNamesEn[chromosome]} {level.SuffixEn}",
							EffectType = ChromosomeEffectTypes[chromosome],
							EffectValue = level.Bonus,
							DescZh = $"{ChromosomeNamesZh[chromosome]}染色体拥有{geneCount}个基因，{level.SuffixZh}，+{level.Bonus * 100:F0}%对应属性",
							DescEn = $"{ChromosomeNamesEn[chromosome]} chromosome has {geneCount} genes, {level.SuffixEn}, +{level.Bonus * 100:F0}% corresponding attributes",
							Tier = level.Tier,
							Category = "同染色体"
						};
						result.Add(linkage);
						break; // 只取最高等级
					}
				}
			}

			return result;
		}

		/// <summary>
		/// 检查跨染色体连锁
		/// </summary>
		private static List<DynamicLinkage> CheckCrossChromosomeLinkages(Actor actor, List<string> unlockedGenes)
		{
			var result = new List<DynamicLinkage>();

			// 按染色体分组统计基因数量
			var chromosomeGeneCounts = new int[8];
			foreach (var geneId in unlockedGenes)
			{
				var geneInfo = ElementDef.GetById(geneId);
				if (geneInfo != null)
				{
					chromosomeGeneCounts[geneInfo.Group]++;
				}
			}

			// 检查跨染色体连锁等级
			for (int i = CrossChromosomeLinkageLevels.Length - 1; i >= 0; i--)
			{
				var level = CrossChromosomeLinkageLevels[i];
				int qualifiedChromosomes = 0;
				for (int chromosome = 0; chromosome < 8; chromosome++)
				{
					if (chromosomeGeneCounts[chromosome] >= level.MinGenesPerChromosome)
					{
						qualifiedChromosomes++;
					}
				}

				if (qualifiedChromosomes >= level.MinChromosomes)
				{
					var linkage = new DynamicLinkage
					{
						Id = $"linkage_cross_{level.MinChromosomes}",
						NameZh = level.NameZh,
						NameEn = level.NameEn,
						EffectType = LinkageEffectType.AllAttributeBonus,
						EffectValue = level.Bonus,
						DescZh = $"{qualifiedChromosomes}条染色体各拥有{level.MinGenesPerChromosome}+基因，{level.NameZh}，+{level.Bonus * 100:F0}%全属性",
						DescEn = $"{qualifiedChromosomes} chromosomes each have {level.MinGenesPerChromosome}+ genes, {level.NameEn}, +{level.Bonus * 100:F0}% all attributes",
						Tier = level.Tier,
						Category = "跨染色体"
					};
					result.Add(linkage);
					break; // 只取最高等级
				}
			}

			return result;
		}

		/// <summary>
		/// 检查表达层级连锁
		/// </summary>
		private static List<DynamicLinkage> CheckExpressionLinkages(Actor actor, List<string> unlockedGenes)
		{
			var result = new List<DynamicLinkage>();

			// 统计各表达层级的基因数量
			var expressionLevelCounts = new int[6]; // 0-5级
			foreach (var geneId in unlockedGenes)
			{
				int exprLevel = CultivationData.GetGeneExpressionLevel(actor, geneId);
				if (exprLevel >= 0 && exprLevel <= 5)
				{
					expressionLevelCounts[exprLevel]++;
				}
			}

			// 检查表达层级连锁等级
			for (int i = ExpressionLinkageLevels.Length - 1; i >= 0; i--)
			{
				var level = ExpressionLinkageLevels[i];
				// 统计达到最低表达层级的基因数量
				int qualifiedGenes = 0;
				for (int exprLevel = level.MinExpressionLevel; exprLevel <= 5; exprLevel++)
				{
					qualifiedGenes += expressionLevelCounts[exprLevel];
				}

				if (qualifiedGenes >= level.MinGenes)
				{
					var linkage = new DynamicLinkage
					{
						Id = $"linkage_expression_{level.MinExpressionLevel}",
						NameZh = level.NameZh,
						NameEn = level.NameEn,
						EffectType = LinkageEffectType.SkillDamageBonus,
						EffectValue = level.Bonus,
						DescZh = $"{qualifiedGenes}个基因达到{level.MinExpressionLevel}级表达，{level.NameZh}，+{level.Bonus * 100:F0}%技能伤害",
						DescEn = $"{qualifiedGenes} genes reach level {level.MinExpressionLevel} expression, {level.NameEn}, +{level.Bonus * 100:F0}% skill damage",
						Tier = level.Tier,
						Category = "表达层级"
					};
					result.Add(linkage);
					break; // 只取最高等级
				}
			}

			return result;
		}

		/// <summary>
		/// 获取单位的总伤害加成（来自动态基因连锁）
		/// </summary>
		public static float GetTotalDamageBonus(Actor actor)
		{
			if (actor == null) return 0f;
			float cached = CultivationData.GetLinkageDamageCache(actor);
			if (cached >= 0f) return cached;
			RefreshLinkageCaches(actor);
			return CultivationData.GetLinkageDamageCache(actor);
		}

		/// <summary>
		/// 获取单位的总防御加成（来自动态基因连锁）
		/// </summary>
		public static float GetTotalDefenseBonus(Actor actor)
		{
			if (actor == null) return 0f;
			float cached = CultivationData.GetLinkageDefenseCache(actor);
			if (cached >= 0f) return cached;
			RefreshLinkageCaches(actor);
			return CultivationData.GetLinkageDefenseCache(actor);
		}

		/// <summary>
		/// 获取单位的总生命加成（来自动态基因连锁）
		/// </summary>
		public static float GetTotalHealthBonus(Actor actor)
		{
			if (actor == null) return 0f;
			float cached = CultivationData.GetLinkageHealthCache(actor);
			if (cached >= 0f) return cached;
			RefreshLinkageCaches(actor);
			return CultivationData.GetLinkageHealthCache(actor);
		}

		/// <summary>
		/// 计算并缓存所有连锁加成（年度Tick时调用；战斗时直接读缓存，与GE缓存同模式）
		/// </summary>
		public static void RefreshLinkageCaches(Actor actor)
		{
			if (actor == null) return;
			try
			{
				float dmg = ComputeTotalBonus(actor, LinkageEffectType.DamageBonus);
				float def = ComputeTotalBonus(actor, LinkageEffectType.DefenseBonus);
				float hp = ComputeTotalBonus(actor, LinkageEffectType.HealthBonus);

				CultivationData.SetLinkageDamageCache(actor, dmg);
				CultivationData.SetLinkageDefenseCache(actor, def);
				CultivationData.SetLinkageHealthCache(actor, hp);
			}
			catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] RefreshLinkageCaches 异常: " + dsEx.Message); }
		}

		/// <summary>计算单类连锁加成（含GE倍率）</summary>
		private static float ComputeTotalBonus(Actor actor, LinkageEffectType type)
		{
			var linkages = CheckAllLinkages(actor);
			float total = 0f;
			foreach (var linkage in linkages)
			{
				if (linkage.EffectType == type ||
					linkage.EffectType == LinkageEffectType.AllAttributeBonus)
				{
					total += linkage.EffectValue;
				}
			}
			// 接入GE公式：基因能量越高，基因连锁效果越强（最多+50%）
			if (actor != null && total > 0f)
			{
				try {
					float geBonus = 0f;
					if (type == LinkageEffectType.DamageBonus) geBonus = Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
					else if (type == LinkageEffectType.DefenseBonus) geBonus = Code.Core.GeneEnergyCalculator.GetArmorBonus(actor);
					else geBonus = Code.Core.GeneEnergyCalculator.GetHealthBonus(actor);
					float geMultiplier = 1f + Mathf.Min(0.5f, geBonus / 100f); // GE100=+50%
					total *= geMultiplier;
				} catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ComputeTotalBonus 异常: " + dsEx.Message); }
			}
			return total;
		}

		/// <summary>
		/// 获取单位的总技能伤害加成（来自动态基因连锁）
		/// </summary>
		public static float GetTotalSkillDamageBonus(Actor actor)
		{
			var linkages = CheckAllLinkages(actor);
			float total = 0f;
			foreach (var linkage in linkages)
			{
				if (linkage.EffectType == LinkageEffectType.SkillDamageBonus)
				{
					total += linkage.EffectValue;
				}
			}
			// 接入GE公式：基因能量越高，基因连锁效果越强（最多+50%）
			if (actor != null && total > 0f)
			{
				try {
					float geBonus = Code.Core.GeneEnergyCalculator.GetDamageBonus(actor);
					float geMultiplier = 1f + Mathf.Min(0.5f, geBonus / 100f); // GE100=+50%
					total *= geMultiplier;
				} catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] GetTotalSkillDamageBonus 异常: " + dsEx.Message); }
			}
			return total;
		}

		/// <summary>
		/// 获取包含指定基因的所有连锁规则（用于基因图谱提示）
		/// </summary>
		public static List<string> GetLinkageHintsForGene(string geneId)
		{
			var result = new List<string>();
			var geneInfo = ElementDef.GetById(geneId);
			if (geneInfo == null) return result;

			int chromosome = geneInfo.Group;
			result.Add($"同染色体连锁：{ChromosomeNamesZh[chromosome]}染色体拥有更多基因可触发连锁（2/4/6/7个基因）");
			result.Add($"跨染色体连锁：多条染色体各拥有3+基因可触发协同效果");
			result.Add($"表达层级连锁：多个基因达到高表达层级可触发技能伤害加成");

			return result;
		}
	}
}
