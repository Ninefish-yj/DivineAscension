
using System.Collections.Generic;
using Code.UI;

namespace Code.Core
{
    /// <summary>
    /// 事件文案管理器
    /// 集中管理所有事件记录和全局通知的文案
    /// 所有文案走UILocalization本地化系统
    /// </summary>
    public static class DSEventTexts
    {
        /// <summary>当前语言：cz=中文，en=英文</summary>
        public static string CurrentLanguage = "cz";

        // ============================================================
        //  境界突破文案（先驱者 + 普通突破）
        //  所有文案走UILocalization，统一key格式：Divine_{tier}_breakthrough / Divine_{tier}_desc
        // ============================================================

        /// <summary>获取境界突破标题</summary>
        public static string GetBreakthroughTitle(int tier)
        {
            return UILocalization.Get($"Divine_{tier}_breakthrough");
        }

        /// <summary>获取境界突破描述</summary>
        public static string GetBreakthroughDesc(int tier)
        {
            return UILocalization.Get($"Divine_{tier}_desc");
        }

        // ============================================================
        //  先驱者文案（每个境界第一个突破的人）
        //  key格式：pioneer_full_title / pioneer_history_title / pioneer_history_desc
        // ============================================================

        /// <summary>获取先驱者完整通知标题（名字-境界名首位完成了突破标题）</summary>
        public static string GetPioneerFullTitle(string actorName, int tier)
        {
            // 全局通知格式：称号-名字 首位 突破了标题（不含境界名）
            return string.Format(UILocalization.Get("pioneer_full_title"), actorName, GetBreakthroughTitle(tier));
        }

        /// <summary>获取先驱者历史记录标题（不含名字，原版历史面板自动显示单位名作标题）</summary>
        public static string GetPioneerHistoryTitle(string actorName, int tier)
        {
            // 格式：{单位名}首位{突破标题}
            return string.Format(UILocalization.Get("pioneer_history_title"), actorName, GetBreakthroughTitle(tier));
        }

        /// <summary>获取先驱者历史记录描述</summary>
        public static string GetPioneerHistoryDesc(string actorName, int tier, string tierName)
        {
            // 格式：成为世界上首位{境界}，{突破描述}（标题已包含名字，描述不再重复）
            string breakthroughDesc = GetBreakthroughDesc(tier);
            return string.Format(UILocalization.Get("pioneer_history_desc"), tierName, breakthroughDesc);
        }

        // ============================================================
        //  跳阶首位突破文案（越级突破，特殊文案）
        //  key格式：skip_pioneer_full_title / skip_pioneer_history_title / skip_pioneer_history_desc
        // ============================================================

        /// <summary>获取跳阶先驱者完整通知标题</summary>
        public static string GetSkipPioneerFullTitle(string actorName, int tier, int fromTier)
        {
            return string.Format(UILocalization.Get("skip_pioneer_full_title"), actorName, fromTier, tier, GetBreakthroughTitle(tier));
        }

        /// <summary>获取跳阶先驱者历史记录描述</summary>
        public static string GetSkipPioneerHistoryDesc(string actorName, int tier, string tierName, int fromTier)
        {
            // 格式：{单位名}从{fromTier}阶擢升至{tier}阶，{突破标题}
            return string.Format(UILocalization.Get("skip_pioneer_history_desc"), actorName, fromTier, tier, GetBreakthroughTitle(tier));
        }

        // ============================================================
        //  普通重大境界突破描述（非先驱者）
        //  key格式：event_break_N_desc（N为阶数）
        // ============================================================

        // ============================================================
        // 真神相关文案（多套判词，按主要基因染色体分类，每条染色体3套）
        //  key格式：god_birth_{group}_{index} / god_death_{group}_{index}
        // ============================================================

        /// <summary>真神诞生标题</summary>
        public static string GetTrueGodBirthTitle()
        {
            return GetBreakthroughTitle(13);
        }

        /// <summary>获取真神诞生判词（按主要基因染色体随机选择，每条染色体3套）</summary>
        public static string GetTrueGodBirthDesc(string godName, int elementGroup)
        {
            // 主要基因染色体：0=力量, 1=敏捷, 2=体质, 3=智力, 4=感知, 5=意志, 6=异能, 7=潜能
            string[] groupNames = { "matter", "life", "energy", "force", "time", "spirit", "info", "concept" };
            string groupName = elementGroup >= 0 && elementGroup < groupNames.Length ? groupNames[elementGroup] : "concept";

            // 随机选择1-3号判词
            int index = UnityEngine.Random.Range(1, 4);
            string key = $"god_birth_{groupName}_{index}";
            string desc = UILocalization.Get(key);

            // 如果没有找到，使用通用判词
            if (desc == key)
            {
                desc = UILocalization.Get("god_birth_concept_1");
            }

            return CurrentLanguage == "en"
                ? $"{godName}: {desc}"
                : $"{godName}：{desc}";
        }

        // ============================================================
        //  灾变相关文案
        // ============================================================

        // ============================================================
        //  组织相关文案
        // ============================================================

        // ============================================================
        //  纪元相关文案
        // ============================================================

    }
}
