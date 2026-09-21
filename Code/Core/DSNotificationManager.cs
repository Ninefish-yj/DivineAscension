// ============================================================

using System;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 屏幕通知管理器
    /// 通过WorldBox原生WorldTip系统显示大事件屏幕通知
    /// </summary>
    public static class DSNotificationManager
    {
        // 节流控制
        private static float _lastMajorNotifyTime = -100f;
        private static float _lastInfoNotifyTime = -100f;
        private static float _lastWarningNotifyTime = -100f;

        // 节流间隔（秒）
        private const float MAJOR_COOLDOWN = 1.5f;    // 大事件最小间隔
        private const float INFO_COOLDOWN = 0.8f;      // 信息最小间隔
        private const float WARNING_COOLDOWN = 0.5f;   // 警告最小间隔

        // 通知显示时长（秒）
        private const float MAJOR_DURATION = 5.0f;
        private const float INFO_DURATION = 3.0f;
        private const float WARNING_DURATION = 4.0f;

        /// <summary>初始化通知管理器</summary>
        public static void Init()
        {
            try
            {
                DSDebug.Verbose("[通知系统] 初始化完成，WorldTip通知已就绪");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[通知系统] 初始化失败: " + e.Message);
            }
        }

        /// <summary>
        /// 大事件中央通知（高阶突破、灾变、真神诞生等）
        /// 注意：全局提示只显示标题，不显示描述
        /// </summary>
        public static void NotifyMajor(string title, string description)
        {
            try
            {
                // 检查全局通知开关
                if (!Code.Core.DengShenConfig.EnableGlobalNotifications) return;

                if (!CheckCooldown(ref _lastMajorNotifyTime, MAJOR_COOLDOWN)) return;

                // 全局提示只显示标题，不显示描述
                // 直接调用WorldTip.showNow（参考西幻世界实现）
                // 参数：文本, 是否显示图标, 位置(top/center/bottom), 显示时长
                WorldTip.showNow(title, false, "top", MAJOR_DURATION);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[通知系统] 大事件通知失败: " + e.Message);
            }
        }

        /// <summary>
        /// 普通信息顶部提示
        /// </summary>
        public static void NotifyInfo(string message)
        {
            try
            {
                // 检查全局通知开关
                if (!Code.Core.DengShenConfig.EnableGlobalNotifications) return;

                if (!CheckCooldown(ref _lastInfoNotifyTime, INFO_COOLDOWN)) return;

                // 直接调用WorldTip.showNow（参考西幻世界实现）
                WorldTip.showNow(message, false, "top", INFO_DURATION);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[通知系统] 信息通知失败: " + e.Message);
            }
        }

        /// <summary>
        /// 警告提示（规则反噬、错误等）
        /// </summary>
        public static void NotifyWarning(string message)
        {
            try
            {
                // 检查全局通知开关（警告通知不受开关限制，总是显示）
                // if (!Code.Core.DengShenConfig.EnableGlobalNotifications) return;

                if (!CheckCooldown(ref _lastWarningNotifyTime, WARNING_COOLDOWN)) return;

                // 警告使用顶部提示
                WorldTip.showNow(message, false, "top", WARNING_DURATION);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[通知系统] 警告通知失败: " + e.Message);
            }
        }

        // ============================================================
        // 内部实现
        // ============================================================

        /// <summary>检查节流冷却，返回true表示可以发送</summary>
        private static bool CheckCooldown(ref float lastTime, float cooldown)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastTime < cooldown)
                return false;
            lastTime = now;
            return true;
        }
    }
}
