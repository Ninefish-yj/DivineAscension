// ============================================================

using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// NML原生配置回调类
    /// 配置项在default_config.json中定义，NML自动在游戏设置界面生成UI
    /// 回调方法格式：类名:方法名
    /// </summary>
    public static class DengShenConfig
    {
        // === 称号和境界后缀 ===
        public static bool EnableTitleSuffix = true;

        // === 自动收藏（按具体境界分开，删除笼统的"高境界/突破"和组织收藏）===
        public static bool AutoFavoriteTier8 = false;   // 8阶干涉者以上
        public static bool AutoFavoriteTier9 = false;   // 9阶解析者以上
        public static bool AutoFavoriteTier10 = true;   // 10阶使徒级以上
        public static bool AutoFavoriteTier11 = true;   // 11阶登神级以上
        public static bool AutoFavoriteTier12 = true;   // 12阶真神级以上
        public static bool AutoFavoriteTier13 = true;   // 13阶超神级

        // === 修炼系统 ===
        public static bool EnableAutoCultivation = true;
        public static bool EnableAutoAwakening = true;
        public static bool EnableAutoBreakthrough = true;
        public static bool EnableAnimalAwakening = true;

        // === 世界交互 ===
        public static bool EnableDisasterSystem = true;
        public static bool EnableVoidRifts = true;
        public static bool EnableRealmEvents = true;

        // === 生命恢复全局总闸（开关：是否在战斗中拦截一切外部治疗）===
        public static bool EnableGlobalHealthSuppression = true;

        // === 全局播报总闸（开关：是否弹出屏幕中央大通知）===
        public static bool EnableGlobalNotifications = true;

        // === 黑色方碑效果（开关：4阶突破时调用原版方碑进化）===
        public static bool EnableMonolithEffect = true;

        // === UI设置 ===

        // === 修炼参数（滑块）===
        public static float CultivationRateMultiplier = 3.0f;
        public static float MortalAwakenChance = 0.01f;
        public static float TurbulenceDecayRate = 5.0f;

        // === 调试设置 ===
        public static bool EnableVerboseLog = false;

        // ============================================================
        //  回调方法（NML配置改变时调用）
        // ============================================================

        // === 称号和境界后缀回调 ===
        public static void EnableTitleSuffixCallBack(bool newValue)
        {
            EnableTitleSuffix = newValue;
        }

        // === 自动收藏回调（按境界分开）===
        public static void AutoFavoriteTier8CallBack(bool newValue)
        {
            AutoFavoriteTier8 = newValue;
        }

        public static void AutoFavoriteTier9CallBack(bool newValue)
        {
            AutoFavoriteTier9 = newValue;
        }

        public static void AutoFavoriteTier10CallBack(bool newValue)
        {
            AutoFavoriteTier10 = newValue;
        }

        public static void AutoFavoriteTier11CallBack(bool newValue)
        {
            AutoFavoriteTier11 = newValue;
        }

        public static void AutoFavoriteTier12CallBack(bool newValue)
        {
            AutoFavoriteTier12 = newValue;
        }

        public static void AutoFavoriteTier13CallBack(bool newValue)
        {
            AutoFavoriteTier13 = newValue;
        }

        // === 修炼系统回调 ===
        public static void EnableAutoCultivationCallBack(bool newValue)
        {
            EnableAutoCultivation = newValue;
        }

        public static void EnableAutoAwakeningCallBack(bool newValue)
        {
            EnableAutoAwakening = newValue;
        }

        public static void EnableAutoBreakthroughCallBack(bool newValue)
        {
            EnableAutoBreakthrough = newValue;
        }

        public static void EnableAnimalAwakeningCallBack(bool newValue)
        {
            EnableAnimalAwakening = newValue;
        }

        // === 世界交互回调 ===
        public static void EnableDisasterSystemCallBack(bool newValue)
        {
            EnableDisasterSystem = newValue;
        }

        public static void EnableVoidRiftsCallBack(bool newValue)
        {
            EnableVoidRifts = newValue;
        }

        public static void EnableRealmEventsCallBack(bool newValue)
        {
            EnableRealmEvents = newValue;
        }


        public static void EnableGlobalHealthSuppressionCallBack(bool newValue)
        {
            EnableGlobalHealthSuppression = newValue;
        }

        public static void EnableGlobalNotificationsCallBack(bool newValue)
        {
            EnableGlobalNotifications = newValue;
        }

        public static void EnableMonolithEffectCallBack(bool newValue)
        {
            EnableMonolithEffect = newValue;
        }


        // === UI设置回调 ===

        // === 修炼参数回调 ===
        public static void CultivationRateMultiplierCallBack(float newValue)
        {
            CultivationRateMultiplier = newValue;
        }

        public static void MortalAwakenChanceCallBack(float newValue)
        {
            MortalAwakenChance = newValue;
        }

        public static void TurbulenceDecayRateCallBack(float newValue)
        {
            TurbulenceDecayRate = newValue;
        }

        // === 调试设置回调 ===
        public static void EnableVerboseLogCallBack(bool newValue)
        {
            EnableVerboseLog = newValue;
            DSDebug.EnableVerboseLog = newValue;
        }

    }

    /// <summary>
    /// 配置管理器（兼容层）
    /// 所有配置值实际存储在DengShenConfig中，NML自动管理持久化
    /// 此类保留是为了兼容现有代码，所有静态属性都转发到DengShenConfig
    /// </summary>
    public static class DSConfigManager
    {
        // ============================================================
        //  兼容属性（转发到DengShenConfig）
        // ============================================================

        // === 称号和境界后缀 ===
        public static bool EnableTitleSuffix
        {
            get => DengShenConfig.EnableTitleSuffix;
            set => DengShenConfig.EnableTitleSuffix = value;
        }

        // === 自动收藏（按境界分开）===
        public static bool AutoFavoriteTier8
        {
            get => DengShenConfig.AutoFavoriteTier8;
            set => DengShenConfig.AutoFavoriteTier8 = value;
        }

        public static bool AutoFavoriteTier9
        {
            get => DengShenConfig.AutoFavoriteTier9;
            set => DengShenConfig.AutoFavoriteTier9 = value;
        }

        public static bool AutoFavoriteTier10
        {
            get => DengShenConfig.AutoFavoriteTier10;
            set => DengShenConfig.AutoFavoriteTier10 = value;
        }

        public static bool AutoFavoriteTier11
        {
            get => DengShenConfig.AutoFavoriteTier11;
            set => DengShenConfig.AutoFavoriteTier11 = value;
        }

        public static bool AutoFavoriteTier12
        {
            get => DengShenConfig.AutoFavoriteTier12;
            set => DengShenConfig.AutoFavoriteTier12 = value;
        }

        public static bool AutoFavoriteTier13
        {
            get => DengShenConfig.AutoFavoriteTier13;
            set => DengShenConfig.AutoFavoriteTier13 = value;
        }

        // === 修炼系统 ===
        public static bool EnableAutoCultivation
        {
            get => DengShenConfig.EnableAutoCultivation;
            set => DengShenConfig.EnableAutoCultivation = value;
        }

        public static bool EnableAutoAwakening
        {
            get => DengShenConfig.EnableAutoAwakening;
            set => DengShenConfig.EnableAutoAwakening = value;
        }

        public static bool EnableAutoBreakthrough
        {
            get => DengShenConfig.EnableAutoBreakthrough;
            set => DengShenConfig.EnableAutoBreakthrough = value;
        }

        public static bool EnableAnimalAwakening
        {
            get => DengShenConfig.EnableAnimalAwakening;
            set => DengShenConfig.EnableAnimalAwakening = value;
        }

        // === 世界交互 ===
        public static bool EnableDisasterSystem
        {
            get => DengShenConfig.EnableDisasterSystem;
            set => DengShenConfig.EnableDisasterSystem = value;
        }

        public static bool EnableVoidRifts
        {
            get => DengShenConfig.EnableVoidRifts;
            set => DengShenConfig.EnableVoidRifts = value;
        }

        public static bool EnableRealmEvents
        {
            get => DengShenConfig.EnableRealmEvents;
            set => DengShenConfig.EnableRealmEvents = value;
        }


        public static bool EnableGlobalHealthSuppression
        {
            get => DengShenConfig.EnableGlobalHealthSuppression;
            set => DengShenConfig.EnableGlobalHealthSuppression = value;
        }

        public static bool EnableGlobalNotifications
        {
            get => DengShenConfig.EnableGlobalNotifications;
            set => DengShenConfig.EnableGlobalNotifications = value;
        }


        public static bool EnableMonolithEffect
        {
            get => DengShenConfig.EnableMonolithEffect;
            set => DengShenConfig.EnableMonolithEffect = value;
        }

        // === UI设置 ===

        public static string CurrentLanguage = "cz";

        // ============================================================
        //  兼容方法（空操作，NML自动管理持久化）
        // ============================================================

        /// <summary>加载配置（空操作，NML自动管理）</summary>
        public static void LoadConfig()
        {
            // NML原生配置系统自动加载和持久化，不需要手动操作
            DSDebug.Verbose("[DivineAscension] 配置系统已初始化（NML原生配置）");
        }

    }
}

