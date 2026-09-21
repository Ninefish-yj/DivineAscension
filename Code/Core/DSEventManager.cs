
using System;
using Code.UI;
using Code.Core;
using System.Collections.Generic;
using UnityEngine;
using Code.Data;

namespace Code.Core
{
    /// <summary>模组事件类型</summary>
    public enum DSEventType
    {
        Breakthrough,    // 突破事件
        RuleBacklash, // 规则反噬事件
        Awakening,       // 觉醒事件
        Disaster,        // 灾变事件
        Faction,         // 组织事件
        Divine,          // 维度事件
        System,          // 系统事件
        Warning,         // 警告事件
        Error            // 错误事件
    }

    /// <summary>模组事件记录</summary>
    public class DSEventRecord
    {
        public int Year;              // 事件发生年份
        public DSEventType Type;      // 事件类型
        public string Title;          // 事件标题
        public string Description;    // 事件描述
        public string ActorName;      // 相关单位名称（可选）
        public long ActorId;          // 相关单位ID（可选）
    }

    /// <summary>
    /// 模组事件管理器
    /// 统一管理模组重大事件的记录和显示
    /// </summary>
    public static class DSEventManager
    {
        // 事件记录列表（最多保留200条）
        private static readonly List<DSEventRecord> _events = new List<DSEventRecord>();
        private const int MAX_EVENTS = 200;

        // 日志输出控制
        private static bool _logToConsole = true;   // 是否输出到控制台
        private static bool _logDetailed = false;    // 是否输出详细日志

        /// <summary>初始化事件管理器</summary>
        public static void Init()
        {
            _events.Clear();
            DSDebug.Verbose("事件管理器初始化完成");
        }

