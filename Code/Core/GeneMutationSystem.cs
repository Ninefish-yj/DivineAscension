using System;
using Code;
using System.Collections.Generic;
using UnityEngine;
using Code.Realm;
using Code.Data;

namespace Code.Core
{
	/// <summary>
	/// 基因突变系统 - 完全重构
	/// 
	/// 核心机制：
	/// 1. 基因突变是随机发生的，不是完成任务获得
	/// 2. 突破境界时大概率发生基因突变（消耗能量）
	/// 3. 战斗/修炼中小概率发生基因突变（消耗寿命）
	/// 4. 基因突变可以激活新基因，或提升已有基因的表达层级
	/// 5. 基因表达层级越高，突变概率越低，但效果越强
	/// </summary>
	public static class GeneMutationSystem
	{
		/// <summary>
		/// 突破境界时的基因突变
		/// 大概率发生，消耗能量
		/// </summary>
		public static void MutateOnBreakthrough(Actor actor, int newTier)
		{
			if (actor == null || !actor.isAlive()) return;
			if (!CultivationData.IsAscended(actor)) return;

			// 突破境界时基因突变概率：境界越高，概率越高
			float mutationChance = Mathf.Clamp(0.3f + newTier * 0.05f, 0.3f, 0.8f);
			
			// GE基因解锁加成（基于基因能量，上限+20%）
			float geUnlockBonus = GeneEnergyCalculator.GetElementUnlockBonus(actor);
			// 纪元基因解锁加成（直接读取纪元效果，如信息纪元+15%）
			float eraUnlockBonus = GeneEnergyCalculator.GetEraElementUnlockBonus();
			// 总突变概率 = 基础概率 × (1 + GE加成 + 纪元加成)
			mutationChance = Mathf.Clamp(mutationChance * (1f + geUnlockBonus + eraUnlockBonus), 0.3f, 0.95f);
			
			if (UnityEngine.Random.value > mutationChance)
			{
				DSDebug.Verbose($"[基因突变] {actor.getName()} 突破到{newTier}阶时未发生基因突变（概率{mutationChance:F0}%）");
				return;
			}

			// 消耗能量
			float energyCost = 10f + newTier * 5f;
			float currentEnergy = CultivationData.GetEnergy(actor);
			CultivationData.SetEnergy(actor, Mathf.Max(0f, currentEnergy - energyCost));

			// 发生基因突变
			int mutationCount = UnityEngine.Random.Range(1, Mathf.Min(3, newTier / 2 + 1));
			List<string> mutatedGenes = new List<string>();

			for (int i = 0; i < mutationCount; i++)
			{
				// 50%概率激活新基因，50%概率提升已有基因表达层级
				if (UnityEngine.Random.value < 0.5f)
				{
					string geneId = SelectRandomNewGene(actor, newTier);
					if (geneId != null && !mutatedGenes.Contains(geneId))
					{
						if (CultivationData.UnlockElement(actor, geneId))
						{
							// 新激活的基因默认低表达
							GeneExpressionSystem.SetExpressionLevel(actor, geneId, GeneExpressionSystem.ExpressionLevel.Low);
							mutatedGenes.Add(geneId);
							var gene = ElementDef.GetById(geneId);
							DSDebug.Verbose($"[基因突变] {actor.getName()} 突破突变激活新基因: {gene?.NameZh}({geneId})");
						}
					}
				}
				else
				{
					// 提升已有基因的表达层级
					string geneId = SelectRandomGeneForUpgrade(actor);
					if (geneId != null && !mutatedGenes.Contains(geneId))
					{
						if (GeneExpressionSystem.UpgradeExpressionLevel(actor, geneId))
						{
							mutatedGenes.Add(geneId);
							var gene = ElementDef.GetById(geneId);
							DSDebug.Verbose($"[基因突变] {actor.getName()} 突破突变提升基因表达: {gene?.NameZh}({geneId})");
						}
					}
				}
			}

			if (mutatedGenes.Count > 0)
			{
				DSDebug.Verbose($"[基因突变] {actor.getName()} 突破到{newTier}阶时发生基因突变，影响{mutatedGenes.Count}个基因，消耗能量{energyCost:F0}");
				// 检查基因连锁效果
				try { GeneLinkageSystem.CheckAllLinkages(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] MutateOnBreakthrough 异常: " + dsEx.Message); }
			}
		}

		/// <summary>
		/// 年度基因突变（小概率）
		/// 消耗寿命，每年检查一次
		/// </summary>
		public static void AnnualMutationCheck(Actor actor)
		{
			if (actor == null || !actor.isAlive()) return;
			if (!CultivationData.IsAscended(actor)) return;

			// 年度基因突变基础概率：1-3%
			float baseChance = 0.02f;
			
			// 境界越高，概率越高
			int tier = CultivationData.GetRealmTier(actor);
			baseChance += tier * 0.005f;
			
			// 能量越高，概率越高
			float energy = CultivationData.GetEnergy(actor);
			if (energy > 50f) baseChance += 0.01f;
			if (energy > 100f) baseChance += 0.01f;
			
			// 污染越高，概率越高（但可能是恶性突变）
			float turbulence = CultivationData.GetTurbulence(actor);
			if (turbulence > 50f) baseChance += 0.02f;
			
			baseChance = Mathf.Clamp(baseChance, 0.01f, 0.1f);
			
			// GE基因解锁加成（基于基因能量，上限+20%）
			float geUnlockBonus = GeneEnergyCalculator.GetElementUnlockBonus(actor);
			// 纪元基因解锁加成（直接读取纪元效果，如信息纪元+15%）
			float eraUnlockBonus = GeneEnergyCalculator.GetEraElementUnlockBonus();
			// 总突变概率 = 基础概率 × (1 + GE加成 + 纪元加成)
			baseChance = Mathf.Clamp(baseChance * (1f + geUnlockBonus + eraUnlockBonus), 0.01f, 0.2f);

			if (UnityEngine.Random.value > baseChance) return;

			// 消耗寿命（通过反射修改actorData的lifespan字段）
			float lifespanCost = UnityEngine.Random.Range(1f, 5f);
			try
			{
				var actorData = Code.Data.ActorDataAccessor.GetData(actor);
				if (actorData != null)
				{
					var lifespanField = actorData.GetType().GetField("lifespan", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					if (lifespanField != null)
					{
						float currentLifespan = (float)lifespanField.GetValue(actorData);
						lifespanField.SetValue(actorData, Mathf.Max(1f, currentLifespan - lifespanCost));
					}
				}
			}
			catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] AnnualMutationCheck 异常: " + dsEx.Message); }

			// 发生基因突变
			// 50%概率激活新基因，50%概率提升已有基因表达层级
			if (UnityEngine.Random.value < 0.5f)
			{
				string geneId = SelectRandomNewGene(actor, tier);
				if (geneId != null)
				{
					if (CultivationData.UnlockElement(actor, geneId))
					{
						GeneExpressionSystem.SetExpressionLevel(actor, geneId, GeneExpressionSystem.ExpressionLevel.Low);
						var gene = ElementDef.GetById(geneId);
						DSDebug.Verbose($"[基因突变] {actor.getName()} 年度突变激活新基因: {gene?.NameZh}({geneId})，消耗寿命{lifespanCost:F1}年");
						try { GeneLinkageSystem.CheckAllLinkages(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] AnnualMutationCheck 异常: " + dsEx.Message); }
					}
				}
			}
			else
			{
				string geneId = SelectRandomGeneForUpgrade(actor);
				if (geneId != null)
				{
					if (GeneExpressionSystem.UpgradeExpressionLevel(actor, geneId))
					{
						var gene = ElementDef.GetById(geneId);
						DSDebug.Verbose($"[基因突变] {actor.getName()} 年度突变提升基因表达: {gene?.NameZh}({geneId})，消耗寿命{lifespanCost:F1}年");
						try { GeneLinkageSystem.CheckAllLinkages(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] AnnualMutationCheck 异常: " + dsEx.Message); }
					}
				}
			}
		}

		/// <summary>
		/// 战斗时基因突变（小概率）
		/// </summary>
		public static void CombatMutationCheck(Actor actor)
		{
			if (actor == null || !actor.isAlive()) return;
			if (!CultivationData.IsAscended(actor)) return;

			// 战斗时基因突变概率：0.5-2%
			float mutationChance = 0.01f;
			
			// 生命越低，概率越高（生死关头激发潜能）
			float healthRatio = actor.getHealth() / actor.getMaxHealth();
			if (healthRatio < 0.3f) mutationChance += 0.02f;
			else if (healthRatio < 0.5f) mutationChance += 0.01f;
			
			// 境界越高，概率越高
			int tier = CultivationData.GetRealmTier(actor);
			mutationChance += tier * 0.002f;
			
			mutationChance = Mathf.Clamp(mutationChance, 0.005f, 0.05f);
			
			// GE基因解锁加成（基于基因能量，上限+20%）
			float geUnlockBonus = GeneEnergyCalculator.GetElementUnlockBonus(actor);
			// 纪元基因解锁加成（直接读取纪元效果，如信息纪元+15%）
			float eraUnlockBonus = GeneEnergyCalculator.GetEraElementUnlockBonus();
			// 总突变概率 = 基础概率 × (1 + GE加成 + 纪元加成)
			mutationChance = Mathf.Clamp(mutationChance * (1f + geUnlockBonus + eraUnlockBonus), 0.005f, 0.1f);

			if (UnityEngine.Random.value > mutationChance) return;

			// 发生基因突变（战斗中只提升表达层级，不激活新基因）
			string geneId = SelectRandomGeneForUpgrade(actor);
			if (geneId != null)
			{
				if (GeneExpressionSystem.UpgradeExpressionLevel(actor, geneId))
				{
					var gene = ElementDef.GetById(geneId);
					DSDebug.Verbose($"[基因突变] {actor.getName()} 战斗中突变提升基因表达: {gene?.NameZh}({geneId})");
					try { GeneLinkageSystem.CheckAllLinkages(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] CombatMutationCheck 异常: " + dsEx.Message); }
				}
			}
		}

		/// <summary>
		/// 选择随机新基因进行激活
		/// 基因表达层级越低，被选中的概率越高
		/// </summary>
		private static string SelectRandomNewGene(Actor actor, int tier)
		{
			var unlockedElements = CultivationData.GetUnlockedElements(actor);
			var allElements = ElementDef.AllElements;
			if (allElements == null || allElements.Count == 0) return null;

			// 收集未解锁的基因
			List<ElementDef.ElementInfo> availableGenes = new List<ElementDef.ElementInfo>();
			foreach (var elem in allElements)
			{
				if (elem == null) continue;
				if (unlockedElements.Contains(elem.Id)) continue;
				if (elem.Period >= 7) continue; // 本源表达层无法通过突变获得
				// 境界限制：高表达层级的基因需要高境界才能突变
				if (elem.Period > tier + 1) continue;
				availableGenes.Add(elem);
			}

			if (availableGenes.Count == 0) return null;

			// 按表达层级加权随机选择：层级越低，权重越高
			List<float> weights = new List<float>();
			float totalWeight = 0f;
			foreach (var gene in availableGenes)
			{
				float weight = Mathf.Pow(0.7f, gene.Period - 1); // 第1层权重1，第2层0.7，第3层0.49...
				weights.Add(weight);
				totalWeight += weight;
			}

			// 加权随机选择
			float randomValue = UnityEngine.Random.value * totalWeight;
			float cumulativeWeight = 0f;
			for (int i = 0; i < availableGenes.Count; i++)
			{
				cumulativeWeight += weights[i];
				if (randomValue <= cumulativeWeight)
				{
					return availableGenes[i].Id;
				}
			}

			return availableGenes[availableGenes.Count - 1].Id;
		}

		/// <summary>
		/// 选择随机已有基因进行表达层级提升
		/// 表达层级越低，被选中的概率越高
		/// </summary>
		private static string SelectRandomGeneForUpgrade(Actor actor)
		{
			var unlockedElements = CultivationData.GetUnlockedElements(actor);
			if (unlockedElements == null || unlockedElements.Count == 0) return null;

			// 收集可以提升表达层级的基因（未达到最高层级）
			List<string> upgradeableGenes = new List<string>();
			List<float> weights = new List<float>();
			float totalWeight = 0f;

			foreach (var geneId in unlockedElements)
			{
				GeneExpressionSystem.ExpressionLevel level = GeneExpressionSystem.GetExpressionLevel(actor, geneId);
				if (level >= GeneExpressionSystem.ExpressionLevel.Mutant) continue; // 已经是最高层级
				
				// 表达层级越低，提升概率越高
				float weight = Mathf.Pow(0.6f, (int)level); // 低表达权重1，中表达0.6，高表达0.36...
				upgradeableGenes.Add(geneId);
				weights.Add(weight);
				totalWeight += weight;
			}

			if (upgradeableGenes.Count == 0) return null;

			// 加权随机选择
			float randomValue = UnityEngine.Random.value * totalWeight;
			float cumulativeWeight = 0f;
			for (int i = 0; i < upgradeableGenes.Count; i++)
			{
				cumulativeWeight += weights[i];
				if (randomValue <= cumulativeWeight)
				{
					return upgradeableGenes[i];
				}
			}

			return upgradeableGenes[upgradeableGenes.Count - 1];
		}
	}
}
