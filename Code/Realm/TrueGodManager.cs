using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Code.Data;
using Code.Core;
using UnityEngine;

namespace Code.Realm
{
	/// <summary>
	/// 真神跨存档互通管理器
	/// 真神角色数据存储在全局文件中，可在不同存档间互通
	/// 当真神诞生时自动保存，新存档加载时自动载入
	/// </summary>
	public static class TrueGodManager
	{
		/// <summary>
		/// 真神数据结构
		/// </summary>
		[Serializable]
		public class TrueGodData
		{
			public string Name;           // 真神名称
			public string Race;           // 种族
			public int RealmTier;         // 境界（13=真神）
			public List<string> UnlockedElements; // 已解锁基因
			public List<string> ActiveCombos;     // 已激活组合
			public float Qi;              // 异能能量
			public float Mastery;         // 能力掌控度
			public float Turbulence;      // 失控指数
			public float EnlightenmentExp; // 深度觉醒经验
			public long AscensionTime;    // 奇点时间（Ticks）
			public string OriginWorld;    // 起源世界名称
			public int KillCount;         // 击杀数
			public int YearsLived;        // 存活年数
		}

		/// <summary>
		/// 真神全局数据列表
		/// </summary>
		[Serializable]
		public class TrueGodGlobalData
		{
			public List<TrueGodData> TrueGods = new List<TrueGodData>();
			public int TotalTrueGods = 0;
			public long LastUpdateTime = 0;
		}

		private static TrueGodGlobalData _globalData;
		private static bool _initialized = false;
		private static readonly object _lock = new object();

		/// <summary>
		/// 全局真神数据文件路径
		/// 存储在data子目录，与日志目录分开，避免清理日志时误删
		/// </summary>
		private static string GlobalDataPath
		{
			get
			{
				// 和游戏其他数据同位置（Application.persistentDataPath）
				// 这样OWL启动器和Steam直接启动都自动匹配，不需要手动处理两个位置
				string dataDir = Path.Combine(
					Application.persistentDataPath,
					"GeneCultivation", "data"
				);
				if (!Directory.Exists(dataDir))
				{
					Directory.CreateDirectory(dataDir);
				}
				// 旧目录数据迁移兼容：新目录无数据时复制旧目录数据，避免改名后丢失跨存档真神记录
				string newFile = Path.Combine(dataDir, "true_gods_global.json");
				string legacyDir = Path.Combine(Application.persistentDataPath, "DivineAscension", "data");
				string legacyFile = Path.Combine(legacyDir, "true_gods_global.json");
				if (!File.Exists(newFile) && File.Exists(legacyFile))
				{
					try { File.Copy(legacyFile, newFile); } catch { }
				}
				return newFile;
			}
		}

		/// <summary>
		/// 初始化全局真神数据
		/// </summary>
		public static void Init()
		{
			lock (_lock)
			{
				if (_initialized) return;
				_initialized = true;

				LoadGlobalData();
				DSDebug.Verbose($"[GeneCultivation] 真神跨存档系统初始化完成，当前共有 {_globalData.TrueGods.Count} 位真神");
			}
		}

		/// <summary>
		/// 加载全局真神数据
		/// </summary>
		private static void LoadGlobalData()
		{
			try
			{
				if (File.Exists(GlobalDataPath))
				{
					string json = File.ReadAllText(GlobalDataPath);
					_globalData = JsonConvert.DeserializeObject<TrueGodGlobalData>(json);
					if (_globalData == null)
					{
						_globalData = new TrueGodGlobalData();
					}
					// 修复：反序列化时TrueGods可能为null，需要初始化为空列表
					if (_globalData.TrueGods == null)
					{
						_globalData.TrueGods = new List<TrueGodData>();
					}
				}
				else
				{
					_globalData = new TrueGodGlobalData();
					SaveGlobalData();
				}
			}
			catch (Exception e)
			{
				DSDebug.Error($"[GeneCultivation] 加载真神全局数据失败: {e.Message}");
				_globalData = new TrueGodGlobalData();
			}
		}

		/// <summary>
		/// 保存全局真神数据
		/// </summary>
		private static void SaveGlobalData()
		{
			try
			{
				_globalData.LastUpdateTime = DateTime.Now.Ticks;
				string json = JsonConvert.SerializeObject(_globalData, Formatting.Indented);
				File.WriteAllText(GlobalDataPath, json);
			}
			catch (Exception e)
			{
				DSDebug.Error($"[GeneCultivation] 保存真神全局数据失败: {e.Message}");
			}
		}

		/// <summary>
		/// 当真神诞生时保存到全局存储
		/// </summary>
		public static void OnTrueGodAscended(Actor actor)
		{
			if (actor == null || !actor.isAlive()) return;

			int tier = CultivationData.GetRealmTier(actor);
			if (tier < 13) return; // 只有真神才跨存档

			lock (_lock)
			{
				if (!_initialized) Init();

				// 检查是否已存在（按名称+起源世界复合键，同名但不同存档世界的真神允许共存，L-3修复）
				string godName = actor.getName();
				string currentWorld = World.world?.name ?? "Unknown";
				var existing = _globalData.TrueGods.FirstOrDefault(g => g.Name == godName && g.OriginWorld == currentWorld);
				if (existing != null)
				{
					_globalData.TrueGods.Remove(existing);
				}

				// 创建真神数据
				var godData = new TrueGodData
				{
					Name = godName,
					Race = ReadAssetId(actor) ?? "Unknown",
					RealmTier = tier,
										UnlockedElements = CultivationData.GetUnlockedElements(actor),
					ActiveCombos = CultivationData.GetActiveCombos(actor),
					Qi = CultivationData.GetEnergy(actor),
					Mastery = CultivationData.GetMastery(actor),
					Turbulence = CultivationData.GetTurbulence(actor),
					EnlightenmentExp = CultivationData.GetEnlightenmentExp(actor),
					AscensionTime = DateTime.Now.Ticks,
					OriginWorld = World.world?.name ?? "Unknown",
					KillCount = 0,
					YearsLived = actor.getAge()
				};

				_globalData.TrueGods.Add(godData);
				_globalData.TotalTrueGods = _globalData.TrueGods.Count;
				SaveGlobalData();

				DSDebug.Verbose($"[GeneCultivation] 真神 {godName} 已载入跨存档神殿，当前共有 {_globalData.TrueGods.Count} 位真神");
			}
		}

