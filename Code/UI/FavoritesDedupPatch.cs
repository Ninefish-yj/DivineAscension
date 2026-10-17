// ============================================================
// 收藏夹显示去重（v0.18.95，治标层）
//
//  根因：原版 WindowFavorites.getObjects() 遍历 World.world.units，
//  对"同一角色在世界存在多个同名实例"（旧版本回归池降临/圣所放置
//  未查重产生的存量重复单位）不设去重，收藏夹会重复显示同一角色。
//
//  本 Patch：按单位名（getName）去重，保留首个实例；并同步
//  _temp_list_actor 字段，保证窗口标题计数与列表一致。
//
//  根治：TrueGodManager 的召唤/放置入口（ReturnGodToWorld /
//  TrySummonRandomGod / TrySummonGodByName / 圣所·回归池 UI 按钮）
//  已加"世界同名存活单位查重"，不再新增重复实例。
// ============================================================

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Code.UI
{
    /// <summary>收藏夹显示去重：Patch 原版 WindowFavorites.getObjects</summary>
    [HarmonyPatch]
    public static class FavoritesDedupPatch
    {
        public static bool Prepare()
        {
            return AccessTools.Method(typeof(WindowFavorites), "getObjects", System.Type.EmptyTypes) != null;
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(WindowFavorites), "getObjects", System.Type.EmptyTypes);
        }

        private static void Postfix(WindowFavorites __instance, ref List<Actor> __result)
        {
            if (__result == null || __result.Count <= 1) return;
            try
            {
                var seen = new HashSet<string>();
                var deduped = new List<Actor>();
                foreach (var actor in __result)
                {
                    if (actor == null)
                    {
                        deduped.Add(actor);
                        continue;
                    }
                    string name = actor.getName();
                    if (string.IsNullOrEmpty(name))
                    {
                        deduped.Add(actor);
                        continue;
                    }
                    if (seen.Add(name)) deduped.Add(actor);
                }
                if (deduped.Count != __result.Count)
                {
                    __result = deduped;
                    // 同步计数字段：WindowFavorites.show() 用 _temp_list_actor.Count 显示标题计数
                    Traverse.Create(__instance).Field("_temp_list_actor").SetValue(deduped);
                }
            }
            catch (System.Exception e)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] 收藏夹去重失败: " + e.Message);
            }
        }
    }
}
