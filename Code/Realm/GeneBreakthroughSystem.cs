using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Data;
using Code.Core;

namespace Code.Realm
{
	/// <summary>
	/// 基因突破条件系统 - 完全重构
	/// 
	/// 核心机制：
	/// 1. 低境界（1-4阶）：关注单个基因表达层级
	/// 2. 中境界（5-8阶）：关注染色体整体表达
	/// 3. 高境界（9-12阶）：关注多染色体协同表达
	/// 4. 真神（13阶）：关注极致基因表达
	/// </summary>
	public static class GeneBreakthroughSystem
	{
		/// <summary>
		/// 检查基因表达突破条件
		/// </summary>
		/// <param name="actor">单位</param>
		/// <param name="targetTier">目标境界</param>
		/// <returns>是否满足突破条件</returns>
		public static bool CheckGeneBreakthroughCondition(Actor actor, int targetTier)
		{
			if (actor == null || !actor.isAlive()) return false;
			if (!CultivationData.IsAscended(actor)) return false;

			switch (targetTier)
			{
				// === 低境界（1-4阶）：关注单个基因表达层级 ===
				case 1:
					// 1阶（觉醒者）：觉醒时自动获得
					return true;

				case 2:
					// 2阶（淬体者）：至少1个基因达到"低表达"
					return GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Low) >= 1;

				case 3:
					// 3阶（强化者）：至少2个基因达到"低表达"，或1个基因达到"中表达"
					return GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Low) >= 2 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Medium) >= 1;

				case 4:
					// 4阶（突变者）：至少3个基因达到"低表达"，或2个基因达到"中表达"，或1个基因达到"高表达"
					return GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Low) >= 3 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Medium) >= 2 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 1;

				// === 中境界（5-8阶）：关注染色体整体表达 ===
				case 5:
					// 5阶（掌控者）：至少1条染色体有2个以上基因激活（染色体初步表达）
					return GetChromosomeCountWithMinGenes(actor, 2) >= 1;

				case 6:
					// 6阶（场域者）：至少1条染色体有3个以上基因激活（染色体完整表达）
					return GetChromosomeCountWithMinGenes(actor, 3) >= 1;

				case 7:
					// 7阶（具象者）：至少2条染色体有3个以上基因激活
					return GetChromosomeCountWithMinGenes(actor, 3) >= 2;

				case 8:
					// 8阶（干涉者）：至少3条染色体有3个以上基因激活，或1条染色体有"高表达"基因
					return GetChromosomeCountWithMinGenes(actor, 3) >= 3 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 1;

				// === 高境界（9-12阶）：关注多染色体协同表达 ===
				case 9:
					// 9阶（解析者）：至少4条染色体有3个以上基因激活，或2条染色体有"高表达"基因
					return GetChromosomeCountWithMinGenes(actor, 3) >= 4 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 2;

				case 10:
					// 10阶（使徒级）：至少5条染色体有3个以上基因激活，或3条染色体有"高表达"基因
					return GetChromosomeCountWithMinGenes(actor, 3) >= 5 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 3;

				case 11:
					// 11阶（登神级）：至少6条染色体有3个以上基因激活，或4条染色体有"高表达"基因
					return GetChromosomeCountWithMinGenes(actor, 3) >= 6 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 4;

				case 12:
					// 12阶（真神）：至少7条染色体有3个以上基因激活，或5条染色体有"高表达"基因，或1个基因达到"过表达"
					return GetChromosomeCountWithMinGenes(actor, 3) >= 7 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 5 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Over) >= 1;

				case 13:
					// 13阶（超神级）：全部8条染色体有3个以上基因激活，或6条染色体有"高表达"基因，或2个基因达到"过表达"，或1个基因达到"突变"
					return GetChromosomeCountWithMinGenes(actor, 3) >= 8 ||
						   GetChromosomeCountWithMinLevel(actor, GeneExpressionSystem.ExpressionLevel.High) >= 6 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Over) >= 2 ||
						   GeneExpressionSystem.GetTotalGeneCountAtLevel(actor, GeneExpressionSystem.ExpressionLevel.Mutant) >= 1;

				default:
					return false;
			}
		}

		/// <summary>
		/// 获取有至少指定数量基因激活的染色体数量
		/// </summary>
		private static int GetChromosomeCountWithMinGenes(Actor actor, int minGeneCount)
		{
			int count = 0;
			for (int i = 0; i < 8; i++)
			{
				if (GeneExpressionSystem.GetActivatedGeneCountInChromosome(actor, i) >= minGeneCount)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>
		/// 获取有至少指定表达层级基因的染色体数量
		/// </summary>
		private static int GetChromosomeCountWithMinLevel(Actor actor, GeneExpressionSystem.ExpressionLevel minLevel)
		{
			int count = 0;
			for (int i = 0; i < 8; i++)
			{
				if (GeneExpressionSystem.GetGeneCountAtLevelInChromosome(actor, i, minLevel) >= 1)
				{
					count++;
				}
			}
			return count;
		}

	}
}
