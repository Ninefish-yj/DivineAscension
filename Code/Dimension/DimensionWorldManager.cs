// ============================================================
// 维度空间多世界切换管理器（v0.13.0）
// 维度空间 = 真·独立地图（参考机械师模组 SuperMechMultiWorld 模式）
// 进入：备份主世界 → 加载/生成维度空间地图 → 随行单位以化身重建（同 id + custom_data 全量复制）
// 退出：保存空间地图 → 数据快照 → 加载主世界备份 → 修炼成果写回 → 恢复存档路径
// 种族保持：化身按原 assetId 生成，退出时主世界单位从备份恢复，杜绝"换存档变种族"
// ============================================================

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Code.Core;
using Code.Data;
using Code.UI;
using UnityEngine;

namespace Code.Dimension
{
    /// <summary>
    /// 维度空间多世界切换核心：存档隔离 + 单位随行 + 返回主世界
    /// </summary>
    public static class DimensionWorldManager
    {
        // ========== 存档布局（全部隔离在模组目录，不动玩家原存档） ==========
        private static string SaveRoot => Path.Combine(Application.persistentDataPath, "GenCultivationDim");
        private static string MainBackupDir => Path.Combine(SaveRoot, "main_backup");
        private static string SpaceDir => Path.Combine(SaveRoot, "dim_space");
        private static string StateFile => Path.Combine(SaveRoot, "dim_state.txt");

        private static bool _initialized = false;

        /// <summary>玩家原始存档路径（进入空间前记录，退出时恢复）</summary>
        private static string _originalSavePath = "";

        /// <summary>当前是否处于维度空间地图</summary>
        public static bool IsInDimensionWorld { get; private set; } = false;

        // ========== 单位 custom_data 数据快照 ==========
        /// <summary>单单位 custom_data 全量快照（float/int/bool/string/long 五容器）</summary>
        public class CustomDataSnapshot
        {
            public Dictionary<string, float> Floats = new Dictionary<string, float>();
            public Dictionary<string, int> Ints = new Dictionary<string, int>();
            public Dictionary<string, bool> Bools = new Dictionary<string, bool>();
            public Dictionary<string, string> Strings = new Dictionary<string, string>();
            public Dictionary<string, long> Longs = new Dictionary<string, long>();
        }

        // ========== 初始化 ==========
        public static void Init()
        {
            if (_initialized) return;
            try
            {
                if (!Directory.Exists(SaveRoot)) Directory.CreateDirectory(SaveRoot);
                try
                {
                    _originalSavePath = SaveManager.currentSavePath ?? "";
                }
                catch (Exception) { _originalSavePath = ""; }
                _initialized = true;
                DSDebug.Verbose("[维度空间] 多世界切换系统初始化完成: " + SaveRoot);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 多世界初始化失败: " + e.Message);
            }
        }

