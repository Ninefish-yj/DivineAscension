using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Data;
using Code.Core;

namespace Code.Realm
{
	/// <summary>
	/// 基因表达层级系统 - 完全重构
	/// 
	/// 核心机制：
	/// 1. 每个基因有6个表达层级：沉默、低表达、中表达、高表达、过表达、突变
	/// 2. 表达层级越高，效果越强
	/// 3. 基因突变可以提升表达层级
	/// 4. 修炼可以缓慢提升表达层级
	/// 5. 突破条件与表达层级相关
	/// </summary>
	public static class GeneExpressionSystem
	{
		/// <summary>
		/// 基因表达层级
		/// </summary>
		public enum ExpressionLevel
		{
			Silent = 0,      // 沉默（未激活）
			Low = 1,          // 低表达（基础效果25%）
			Medium = 2,       // 中表达（明显效果50%）
			High = 3,         // 高表达（完整效果100%）
			Over = 4,         // 过表达（超强效果150%，有副作用）
			Mutant = 5        // 突变（极致效果200%，可能产生新能力）
		}

		/// <summary>
		/// 表达层级效果倍率
		/// </summary>
		public static readonly float[] EffectMultipliers = { 0f, 0.25f, 0.5f, 1.0f, 1.5f, 2.0f };

		/// <summary>
		/// 表达层级名称（中文）
		/// </summary>
		public static readonly string[] LevelNamesZh = { "沉默", "低表达", "中表达", "高表达", "过表达", "突变" };

		/// <summary>
		/// 表达层级名称（英文）
		/// </summary>
		public static readonly string[] LevelNamesEn = { "Silent", "Low Expression", "Medium Expression", "High Expression", "Over Expression", "Mutant" };

		/// <summary>
		/// 表达层级颜色
		/// </summary>
		public static readonly Color[] LevelColors = {
			new Color(0.5f, 0.5f, 0.5f),  // 沉默 - 灰色
			new Color(0.6f, 0.8f, 0.6f),  // 低表达 - 浅绿
			new Color(0.4f, 0.8f, 0.4f),  // 中表达 - 绿色
			new Color(0.2f, 0.8f, 0.2f),  // 高表达 - 深绿
			new Color(0.8f, 0.6f, 0.2f),  // 过表达 - 橙色
			new Color(0.9f, 0.3f, 0.3f)   // 突变 - 红色
		};

		/// <summary>
		/// 获取基因的表达层级
		/// </summary>
		public static ExpressionLevel GetExpressionLevel(Actor actor, string geneId)
		{
			if (actor == null || string.IsNullOrEmpty(geneId)) return ExpressionLevel.Silent;
			
			// 如果基因未解锁，返回沉默
			if (!CultivationData.IsElementUnlocked(actor, geneId)) return ExpressionLevel.Silent;
			
			// 获取存储的表达层级（使用CultivationData的公共方法）
			int level = CultivationData.GetGeneExpressionLevel(actor, geneId);
			
			return (ExpressionLevel)Mathf.Clamp(level, 0, 5);
		}

		/// <summary>
		/// 设置基因的表达层级
		/// </summary>
		public static void SetExpressionLevel(Actor actor, string geneId, ExpressionLevel level)
		{
			if (actor == null || string.IsNullOrEmpty(geneId)) return;
			
			CultivationData.SetGeneExpressionLevel(actor, geneId, (int)level);
		}

		/// <summary>
		/// 提升基因的表达层级
		/// </summary>
		/// <returns>是否成功提升</returns>
		public static bool UpgradeExpressionLevel(Actor actor, string geneId)
		{
			if (actor == null || string.IsNullOrEmpty(geneId)) return false;
			
			ExpressionLevel currentLevel = GetExpressionLevel(actor, geneId);
			
			// 已经是最高层级，无法提升
			if (currentLevel >= ExpressionLevel.Mutant) return false;
			
			// 提升层级
			SetExpressionLevel(actor, geneId, currentLevel + 1);
			
			DSDebug.Verbose($"[基因表达] {actor.getName()} 基因{geneId}表达层级提升: {currentLevel} -> {currentLevel + 1}");
			
			return true;
		}

		/// <summary>
		/// 获取某条染色体上已激活基因的数量
		/// </summary>
		public static int GetActivatedGeneCountInChromosome(Actor actor, int chromosome)
		{
			if (actor == null) return 0;
			
			var unlockedGenes = CultivationData.GetUnlockedElements(actor);
			if (unlockedGenes == null || unlockedGenes.Count == 0) return 0;
			
			int count = 0;
			foreach (var geneId in unlockedGenes)
			{
				var gene = Code.ElementDef.GetById(geneId);
				if (gene != null && gene.Group == chromosome)
				{
					count++;
				}
			}
			
			return count;
		}