		/// <summary>
		/// 获取所有真神列表
		/// </summary>
		public static List<TrueGodData> GetAllTrueGods()
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				return new List<TrueGodData>(_globalData.TrueGods);
			}
		}

		/// <summary>
		/// 获取真神总数
		/// </summary>
		public static int GetTrueGodCount()
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				return _globalData.TrueGods.Count;
			}
		}

		/// <summary>
		/// 清空所有真神（管理功能，谨慎使用）
		/// </summary>
		public static void ClearAllTrueGods()
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				_globalData.TrueGods.Clear();
				_globalData.TotalTrueGods = 0;
				SaveGlobalData();
			}
		}

		/// <summary>
		/// 删除单个真神（按名称+起源世界）
		/// </summary>
		public static void DeleteTrueGod(string name, string originWorld)
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				var god = _globalData.TrueGods.FirstOrDefault(g => g.Name == name && g.OriginWorld == originWorld);
				if (god != null)
				{
					_globalData.TrueGods.Remove(god);
					_globalData.TotalTrueGods = _globalData.TrueGods.Count;
					SaveGlobalData();
				}
			}
		}

		/// <summary>
		/// 生成真神单位（按真神记录的原种族 asset_id 真·生成，修复"放置后不是原种族"）
		/// 旧记录（Race=Unknown/空）回退：随机取当前世界一个种族的 asset_id
		/// </summary>
		public static Actor SpawnTrueGod(TrueGodData godData)
		{
			if (godData == null || World.world == null) return null;

			try
			{
				// 1. 确定种族：优先真神记录的真实种族
				string assetId = godData.Race;
				if (string.IsNullOrEmpty(assetId) || assetId == "Unknown")
				{
					assetId = PickRandomRaceId();
					if (string.IsNullOrEmpty(assetId)) return null;
				}

				// 2. 找生成位置（非液体地面 tile）
				WorldTile tile = FindSpawnTile();
				if (tile == null) return null;

				// 3. 按原种族生成单位（pMiracleSpawn=true 奇迹生成、pAdultAge=true 成年体）
				Actor god = World.world.units.spawnNewUnit(assetId, tile, false, true, 6f, null, false, true);
				if (god == null || !god.isAlive()) return null;

				// 4. 恢复真神身份与数据（名字/境界/能量/掌控/失控/已解锁元素）
				god.setName(godData.Name);
				CultivationData.SetRealmTier(god, 13);
				CultivationData.SetEnergy(god, godData.Qi);
				CultivationData.SetMastery(god, godData.Mastery);
				CultivationData.SetTurbulence(god, godData.Turbulence);
				if (godData.UnlockedElements != null)
				{
					foreach (string elementId in godData.UnlockedElements)
					{
						try { CultivationData.UnlockElement(god, elementId); } catch (Exception) { }
					}
				}

				DSDebug.Verbose($"[TrueGodManager] 真神 {godData.Name} 已按原种族 {assetId} 放置");
				return god;
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 生成真神失败: {e.Message}");
				return null;
			}
		}

		/// <summary>读取单位真实种族 asset_id（ActorData.asset_id 为 public 属性，反编译 ActorData.cs:121）</summary>
		private static string ReadAssetId(Actor actor)
		{
			try
			{
				if (actor == null) return null;
				ActorData data = actor.getData() as ActorData;
				if (data == null) return null;
				return string.IsNullOrEmpty(data.asset_id) ? null : data.asset_id;
			}
			catch (Exception) { return null; }
		}

		/// <summary>从当前世界随机取一个种族的 asset_id（旧真神记录无种族时的回退）</summary>
		private static string PickRandomRaceId()
		{
			try
			{
				if (World.world == null || World.world.units == null) return null;
				List<Actor> alive = World.world.units.units_only_alive;
				if (alive == null || alive.Count == 0) return null;
				for (int i = 0; i < 20; i++)
				{
					Actor a = alive[UnityEngine.Random.Range(0, alive.Count)];
					if (a == null) continue;
					string id = ReadAssetId(a);
					if (!string.IsNullOrEmpty(id)) return id;
				}
				return null;
			}
			catch (Exception) { return null; }
		}

		/// <summary>找一个非液体地面 tile（与 DimensionWorldManager.GetSpawnTile 同思路）</summary>
		private static WorldTile FindSpawnTile()
		{
			try
			{
				if (MapBox.instance == null) return null;
				FieldInfo field = typeof(MapBox).GetField("tiles_list",
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				WorldTile[] list = field != null ? field.GetValue(MapBox.instance) as WorldTile[] : null;
				if (list == null || list.Length == 0) return null;

				for (int i = 0; i < 50; i++)
				{
					WorldTile t = list[UnityEngine.Random.Range(0, list.Length)];
					if (t != null && !t.is_liquid) return t;
				}
				for (int i = 0; i < list.Length; i++)
				{
					if (list[i] != null && !list[i].is_liquid) return list[i];
				}
				return null;
			}
			catch (Exception) { return null; }
		}

	}
}




