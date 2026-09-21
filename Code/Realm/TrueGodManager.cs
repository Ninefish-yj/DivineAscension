using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
					"DivineAscension", "data"
				);
				if (!Directory.Exists(dataDir))
				{
					Directory.CreateDirectory(dataDir);
				}
				return Path.Combine(dataDir, "true_gods_global.json");
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
				DSDebug.Verbose($"[DivineAscension] 真神跨存档系统初始化完成，当前共有 {_globalData.TrueGods.Count} 位真神");
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
				DSDebug.Error($"[DivineAscension] 加载真神全局数据失败: {e.Message}");
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
				DSDebug.Error($"[DivineAscension] 保存真神全局数据失败: {e.Message}");
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
					Race = "Unknown",
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
					YearsLived = 0
				};

				_globalData.TrueGods.Add(godData);
				_globalData.TotalTrueGods = _globalData.TrueGods.Count;
				SaveGlobalData();

				DSDebug.Verbose($"[DivineAscension] 真神 {godName} 已载入跨存档神殿，当前共有 {_globalData.TrueGods.Count} 位真神");
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
		/// 生成真神单位（简单版：找一个现有单位提升成真神）
		/// </summary>
		public static Actor SpawnTrueGod(TrueGodData godData)
		{
			if (godData == null || World.world == null) return null;

			try
			{
				// 找一个随机单位提升成真神（简单实现，避免API问题）
				foreach (var actor in World.world.units.units_only_alive)
				{
					if (actor != null && actor.isAlive())
					{
						// 设置名字
						actor.setName(godData.Name);

						// 设置为13阶超神级
						Code.Data.CultivationData.SetRealmTier(actor, 13);

						// 同步真神数据
						CultivationData.SetEnergy(actor, godData.Qi);
						CultivationData.SetMastery(actor, godData.Mastery);
						CultivationData.SetTurbulence(actor, godData.Turbulence);

						return actor;
					}
				}
				return null;
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 生成真神失败: {e.Message}");
				return null;
			}
		}

	}
}