		/// <summary>
		/// 获取某条染色体上达到指定表达层级的基因数量
		/// </summary>
		public static int GetGeneCountAtLevelInChromosome(Actor actor, int chromosome, ExpressionLevel minLevel)
		{
			if (actor == null) return 0;
			
			var unlockedGenes = CultivationData.GetUnlockedElements(actor);
			if (unlockedGenes == null || unlockedGenes.Count == 0) return 0;
			
			int count = 0;
			foreach (var geneId in unlockedGenes)
			{
				var gene = Code.ElementDef.GetById(geneId);
				if (gene != null && gene.Group == chromosome)
				{
					ExpressionLevel level = GetExpressionLevel(actor, geneId);
					if (level >= minLevel)
					{
						count++;
					}
				}
			}
			
			return count;
		}

		/// <summary>
		/// 获取达到指定表达层级的基因总数
		/// </summary>
		public static int GetTotalGeneCountAtLevel(Actor actor, ExpressionLevel minLevel)
		{
			if (actor == null) return 0;
			
			var unlockedGenes = CultivationData.GetUnlockedElements(actor);
			if (unlockedGenes == null || unlockedGenes.Count == 0) return 0;
			
			int count = 0;
			foreach (var geneId in unlockedGenes)
			{
				ExpressionLevel level = GetExpressionLevel(actor, geneId);
				if (level >= minLevel)
				{
					count++;
				}
			}
			
			return count;
		}

		/// <summary>
		/// 修炼时缓慢提升基因表达层级
		/// </summary>
		public static void CultivationExpressionUpgrade(Actor actor)
		{
			if (actor == null || !actor.isAlive()) return;
			if (!CultivationData.IsAscended(actor)) return;
			
			var unlockedGenes = CultivationData.GetUnlockedElements(actor);
			if (unlockedGenes == null || unlockedGenes.Count == 0) return;
			
			// 修炼时有1%概率随机提升一个基因的表达层级
			if (UnityEngine.Random.value > 0.01f) return;
			
			// 随机选择一个基因
			string randomGene = unlockedGenes[UnityEngine.Random.Range(0, unlockedGenes.Count)];
			
			// 提升表达层级（高表达层级提升概率更低）
			ExpressionLevel currentLevel = GetExpressionLevel(actor, randomGene);
			float upgradeChance = Mathf.Pow(0.5f, (int)currentLevel); // 低表达50%，中表达25%，高表达12.5%...
			
			if (UnityEngine.Random.value < upgradeChance)
			{
				UpgradeExpressionLevel(actor, randomGene);
			}
		}
		
		/// <summary>
		/// 获取技能伤害加成（基于基因表达层级）
		/// 高表达层级的基因可以提升技能伤害
		/// </summary>
		public static float GetSkillDamageBonus(Actor actor)
		{
			if (actor == null || !CultivationData.IsAscended(actor)) return 0f;
			
			try
			{
				var unlockedGenes = CultivationData.GetUnlockedElements(actor);
				if (unlockedGenes == null || unlockedGenes.Count == 0) return 0f;
				
				float totalBonus = 0f;
				foreach (var geneId in unlockedGenes)
				{
					int exprLevel = CultivationData.GetGeneExpressionLevel(actor, geneId);
					// 表达层级越高，技能伤害加成越多
					// 低表达+1%，中表达+2%，高表达+4%，过表达+6%，突变+10%
					float[] levelBonus = { 0f, 0.01f, 0.02f, 0.04f, 0.06f, 0.10f };
					if (exprLevel >= 0 && exprLevel < levelBonus.Length)
					{
						totalBonus += levelBonus[exprLevel];
					}
				}
				
				// 上限+50%
				return Mathf.Min(0.5f, totalBonus);
			}
			catch
			{
				return 0f;
			}
		}
		
		/// <summary>
		/// 获取技能治疗加成（基于基因表达层级）
		/// </summary>
		public static float GetSkillHealBonus(Actor actor)
		{
			if (actor == null || !CultivationData.IsAscended(actor)) return 0f;
			
			try
			{
				var unlockedGenes = CultivationData.GetUnlockedElements(actor);
				if (unlockedGenes == null || unlockedGenes.Count == 0) return 0f;
				
				float totalBonus = 0f;
				foreach (var geneId in unlockedGenes)
				{
					// 只有体质染色体和异能染色体的基因影响治疗
					var gene = ElementDef.GetById(geneId);
					if (gene == null) continue;
					if (gene.Group != 2 && gene.Group != 6) continue; // 2=体质，6=异能
					
					int exprLevel = CultivationData.GetGeneExpressionLevel(actor, geneId);
					float[] levelBonus = { 0f, 0.01f, 0.02f, 0.04f, 0.06f, 0.10f };
					if (exprLevel >= 0 && exprLevel < levelBonus.Length)
					{
						totalBonus += levelBonus[exprLevel];
					}
				}
				
				return Mathf.Min(0.5f, totalBonus);
			}
			catch
			{
				return 0f;
			}
		}
	}
}


