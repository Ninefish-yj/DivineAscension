
using System;
using UnityEngine;

namespace Code.UI
{
    public static class UILocalization
    {
        // 当前语言（跟随游戏设置，默认中文cz）
        public static string CurrentLanguage = "cz";

        private static string _lastError = "";

        /// <summary>
        /// 获取本地化文本（使用游戏原生NML本地化系统）
        /// </summary>
        public static string Get(string key)
        {
            try
            {
                // NML自动加载Locales/cz.json和Locales/en.json
                // 第三个参数false表示不使用缓存，确保获取最新翻译
                string result = LocalizedTextManager.getText(key, null, false);
                if (!string.IsNullOrEmpty(result) && result != key)
                {
                    return result;
                }
                return key;
            }
            catch (Exception e)
            {
                _lastError = e.Message;
                return key;
            }
        }

        /// <summary>
        /// 获取境界名称
        /// </summary>
        public static string GetTierName(int tier)
        {
            // 从本地化文件获取境界名称（统一key格式：Divine_{tier}_name）
            if (tier >= 1 && tier <= 13)
            {
                return Get($"Divine_{tier}_name");
            }
            return "凡人";
        }


    }
}
