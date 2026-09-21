using System;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 纪元桥接：把我们模组的纪元系统和原版WorldAge系统对接
    /// （NML publicized 环境下直接访问 internal 成员，无需反射）
    /// </summary>
    public static class EraBridge
    {
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                // publicized 环境下 World.world.era_manager 可直接访问（MapBox.era_manager 是 internal）
                if (World.world.era_manager == null)
                {
                    DSDebug.Warning("[EraBridge] era_manager 为空，纪元桥接不可用");
                    return;
                }
                DSDebug.Verbose("[EraBridge] 纪元桥接初始化完成");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[EraBridge] 初始化失败: {e.Message}");
            }
        }

        /// <summary>获取当前原版时代名称</summary>
        public static string GetCurrentAgeName()
        {
            try
            {
                var age = World.world.era_manager?.getCurrentAge();
                if (age == null) return "未知时代";
                return age.id ?? "未知时代";
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[EraBridge] 获取当前时代失败: {e.Message}");
                return "未知时代";
            }
        }

        /// <summary>切换到超神纪元（age_ds_transcendent）</summary>
        public static bool SwitchToTranscendentAge()
        {
            try
            {
                var mgr = World.world.era_manager;
                if (mgr == null) return false;

                var trueGodAge = AssetManager.era_library.get("age_ds_transcendent");
                if (trueGodAge == null)
                {
                    DSDebug.Verbose("[EraBridge] 未找到 age_ds_transcendent");
                    return false;
                }

                // 槽位0 = 当前纪元槽
                mgr.setAgeToSlot(trueGodAge, 0);
                DSDebug.Verbose("[EraBridge] 已切换到超神纪元（age_ds_transcendent）");
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[EraBridge] 切换到超神纪元失败: {e.Message}");
                return false;
            }
        }

        /// <summary>突破到真神级/超神级时，自动切换到超神纪元</summary>
        public static void OnTranscendenceBreakthrough(int newTier)
        {
            try
            {
                if (newTier >= 13)
                {
                    DSDebug.Verbose($"[EraBridge] 突破到{newTier}阶，切换到超神纪元");
                    SwitchToTranscendentAge();
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[EraBridge] 突破触发时代切换失败: {e.Message}");
            }
        }
    }
}
