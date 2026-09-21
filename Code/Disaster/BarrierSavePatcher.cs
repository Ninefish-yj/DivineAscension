
using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Code.Core;

namespace Code.Disaster
{
    /// <summary>屏障存档数据结构</summary>
    [Serializable]
    public class BarrierSaveData
    {
        public float BarrierIntegrity = 100f;
        public float BarrierDecayRate = 0f;
        public bool BarrierBrokenApplied = false;
    }

    /// <summary>屏障存档补丁（随存档独立存储）</summary>
    public static class BarrierSavePatcher
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
                _harmony = new Harmony("DivineAscension.BarrierSave");

                var loadMethod = AccessTools.Method(typeof(SaveManager), "loadWorld", new Type[] { typeof(string), typeof(bool) });
                if (loadMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(BarrierSavePatcher), nameof(LoadWorldPostfix));
                    _harmony.Patch(loadMethod, postfix: postfix);
                    DSDebug.Verbose("[BarrierSave] loadWorld补丁成功");
                }

                var saveMethod = AccessTools.Method(typeof(SaveManager), "saveMapData");
                if (saveMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(BarrierSavePatcher), nameof(SaveMapDataPostfix));
                    _harmony.Patch(saveMethod, postfix: postfix);
                    DSDebug.Verbose("[BarrierSave] saveMapData补丁成功");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[BarrierSave] 初始化失败: " + e.Message);
            }
        }

        /// <summary>加载世界后读取屏障数据</summary>
        private static void LoadWorldPostfix(string pPath)
        {
            try
            {
                string saveFolder = GetSaveFolderPath(pPath);
                if (string.IsNullOrEmpty(saveFolder)) return;

                string saveFile = Path.Combine(saveFolder, "divine_ascension_barrier.json");
                if (!File.Exists(saveFile))
                {
                    // 新存档：保持默认100%
                    DSDebug.Verbose("[BarrierSave] 新存档，屏障默认100%");
                    return;
                }

                string json = File.ReadAllText(saveFile);
                if (string.IsNullOrEmpty(json)) return;

                var data = JsonUtility.FromJson<BarrierSaveData>(json);
                if (data == null) return;

                // 恢复屏障数据（覆盖Init的100%默认值）
                DisasterManager.SetBarrierIntegrity(data.BarrierIntegrity);
                DisasterManager.SetBarrierBrokenApplied(data.BarrierBrokenApplied);
                DSDebug.Verbose($"[BarrierSave] 从存档加载屏障完整度: {data.BarrierIntegrity:F1}%");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[BarrierSave] 加载屏障数据失败: " + e.Message);
            }
        }

        /// <summary>保存地图时写入屏障数据</summary>
        private static void SaveMapDataPostfix(string pFolder)
        {
            try
            {
                string saveFolder = GetSaveFolderPath(pFolder);
                if (string.IsNullOrEmpty(saveFolder)) return;

                var data = new BarrierSaveData
                {
                    BarrierIntegrity = DisasterManager.GetBarrierIntegrity(),
                    BarrierDecayRate = DisasterManager.GetBarrierDecayRate(),
                    BarrierBrokenApplied = DisasterManager.IsBarrierBrokenApplied()
                };

                string json = JsonUtility.ToJson(data, true);
                string saveFile = Path.Combine(saveFolder, "divine_ascension_barrier.json");
                File.WriteAllText(saveFile, json);
                DSDebug.Verbose($"[BarrierSave] 保存屏障数据: {data.BarrierIntegrity:F1}%");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[BarrierSave] 保存屏障数据失败: " + e.Message);
            }
        }

        /// <summary>获取存档文件夹路径（复用与SectArchivePatcher相同的逻辑）</summary>
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
            catch
            {
                return null;
            }
        }
    }
}