        /// <summary>记录重大事件</summary>
        public static void RecordEvent(DSEventType type, string title, string description, string actorName = null, long actorId = 0, int tier = 0, Actor actor = null, bool suppressNotification = false)
        {
            int year = GetCurrentYear();
            var record = new DSEventRecord
            {
                Year = year,
                Type = type,
                Title = title,
                Description = description,
                ActorName = actorName,
                ActorId = actorId
            };

            _events.Add(record);

            // 限制最大记录数
            if (_events.Count > MAX_EVENTS)
            {
                _events.RemoveRange(0, _events.Count - MAX_EVENTS);
            }

            //  全局统计钩子：根据事件类型递增对应计数器
            try
            {
                switch (type)
                {
                    case DSEventType.Breakthrough:
                        DSGlobalStats.IncrementBreakthroughCount();
                        break;
                    case DSEventType.Disaster:
                        DSGlobalStats.IncrementDisasterCount();
                        break;
                    case DSEventType.Awakening:
                        DSGlobalStats.IncrementAwakeningCount();
                        break;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] RecordEvent 异常: " + dsEx.Message); }

            // 控制台只输出重要事件（突破、规则反噬、灾变、警告、错误）
            if (_logToConsole && ShouldLogToConsole(type, tier))
            {
                string prefix = GetEventPrefix(type);
                DSDebug.Verbose($"{prefix} [第{year}年] {title}: {description}");
            }

            // 写入WorldBox原生历史系统
            WriteToHistory(type, title, description, actorName, tier, actor);

            // 触发屏幕通知
            TriggerNotification(type, title, description, tier, suppressNotification);
        }

        /// <summary>将事件写入WorldBox原生历史日志</summary>
        private static void WriteToHistory(DSEventType type, string title, string description, string actorName, int tier, Actor actor = null)
        {
            try
            {
                string category = EventTypeToHistoryCategory(type, tier);
                if (string.IsNullOrEmpty(category)) return;

                // 历史日志只记录有意义的大事件，避免低阶突破刷屏
                if (!ShouldWriteToHistory(type, tier, title)) return;

                //  关键修复：如果有Actor对象，调用LogEventWithActor设置unit和location，支持地图定位和单位跟随
                if (actor != null)
                {
                    DSHistoryManager.LogEventWithActor(category, title, description, actor);
                }
                else
                {
                    DSHistoryManager.LogEvent(category, title, description, actorName);
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[事件管理器] 写入历史失败: " + e.Message);
            }
        }

        /// <summary>判断是否应该写入历史日志</summary>
        private static bool ShouldWriteToHistory(DSEventType type, int tier, string title = null)
        {
            switch (type)
            {
                case DSEventType.Breakthrough:
                    // 记录6阶以上重大突破，以及每个境界的先驱突破（title包含"先驱"）
                    if (tier >= 6) return true;
                    if (TitleContains(title, UILocalization.Get("event_label_first"))) return true;
                    // 跳阶突破（非首位）：罕见大事件，无论阶数一律记录（金色突破记录）
                    if (TitleContains(title, UILocalization.Get("skip_breakthrough_label"))) return true;
                    return false;
                case DSEventType.Faction:
                case DSEventType.Disaster:
                case DSEventType.Divine:
                    return true;
                case DSEventType.Awakening:
                case DSEventType.RuleBacklash:
                case DSEventType.Warning:
                case DSEventType.Error:
                case DSEventType.System:
                default:
                    return false;
            }
        }

        /// <summary>忽略大小写的标题包含判断（兼容中英文先驱/跳阶标签）</summary>
        private static bool TitleContains(string title, string key)
        {
            return !string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(key)
                && title.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>事件类型映射到历史类别</summary>
        private static string EventTypeToHistoryCategory(DSEventType type, int tier)
        {
            switch (type)
            {
                case DSEventType.Breakthrough: return "Breakthrough";
                case DSEventType.Awakening:    return "Awakening";
                case DSEventType.Faction:       return "Faction";
                case DSEventType.Disaster:      return "Anomaly";  // 世界异象用蓝色，不用灾变红色
                case DSEventType.Divine:        return "Divine";
                case DSEventType.System:        return "System";
                default:                         return null;
            }
        }

        /// <summary>根据事件类型触发屏幕通知</summary>
        private static void TriggerNotification(DSEventType type, string title, string description, int tier, bool suppressNotification = false)
        {
            try
            {
                // 全局播报总闸：关了就只记录历史，不弹屏幕通知
                if (!DSConfigManager.EnableGlobalNotifications) return;
                switch (type)
                {
                    case DSEventType.Breakthrough:
                        // 只通知8阶以上真正重大的突破
                        // 跳阶路径已在 OnSkipTierPioneer 弹过顶部提示，suppressNotification 时跳过中央通知避免重复
                        if (tier >= 8 && !suppressNotification)
                        {
                            DSNotificationManager.NotifyMajor(title, description);
                        }
                        break;

                    case DSEventType.Disaster:
                        // 灾变：大事件中央通知
                        DSNotificationManager.NotifyMajor(title, description);
                        break;

                    case DSEventType.Divine:
                        // 真神诞生：大事件中央通知（维度空间进入不通知）
                        if (title.Contains(UILocalization.Get("event_label_god")) || title.Contains(UILocalization.Get("event_label_divine_punishment")) || title.Contains(UILocalization.Get("event_label_divine_blessing")) || title.Contains(UILocalization.Get("event_label_disciple")) || title.Contains(UILocalization.Get("event_label_religion")) || title.Contains(UILocalization.Get("event_label_era")))
                        {
                            DSNotificationManager.NotifyMajor(title, description);
                        }
                        break;

                    case DSEventType.RuleBacklash:
                    case DSEventType.Error:
                    case DSEventType.Warning:
                    case DSEventType.Awakening:
                    case DSEventType.Faction:
                    case DSEventType.System:
                    default:
                        // 其他事件不弹通知，只记录到内部事件列表
                        break;
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[事件管理器] 触发通知失败: " + e.Message);
            }
        }

        /// <summary>判断是否应该输出到控制台</summary>
        private static bool ShouldLogToConsole(DSEventType type, int tier = 0)
        {
            switch (type)
            {
                case DSEventType.Breakthrough:
                    // 只记录6阶以上的突破到控制台
                    return tier >= 6;
                case DSEventType.RuleBacklash:
                case DSEventType.Disaster:
                case DSEventType.Warning:
                case DSEventType.Error:
                case DSEventType.System:
                    return true;
                case DSEventType.Awakening:
                    // 觉醒事件不输出到控制台（太多了）
                    return false;
                default:
                    return _logDetailed;
            }
        }

        /// <summary>获取事件前缀</summary>
        private static string GetEventPrefix(DSEventType type)
        {
            switch (type)
            {
                case DSEventType.Breakthrough: return "";
                case DSEventType.RuleBacklash: return "";
                case DSEventType.Awakening: return "";
                case DSEventType.Disaster: return "";
                case DSEventType.Faction: return "";
                case DSEventType.Divine: return "";
                case DSEventType.System: return "";
                case DSEventType.Warning: return "";
                case DSEventType.Error: return "";
                default: return "";
            }
        }

        /// <summary>获取当前年份</summary>
        private static int GetCurrentYear()
        {
            if (World.world == null) return 0;
            return (int)(World.world.getCurWorldTime() / 365.0);
        }
    }
}







