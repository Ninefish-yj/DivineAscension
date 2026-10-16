using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Code.Data;
using Code.Core;
using UnityEngine;

namespace Code.Realm
{
	/// <summary>
	/// 神名全局注册表（跨存档唯一，v0.18.39）
	/// 神名一旦被某单位获得即永久占用，独属于该单位；即使换存档、读旧档，该神名也不再分配给任何其他单位。
	/// 存储于 persistentDataPath/GeneCultivation/data/divine_names.json，
	/// 与真神跨存档神殿（TrueGodManager）同目录，跨存档共享。
	/// 职责单一：只管理"神名→单位"的占用关系；选名逻辑由 RealmMechanics 负责。
	/// </summary>
	public static class DivineNameRegistry
	{
		/// <summary>神名占用记录</summary>
		[Serializable]
		public class DivineNameEntry
		{
			public string OwnerName;   // 占用单位名称
			public int OwnerId;        // 占用单位ID（ActorData.id）
			public string WorldName;   // 占用时所在世界
			public long TakenAt;       // 占用时间（Ticks）
		}

		/// <summary>注册表持久化结构</summary>
		[Serializable]
		public class RegistryData
		{
			public Dictionary<string, DivineNameEntry> Names = new Dictionary<string, DivineNameEntry>();
			public int TotalTaken = 0;
		}

		private static RegistryData _data;
		private static bool _loaded = false;
		private static bool _swept = false;
		private static string _sweptWorld = null;
		private static readonly object _lock = new object();

		/// <summary>注册表文件路径（与真神神殿同目录，跨存档共享）</summary>
		private static string FilePath
		{
			get
			{
				string dir = Path.Combine(Application.persistentDataPath, "GeneCultivation", "data");
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				return Path.Combine(dir, "divine_names.json");
			}
		}

		/// <summary>懒加载注册表（线程安全）</summary>
		public static void EnsureLoaded()
		{
			lock (_lock)
			{
				if (_loaded) return;
				_loaded = true;
				try
				{
					if (File.Exists(FilePath))
					{
						string json = File.ReadAllText(FilePath);
						_data = JsonConvert.DeserializeObject<RegistryData>(json);
					}
					if (_data == null || _data.Names == null)
					{
						_data = new RegistryData();
					}
					_data.TotalTaken = _data.Names.Count;
				}
				catch (Exception e)
				{
					DSDebug.Warning("[GeneCultivation] 神名注册表加载失败: " + e.Message);
					_data = new RegistryData();
				}
			}
		}

		/// <summary>持久化到磁盘</summary>
		private static void Save()
		{
			lock (_lock)
			{
				try
				{
					_data.TotalTaken = _data.Names.Count;
					File.WriteAllText(FilePath, JsonConvert.SerializeObject(_data, Formatting.Indented));
				}
				catch (Exception e)
				{
					DSDebug.Warning("[GeneCultivation] 神名注册表保存失败: " + e.Message);
				}
			}
		}

		/// <summary>查询神名是否已被占用（跨存档永久占用）</summary>
		public static bool IsTaken(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				return _data.Names.ContainsKey(name);
			}
		}

		/// <summary>已占用神名总数（调试/统计）</summary>
		public static int Count
		{
			get
			{
				EnsureLoaded();
				lock (_lock) { return _data.Names.Count; }
			}
		}

		/// <summary>
		/// 将神名登记给该单位。幂等：同一单位（同名+同ID）重复登记返回 true；
		/// 神名已被其他单位占用时返回 false（绝不覆盖他人神名）。
		/// </summary>
		public static bool Claim(string name, Actor god)
		{
			if (string.IsNullOrEmpty(name) || god == null) return false;
			EnsureLoaded();
			lock (_lock)
			{
				if (_data.Names.TryGetValue(name, out var existing))
				{
					// 同一单位重复登记：幂等放行（读档后单位重新触发登神流程）
					if (existing.OwnerId == ReadActorId(god) && existing.OwnerName == god.getName())
					{
						return true;
					}
					return false; // 神名属于其他单位，不覆盖
				}
				_data.Names[name] = new DivineNameEntry
				{
					OwnerName = god.getName(),
					OwnerId = ReadActorId(god),
					WorldName = World.world?.name ?? "Unknown",
					TakenAt = DateTime.Now.Ticks
				};
				Save();
				return true;
			}
		}

		/// <summary>
		/// 旧档兼容：把当前世界所有已持有 divine_name 的单位登记进注册表（幂等）。
		/// 绑定世界名：切换世界/读新存档后自动重新扫描，防止新世界旧档单位漏扫。
		/// 防止旧存档里已存在的神名被新分配重复使用；旧档自身的历史撞名不追溯覆盖。
		/// </summary>
		public static void EnsureSwept()
		{
			EnsureLoaded();
			lock (_lock)
			{
				string worldName = World.world?.name ?? "Unknown";
				if (_swept && _sweptWorld == worldName) return;
				_swept = true;
				_sweptWorld = worldName;
				try
				{
					if (World.world?.units == null) return;
					var units = World.world.units.units_only_alive;
					if (units == null) return;
					int added = 0;
					foreach (var unit in units)
					{
						if (unit == null) continue;
						var data = ActorDataAccessor.GetData(unit);
						if (data?.custom_data_string == null) continue;
						// v0.18.41 神位制：东名（divine_name）+ 西名（divine_name_west）分别登记；旧档仅单键自动兼容
						string dn;
						if (data.custom_data_string.TryGetValue("divine_name", out dn) && !string.IsNullOrEmpty(dn))
						{
							if (!_data.Names.ContainsKey(dn)) // 保留先登记者
							{
								_data.Names[dn] = new DivineNameEntry
								{
									OwnerName = unit.getName(),
									OwnerId = ReadActorId(unit),
									WorldName = World.world?.name ?? "Unknown",
									TakenAt = DateTime.Now.Ticks
								};
								added++;
							}
						}
						string dnw;
						if (data.custom_data_string.TryGetValue("divine_name_west", out dnw) && !string.IsNullOrEmpty(dnw))
						{
							if (!_data.Names.ContainsKey(dnw))
							{
								_data.Names[dnw] = new DivineNameEntry
								{
									OwnerName = unit.getName(),
									OwnerId = ReadActorId(unit),
									WorldName = World.world?.name ?? "Unknown",
									TakenAt = DateTime.Now.Ticks
								};
								added++;
							}
						}
					}
					if (added > 0) Save();
					DSDebug.Verbose($"[GeneCultivation] 神名注册表扫描完成，本次登记 {added} 个，累计 {_data.Names.Count} 个");
				}
				catch (Exception e)
				{
					DSDebug.Warning("[GeneCultivation] 神名注册表扫描异常: " + e.Message);
				}
			}
		}

		/// <summary>读取单位ID（ActorData.id，与神名系统其他模块一致）</summary>
		private static int ReadActorId(Actor actor)
		{
			try
			{
				return (int)ActorDataAccessor.GetData(actor).id;
			}
			catch (Exception) { return 0; }
		}
	}
}
