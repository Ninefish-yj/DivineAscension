// ============================================================

using UnityEngine;
using Code.Data;

using Code.UI;

namespace Code.Core
{
    /// <summary>
    /// 自动收藏管理器
    /// 当异能者达到特定境界时自动标记为收藏
    /// </summary>
    public static class AutoFavoriteManager
    {
        // ============================================================
        //  配置开关（统一读取 DengShenConfig，NML设置界面直接生效）
        //  旧版自有静态字段已移除，避免与配置系统脱节
        // ============================================================

        // ============================================================
        //  自动收藏境界阈值（按具体境界分开，不再用笼统的"高境界/突破"）
        // ============================================================
        public const int TIER_8_THRESHOLD = 8;   // 干涉者（基因称号解锁）
        public const int TIER_9_THRESHOLD = 9;   // 解析者
        public const int TIER_10_THRESHOLD = 10;  // 使徒级（触及空间法则）
        public const int TIER_11_THRESHOLD = 11;  // 登神级（脱离种族）
        public const int TIER_12_THRESHOLD = 12;  // 真神级（神性觉醒）
        public const int TIER_13_THRESHOLD = 13;  // 真神（宇宙终极）

        // ============================================================
        //  公共API
        // ============================================================

        /// <summary>检查并自动收藏单位（进阶时调用）</summary>
        public static void CheckAndAutoFavorite(Actor actor)
        {
            if (actor == null) return;

            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0) return;

                bool shouldFavorite = false;
                string reason = "";

                // 按具体境界检查（从高到低，命中即停）
                if (tier >= TIER_13_THRESHOLD)
                {
                    // 13阶超神级：独立开关
                    if (DengShenConfig.AutoFavoriteTier13)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(13);
                    }
                }
                else if (tier >= TIER_12_THRESHOLD)
                {
                    // 12阶真神级：独立开关
                    if (DengShenConfig.AutoFavoriteTier12)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(12);
                    }
                }
                else if (tier >= TIER_11_THRESHOLD)
                {
                    // 11阶登神级：独立开关
                    if (DengShenConfig.AutoFavoriteTier11)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(11);
                    }
                }
                else if (tier >= TIER_10_THRESHOLD)
                {
                    // 10阶使徒级：独立开关
                    if (DengShenConfig.AutoFavoriteTier10)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(10);
                    }
                }
                else if (tier >= TIER_9_THRESHOLD)
                {
                    // 9阶解析者：独立开关
                    if (DengShenConfig.AutoFavoriteTier9)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(9);
                    }
                }
                else if (tier >= TIER_8_THRESHOLD)
                {
                    // 8阶干涉者：独立开关
                    if (DengShenConfig.AutoFavoriteTier8)
                    {
                        shouldFavorite = true;
                        reason = UILocalization.GetTierName(8);
                    }
                }

                // 执行收藏（仅按境界阈值，组织首领不再自动收藏）
                if (shouldFavorite && !ActorDataAccessor.GetData(actor).favorite)
                {
                    ActorDataAccessor.GetData(actor).favorite = true;
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 已自动收藏（{reason}）");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"自动收藏检查失败: {e.Message}");
            }
        }

    }
}






