// ============================================================
// 鏂囦欢: Code/Core/DSHistoryManager.cs
// 妯″潡: 鐧荤闀块樁 - 鍘嗗彶绯荤粺绠＄悊鍣?
// 鑱岃矗: 灏嗘ā缁勫ぇ浜嬩欢鍐欏叆WorldBox鍘熺敓鍘嗗彶绯荤粺
// 瀹炵幇鍙傝€? 瑗垮够涓栫晫妯＄粍 WitchcraftWorldLog.cs
//   1. 娉ㄥ唽鍘嗗彶鍒嗙粍 (AssetManager.history_groups)
//   2. 娉ㄥ唽鏃ュ織璧勪骇 (AssetManager.world_log_library)
//   3. 娣诲姞娑堟伅: new WorldLogMessage(asset, text, null, null).add()

using System;
using Code.UI;
using UnityEngine;
using Code.Core;

namespace Code.Core
{
    /// <summary>
    /// 鍘嗗彶绯荤粺绠＄悊鍣?
    /// 灏嗘ā缁勫ぇ浜嬩欢鍐欏叆WorldBox鍘熺敓鍘嗗彶鏃ュ織锛岀帺瀹跺彲鍦ㄥ巻鍙茬晫闈㈡煡鐪?
    /// 鍙傝€冭タ骞讳笘鐣屾ā缁勭殑绠€鍗曞疄鐜版柟寮?
    /// </summary>
    public static class DSHistoryManager
    {
        private static bool _initialized = false;

        // 鍘嗗彶鍒嗙粍ID
        private const string GROUP_DS = "DengShen";
        private const string GROUP_ICON_PATH = "ui/iconBook";

        // 鑷畾涔塛orldLogAsset锛堥潤鎬佸紩鐢紝鏂逛究鍚庣画浣跨敤锛?
        public static WorldLogAsset BreakthroughLog;
        public static WorldLogAsset AwakeningLog;
        public static WorldLogAsset ElementLog;
        public static WorldLogAsset FactionLog;
        public static WorldLogAsset DisasterLog;
                public static WorldLogAsset DivineLog;
        public static WorldLogAsset AnomalyLog;  // 世界异象
        public static WorldLogAsset SystemLog;

        /// <summary>鍒濆鍖栧巻鍙茬郴缁燂紝娉ㄥ唽鍘嗗彶鍒嗙粍鍜岃嚜瀹氫箟鏃ュ織璧勪骇</summary>
        public static void Init()
        {
            if (_initialized) return;
            try
            {
                // 1. 娉ㄥ唽鍘嗗彶鍒嗙粍锛堝湪鍘嗗彶璁板綍闈㈡澘涓樉绀哄垎缁勬爣绛撅級
                RegisterHistoryGroup();

                // 2. 娉ㄥ唽鎵€鏈夋棩蹇楄祫浜?
                RegisterAllLogAssets();

                _initialized = true;
                DSDebug.Verbose("[DivineAscension] History system log");
            }
            catch (Exception)
            {
                DSDebug.Warning("[DivineAscension] History system warning");
            }
        }

        /// <summary>娉ㄥ唽鍘嗗彶鍒嗙粍</summary>
        private static void RegisterHistoryGroup()
        {
            try
            {
                if (AssetManager.history_groups == null)
                {
                DSDebug.Verbose("[DivineAscension] History system verbose");
                    return;
                }

                // 妫€鏌ュ垎缁勬槸鍚﹀凡瀛樺湪
                bool groupExists = false;
                foreach (HistoryGroupAsset group in AssetManager.history_groups.list)
                {
                    if (group.id == GROUP_DS)
                    {
                        groupExists = true;
                        break;
                    }
                }

                if (!groupExists)
                {
                    HistoryGroupAsset dsGroup = new HistoryGroupAsset
                    {
                        id = GROUP_DS,
                        icon_path = GROUP_ICON_PATH
                    };
                    AssetManager.history_groups.add(dsGroup);
                DSDebug.Verbose("[DivineAscension] History system log");
                }
            }
            catch (Exception)
            {
                DSDebug.Warning("[DivineAscension] History system warning");
            }
        }

