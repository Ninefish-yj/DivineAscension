// ============================================================

using UnityEngine;
using Code.Data;

namespace Code.Core
{
    /// <summary>
    /// 状态栏系统管理器
    /// 使用游戏原生 addStatusEffect API，状态效果显示在单位窗口的状态栏中
    /// </summary>
    public static class DSStatusManager
    {
        // ============================================================
        //  状态效果ID常量（使用游戏原生状态系统）
        // ============================================================
        public const string STATUS_DEEP_AWAKENING = "ds_deep_awakening";      // 深度觉醒
        public const string STATUS_OUT_OF_CONTROL = "ds_status_out_of_control"; // 能力失控
        public const string STATUS_VOID_CORRUPTION = "ds_void_corruption";      // 虚空侵蚀
        public const string STATUS_FOUNDATION_DAMAGED = "ds_foundation_damaged"; // 能力根基受损
        public const string STATUS_ERA_DOMINATOR = "ds_era_dominator";          // 真神纪元主宰加成

        // ============================================================
        //  状态效果持续时间（秒，游戏内时间）
        // ============================================================
        public const float DURATION_DEEP_AWAKENING = 180f;    // 深度觉醒：3年（60秒/年 × 3）
        public const float DURATION_OUT_OF_CONTROL = 180f; // 能力失控：3年
        public const float DURATION_VOID_CORRUPTION = 300f;    // 虚空侵蚀：5年
        public const float DURATION_FOUNDATION_DAMAGED = 600f;  // 能力根基受损：10年

        // ============================================================
        //  公共API
        // ============================================================

        /// <summary>
        /// 安全调用addStatusEffect（反射调用受保护方法）
        /// addStatusEffect是BaseSimObject的受保护方法，不能直接调用
        /// </summary>
        public static void SafeAddStatusEffect(Actor actor, string statusId, float duration, bool addToUI = true)
        {
            if (actor == null) return;
            try
            {
                // 直接调用addStatusEffect方法
                actor.addStatusEffect(statusId, duration, addToUI);
            }
            catch (System.Exception e)
            {
                DSDebug.Verbose($"SafeAddStatusEffect失败: {e.Message}");
            }
        }

        /// <summary>添加深度觉醒状态（修炼速度+100%，持续3年）</summary>
        public static void AddDeepAwakening(Actor actor)
        {
            if (actor == null) return;
            try
            {
                SafeAddStatusEffect(actor, STATUS_DEEP_AWAKENING, DURATION_DEEP_AWAKENING, true);
                CultivationData.SetEnlightenmentDuration(actor, DURATION_DEEP_AWAKENING / 60f); // 同步到元数据
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 获得深度觉醒状态（3年）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加深度觉醒状态失败: {e.Message}");
            }
        }

        /// <summary>添加能力失控状态（攻击+50%，防御-50%，持续3年）</summary>
        public static void AddOutOfControl(Actor actor)
        {
            if (actor == null) return;
            try
            {
                SafeAddStatusEffect(actor, STATUS_OUT_OF_CONTROL, DURATION_OUT_OF_CONTROL, true);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 获得能力失控状态（3年）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加能力失控状态失败: {e.Message}");
            }
        }

        /// <summary>添加虚空侵蚀状态（修炼-30%，失控+50%，持续5年）</summary>
        public static void AddVoidCorruption(Actor actor)
        {
            if (actor == null) return;
            try
            {
                SafeAddStatusEffect(actor, STATUS_VOID_CORRUPTION, DURATION_VOID_CORRUPTION, true);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 获得虚空侵蚀状态（5年）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加虚空侵蚀状态失败: {e.Message}");
            }
        }

        /// <summary>添加能力根基受损状态（进阶成功率-50%，持续10年）</summary>
        public static void AddFoundationDamaged(Actor actor)
        {
            if (actor == null) return;
            try
            {
                SafeAddStatusEffect(actor, STATUS_FOUNDATION_DAMAGED, DURATION_FOUNDATION_DAMAGED, true);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 获得能力根基受损状态（10年）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加能力根基受损状态失败: {e.Message}");
            }
        }

        /// <summary>按境界标记技能掌握（突破成功时调用）
        /// 注意：境界技能是战斗中自动释放的限时技能，不在突破时授予常驻状态效果
        /// 战斗中释放时由 RealmAbilities.AddAbilityStatusEffect 授予限时状态（Buff=30秒等）
        /// </summary>
        public static void AddTierAbility(Actor actor, int tier)
        {
            if (actor == null || tier < 1 || tier > 13) return;

            // 只做日志记录，不授予常驻状态效果
            // 技能释放逻辑在 AbilityStatusPatch.TryAutoCastAbilityInCombat（战斗中自动释放）
            // 释放时调用 RealmAbilities.UseAbility -> AddAbilityStatusEffect（限时状态效果）
            DSDebug.Verbose($"[DivineAscension] {actor.getName()} 达到{tier}阶，掌握境界技能（战斗中自动释放，限时效果）");
        }

        /// <summary>按境界移除技能标记（阶位回退时调用，防止重复掌握提示）
        /// 境界技能为战斗中自动释放的限时技能，无常驻状态可移除，仅做安全清理</summary>
        public static void RemoveTierAbilities(Actor actor, int tier)
        {
            if (actor == null || tier < 1 || tier > 13) return;
            DSDebug.Verbose($"[DivineAscension] {actor.getName()} 回退至{tier}阶以下，不再掌握对应境界技能");
        }

        /// <summary>检查单位是否有指定状态</summary>
        public static bool HasStatus(Actor actor, string statusId)
        {
            if (actor == null) return false;
            try
            {
                return actor.hasStatus(statusId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>移除指定状态</summary>
        public static void RemoveStatus(Actor actor, string statusId)
        {
            if (actor == null) return;
            try
            {
                actor.finishStatusEffect(statusId);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 移除状态 {statusId}");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"移除状态失败: {e.Message}");
            }
        }

        /// <summary>添加真神纪元主宰加成（攻击+20%，防御+20%，速度+10%，永久）</summary>
        public static void AddEraDominatorBuff(Actor actor)
        {
            if (actor == null) return;
            try
            {
                SafeAddStatusEffect(actor, STATUS_ERA_DOMINATOR, 999999f, true);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 成为纪元主宰，获得主宰加成（攻击+20%，防御+20%，速度+10%）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"添加主宰加成失败: {e.Message}");
            }
        }

        /// <summary>移除真神纪元主宰加成</summary>
        public static void RemoveEraDominatorBuff(Actor actor)
        {
            if (actor == null) return;
            try
            {
                actor.finishStatusEffect(STATUS_ERA_DOMINATOR);
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 失去纪元主宰加成");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"移除主宰加成失败: {e.Message}");
            }
        }

        /// <summary>年度Tick：更新状态持续时间（同步到元数据）</summary>
        public static void OnAnnualTick(Actor actor)
        {
            if (actor == null) return;

            try
            {
                // 同步深度觉醒状态到元数据
                if (HasStatus(actor, STATUS_DEEP_AWAKENING))
                {
                    // 深度觉醒状态由游戏原生系统管理持续时间
                    // 元数据中的持续时间在年度Tick中递减
                }
                else
                {
                    // 如果状态已过期，确保元数据也清零
                    if (CultivationData.GetEnlightenmentDuration(actor) > 0)
                    {
                        CultivationData.SetEnlightenmentDuration(actor, 0f);
                    }
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"年度状态更新失败: {e.Message}");
            }
        }
    }
}