        // ========== 进入维度空间（世界切换） ==========
        /// <summary>单位进入维度空间（主地图 → 维度空间地图，所有已登记的空间居民随行）</summary>
        public static bool TryEnterDimensionWorld(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            Init();

            if (IsInDimensionWorld)
            {
                DSNotificationManager.NotifyInfo("维度空间已开启，无需重复进入。");
                return false;
            }

            try
            {
                // 防御：境界同步（同 EnterDimension 原逻辑）
                try { CultivationData.SyncFromTraits(actor); } catch (Exception) { }

                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 12)
                {
                    DSNotificationManager.NotifyInfo(UILocalization.Get("dim_cannot_enter"));
                    return false;
                }
                if (DimensionRealmManager.IsBanned(actor.getID()))
                {
                    DSNotificationManager.NotifyInfo("维度空间拒绝进入：该单位已被空间主宰封禁");
                    return false;
                }

                // 标记进入者为空间居民
                CultivationData.SetInDimensionSpace(actor, true);

                // 收集随行单位 = 主地图上所有已登记的空间居民（12阶+存活）
                var travelers = CollectTravelers();
                if (travelers.Count == 0)
                {
                    CultivationData.SetInDimensionSpace(actor, false);
                    DSNotificationManager.NotifyInfo("维度空间进入失败：没有可随行的空间居民。");
                    return false;
                }

                // 记录原始存档路径（若尚未记录）
                if (string.IsNullOrEmpty(_originalSavePath))
                {
                    try { _originalSavePath = SaveManager.currentSavePath ?? ""; } catch (Exception) { }
                }

                // ① 备份主世界（压缩、免目录校验——目录可能不存在需自建）
                bool backed = SaveWorldTo(MainBackupDir);
                if (!backed)
                {
                    CultivationData.SetInDimensionSpace(actor, false);
                    DSNotificationManager.NotifyInfo("维度空间进入失败：主世界备份失败，已取消切换。");
                    return false;
                }

                // ② 切换地图：有空间存档则加载，否则生成新空间地图
                bool spaceExists = HasSave(SpaceDir);
                if (spaceExists)
                {
                    LoadWorldFrom(SpaceDir);
                }
                else
                {
                    if (!GenerateSpaceWorld()) return false;
                }

                // ③ 化身重建：确保每个随行单位在空间地图都有同 id 化身
                int spawned = 0;
                foreach (Actor t in travelers)
                {
                    if (t == null || !t.isAlive()) continue;
                    if (FindActorById(t.getID()) == null)
                    {
                        if (SpawnTraveler(t)) spawned++;
                    }
                }
                DSDebug.Verbose($"[维度空间] 空间化身重建完成，新建 {spawned} 个");

                // ④ 状态切换
                IsInDimensionWorld = true;
                try { SaveManager.currentSavePath = SpaceDir; } catch (Exception) { }
                WriteState(true);

                // ⑤ 重建空间居民列表（空间地图化身）
                try { DimensionRealmManager.RebuildInhabitants(); } catch (Exception) { }

                DSNotificationManager.NotifyMajor(UILocalization.Get("dim_enter"),
                    string.Format(UILocalization.Get("dim_enter_macro"), actor.getName()));
                DSDebug.Warning($"[GeneCultivation] 维度空间已开启（世界切换）：{travelers.Count} 名空间居民随行");
                return true;
            }
            catch (Exception e)
            {
                try { CultivationData.SetInDimensionSpace(actor, false); } catch (Exception) { }
                DSDebug.Warning("[GeneCultivation] 进入维度空间（世界切换）失败: " + e.Message);
                return false;
            }
        }

        // ========== 返回主世界（世界切换） ==========
        /// <summary>返回主世界（维度空间地图 → 主地图；修炼成果写回，空间居民标记清除）</summary>
        public static bool ReturnToMainWorld()
        {
            if (!IsInDimensionWorld) return false;
            Init();

            try
            {
                // ① 保存空间地图（化身与空间数据持久化，供下次进入）
                bool saved = SaveWorldTo(SpaceDir);
                if (!saved)
                {
                    DSNotificationManager.NotifyInfo("返回主世界失败：维度空间存档失败。");
                    return false;
                }

                // ② 采集空间居民数据快照（先取纯数据，切图后对象失效也不影响）
                var snapshots = new Dictionary<long, CustomDataSnapshot>();
                foreach (Actor a in EnumerateAliveActors())
                {
                    if (a == null || !a.isAlive()) continue;
                    if (!CultivationData.IsInDimensionSpace(a)) continue;
                    snapshots[a.getID()] = ReadCustomData(a);
                }

                // ③ 加载主世界备份
                bool loaded = LoadWorldFrom(MainBackupDir);
                if (!loaded)
                {
                    DSNotificationManager.NotifyInfo("返回主世界失败：主世界备份加载失败。");
                    return false;
                }

                // ④ 修炼成果写回 + 清除空间标记（主世界单位 = 备份中的原单位）
                int written = 0;
                foreach (var kv in snapshots)
                {
                    Actor mainUnit = FindActorById(kv.Key);
                    if (mainUnit == null || !mainUnit.isAlive()) continue;
                    WriteCustomData(mainUnit, kv.Value);
                    try { CultivationData.SetInDimensionSpace(mainUnit, false); } catch (Exception) { }
                    written++;
                }
                DSDebug.Verbose($"[维度空间] 主世界单位回写完成：{written} 名");

                // ⑤ 状态恢复
                IsInDimensionWorld = false;
                try { SaveManager.currentSavePath = _originalSavePath; } catch (Exception) { }
                WriteState(false);

                // ⑥ 重建居民列表（主地图无空间居民）
                try { DimensionRealmManager.RebuildInhabitants(); } catch (Exception) { }

                DSNotificationManager.NotifyMajor(UILocalization.Get("dim_exit"),
                    string.Format(UILocalization.Get("dim_exit"), "空间主宰"));
                DSDebug.Warning("[GeneCultivation] 已返回主世界（世界切换），修炼成果已写回");
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[GeneCultivation] 返回主世界（世界切换）失败: " + e.Message);
                return false;
            }
        }