        /// <summary>娉ㄥ唽鎵€鏈夋棩蹇楄祫浜?/summary>
        private static void RegisterAllLogAssets()
        {
            // 7绫绘棩蹇楄祫浜?
            BreakthroughLog = RegisterLogAsset("ds_log_breakthrough", UILocalization.Get("history_category_breakthrough"), "#C9A86A");
            AwakeningLog    = RegisterLogAsset("ds_log_awakening",    UILocalization.Get("history_category_awakening"), "#7BA7BC");
            ElementLog       = RegisterLogAsset("ds_log_element",       UILocalization.Get("history_category_element"), "#B87A9E");
            FactionLog       = RegisterLogAsset("ds_log_faction",       UILocalization.Get("history_category_sect"), "#7BA67B");
            DisasterLog      = RegisterLogAsset("ds_log_disaster",      UILocalization.Get("history_category_disaster"), "#BC7A5A");
                    DivineLog        = RegisterLogAsset("ds_log_divine",        UILocalization.Get("history_category_god"), "#9E8ABC");
        AnomalyLog       = RegisterLogAsset("ds_log_anomaly",       UILocalization.Get("history_category_anomaly"), "#6A9BC9");  // 蓝色：世界异象
            SystemLog        = RegisterLogAsset("ds_log_system",        UILocalization.Get("history_category_system"), "#8A9AAB");
        }

        /// <summary>娉ㄥ唽鍗曚釜鏃ュ織璧勪骇锛堝弬鑰冭タ骞讳笘鐣屽疄鐜帮級</summary>
        private static WorldLogAsset RegisterLogAsset(string id, string localeId, string colorHex)
        {
            try
            {
                // 妫€鏌ヨ祫浜ф槸鍚﹀凡瀛樺湪
                if (AssetManager.world_log_library.has(id))
                {
                    return AssetManager.world_log_library.get(id);
                }

                // 鍒涘缓骞舵坊鍔犳柊璧勪骇
                WorldLogAsset asset = new WorldLogAsset
                {
                    id = id,
                    group = GROUP_DS,
                    path_icon = "ui/iconBook",
                    color = ParseColor(colorHex),
                    text_replacer = delegate (WorldLogMessage pMessage, ref string pText)
                    {
                        // 瑗垮够涓栫晫瀹炵幇锛氫粠special1鑾峰彇鏄剧ず鏂囨湰
                        if (!string.IsNullOrEmpty(pMessage.special1))
                        {
                            pText = pMessage.special1;
                        }
                    }
                };

                return AssetManager.world_log_library.add(asset);
            }
            catch (Exception)
            {
                DSDebug.Warning("[DivineAscension] History system warning");
                return null;
            }
        }

        /// <summary>璁板綍澶т簨浠跺埌WorldBox鍘熺敓鍘嗗彶绯荤粺锛堢畝鍖栫増锛屽弬鑰冭タ骞讳笘鐣岋級</summary>
        public static void LogEvent(string category, string title, string description, string actorName = null)
        {
            try
            {
                if (World.world == null) return;

                // 鏍规嵁鍒嗙被鑾峰彇瀵瑰簲鐨勬棩蹇楄祫浜?
                WorldLogAsset asset = CategoryToAsset(category);
                if (asset == null)
                {
                DSDebug.Verbose("[DivineAscension] History system verbose");
                    return;
                }

                // 鏋勫缓鏃ュ織鏂囨湰
                string logText = title;
                if (!string.IsNullOrEmpty(description))
                {
                    logText = title + " " + description;
                }
                if (!string.IsNullOrEmpty(actorName))
                {
                    logText = actorName + " " + logText;
                }

                // 鍒涘缓鏃ュ織娑堟伅锛堝弬鑰冭タ骞讳笘鐣屽疄鐜帮紝鐩存帴鎶婃枃鏈斁鍦ㄧ浜屼釜鍙傛暟锛?
                WorldLogMessage message = new WorldLogMessage(asset, "", null, null);
                message.special1 = logText;

                // 璁剧疆棰滆壊
                message.color_special1 = asset.color;

                //  鍏抽敭锛氱洿鎺ヨ皟鐢╝dd()鏂规硶娣诲姞鍒板巻鍙茶褰曪紙涓嶉渶瑕佸弽灏勬煡鎵剧獥鍙ｏ級
                message.add();

                DSDebug.Verbose("[DivineAscension] History system verbose");
            }
            catch (Exception)
            {
                DSDebug.Warning("[DivineAscension] History system warning");
            }
        }

