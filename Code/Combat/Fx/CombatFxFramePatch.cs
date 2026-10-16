// ============================================================
// 战斗表现层·帧驱动（v0.18.47）
// 拦截原版世界模拟入口 MapBox.checkMainSimulationUpdate：
//   Prefix  —— 击停/冻结/慢动作空帧时返回 false，跳过世界模拟
//   Postfix —— 每帧驱动演出链（HitStop 倒计时、Cinema 计时、
//              粒子更新、导演定时器），并懒清理已销毁世界
// 参考「战斗之神」的 SakugaMod.SimulationPatch 设计。
// ============================================================
using System;
using HarmonyLib;
using UnityEngine;

namespace Code.Combat.Fx
{
    [HarmonyPatch(typeof(MapBox), "checkMainSimulationUpdate")]
    public static class CombatFxFramePatch
    {
        /// <summary>本帧世界模拟是否被跳过（演出仍在继续）。</summary>
        internal static bool Skipped;

        private static bool Prefix()
        {
            try
            {
                Skipped = CombatFxHitStop.Active || CombatFxCinema.SkipFrame();
                return !Skipped;
            }
            catch
            {
                Skipped = false;
                return true;
            }
        }

        private static void Postfix()
        {
            try
            {
                CombatFxHitStop.Tick();
                CombatFxCinema.Tick();
                // 模拟时间步：击停/冻结/暂停时为 0（普通粒子冻结；Cine 粒子用 Cinema.Dt 继续）
                float dt = Skipped || World.world == null || World.world.isPaused()
                    ? 0f
                    : Mathf.Min(Time.deltaTime, 0.05f) * CombatFxCinema.Rate;
                CombatFxCore.Update(dt, CombatFxCinema.Dt);
                CombatFxDirector.Tick();
                // 世界已销毁/存档切换：懒清理演出（新世界由业务重新触发演出）
                if (World.world == null && CombatFxCore.Live > 0)
                {
                    CombatFxDirector.ClearAll();
                }
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] frame: " + e.Message);
            }
        }
    }
}
