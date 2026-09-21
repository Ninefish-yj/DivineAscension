
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Code.Core;

namespace Code.Sect
{
    /// <summary>灭门组织档案（可序列化）</summary>
    [Serializable]
    public class SectArchive
    {
        public string Name;          // 组织名
        public string AncestorName;  // 创始人姓名（可能已失传）
        public int AncestorTier;     // 创始人境界
        public int Breaks;           // 传承断裂次数
        public int BreakYear;        // 最近断裂年份
        public int DeathYear;        // 灭门年份（限时复燃窗口起点）
    }

    /// <summary>组织档案存档补丁（随存档独立存储）</summary>
    public static class SectArchivePatcher
    {
        private static Harmony _harmony;
        private static bool _initialized = false;

        /// <summary>初始化补丁（只调用一次，ModClass.OnModLoad）</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                _harmony = new Harmony("DivineAscension.SectArchive");

                var loadMethod = AccessTools.Method(typeof(SaveManager), "loadWorld", new Type[] { typeof(string), typeof(bool) });
                if (loadMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(SectArchivePatcher), nameof(LoadWorldPostfix));
                    _harmony.Patch(loadMethod, postfix: postfix);
                    DSDebug.Verbose("[SectArchive] loadWorld补丁成功");
                }
                else
                {
                    DSDebug.Warning("[SectArchive] 未找到SaveManager.loadWorld方法");
                }

                var saveMethod = AccessTools.Method(typeof(SaveManager), "saveMapData");
                if (saveMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(SectArchivePatcher), nameof(SaveMapDataPostfix));
                    _harmony.Patch(saveMethod, postfix: postfix);
                    DSDebug.Verbose("[SectArchive] saveMapData补丁成功");
                }
                else
                {
                    DSDebug.Warning("[SectArchive] 未找到SaveManager.saveMapData方法");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[SectArchive] 初始化失败: " + e.Message);
            }
        }

        /// <summary>加载世界后读取组织档案</summary>
        private static void LoadWorldPostfix(string pPath)
        {
            try
            {
                string saveFolder = GetSaveFolderPath(pPath);
                if (string.IsNullOrEmpty(saveFolder)) return;

                string archiveFile = Path.Combine(saveFolder, "divine_ascension_sect_archives.json");
                var loaded = new List<SectArchive>();
                if (File.Exists(archiveFile))
                {
                    string json = File.ReadAllText(archiveFile);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var wrapper = JsonUtility.FromJson<SerializableSectArchiveList>(json);
                        if (wrapper != null && wrapper.archives != null)
                            loaded = wrapper.archives;
                    }
                }
                // 过滤超时档案（超时永久湮灭）
                SectManager.SetArchives(SectManager.PruneArchives(loaded));
                DSDebug.Verbose("[SectArchive] 从存档加载组织档案: " + loaded.Count + "条");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[SectArchive] 加载组织档案失败: " + e.Message);
            }
        }

        /// <summary>保存地图时写入组织档案（只写未超时的）</summary>
        private static void SaveMapDataPostfix(string pFolder)
        {
            try
            {
                string saveFolder = GetSaveFolderPath(pFolder);
                if (string.IsNullOrEmpty(saveFolder)) return;

                var archives = SectManager.GetArchives();
                if (archives == null || archives.Count == 0) return;

                var valid = SectManager.PruneArchives(archives);
                var wrapper = new SerializableSectArchiveList { archives = valid };
                string json = JsonUtility.ToJson(wrapper, true);

                string archiveFile = Path.Combine(saveFolder, "divine_ascension_sect_archives.json");
                File.WriteAllText(archiveFile, json);
                DSDebug.Verbose("[SectArchive] 保存组织档案到存档: " + valid.Count + "条");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[SectArchive] 保存组织档案失败: " + e.Message);
            }
        }

        /// <summary>获取存档文件夹路径（复用与 PioneerSavePatcher 相同的回退逻辑）</summary>
        private static string GetSaveFolderPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                try
                {
                    var folderMethod = AccessTools.Method(typeof(SaveManager), "folderPath");
                    if (folderMethod != null)
                    {
                        string folder = folderMethod.Invoke(null, new object[] { path }) as string;
                        if (!string.IsNullOrEmpty(folder)) return folder;
                    }
                }
                catch { }
                if (Directory.Exists(path)) return path;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                return null;
            }
            catch { return null; }
        }

        /// <summary>可序列化的档案列表包装（JsonUtility不支持顶层数组）</summary>
        [Serializable]
        private class SerializableSectArchiveList
        {
            public List<SectArchive> archives;
        }
    }
}