        /// <summary>璁板綍澶т簨浠讹紝甯ctor寮曠敤锛堝弬鑰冭タ骞讳笘鐣孉ddBreakthroughLog锛?/summary>
        public static void LogEventWithActor(string category, string title, string description, Actor actor = null)
        {
            try
            {
                if (World.world == null) return;

                WorldLogAsset asset = CategoryToAsset(category);
                if (asset == null) return;

                // 鏋勫缓鏃ュ織鏂囨湰
                string logText = title;
                if (!string.IsNullOrEmpty(description))
                {
                    logText = title + " " + description;
                }

                // 鍒涘缓鏃ュ織娑堟伅锛堝弬鑰冭タ骞讳笘鐣屽疄鐜帮紝鐩存帴鎶婃枃鏈斁鍦ㄧ浜屼釜鍙傛暟锛?
                WorldLogMessage message = new WorldLogMessage(asset, "", null, null);
                message.special1 = logText;

                // 璁剧疆鍗曚綅淇℃伅锛堝弬鑰冭タ骞讳笘鐣屽疄鐜帮級
                if (actor != null)
                {
                    message.unit = actor;
                    message.unit_id = actor.getID();
                    message.location = actor.current_position;
                }

                // 璁剧疆棰滆壊
                message.color_special1 = asset.color;

                // 璁剧疆鏃堕棿鎴冲亸绉伙細鍚屼竴tick鍐呭厛鍐欏叆鐨勬敹鏉熻褰曟瘮涓昏褰曟棭1tick锛屼笘鐣屽巻鍙叉寜timestamp闄嶅簭鏄剧ず锛屼繚璇佷富璁板綍鍦ㄤ笂銆佹敹鏉熷湪涓嬶紙鍚宼ick鍐呭涓ゆ潯璁板綍List.Sort涓嶇ǔ瀹氾級

                // 鐩存帴娣诲姞鍒板巻鍙茶褰?
                message.add();
            }
            catch (Exception)
            {
                DSDebug.Warning("[DivineAscension] History system warning");
            }
        }

        /// <summary>鏍规嵁鍒嗙被鑾峰彇瀵瑰簲鐨勬棩蹇楄祫浜?/summary>
        private static WorldLogAsset CategoryToAsset(string category)
        {
            if (string.IsNullOrEmpty(category)) return SystemLog;
            switch (category.ToLowerInvariant())
            {
                case "breakthrough": return BreakthroughLog;
                case "awakening":    return AwakeningLog;
                case "element":      return ElementLog;
                case "faction":      return FactionLog;
                case "disaster":     return DisasterLog;
                                case "divine":       return DivineLog;
                case "anomaly":      return AnomalyLog;
                case "system":       return SystemLog;
                default:             return SystemLog;
            }
        }

        /// <summary>瑙ｆ瀽棰滆壊瀛楃涓?/summary>
        private static Color ParseColor(string hex)
        {
            try
            {
                if (ColorUtility.TryParseHtmlString(hex, out Color color))
                {
                    return color;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] ParseColor 异常: " + dsEx.Message); }
            return Toolbox.color_log_neutral;
        }
    }
}




