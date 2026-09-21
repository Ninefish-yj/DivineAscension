
using System;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 状态检查辅助
    /// </summary>
    public static class StatusHelper
    {
        /// <summary>直接调用hasStatus方法</summary>
        public static bool HasStatus(BaseSimObject obj, string statusId)
        {
            try
            {
                if (obj == null || string.IsNullOrEmpty(statusId)) return false;
                return obj.hasStatus(statusId);
            }
            catch
            {
                return false;
            }
        }
    }
}

namespace Code.Data
{
    /// <summary>
    /// Actor数据访问器
    /// 直接访问data/stats字段（源码模组模式，完全信任）
    /// </summary>
    public static class ActorDataAccessor
    {
        /// <summary>初始化（源码模式下不需要，保留空方法兼容）</summary>
        public static void Init()
        {
            Code.Core.DSDebug.Verbose("[DivineAscension] ActorDataAccessor 源码模式，直接访问data/stats");
        }

        /// <summary>获取Actor的data字段（直接访问）</summary>
        public static ActorData GetData(Actor actor)
        {
            return actor?.data;
        }

        /// <summary>获取BaseSimObject的stats字段（直接访问）</summary>
        public static object GetStats(BaseSimObject obj)
        {
            return obj?.stats;
        }
    }
}