        // ========== 世界存档/加载/生成 ==========
        private static bool SaveWorldTo(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                try { SaveManager.currentSavePath = dir; } catch (Exception) { }
                SaveManager.saveWorldToDirectory(dir, true, false);
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 世界存档失败: " + e.Message);
                return false;
            }
        }

        private static bool LoadWorldFrom(string path)
        {
            try
            {
                try { SaveManager.currentSavePath = path; } catch (Exception) { }

                // MapBox.instance.save_manager 是 internal SaveManager
                // loadWorld(string, bool) 是 internal 方法，反射调用（照机械师模组 SuperMechMultiWorld 模式）
                FieldInfo saveMgrField = typeof(MapBox).GetField("save_manager",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (saveMgrField == null || MapBox.instance == null)
                {
                    DSDebug.Warning("[维度空间] 无法获取 save_manager 字段");
                    return false;
                }
                object saveMgr = saveMgrField.GetValue(MapBox.instance);
                if (saveMgr == null)
                {
                    DSDebug.Warning("[维度空间] save_manager 为空");
                    return false;
                }
                MethodInfo loadMethod = saveMgr.GetType().GetMethod("loadWorld",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(string), typeof(bool) },
                    null);
                if (loadMethod == null)
                {
                    DSDebug.Warning("[维度空间] 无法获取 loadWorld 方法");
                    return false;
                }
                loadMethod.Invoke(saveMgr, new object[] { path, false });
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 世界加载失败: " + e.Message);
                return false;
            }
        }

        /// <summary>生成全新维度空间地图（随后立即存档）</summary>
        private static bool GenerateSpaceWorld()
        {
            try
            {
                if (!Directory.Exists(SpaceDir)) Directory.CreateDirectory(SpaceDir);
                try { SaveManager.currentSavePath = SpaceDir; } catch (Exception) { }

                if (MapBox.instance != null)
                {
                    MapBox.instance.generateNewMap();
                }

                // 立即存档（首进即有存档，避免空地图状态下崩溃丢状态）
                SaveManager.saveWorldToDirectory(SpaceDir, true, false);
                DSDebug.Verbose("[维度空间] 新维度空间地图生成完成");
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 生成维度空间地图失败: " + e.Message);
                return false;
            }
        }

        private static bool HasSave(string dir)
        {
            try
            {
                return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "map.meta"));
            }
            catch (Exception) { return false; }
        }

        // ========== 单位随行：化身重建 + custom_data 全量复制 ==========
        /// <summary>重建化身：按原种族(assetId)生成，反射还原原 id，复制全部 custom_data</summary>
        private static bool SpawnTraveler(Actor source)
        {
            try
            {
                string assetId = GetAssetId(source);
                if (string.IsNullOrEmpty(assetId)) return false;

                WorldTile tile = GetSpawnTile();
                if (tile == null) return false;

                // 按原种族生成（种族保持）
                Actor avatar = World.world.units.spawnNewUnit(assetId, tile, false, true, 6f, null, false, true);
                if (avatar == null || !avatar.isAlive()) return false;

                // 还原原 id（反射：id 在 actorData 上，spawn 后为新建 id）
                TrySetActorId(avatar, source.getID());

                // 全量复制 custom_data（境界/能量/失控/空间等级/维度标记等）
                CopyCustomData(source, avatar);

                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 化身重建失败: " + e.Message);
                return false;
            }
        }

        private static string GetAssetId(Actor actor)
        {
            try
            {
                object data = actor.getData();
                if (data == null) return null;
                var prop = data.GetType().GetProperty("asset_id",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return prop.GetValue(data) as string;
                var field = data.GetType().GetField("asset_id",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(data) as string;
                return null;
            }
            catch (Exception) { return null; }
        }

        private static void TrySetActorId(Actor actor, long originalId)
        {
            try
            {
                object data = actor.getData();
                if (data == null) return;
                var field = data.GetType().GetField("id",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(data, originalId);
            }
            catch (Exception) { }
        }

        /// <summary>复制源单位 custom_data 五容器到目标单位</summary>
        private static void CopyCustomData(Actor source, Actor target)
        {
            try
            {
                object srcData = source.getData();
                object dstData = target.getData();
                if (srcData == null || dstData == null) return;
                CopyContainer<float>(srcData, dstData, "custom_data_float");
                CopyContainer<int>(srcData, dstData, "custom_data_int");
                CopyContainer<bool>(srcData, dstData, "custom_data_bool");
                CopyContainer<string>(srcData, dstData, "custom_data_string");
                CopyContainer<long>(srcData, dstData, "custom_data_long");
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] custom_data 复制失败: " + e.Message);
            }
        }

        private static void CopyContainer<T>(object srcData, object dstData, string fieldName)
        {
            try
            {
                FieldInfo field = typeof(BaseSystemData).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;
                var srcContainer = field.GetValue(srcData);
                if (srcContainer == null) return;

                var dstContainer = field.GetValue(dstData);
                if (dstContainer == null)
                {
                    // 目标容器为空：实例化同类型容器（与 CultivationData.EnsureCustomDataInitialized 一致）
                    dstContainer = Activator.CreateInstance(typeof(CustomDataContainer<T>));
                    field.SetValue(dstData, dstContainer);
                }

                // 读源 dict
                var dictField = srcContainer.GetType().GetField("dict",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dictField == null) return;
                var srcDict = dictField.GetValue(srcContainer) as Dictionary<string, T>;
                if (srcDict == null) return;
                var dstDict = dictField.GetValue(dstContainer) as Dictionary<string, T>;
                if (dstDict == null) return;

                dstDict.Clear();
                foreach (var kv in srcDict)
                {
                    dstDict[kv.Key] = kv.Value;
                }
            }
            catch (Exception) { }
        }

        /// <summary>读取单位 custom_data 为纯数据快照</summary>
        public static CustomDataSnapshot ReadCustomData(Actor actor)
        {
            var snap = new CustomDataSnapshot();
            try
            {
                object data = actor.getData();
                if (data == null) return snap;
                ReadContainer(data, "custom_data_float", snap.Floats);
                ReadContainer(data, "custom_data_int", snap.Ints);
                ReadContainer(data, "custom_data_bool", snap.Bools);
                ReadContainer(data, "custom_data_string", snap.Strings);
                ReadContainer(data, "custom_data_long", snap.Longs);
            }
            catch (Exception) { }
            return snap;
        }

        private static void ReadContainer<T>(object data, string fieldName, Dictionary<string, T> target)
        {
            try
            {
                FieldInfo field = typeof(BaseSystemData).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;
                var container = field.GetValue(data);
                if (container == null) return;
                var dictField = container.GetType().GetField("dict",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dictField == null) return;
                var dict = dictField.GetValue(container) as Dictionary<string, T>;
                if (dict == null) return;
                foreach (var kv in dict) target[kv.Key] = kv.Value;
            }
            catch (Exception) { }
        }

        /// <summary>把数据快照写回单位 custom_data</summary>
        public static void WriteCustomData(Actor actor, CustomDataSnapshot snap)
        {
            if (snap == null) return;
            try
            {
                object data = actor.getData();
                if (data == null) return;
                WriteContainer(data, "custom_data_float", snap.Floats);
                WriteContainer(data, "custom_data_int", snap.Ints);
                WriteContainer(data, "custom_data_bool", snap.Bools);
                WriteContainer(data, "custom_data_string", snap.Strings);
                WriteContainer(data, "custom_data_long", snap.Longs);
            }
            catch (Exception) { }
        }

        private static void WriteContainer<T>(object data, string fieldName, Dictionary<string, T> values)
        {
            try
            {
                FieldInfo field = typeof(BaseSystemData).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;
                var container = field.GetValue(data);
                if (container == null)
                {
                    // 目标容器为空：实例化同类型容器（与 CultivationData.EnsureCustomDataInitialized 一致）
                    container = Activator.CreateInstance(typeof(CustomDataContainer<T>));
                    field.SetValue(data, container);
                }
                var dictField = container.GetType().GetField("dict",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dictField == null) return;
                var dict = dictField.GetValue(container) as Dictionary<string, T>;
                if (dict == null) return;
                dict.Clear();
                foreach (var kv in values) dict[kv.Key] = kv.Value;
            }
            catch (Exception) { }
        }

        // ========== 辅助 ==========
        /// <summary>收集主地图上所有已登记的空间居民（12阶+存活，且 IsInDimensionSpace=true）</summary>
        private static List<Actor> CollectTravelers()
        {
            var result = new List<Actor>();
            try
            {
                if (World.world == null || World.world.units == null) return result;
                foreach (Actor a in World.world.units.units_only_alive)
                {
                    if (a == null || !a.isAlive()) continue;
                    try
                    {
                        if (CultivationData.IsInDimensionSpace(a)) result.Add(a);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return result;
        }

        private static IEnumerable<Actor> EnumerateAliveActors()
        {
            try
            {
                if (World.world == null || World.world.units == null) return new List<Actor>();
                return World.world.units.units_only_alive;
            }
            catch (Exception) { return new List<Actor>(); }
        }

        private static Actor FindActorById(long id)
        {
            try { return SystemManagerExtensions.FindActorById(id); } catch (Exception) { return null; }
        }

        /// <summary>取空间地图一个非液体 tile（反射 tiles_list）</summary>
        private static WorldTile GetSpawnTile()
        {
            try
            {
                if (MapBox.instance == null) return null;
                FieldInfo field = typeof(MapBox).GetField("tiles_list",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var list = field != null ? field.GetValue(MapBox.instance) as WorldTile[] : null;
                if (list == null || list.Length == 0) return null;

                // 随机采样若干次，取第一个非液体 tile
                for (int i = 0; i < 50; i++)
                {
                    WorldTile t = list[UnityEngine.Random.Range(0, list.Length)];
                    if (t != null && !t.is_liquid) return t;
                }
                // 全失败：取地图中心附近
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i] != null && !list[i].is_liquid) return list[i];
                }
                return null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>写空间状态标记（持久化；供游戏重启后恢复空间）</summary>
        private static void WriteState(bool inSpace)
        {
            try
            {
                File.WriteAllText(StateFile, inSpace ? "1" : "0");
            }
            catch (Exception) { }
        }

        /// <summary>读取空间状态标记</summary>
        public static bool ReadState()
        {
            try
            {
                if (!File.Exists(StateFile)) return false;
                return File.ReadAllText(StateFile).Trim() == "1";
            }
            catch (Exception) { return false; }
        }

        /// <summary>清空多世界运行时状态（主世界单位标记也一并清除，防卡死）</summary>
        public static void Clear()
        {
            try
            {
                IsInDimensionWorld = false;
                WriteState(false);
                _originalSavePath = "";
                try
                {
                    if (World.world != null && World.world.units != null)
                    {
                        foreach (Actor a in World.world.units.units_only_alive)
                        {
                            if (a != null && a.isAlive() && CultivationData.IsInDimensionSpace(a))
                            {
                                try { CultivationData.SetInDimensionSpace(a, false); } catch (Exception) { }
                            }
                        }
                    }
                }
                catch (Exception) { }
                try { DimensionRealmManager.RebuildInhabitants(); } catch (Exception) { }
                DSDebug.Verbose("[维度空间] 多世界状态已清理");
            }
            catch (Exception) { }
        }
    }
}
