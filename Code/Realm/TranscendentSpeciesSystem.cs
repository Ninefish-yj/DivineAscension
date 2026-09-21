// ============================================================

using System;
using Code.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Code.Core;
using Code.Traits;
using Code.Data;
using HarmonyLib;
using UnityEngine;

namespace Code.Realm
{
    public static class TranscendentSpeciesSystem
    {
        public const string TRAIT_SPECIES_TRANSCENDENT = "ds_species_transcendent";
        public const string TRAIT_PERFECT_LIFE = "ds_perfect_life";

        private static bool _registered = false;

        // 方碑进化期间阻止外形改变的标志位
        public static bool _blockAppearanceChange = false;

        // 不好的基因（WorldBox 常见负面特质）：受黑色方碑影响后优化基因时剔除
        // 注意：只剔"基因"类负面特质；突变特质（出生/成长突变池）不在列表内
        // 修炼前受到的影响（已有突变）保留；4阶起免疫新的突变（由Patch保证）

        public static void RegisterTraits()
        {
            if (_registered) return;
            _registered = true;
            try
            {
                var group = new ActorTraitGroupAsset();
                group.id = "ds_transcendent_group";
                group.name = "trait_group_ds_transcendent_group";
                group.color = "#4A4A4A";
                AssetManager.trait_groups.add(group);

                var species = new ActorTrait
                {
                    id = TRAIT_SPECIES_TRANSCENDENT,
                    group_id = "ds_transcendent_group",
                    can_be_removed = false,
                    can_be_given = false,
                    needs_to_be_explored = false,
                    has_locales = true,
                    has_description_1 = true,
                    base_stats = new BaseStats()
                    {
                        ["health"] = 600, ["damage"] = 35, ["speed"] = 1.5f, ["mana"] = 300,
                        ["stamina"] = 120, ["intelligence"] = 18, ["armor"] = 30, ["warfare"] = 10
                    },
                    path_icon = "trait/ds_species_transcendent"
                };
                AssetManager.traits.add(species);

                var perfectLife = new ActorTrait
                {
                    id = TRAIT_PERFECT_LIFE,
                    group_id = "ds_transcendent_group",
                    can_be_removed = false,          // 不可被去除
                    can_be_given = false,
                    needs_to_be_explored = false,
                    has_locales = true,
                    has_description_1 = true,
                    is_mutation_box_allowed = true,  // 归入原版UILocalization.Get("species_mutation")分类（突变箱）
                    base_stats = new BaseStats()
                    {
                        ["health"] = 300, ["damage"] = 20, ["speed"] = 1.0f, ["mana"] = 150,
                        ["stamina"] = 60, ["intelligence"] = 10, ["armor"] = 15
                    },
                    path_icon = "trait/ds_perfect_life"
                };
                AssetManager.traits.add(perfectLife);
                // 放入原版突变箱池（linkAssets阶段可能已过，手动确保进入突变分类）
                if (!AssetManager.traits.pot_traits_mutation_box.Contains(perfectLife))
                {
                    AssetManager.traits.pot_traits_mutation_box.Add(perfectLife);
                }

                DSDebug.Verbose("[DivineAscension] 登神种族特质注册完成（独立种族 + 完美生命）");
            }
            catch (Exception e)
            {
                DSDebug.Error("[DivineAscension] 登神种族特质注册失败: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// 登神级（11阶）单位面板：种族名位置显示其前缀称号（元素名号）
        /// "脱离原种族成为独立种族"的身份标记，种族名即其名号（如"绝对意志"）。
        /// </summary>
        [HarmonyPatch(typeof(UnitWindow), "checkMainTabTitle")]
        public static class TranscendentSpeciesNamePatch
        {
            static void Postfix(UnitWindow __instance)
            {
                try
                {
                    if (__instance == null) return;
                    Actor actor = Traverse.Create(__instance).Property("actor").GetValue<Actor>();
                    if (actor == null || !actor.hasTrait(TRAIT_SPECIES_TRANSCENDENT)) return;
                    string title = TitleSuffixManager.GetElementTitle(actor);
                    if (string.IsNullOrEmpty(title)) return;
                    LocalizedText titleText = Traverse.Create(__instance).Field("_main_tab_title").GetValue<LocalizedText>();
                    if (titleText == null) return;
                    // 运行时 Assembly-CSharp 未 publicize：LocalizedText.text 字段不可直接访问，用反射
                    var textField = typeof(LocalizedText).GetField("text",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (textField == null) return;
                    var textComp = textField.GetValue(titleText) as UnityEngine.UI.Text;
                    if (textComp == null) return;
                    textComp.text = title;
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] RegisterTraits 异常: " + dsEx.Message); }
            }
        }

        public static bool IsTranscendent(Actor actor)
        {
            return actor != null && actor.hasTrait(TRAIT_SPECIES_TRANSCENDENT);
        }

        /// <summary>是否免疫新的突变特质：突破4阶（突变者）起生效，直到13阶</summary>
        public static bool IsMutationImmune(Actor actor)
        {
            return actor != null && CultivationData.GetRealmTier(actor) >= 4;
        }

        /// <summary>
        /// 黑色方碑影响 + 随机赋予好特质：突破4阶（突变者）时执行一次。
        /// 顺序：先被黑色方碑的效果影响（创建新亚种，基于原亚种基因复制，外形保持不变），
        /// 再随机赋予这个亚种好的亚种特质（突变特质不在此列）。
        /// 不再对原版基因下手（不做基因优化、不剔除坏基因）。
        /// 从4阶起免疫新的突变特质（由下方Harmony Patch保证）。
        /// </summary>
        /// <summary>
        /// 黑色方碑影响：突破4阶（突变者）时执行一次。
        /// 只在单位位置播放原版方碑辐射特效（以单位为中心释放），不做其他修改。
        /// 从4阶起免疫新的突变特质（由下方Harmony Patch保证）。
        /// </summary>
        /// <summary>
        /// 黑色方碑影响：突破4阶（突变者）时执行一次。
        /// 播放方碑辐射特效 + 调用原版方碑突变机制（让单位真正受到方碑辐射影响）。
        /// 从4阶起免疫新的突变特质（由下方Harmony Patch保证）。
        /// </summary>
        public static void ApplyMonolithEvolution(Actor actor)
        {
            if (actor == null)
            {
                DSDebug.Warning("[DivineAscension] ApplyMonolithEvolution: actor为null");
                return;
            }
            // 开关检查：关了就不执行方碑效果
            if (!Code.Core.DSConfigManager.EnableMonolithEffect) return;
            
            if (actor.hasTrait(TraitManager.STATUS_GENE_MUTATION))
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 已有基因突变者特质（方碑标记），跳过");
                return;
            }
            try
            {
                // 4阶专属：调用原版黑色方碑进化机制（真正的基因优化/新亚种）
                bool success = ActionLibrary.tryToEvolveUnitViaMonolith(actor);
                
                // 方碑成功了才加基因突变者特质（标记方碑影响成功）
                if (success)
                {
                    actor.addTrait(TraitManager.STATUS_GENE_MUTATION, false);
                    DSDebug.Verbose($"[DivineAscension]  {actor.getName()} 方碑进化成功，获得基因突变者特质");
                }
                else
                {
                    DSDebug.Verbose($"[DivineAscension]  {actor.getName()} 方碑进化失败，不加基因突变者特质");
                }
            }
            catch (Exception e)
            {
                DSDebug.Error("[DivineAscension]  添加基因突变者特质失败: " + e.Message);
            }
        }

        /// <summary>
        /// 登神升华：突破11阶（登神级）时执行。
        /// 脱离原种族成为独立种族（身份标记），并授予不可去除的基因特质【完美生命】（归入原版突变分类）。
        /// 基因序列协调效应：扫描单位基因带来的属性，翻倍后由完美生命特质提供（替代方案）。
        /// 突变免疫已在4阶生效。
        /// </summary>
        public static void ApplyTranscendence(Actor actor)
        {
            if (actor == null) return;
            RegisterTraits();
            try
            {
                if (actor.hasTrait(TRAIT_SPECIES_TRANSCENDENT)) return; // 已升华，不重复

                actor.addTrait(TRAIT_SPECIES_TRANSCENDENT, false);
                // 【完美生命】主授予在11阶登神级：基因完美的标志（突变分类、不可去除、入突变箱池）
                if (!actor.hasTrait(TRAIT_PERFECT_LIFE))
                {
                    actor.addTrait(TRAIT_PERFECT_LIFE, false);
                }

                //  脱离原种族：为登神级创建独立亚种（避免多个登神级共享原种族亚种对象导致种族名反复覆盖）
                string elementTitle = Code.Realm.TitleSuffixManager.GetElementTitle(actor);
                string baseName = !string.IsNullOrEmpty(elementTitle) ? elementTitle : actor.getName();
                string subspeciesName = string.Format(UILocalization.Get("species_descendant"), baseName);
                DetachSubspecies(actor, subspeciesName);

                //  基因序列协调效应（替代方案）：扫描单位基因带来的属性，翻倍后由完美生命特质提供
                string geneticBoost = ScanAndApplyGeneticBoost(actor);

                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 突破登神级！脱离原种族成为独立种族（亚种名：{subspeciesName}），授予【完美生命】基因特质（突变分类、不可去除）。基因序列协调效应：{geneticBoost}");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 登神升华失败: " + e.Message);
            }
        }

        /// <summary>
        /// 为登神级创建独立亚种并设置：复制原亚种的基因与特质（外观不变），
        /// 改名为"前缀称号+之裔"，从原亚种单位列表移除，单位切换到新亚种。
        /// 参考 PowerBox SubspeciesDuplicationPower / 西幻世界 的独立亚种做法，
        /// 避免直接修改共享亚种对象导致多个登神级互相覆盖种族名。
        /// </summary>
        private static void DetachSubspecies(Actor actor, string subspeciesName)
        {
            try
            {
                Subspecies oldSpecies = actor.subspecies;
                Subspecies newSpecies = World.world.subspecies.newSpecies(actor.asset, actor.current_tile);
                if (newSpecies == null)
                {
                    DSDebug.Warning($"[DivineAscension] {actor.getName()} 创建独立亚种失败（newSpecies 返回 null）");
                    return;
                }

                if (oldSpecies != null)
                {
                    // 复制原亚种特质（突变特质/出生特质），保留单位原有基因构成
                    // 注意：_traits/_actor_birth_traits 是私有字段，运行时直接访问会抛 FieldAccessException
                    // （编译引用 publicized 程序集能过，运行时原版拒绝），改用公开方法 getTraits()/getActorBirthTraits()
                    foreach (SubspeciesTrait trait in oldSpecies.getTraits()) newSpecies.addTrait(trait);
                    newSpecies.nucleus.cloneFrom(oldSpecies.nucleus);
                    SubspeciesActorBirthTraits newBirthTraits = newSpecies.getActorBirthTraits();
                    SubspeciesActorBirthTraits oldBirthTraits = oldSpecies.getActorBirthTraits();
                    newBirthTraits.reset();
                    foreach (ActorTrait bTrait in oldBirthTraits.getTraits()) newBirthTraits.addTrait(bTrait);
                    // 从原亚种单位列表移除（不再属于原种族）
                    oldSpecies.units.Remove(actor);
                }

                // 写新亚种名（反射，兼容运行时访问权限）
                var nameField = newSpecies.GetType().GetField("name",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nameField != null)
                {
                    nameField.SetValue(newSpecies, subspeciesName);
                }
                else
                {
                    var setNameMethod = newSpecies.GetType().GetMethod("set_name",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (setNameMethod != null)
                    {
                        setNameMethod.Invoke(newSpecies, new object[] { subspeciesName });
                    }
                }
                var nameLocField = newSpecies.GetType().GetField("name_localized",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nameLocField != null)
                {
                    var locText = nameLocField.GetValue(newSpecies);
                    if (locText != null)
                    {
                        var textField = locText.GetType().GetField("text",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (textField != null) textField.SetValue(locText, subspeciesName);
                    }
                }

                actor.setSubspecies(newSpecies);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 亚种名已改为：{subspeciesName}");
            }
            catch (Exception ex)
            {
                DSDebug.Warning($"[DivineAscension] 创建独立亚种失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 基因序列协调效应（替代方案）：扫描单位基因和特质带来的属性加成，
        /// 计算总属性后翻倍，存储到custom_data，并返回属性摘要字符串。
        /// 使用反射安全访问BaseStats（不是字典，是有固定属性的类）。
        /// </summary>
        private static string ScanAndApplyGeneticBoost(Actor actor)
        {
            try
            {
                var totalStats = new System.Collections.Generic.Dictionary<string, float>();
                string[] statKeys = { "health", "damage", "speed", "mana", "stamina", "intelligence", "armor", "warfare" };

                //  辅助方法：用反射从BaseStats获取属性值（BaseStats不是字典，是固定属性类）
                System.Func<object, string, float> getStatValue = (stats, key) =>
                {
                    try
                    {
                        if (stats == null) return 0f;
                        var prop = stats.GetType().GetProperty(key,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (prop != null)
                        {
                            object val = prop.GetValue(stats);
                            if (val != null) return System.Convert.ToSingle(val);
                        }
                        var field = stats.GetType().GetField(key,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            object val = field.GetValue(stats);
                            if (val != null) return System.Convert.ToSingle(val);
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ScanAndApplyGeneticBoost 异常: " + dsEx.Message); }
                    return 0f;
                };

                //  辅助方法：累加BaseStats的所有属性到totalStats
                System.Action<object> addBaseStats = (stats) =>
                {
                    if (stats == null) return;
                    foreach (string key in statKeys)
                    {
                        float val = getStatValue(stats, key);
                        if (val != 0f)
                        {
                            if (!totalStats.ContainsKey(key)) totalStats[key] = 0;
                            totalStats[key] += val;
                        }
                    }
                };

                // 扫描特质属性（用反射获取特质列表，兼容traits/saved_traits等不同字段名）
                try
                {
                    var traitListField = ActorDataAccessor.GetData(actor).GetType().GetField("saved_traits",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (traitListField == null)
                    {
                        traitListField = ActorDataAccessor.GetData(actor).GetType().GetField("traits",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                    if (traitListField != null)
                    {
                        var traitList = traitListField.GetValue(ActorDataAccessor.GetData(actor)) as System.Collections.IEnumerable;
                        if (traitList != null)
                        {
                            foreach (var traitIdObj in traitList)
                            {
                                try
                                {
                                    string traitId = traitIdObj?.ToString();
                                    if (string.IsNullOrEmpty(traitId)) continue;
                                    ActorTrait trait = AssetManager.traits.get(traitId);
                                    if (trait != null && trait.base_stats != null)
                                    {
                                        addBaseStats(trait.base_stats);
                                    }
                                }
                                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ScanAndApplyGeneticBoost 异常: " + dsEx.Message); }
                            }
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ScanAndApplyGeneticBoost 异常: " + dsEx.Message); }

                // 扫描基因属性（亚种 nucleus 中的基因）
                try
                {
                    var nucleus = actor.subspecies?.nucleus;
                    if (nucleus != null && nucleus.chromosomes != null)
                    {
                        foreach (var chr in nucleus.chromosomes)
                        {
                            if (chr.genes != null)
                            {
                                foreach (var g in chr.genes)
                                {
                                    GeneAsset gene = g as GeneAsset;
                                    if (gene != null && gene.base_stats != null)
                                    {
                                        addBaseStats(gene.base_stats);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ScanAndApplyGeneticBoost 异常: " + dsEx.Message); }

                // 计算翻倍后的属性（基因序列协调效应 = 基因属性×2）
                var boostStats = new System.Collections.Generic.Dictionary<string, float>();
                foreach (var kv in totalStats)
                {
                    boostStats[kv.Key] = kv.Value * 2f; // 翻倍
                }

                // 构建属性摘要字符串
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append(UILocalization.Get("species_gene_scan_prefix"));
                bool first = true;
                foreach (string key in statKeys)
                {
                    if (boostStats.ContainsKey(key) && boostStats[key] > 0)
                    {
                        if (!first) sb.Append(", ");
                        sb.Append($"{key}+{boostStats[key]:0}");
                        first = false;
                    }
                }
                sb.Append(UILocalization.Get("species_gene_scan_suffix"));

                // 存储到 custom_data，供UI显示
                try
                {
                    CultivationData.EnsureCustomDataInitialized(actor);
                    if (ActorDataAccessor.GetData(actor).custom_data_string != null)
                    {
                        ActorDataAccessor.GetData(actor).custom_data_string["ds_genetic_boost"] = sb.ToString();
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ScanAndApplyGeneticBoost 异常: " + dsEx.Message); }

                return sb.ToString();
            }
            catch (Exception e)
            {
                return $"基因属性扫描失败: {e.Message}";
            }
        }
    }

    // ============ 突变免疫：4阶（突变者）起不再受到新的突变特质影响（修炼前已有的保留） ============
    [HarmonyPatch(typeof(Actor), "checkTraitMutationGrowUp")]
    public static class TranscendentMutationGrowUpPatch
    {
        static bool Prefix(Actor __instance)
        {
            return !TranscendentSpeciesSystem.IsMutationImmune(__instance);
        }
    }

    [HarmonyPatch(typeof(Actor), "checkTraitMutationOnBirth")]
    public static class TranscendentMutationBirthPatch
    {
        static bool Prefix(Actor __instance)
        {
            return !TranscendentSpeciesSystem.IsMutationImmune(__instance);
        }
    }

}

