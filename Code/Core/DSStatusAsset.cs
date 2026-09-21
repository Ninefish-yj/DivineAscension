// ============================================================

using UnityEngine;
using NeoModLoader.api;

namespace Code.Core
{
    /// <summary>
    /// 技能状态效果注册
    /// 使用游戏原生 StatusAsset API 注册每个技能的状态效果
    /// </summary>
    public static class DSStatusAsset
    {
        private static bool _initialized = false;

        /// <summary>初始化所有技能状态效果</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // 1阶 觉醒者 - 异能冲击（伤害型，短暂伤害加成）
                RegisterStatus(
                    "ds_ability_energy_bolt",
                    "ui/ability_icons/ds_ability_energy_bolt",
                    "status_title_energy_bolt",
                    "status_desc_energy_bolt",
                    5f
                );

                // 4阶 突变者 - 治愈之光（治疗型，持续恢复生命）
                RegisterStatus(
                    "ds_ability_healing_light",
                    "ui/ability_icons/ds_ability_healing_light",
                    "status_title_healing_light",
                    "status_desc_healing_light",
                    10f
                );

                // 5阶 调控者 - 能量风暴（伤害型，范围伤害加成）
                RegisterStatus(
                    "ds_ability_energy_storm",
                    "ui/ability_icons/ds_ability_energy_storm",
                    "status_title_energy_storm",
                    "status_desc_energy_storm",
                    5f
                );

                // 6阶 场域者 - 场域展开（终极型，全属性+100%）
                RegisterStatus(
                    "ds_ability_domain_expand",
                    "ui/ability_icons/ds_ability_domain_expand",
                    "status_title_domain_expand",
                    "status_desc_domain_expand",
                    60f
                );

                // 8阶 干涉者 - 规则压制（减益型，敌人能力下降）
                RegisterStatus(
                    "ds_ability_law_suppress",
                    "ui/ability_icons/ds_ability_law_suppress",
                    "status_title_law_suppress",
                    "status_desc_law_suppress",
                    20f
                );

                // 9阶 解析者 - 本源汲取（功能型，恢复能量）
                RegisterStatus(
                    "ds_ability_origin_drain",
                    "ui/ability_icons/ds_ability_origin_drain",
                    "status_title_origin_drain",
                    "status_desc_origin_drain",
                    15f
                );

                // 10阶 使徒级 - 空间撕裂（终极型，伤害+裂隙）
                RegisterStatus(
                    "ds_ability_space_tear",
                    "ui/ability_icons/ds_ability_space_tear",
                    "status_title_space_tear",
                    "status_desc_space_tear",
                    60f
                );

                // 11阶 登神级 - 超神级降临（终极型，友军无敌+恢复）
                RegisterStatus(
                    "ds_ability_sanctuary",
                    "ui/ability_icons/ds_ability_sanctuary",
                    "status_title_sanctuary",
                    "status_desc_sanctuary",
                    60f
                );

                // 12阶 真神级 - 维度打击（终极型，全屏伤害）
                RegisterStatus(
                    "ds_ability_dimensional_strike",
                    "ui/ability_icons/ds_ability_dimensional_strike",
                    "status_title_dimensional_strike",
                    "status_desc_dimensional_strike",
                    60f
                );

                // 13阶 超神级 - 神罚（终极型，毁灭区域）
                RegisterStatus(
                    "ds_ability_divine_punishment",
                    "ui/ability_icons/ds_ability_divine_punishment",
                    "status_title_divine_punishment",
                    "status_desc_divine_punishment",
                    60f
                );

                                // 深度觉醒（修炼速度+100%，持续3年）
                RegisterStatus(
                    "ds_deep_awakening",
                    "trait/ds_status_enlightenment",
                    "status_title_deep_awakening",
                    "status_desc_deep_awakening",
                    180f
                );

                // 能力失控（攻击+30%，防御-50%，持续3年）
                RegisterStatus(
                    "ds_status_out_of_control",
                    "trait/ds_status_demonic_obsession",
                    "status_title_out_of_control",
                    "status_desc_out_of_control",
                    180f
                );


                // 虚空侵蚀（防御-30%，速度-20%，持续5年）
                RegisterStatus(
                    "ds_void_corruption",
                    "trait/ds_status_void_corruption",
                    "status_title_void_corruption",
                    "status_desc_void_corruption",
                    300f
                );

                // 能力根基受损（修炼速度-50%，持续10年）
                RegisterStatus(
                    "ds_foundation_damaged",
                    "trait/ds_status_damaged_foundation",
                    "status_title_foundation_damaged",
                    "status_desc_foundation_damaged",
                    600f
                );
// 真神纪元主宰加成（永久状态，攻击+20%，防御+20%，速度+10%）
                RegisterStatus(
                    "ds_era_dominator",
                    "otherAssets/iconAgeTrueGod",
                    "status_title_era_dominator",
                    "status_desc_era_dominator",
                    999999f
                );

                // 虚弱期状态（挑战主宰失败，持续20年，全属性降低50%，期间无法再次挑战）
                RegisterStatus(
                    "ds_era_weakened",
                    "ui/ability_icons/ds_ability_energy_shield",
                    "status_title_era_weakened",
                    "status_desc_era_weakened",
                    1200f
                );

