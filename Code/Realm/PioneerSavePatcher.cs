// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 先驱者存档内存储补丁
    /// 补丁SaveManager.loadWorld和saveMapData，把先驱者数据存储在存档目录中
    /// </summary>
    public static class PioneerSavePatcher
    {
        private static Harmony _harmony;
        private static bool _initialized = false;
        private static string _currentSaveFolder = null;

        /// <summary>初始化补丁（只调用一次）</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                _harmony = new Harmony("DivineAscension.PioneerSave");

                // 补丁SaveManager.loadWorld - 加载世界时读取先驱者数据
                // 注意：loadWorld有重载（无参/string,bool），必须按完整签名匹配 loadWorld(string pPath, bool pLoadWorkshop=false)
                var loadMethod = AccessTools.Method(typeof(SaveManager), "loadWorld", new System.Type[] { typeof(string), typeof(bool) });
                if (loadMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(PioneerSavePatcher), nameof(LoadWorldPostfix));
                    _harmony.Patch(loadMethod, postfix: postfix);
                    DSDebug.Verbose("[PioneerSave] loadWorld补丁成功");
                }
                else
                {
                    DSDebug.Warning("[PioneerSave] 未找到SaveManager.loadWorld方法");
                }

                // 补丁SaveManager.saveMapData - 保存地图时写入先驱者数据
                var saveMethod = AccessTools.Method(typeof(SaveManager), "saveMapData");
                if (saveMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(PioneerSavePatcher), nameof(SaveMapDataPostfix));
                    _harmony.Patch(saveMethod, postfix: postfix);
                    DSDebug.Verbose("[PioneerSave] saveMapData补丁成功");
                }
                else
                {
                    DSDebug.Warning("[PioneerSave] 未找到SaveManager.saveMapData方法");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[PioneerSave] 初始化失败: " + e.Message);
            }
        }

        /// <summary>加载世界后读取先驱者数据</summary>
        private static void LoadWorldPostfix(string pPath)
        {
            try
            {
                // 获取存档文件夹路径
                _currentSaveFolder = GetSaveFolderPath(pPath);
                if (string.IsNullOrEmpty(_currentSaveFolder)) return;

                // 标记"已从存档恢复"：读档后首次记录只对齐世界名，不触发新世界重置
                // （loadWorld是异步流程，此处World.world可能尚未加载，世界名变化检测会误判）
                Code.Realm.RealmJudge.MarkLoadedFromSave();

                // 从存档目录读取先驱者数据
                string pioneerFile = Path.Combine(_currentSaveFolder, "divine_ascension_pioneers.json");
                if (File.Exists(pioneerFile))
                {
                    string json = File.ReadAllText(pioneerFile);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var dict = JsonUtility.FromJson<SerializablePioneerDict>(json);
                        if (dict != null && dict.keys != null)
                        {
                            // 清除旧数据，加载新数据
                            Code.Realm.RealmJudge.ClearPioneersInternal();
                            for (int i = 0; i < dict.keys.Count && i < dict.values.Count; i++)
                            {
                                Code.Realm.RealmJudge.SetPioneerInternal(dict.keys[i], dict.values[i]);
                            }
                            DSDebug.Verbose("[PioneerSave] 从存档加载先驱者数据: " + dict.keys.Count + "条");
                        }
                    }
                }
                else
                {
                    // 新存档，清除先驱者数据
                    Code.Realm.RealmJudge.ClearPioneersInternal();
                    DSDebug.Verbose("[PioneerSave] 新存档，先驱者数据已清空");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[PioneerSave] 加载先驱者数据失败: " + e.Message);
            }
        }

        /// <summary>保存地图时写入先驱者数据</summary>
        private static void SaveMapDataPostfix(string pFolder)
        {
            try
            {
                // 获取存档文件夹路径
                string saveFolder = GetSaveFolderPath(pFolder);
                if (string.IsNullOrEmpty(saveFolder)) return;

                // 获取先驱者数据
                var pioneers = Code.Realm.RealmJudge.GetAllPioneersInternal();
                if (pioneers == null || pioneers.Count == 0) return;

                // 序列化为JSON
                var dict = new SerializablePioneerDict();
                dict.keys = new List<int>(pioneers.Keys);
                dict.values = new List<string>(pioneers.Values);
                string json = JsonUtility.ToJson(dict, true);

                // 写入存档目录
                string pioneerFile = Path.Combine(saveFolder, "divine_ascension_pioneers.json");
                File.WriteAllText(pioneerFile, json);
                DSDebug.Verbose("[PioneerSave] 保存先驱者数据到存档: " + pioneers.Count + "条");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[PioneerSave] 保存先驱者数据失败: " + e.Message);
            }
        }

        /// <summary>获取存档文件夹路径</summary>
        private static string GetSaveFolderPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;

                // 使用SaveManager.folderPath获取文件夹路径（如果可用）
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

                // 回退：直接使用路径作为文件夹
                if (Directory.Exists(path)) return path;

                // 如果是文件路径，取目录
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;

                return null;
            }
            catch { return null; }
        }

        /// <summary>可序列化的先驱者字典</summary>
        [Serializable]
        private class SerializablePioneerDict
        {
            public List<int> keys;
            public List<string> values;
        }
    }
}


