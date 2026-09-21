
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 工具类（publicized环境下直接访问internal字段，不用反射）
    /// </summary>
    public static class DSReflectionHelper
    {
        /// <summary>
        /// 获取 MapBox.map_stats 字段（publicized直连，不用反射）
        /// </summary>
        public static MapStats GetMapStats()
        {
            try
            {
                if (World.world == null) return null;
                return World.world.map_stats;
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取 MapBox.era_manager 字段（publicized直连，不用反射）
        /// </summary>
        public static WorldAgeManager GetEraManager()
        {
            try
            {
                if (World.world == null) return null;
                return World.world.era_manager;
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取世界时间（map_stats.world_time）
        /// </summary>
        public static float GetWorldTime()
        {
            try
            {
                MapStats mapStats = GetMapStats();
                if (mapStats == null) return 0f;
                return (float)mapStats.world_time;
            }
            catch { return 0f; }
        }

        /// <summary>
        /// 获取下一个ID（map_stats.getNextId(prefix)）
        /// </summary>
        public static long? GetNextId(string prefix)
        {
            try
            {
                MapStats mapStats = GetMapStats();
                if (mapStats == null) return null;
                return mapStats.getNextId(prefix);
            }
            catch { return null; }
        }

        /// <summary>
        /// 安全调用addStatusEffect（直接调用，publicized环境下可访问）
        /// </summary>
        public static void SafeAddStatusEffect(Actor actor, string statusId, float duration, bool addToUI = true)
        {
            if (actor == null) return;
            try
            {
                actor.addStatusEffect(statusId, duration, addToUI);
            }
            catch (System.Exception e)
            {
                DSDebug.Verbose($"SafeAddStatusEffect失败: {e.Message}");
            }
        }
    }
}