                // 能量生命体状态（真神在维度空间创造，全属性+20%，持续1年）
                RegisterStatus(
                    "ds_energy_lifeform",
                    "ui/ability_icons/ds_ability_body_boost",
                    "status_title_energy_lifeform",
                    "status_desc_energy_lifeform",
                    60f
                );

                // 维度空间状态（单位进入维度空间，半透明、无敌、不可选中、AI暂停）
                RegisterStatus(
                    "ds_in_dimension",
                    "otherAssets/iconAgeTrueGod",
                    "status_title_in_dimension",
                    "status_desc_in_dimension",
                    999999f
                );

                DSDebug.Verbose("[DivineAscension] 技能状态效果注册完成，共17个状态效果");
            }
            catch (System.Exception e)
            {
                DSDebug.Error("[DivineAscension] 技能状态效果注册失败: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>注册单个状态效果</summary>
        private static void RegisterStatus(string id, string iconPath, string localeId, string localeDesc, float defaultDuration)
        {
            try
            {
                // 使用反射创建StatusAsset（因为运行时可能是internal）
                System.Type statusAssetType = System.Type.GetType("StatusAsset, Assembly-CSharp");
                if (statusAssetType == null)
                {
                    DSDebug.Warning("[DivineAscension] 未找到StatusAsset类型，状态效果注册跳过: " + id);
                    return;
                }

                object statusAsset = System.Activator.CreateInstance(statusAssetType);

                // 设置字段
                SetField(statusAsset, "id", id);
                SetField(statusAsset, "path_icon", iconPath);
                SetField(statusAsset, "locale_id", localeId);
                SetField(statusAsset, "locale_description", localeDesc);
                SetField(statusAsset, "allow_timer_reset", false);

                // 添加到AssetManager.status
                System.Type assetManagerType = System.Type.GetType("AssetManager, Assembly-CSharp");
                if (assetManagerType != null)
                {
                    var statusField = assetManagerType.GetField("status", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (statusField != null)
                    {
                        object statusLibrary = statusField.GetValue(null);
                        var addMethod = statusLibrary.GetType().GetMethod("add", new[] { statusAssetType });
                        if (addMethod != null)
                        {
                            addMethod.Invoke(statusLibrary, new[] { statusAsset });
                            DSDebug.Verbose("[DivineAscension] 状态效果注册成功: " + id);
                        }
                        else
                        {
                            DSDebug.Warning("[DivineAscension] 未找到status.add方法: " + id);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 状态效果注册失败 " + id + ": " + e.Message);
            }
        }

        /// <summary>使用反射设置字段值</summary>
        private static void SetField(object obj, string fieldName, object value)
        {
            try
            {
                var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                }
                else
                {
                    var prop = obj.GetType().GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(obj, value);
                    }
                }
            }
            catch { }
        }

        // ============================================================
        //  状态效果实际效果定义（在战斗计算中使用）
        // ============================================================

        /// <summary>获取状态效果的伤害加成（攻击方）</summary>
        public static float GetDamageBonus(Actor actor)
        {
            if (actor == null) return 1f;
            float bonus = 1f;

            try
            {
                // 场域展开：全属性+100%（攻击+100%）
                if (actor.hasStatus("ds_ability_domain_expand")) bonus *= 2.0f;
                // 异能冲击：短暂伤害加成+30%
                if (actor.hasStatus("ds_ability_energy_bolt")) bonus *= 1.3f;
                // 能量风暴：范围伤害加成+50%
                if (actor.hasStatus("ds_ability_energy_storm")) bonus *= 1.5f;
                // 空间撕裂：伤害+100%
                if (actor.hasStatus("ds_ability_space_tear")) bonus *= 2.0f;
                // 维度打击：全屏伤害+200%
                if (actor.hasStatus("ds_ability_dimensional_strike")) bonus *= 3.0f;
                // 神罚：毁灭区域，伤害+500%
                if (actor.hasStatus("ds_ability_divine_punishment")) bonus *= 6.0f;
            }
            catch { }

            return bonus;
        }

        /// <summary>获取状态效果的减伤（防御方）</summary>
        public static float GetDamageReduction(Actor actor)
        {
            if (actor == null) return 1f;
            float reduction = 1f;

            try
            {
                // 场域展开：全属性+100%（防御+100%，减伤50%）
                if (actor.hasStatus("ds_ability_domain_expand")) reduction *= 0.5f;
                // 超神级降临：友军无敌（减伤100%）
                if (actor.hasStatus("ds_ability_sanctuary")) reduction *= 0.0f;
                // 治愈之光：减伤10%
                if (actor.hasStatus("ds_ability_healing_light")) reduction *= 0.9f;
            }
            catch { }

            return reduction;
        }

        /// <summary>获取状态效果的速度加成</summary>
        public static float GetSpeedBonus(Actor actor)
        {
            if (actor == null) return 1f;
            float bonus = 1f;

            try
            {
                // 场域展开：全属性+100%（速度+100%）
                if (actor.hasStatus("ds_ability_domain_expand")) bonus *= 2.0f;
            }
            catch { }

            return bonus;
        }

    }
}





