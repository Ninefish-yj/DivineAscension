// ============================================================
using System;
using Code.UI;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Code.Data;
using Code.Core;
namespace Code.UI
{
    /// <summary>
    /// 在单位窗口显示异能相关信息（Postfix：排在原生行之后，不影响其他模组）
    /// </summary>
    [HarmonyPatch(typeof(UnitWindow), "showStatsRows")]
    public static class UnitWindowIntegration
    {
        [HarmonyPostfix]
        public static void Postfix(UnitWindow __instance)
        {
            try
            {
                Actor actor = null;
                try
                {
                    var actorField = typeof(UnitWindow).GetField("actor",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (actorField != null)
                    {
                        actor = actorField.GetValue(__instance) as Actor;
                    }
                }
                catch { }
                if (actor == null) actor = __instance.GetActor();
                if (actor == null || !actor.isAlive())
                {
                    return;
                }
                int tier = Data.CultivationData.GetRealmTier(actor);
                // 行1：异能境界（凡人显示"凡人"）
                string realmText = BuildRealmText(actor, tier);
                ShowStatRow(__instance, UILocalization.Get("unit_realm"), realmText,
                    null, MetaType.None, -1L, false, null, null, null, false);
                // 行1.2：登神级种族（11阶以上脱离原种族成为独立种族，种族名=前缀称号+之裔）
                if (Code.Realm.TranscendentSpeciesSystem.IsTranscendent(actor))
                {
                    string title = Code.Realm.TitleSuffixManager.GetElementTitle(actor);
                    string speciesName = !string.IsNullOrEmpty(title) ? title + UILocalization.Get("unit_descendant_suffix") : UILocalization.Get("unit_species_transcendent");
                    ShowStatRow(__instance, UILocalization.Get("unit_species"), speciesName,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行2：异能能量
                float qi = Data.CultivationData.GetEnergy(actor);
                float nextThreshold = GetNextRealmThreshold(tier);
                string qiText = string.Format("{0:F0} / {1:F0}", qi, nextThreshold);
                ShowStatRow(__instance, UILocalization.Get("unit_qi"), qiText,
                    null, MetaType.None, -1L, false, null, null, null, false);
                // 行3：失控指数
                float turb = Data.CultivationData.GetTurbulence(actor);
                string turbStatus = turb >= 70 ? UILocalization.Get("turb_critical") : turb >= 50 ? UILocalization.Get("turb_warning") : UILocalization.Get("turb_stable");
                string turbText = string.Format("{0:F1}/100 {1}", turb, turbStatus);
                ShowStatRow(__instance, UILocalization.Get("unit_turbulence"), turbText,
                    null, MetaType.None, -1L, false, null, null, null, false);
                // 行3.5：能力掌控度（现代异能体系，替代印记系统）
                if (tier < 13)
                {
                    float mastery = Data.CultivationData.GetMastery(actor);
                    string masteryStatus = mastery >= 100 ? UILocalization.Get("mastery_complete") : mastery >= 70 ? UILocalization.Get("mastery_high") : mastery >= 30 ? UILocalization.Get("mastery_medium") : UILocalization.Get("mastery_low");
                    string masteryText = string.Format("{0:F1}/100 {1}", mastery, masteryStatus);
                    ShowStatRow(__instance, UILocalization.Get("unit_mastery"), masteryText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行3.8：基因图谱（点击打开基因图谱弹窗，凡人显示未觉醒）
                if (tier <= 13)
                {
                    string geneText = BuildGeneGraphText(actor, tier);
                    if (!string.IsNullOrEmpty(geneText))
                    {
                        ShowStatRow(__instance, UILocalization.Get("unit_gene_graph"), geneText,
                            null, MetaType.None, -1L, false, null, null, null, false);
                    }
                }
                // 行4：状态效果（深度觉醒、能力失控等，这些会同时显示在游戏原生状态栏中）
                string statusText = BuildStatusText(actor);
                if (!string.IsNullOrEmpty(statusText))
                {
                    ShowStatRow(__instance, UILocalization.Get("unit_ability_status"), statusText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行5：境界专属技能（显示当前境界解锁的技能）
                string abilityText = BuildAbilityText(actor, tier);
                if (!string.IsNullOrEmpty(abilityText))
                {
                    ShowStatRow(__instance, UILocalization.Get("unit_abilities"), abilityText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行5.5：组织信息（所属组织 + 职位）
                string sectText = BuildSectText(actor);
                if (!string.IsNullOrEmpty(sectText))
                {
                    ShowStatRow(__instance, UILocalization.Get("unit_sect"), sectText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行5.8：深度觉醒经验
                if (tier > 0 && tier < 13)
                {
                    float enlightExp = Data.CultivationData.GetEnlightenmentExp(actor);
                    float enlightNeeded = 500f * (tier + 1);
                    string enlightText = string.Format("{0:F0} / {1:F0}", enlightExp, enlightNeeded);
                    ShowStatRow(__instance, UILocalization.Get("unit_enlightenment_exp"), enlightText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 行6：称号信息（如果有）
                string titleText = BuildTitleText(actor, tier);
                if (!string.IsNullOrEmpty(titleText))
                {
                    ShowStatRow(__instance, UILocalization.Get("unit_title"), titleText,
                        null, MetaType.None, -1L, false, null, null, null, false);
                }
                // 按钮：打开基因图谱弹窗（所有单位都显示，包括凡人）
                // 包裹在try-catch中，防止在非OnGUI方法中调用GUI函数报错
                try
                {
                    if (Event.current != null)
                    {
                        GUILayout.Space(4);
                        if (GUILayout.Button(UILocalization.Get("unit_gene_detail"), GUILayout.Height(28)))
                        {
                            GeneGraphWindow.Show(actor);
                        }
                    }
                }
                catch (Exception guiEx)
                {
                    DSDebug.Verbose("[单位窗口] GUI按钮渲染跳过: " + guiEx.Message);
                }
                // === H-2修复：境界技能释放入口 ===
                // 仅异能者（tier>0）显示技能释放按钮
                // 包裹在try-catch中，防止在非OnGUI方法中调用GUI函数报错
                if (tier > 0)
                {
                    try
                    {
                        if (Event.current != null)
                        {
                            var abilities = Code.Realm.RealmAbilities.GetAbilitiesForActor(actor);
                            if (abilities != null && abilities.Count > 0)
                            {
                                GUILayout.Space(6);
                                GUILayout.Label(UILocalization.Get("unit_abilities") + ":", GUILayout.Height(20));
                                foreach (var ab in abilities)
                                {
                                    bool onCooldown = Code.Realm.RealmAbilities.IsOnCooldown(actor, ab.Id);
                                    float remainingCd = Code.Realm.RealmAbilities.GetRemainingCooldown(actor, ab.Id);
                                    float energy = Data.CultivationData.GetEnergy(actor);
                                    bool canCast = !onCooldown && energy >= ab.EnergyCost && tier >= ab.RequiredTier;
                                    string btnLabel = onCooldown
                                        ? string.Format("{0} ({1}{2})", ab.Name, remainingCd, UILocalization.Get("skill_cooldown_unit"))
                                        : string.Format("{0} ({1}{2})", ab.Name, ab.EnergyCost, UILocalization.Get("skill_energy_unit"));
                                    GUI.enabled = canCast;
                                    if (GUILayout.Button(btnLabel, GUILayout.Height(24)))
                                    {
                                        bool success = Code.Realm.RealmAbilities.UseAbility(actor, ab.Id);
                                        if (success)
                                        {
                                            DSDebug.Verbose(string.Format("[DivineAscension] {0} 释放技能: {1}", actor.getName(), ab.Name));
                                        }
                                    }
                                    GUI.enabled = true;
                                }
                            }
                        }
                    }
                    catch (Exception guiEx)
                    {
                        DSDebug.Verbose("[单位窗口] 技能GUI渲染跳过: " + guiEx.Message);
                    }
                }
                // 建筑按钮已移到神力栏，单位窗口里不再显示
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 单位窗口集成异常: " + e.Message);
            }
        }
        /// <summary>
        /// showStatRow 反射调用：运行时 Assembly-CSharp 未 publicize，直接调用会抛 MethodAccessException。
        /// </summary>
        private static System.Reflection.MethodInfo _showStatRowMethod;
        private static void ShowStatRow(UnitWindow window, string label, object value, string value2, MetaType meta, long id, bool hasTooltip, string tipName, string tipDesc, TooltipDataGetter getter, bool showIfDefault)
        {
            try
            {
                if (_showStatRowMethod == null)
                {
                    // 指定精确参数类型避免Ambiguous match
                    _showStatRowMethod = typeof(UnitWindow).GetMethod("showStatRow",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                        null,
                        new System.Type[] { typeof(string), typeof(object), typeof(string), typeof(MetaType), typeof(long), typeof(bool), typeof(string), typeof(string), typeof(TooltipDataGetter), typeof(bool) },
                        null);
                    if (_showStatRowMethod == null)
                    {
                        // fallback: 不指定参数类型
                        _showStatRowMethod = typeof(UnitWindow).GetMethod("showStatRow",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                }
                if (_showStatRowMethod == null) return;
                _showStatRowMethod.Invoke(window, new object[] { label, value, value2, meta, id, hasTooltip, tipName, tipDesc, getter, showIfDefault });
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] showStatRow 反射调用失败: " + e.Message);
            }
        }
        /// <summary>构建基因图谱显示文本（显示当前境界的基因节点解锁情况）</summary>
        private static string BuildGeneGraphText(Actor actor, int tier)
        {
            try
            {
                // 凡人显示"未觉醒"
                if (tier <= 0) return UILocalization.Get("unit_not_awakened");
                var nodes = Code.Realm.GeneGraphManager.GetNodesByTier(tier);
                if (nodes == null || nodes.Count == 0) return "";
                bool isEnglish = UILocalization.CurrentLanguage == "en";
                List<string> nodeTexts = new List<string>();
                int unlockedCount = 0;
                // 表达层级符号与颜色（与GeneGraphWindow层级体系一致：0沉默 1低 2中 3高 4过表达 5突变）
                string[] levelSymbols = { "", "+", "++", "+++", "\u2605", "\u25C6" };
                string[] levelColors = { "#808080", "#99CC99", "#66CC66", "#33CC33", "#CC9933", "#E64D4D" };
                foreach (var node in nodes)
                {
                    bool unlocked = Data.CultivationData.IsGeneUnlocked(actor, node.Id);
                    if (unlocked) unlockedCount++;
                    string nodeName = isEnglish ? node.NameEn : node.NameZh;
                    if (!unlocked)
                    {
                        nodeTexts.Add(string.Format("<color=#808080>{0}</color>", nodeName));
                        continue;
                    }
                    int exprLevel = Data.CultivationData.GetGeneExpressionLevel(actor, node.Id);
                    string color = levelColors[exprLevel];
                    nodeTexts.Add(string.Format("<color={0}>{1}{2}</color>", color, nodeName, levelSymbols[exprLevel]));
                }
                string progress = string.Format("{0}/{1}", unlockedCount, nodes.Count);
                // 显示所有已解锁基因的总数
                int totalUnlocked = Data.CultivationData.GetUnlockedGenes(actor).Count;
                int totalGenes = Code.ElementDef.Count;
                string totalProgress = string.Format("总基因: {0}/{1}", totalUnlocked, totalGenes);
                // 图例：层级符号说明（中英文）
                string legend = isEnglish
                    ? "<color=#99CC99>+Low</color> <color=#66CC66>++Med</color> <color=#33CC33>+++High</color> <color=#CC9933>\u2605Over</color> <color=#E64D4D>\u25C6Mutant</color>"
                    : "<color=#99CC99>+低</color> <color=#66CC66>++中</color> <color=#33CC33>+++高</color> <color=#CC9933>\u2605过表达</color> <color=#E64D4D>\u25C6突变</color>";
                return totalProgress + "  |  本境界: " + progress + "  " + string.Join(" ", nodeTexts.ToArray()) + "  |  " + legend;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] BuildGeneGraphText异常: " + e.Message);
                return "";
            }
        }
        /// <summary>构建境界技能显示文本（只显示当前境界的技能，不显示所有已解锁技能）</summary>
        private static string BuildAbilityText(Actor actor, int tier)
        {
            try
            {
                // 只获取当前境界的技能，不获取1到tier的所有技能
                var abilities = Code.Realm.RealmAbilities.GetAbilitiesForTier(tier);
                // 过滤出当前境界的技能（RequiredTier == tier）
                List<string> abilityNames = new List<string>();
                foreach (var ab in abilities)
                {
                    // 只显示当前境界的技能
                    if (ab.RequiredTier != tier) continue;
                    bool onCooldown = Code.Realm.RealmAbilities.IsOnCooldown(actor, ab.Id);
                    string status = onCooldown ? "(" + UILocalization.Get("skill_cooldown") + ")" : "(" + UILocalization.Get("skill_ready") + ")";
                    abilityNames.Add(ab.GetName() + status);
                }
                return string.Join(" / ", abilityNames.ToArray());
            }
            catch
            {
                return "";
            }
        }
        /// <summary>构建状态效果文本</summary>
        private static string BuildStatusText(Actor actor)
        {
            try
            {
            List<string> statuses = new List<string>();
            float enlightDur = Data.CultivationData.GetEnlightenmentDuration(actor);
            if (enlightDur > 0f)
            {
                statuses.Add(string.Format(UILocalization.Get("status_enlightenment_fmt"), enlightDur));
            }
            float turb = Data.CultivationData.GetTurbulence(actor);
            if (turb >= 70f)
            {
                statuses.Add(UILocalization.Get("status_outofcontrol"));
            }
            if (Code.Dimension.DimensionRealmManager.IsInDimension(actor))
            {
                statuses.Add(UILocalization.Get("status_projection"));
            }
            return string.Join(" ", statuses.ToArray());
            }
            catch
            {
                return "";
            }
        }
        /// <summary>构建境界显示文本</summary>
        private static string BuildRealmText(Actor actor, int tier)
        {
            string tierName = GetTierName(tier);
            return string.Format(UILocalization.Get("realm_format"), tier, tierName);
        }
        /// <summary>获取下一境界异能能量门槛（统一调用RealmJudge，避免硬编码不一致）</summary>
        private static float GetNextRealmThreshold(int currentTier)
        {
            if (currentTier >= 13) return 0f; // 已满阶无下一阶门槛
            return Code.Realm.RealmJudge.GetQiThreshold(currentTier + 1);
        }
        // tier到特质ID的映射（与TraitManager中的特质ID一致）

        public static string GetTierName(int tier)
        {
            // 直接使用UILocalization.GetTierName（硬编码境界名称），避免双语文件key缺失导致显示ID
            return UILocalization.GetTierName(tier);
        }
        /// <summary>构建组织信息文本（所属组织 + 职位 + 贡献）</summary>
        private static string BuildSectText(Actor actor)
        {
            try
            {
                string sectId = Data.CultivationData.GetSectId(actor);
                if (string.IsNullOrEmpty(sectId)) return "";
                var sect = Sect.SectManager.GetSect(sectId);
                if (sect == null) return "";
                int rank = Data.CultivationData.GetSectRank(actor);
                string rankName = rank switch
                {
                    1 => UILocalization.Get("sect_rank_probation"),
                    2 => UILocalization.Get("sect_rank_member"),
                    3 => UILocalization.Get("sect_rank_senior"),
                    4 => UILocalization.Get("sect_rank_captain"),
                    5 => UILocalization.Get("sect_rank_commander"),
                    6 => UILocalization.Get("sect_rank_leader"),
                    7 => UILocalization.Get("sect_rank_deputy"),
                    8 => UILocalization.Get("sect_rank_guardian"),
                    9 => UILocalization.Get("sect_rank_advisor"),
                    10 => UILocalization.Get("sect_rank_heritor"),
                    11 => UILocalization.Get("sect_rank_founder"),
                    _ => UILocalization.Get("sect_rank_member")
                };
                float contribution = Data.CultivationData.GetSectContribution(actor);
                string baseText = string.Format(UILocalization.Get("sect_format"), sect.Name, rankName, UILocalization.Get("unit_contribution"), contribution);
                // 单位面板空间有限，不显示修炼加成（异能图鉴/基因图谱中显示）
                return baseText;
            }
            catch
            {
                return "";
            }
        }
        /// <summary>构建称号信息文本（当前称号 + 境界后缀）</summary>
        private static string BuildTitleText(Actor actor, int tier)
        {
            try
            {
                string suffix = "";
                if (string.IsNullOrEmpty(suffix)) return "";
                return suffix;
            }
            catch
            {
                return "";
            }
        }
    }
    /// <summary>
    /// 在单位窗口添加基因图谱图标按钮
    /// Patch UnitWindow.OnEnable，使用PowerButtonCreator创建图标按钮
    /// </summary>
    [HarmonyPatch(typeof(UnitWindow), "OnEnable")]
    public static class UnitWindowGeneButtonPatch
    {
        private static GameObject _geneButton;
        [HarmonyPrefix]
        public static void Prefix(UnitWindow __instance)
        {
            try
            {
                if (__instance?.GetActor() == null) return;
                // 每次OnEnable都检查按钮是否存在，如果不存在就重新创建
                if (_geneButton == null)
                {
                    CreateGeneButton(__instance);
                }
                // 根据单位是否活着显示/隐藏按钮（所有活着的单位都显示，包括凡人）
                UpdateButtonVisibility(__instance);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 单位窗口基因按钮Patch异常: " + e.Message);
            }
        }
        private static void CreateGeneButton(UnitWindow window)
        {
            try
            {
                // 查找按钮父对象（Background）
                Transform background = window.transform.Find("Background");
                if (background == null)
                {
                    DSDebug.Warning("[DivineAscension] 未找到UnitWindow.Background");
                    return;
                }
                // 按钮位置（x=156, y从84开始，每次减40）
                float buttonX = 156f;
                float buttonY = 84f;
                float buttonSize = 32f;
                // 尝试加载基因图谱图标
                Sprite iconSprite = null;
                try
                {
                    iconSprite = SpriteTextureLoader.getSprite("ui/iconGeneGraph");
                }
                catch { }
                // 如果没有自定义图标，使用默认图标
                if (iconSprite == null)
                {
                    try
                    {
                        iconSprite = SpriteTextureLoader.getSprite("ui/iconBook");
                    }
                    catch { }
                }
                // 用Unity原生API创建按钮
                GameObject buttonObj = new GameObject("GeneGraphButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObj.transform.SetParent(background, false);
                // 设置位置和大小
                RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(buttonX, -buttonY);
                rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize);
                rectTransform.localScale = Vector3.one;
                // 设置图标
                Image image = buttonObj.GetComponent<Image>();
                if (iconSprite != null)
                {
                    image.sprite = iconSprite;
                }
                else
                {
                    image.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                }
                // 设置点击事件
                Button button = buttonObj.GetComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    if (window.GetActor() != null && window.GetActor().isAlive())
                    {
                        GeneGraphWindow.Show(window.GetActor());
                    }
                });
                // 添加提示组件（只设置textOnClick一个字段，避免多行提示）
                try
                {
                    var tipButton = buttonObj.AddComponent<TipButton>();
                    if (tipButton != null)
                    {
                        string tipText = UILocalization.CurrentLanguage == "en" ? "Gene Graph" : UILocalization.Get("unit_gene_graph");
                        tipButton.textOnClick = tipText;
                    }
                }
                catch (System.Exception e)
                {
                    DSDebug.Warning("[DivineAscension] 添加TipButton失败: " + e.Message);
                }
                // 添加拖动脚本（让按钮可以被鼠标拖动，位置保存到PlayerPrefs）
                try
                {
                    buttonObj.AddComponent<GeneButtonDragHandler>();
                }
                catch (System.Exception e)
                {
                    DSDebug.Warning("[DivineAscension] 添加基因按钮拖动脚本失败: " + e.Message);
                }
                _geneButton = buttonObj;
                DSDebug.Verbose("[DivineAscension] 单位窗口基因图谱按钮已创建（支持拖动）");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 创建基因按钮失败: " + e.Message);
            }
        }
        private static void UpdateButtonVisibility(UnitWindow window)
        {
            try
            {
                if (_geneButton == null) return;
                // 所有活着的单位都显示基因图谱按钮（包括凡人）
                bool shouldShow = false;
                if (window.GetActor() != null && window.GetActor().isAlive())
                {
                    shouldShow = true;
                }
                _geneButton.SetActive(shouldShow);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 更新基因按钮可见性失败: " + e.Message);
            }
        }
    }
}

