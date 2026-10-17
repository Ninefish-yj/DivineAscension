using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Code.Data;
using Code.Core;
using UnityEngine;
using Code.Dimension;

namespace Code.Realm
{
	/// <summary>
	/// 真神跨存档互通管理器（v0.18.52 移动语义）
	/// 圣所 = 跨存档移动仓：单位「移入圣所」= 全量数据快照 + 从当前世界移除（原存档不再存在）；
	/// 「放置」= 整档恢复到目标世界 + 删除圣所记录（移动完成）。
	/// 所有数据完整保留：ActorData 全量（含境界/能量/神名/基因等 custom_data）+ 亚种/氏族/文化/语言。
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
			public string DivineName;     // 神名·东方（v0.18.51：跨存档唯一性——放置时恢复本单位的神名，而非重新分配）
			public string DivineNameWest; // 神名·西方（与东方一一对应）
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
			public List<ChromosomeData> Genome; // 基因序列快照（nucleus 染色体；ChromosomeData 为原版 [Serializable] 类，Newtonsoft.Json 可直接序列化）
			public List<string> Traits;   // 特质快照（ActorData.saved_traits；含登神特质/修炼特质/完美生命）

			// v0.18.52 整档快照：所有数据完整保留（跨存档移动）。旧记录（升级前）这些字段为 null → 放置走业务字段重建降级路径
			public ActorData ActorSnapshot;       // 单位全量数据（含 custom_data：境界/能量/神名/基因标签等）
			public SubspeciesData SubspeciesSnapshot; // 专属亚种（登神单位基因载体）
			public CultureData CultureSnapshot;  // 所属文化
			public LanguageData LanguageSnapshot; // 所属语言
			public ClanData ClanSnapshot;        // 所属氏族

			// v0.18.83 神系回归池字段（旧存档缺省：0/null 兼容）
			public long EnshrineTime;            // 入池时间（Ticks，轮换顶替依据）
			public int ReturnCount;              // 已回归次数（预留：多段轮换用，当前回归即出池）
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

			// v0.18.83 神系回归池：登神级(11)/真神(12)/超神(13)突破时自动快照入池（跨存档传承）。
			// 与圣所（TrueGods，手动移动仓）独立：入池=复制快照，单位继续留在当前世界。
			public List<TrueGodData> Reincarnations = new List<TrueGodData>();
		}

		private static TrueGodGlobalData _globalData;
		private static bool _initialized = false;
		private static readonly object _lock = new object();

		/// <summary>整档序列化配置：包含非公开成员（ActorData 大量字段/属性为 internal/private，需完整复制所有数据）</summary>
		private static readonly JsonSerializerSettings PrivateSettings = new JsonSerializerSettings
		{
			ContractResolver = new DefaultContractResolver
			{
#pragma warning disable 618
				DefaultMembersSearchFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
#pragma warning restore 618
			}
		};

		/// <summary>深拷贝（整档快照用：序列化→反序列化，含私有成员；失败返回 null）</summary>
		private static TData DeepCopy<TData>(TData data) where TData : class
		{
			if (data == null) return null;
			try
			{
				return JsonConvert.DeserializeObject<TData>(
					JsonConvert.SerializeObject(data, Formatting.None, PrivateSettings), PrivateSettings);
			}
			catch (Exception) { return null; }
		}

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
		/// 移入圣所（v0.18.52 移动语义）：全量快照 + 从当前世界移除单位
		/// 移入后原存档不再存在该单位；数据（ActorData 全量+亚种/氏族/文化/语言）完整保留，可在任意存档放置。
		/// 仅超神级（13阶）单位可移入。
		/// </summary>
		public static TrueGodData MoveGodToSanctuary(Actor actor)
		{
			if (actor == null || !actor.isAlive() || World.world == null) return null;

			int tier = CultivationData.GetRealmTier(actor);
			if (tier < 13)
			{
				DSDebug.Warning("[GeneCultivation] 仅超神级（13阶）单位可移入圣所跨存档移动");
				return null;
			}

			try
			{
				TrueGodData godData = BuildTrueGodData(actor);

				lock (_lock)
				{
					if (!_initialized) Init();

					// 按名称+起源世界复合键去重（同名不同世界的真神允许共存）
					var existing = _globalData.TrueGods.FirstOrDefault(g => g.Name == godData.Name && g.OriginWorld == godData.OriginWorld);
					if (existing != null)
					{
						_globalData.TrueGods.Remove(existing);
					}
					_globalData.TrueGods.Add(godData);
					_globalData.TotalTrueGods = _globalData.TrueGods.Count;
					SaveGlobalData();
				}

				// 移动语义：从当前世界移除（原存档不再存在该单位；不走死亡链，无尸体/墓碑）
				actor.removeByMetamorphosis();

				DSDebug.Verbose($"[GeneCultivation] 真神 {godData.Name} 已移入圣所（全量数据+离开当前世界），当前共有 {_globalData.TrueGods.Count} 位");
				return godData;
			}
			catch (Exception e)
			{
				DSDebug.Error($"[GeneCultivation] 移入圣所失败: {e.Message}");
				return null;
			}
		}

		/// <summary>
		/// 构建真神整档快照（公共抽取：圣所移动仓与神系回归池共用）
		/// 冻结单位数据到最新（原版 save 前置）→ 全量快照（ActorData+亚种/氏族/文化/语言+业务字段）
		/// </summary>
		private static TrueGodData BuildTrueGodData(Actor actor)
		{
			actor.prepareForSave();
			string godName = actor.getName();
			string currentWorld = World.world?.name ?? "Unknown";

			return new TrueGodData
			{
				Name = godName,
				Race = ReadAssetId(actor) ?? "Unknown",
				RealmTier = CultivationData.GetRealmTier(actor),
				DivineName = ReadDivineName(actor, "divine_name"),
				DivineNameWest = ReadDivineName(actor, "divine_name_west"),
				UnlockedElements = CultivationData.GetUnlockedElements(actor),
				ActiveCombos = CultivationData.GetActiveCombos(actor),
				Qi = CultivationData.GetEnergy(actor),
				Mastery = CultivationData.GetMastery(actor),
				Turbulence = CultivationData.GetTurbulence(actor),
				EnlightenmentExp = CultivationData.GetEnlightenmentExp(actor),
				AscensionTime = DateTime.Now.Ticks,
				OriginWorld = currentWorld,
				KillCount = 0,
				YearsLived = actor.getAge(),
				Genome = actor.subspecies?.nucleus != null ? actor.subspecies.nucleus.getListForSave() : null,
				Traits = ReadSavedTraits(actor),
				ActorSnapshot = DeepCopy(actor.getData() as ActorData),
				SubspeciesSnapshot = DeepCopy(actor.subspecies?.data),
				CultureSnapshot = DeepCopy(actor.culture?.data),
				LanguageSnapshot = DeepCopy(actor.language?.data),
				ClanSnapshot = DeepCopy(actor.clan?.data),
				EnshrineTime = DateTime.Now.Ticks,
				ReturnCount = 0
			};
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
		/// v0.18.85：联动清理——圣所与神系回归池的同一单位一并删除（彻底清除跨存档数据，不留残留）
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
				}
				// 联动清理回归池同名记录（自动传承池——删除圣所单位时一并清除，避免文件夹残留）
				var rein = _globalData.Reincarnations.FirstOrDefault(g => g.Name == name && g.OriginWorld == originWorld);
				if (rein != null)
				{
					_globalData.Reincarnations.Remove(rein);
				}
				SaveGlobalData();
			}
		}

		// ============================================================
		//  神系回归池（v0.18.83 跨存档自动传承 + 席位轮换）
		//  - 自动入池：登神级(11)/真神(12)/超神(13)突破瞬间快照（复制，不移动世界）
		//  - 席位轮换（避免永远是同一个单位）：
		//      ① 段位分档容量：11阶池≤16 / 12阶池≤7 / 13阶池≤3（对应世界名额）
		//      ② 满员顶替：新入池者顶掉同段位最老（入池时间最早），席位必然流转
		//      ③ 回归即出池：召回新世界后从池中删除（同一快照最多回归 1 次）
		//      ④ 升级迁移：单位升入更高段位时，从低段位池移除（每单位占一席）
		// ============================================================

		/// <summary>回归池段位容量（对应世界登神/真神/超神名额）</summary>
		private const int CAP_REINCARNATION_T13 = 3;
		private const int CAP_REINCARNATION_T12 = 7;
		private const int CAP_REINCARNATION_T11 = 16;

		/// <summary>
		/// 自动入池（突破回调调用）：登神级及以上单位突破瞬间复制快照入回归池。
		/// 单位继续留在当前世界；池满时顶替同段位最老，席位轮换。
		/// </summary>
		public static void AutoEnshrineGod(Actor actor)
		{
			if (actor == null || !actor.isAlive() || World.world == null) return;
			int tier = CultivationData.GetRealmTier(actor);
			if (tier < 11 || tier > 13) return;

			try
			{
				TrueGodData godData = BuildTrueGodData(actor);
				int cap = tier == 13 ? CAP_REINCARNATION_T13 : (tier == 12 ? CAP_REINCARNATION_T12 : CAP_REINCARNATION_T11);

				lock (_lock)
				{
					if (!_initialized) Init();
					var pool = _globalData.Reincarnations;

					// ④ 升级迁移：先移除同名（跨段位占位）——单位升段后以新段位重新入池
					var existing = pool.FirstOrDefault(g => g.Name == godData.Name && g.OriginWorld == godData.OriginWorld);
					if (existing != null) pool.Remove(existing);

					// ② 满员顶替：同段位按入池时间最老者移除（席位轮换——不会永远是同一个单位）
					var sameTier = pool.Where(g => g.RealmTier == tier).ToList();
					if (sameTier.Count >= cap)
					{
						var oldest = sameTier.OrderBy(g => g.EnshrineTime).FirstOrDefault();
						if (oldest != null) pool.Remove(oldest);
					}

					pool.Add(godData);
					SaveGlobalData();
				}

				DSDebug.Verbose($"[GeneCultivation] 神系回归池：{godData.Name} 突破至{GetTierName(tier)}自动入池（席位 {GetTierPoolCount(tier)}/{cap}）");
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[GeneCultivation] 自动入池失败: {e.Message}");
			}
		}

		/// <summary>获取某段位回归池当前数量（UI/日志用）</summary>
		private static int GetTierPoolCount(int tier)
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				return _globalData.Reincarnations.Count(g => g.RealmTier == tier);
			}
		}

		/// <summary>
		/// 召回回归池单位到当前世界（新世界开局注入）。
		/// ③ 回归即出池：召回成功即从池中删除（一次性），席位空出给新突破者。
		/// </summary>
		public static int ReturnGodToWorld(int count)
		{
			if (World.world == null || count <= 0) return 0;
			int returned = 0;

			try
			{
				lock (_lock)
				{
					if (!_initialized) Init();
					var pool = _globalData.Reincarnations;
					if (pool.Count == 0) return 0;

					// 随机取 count 个（打乱，避免每次都是同一批最强单位）
					var candidates = pool.OrderBy(g => Guid.NewGuid()).Take(count).ToList();
					foreach (var god in candidates)
					{
						Actor spawned = SpawnTrueGod(god);
						if (spawned != null && spawned.isAlive())
						{
							pool.Remove(god);
							returned++;
							DSDebug.Verbose($"[GeneCultivation] 神系回归：{god.Name} 降临新世界（出池，席位流转）");
						}
					}
					SaveGlobalData();
				}
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[GeneCultivation] 召回回归池异常: {e.Message}");
			}
			return returned;
		}

		/// <summary>
		/// 神名念诵召唤（v0.18.86）：随机召回回归池 1 位神到当前世界（回归即出池，席位轮换）。
		/// 返回召回单位名称（无神可召/失败返回 null）。
		/// </summary>
		public static string TrySummonRandomGod()
		{
			if (World.world == null) return null;
			try
			{
				lock (_lock)
				{
					if (!_initialized) Init();
					var pool = _globalData.Reincarnations;
					if (pool.Count == 0) return null;

					// 随机取 1 位（打乱，避免永远召唤同一批）
					var god = pool.OrderBy(g => Guid.NewGuid()).First();
					Actor spawned = SpawnTrueGod(god);
					if (spawned != null && spawned.isAlive())
					{
						pool.Remove(god);
						SaveGlobalData();
						DSDebug.Verbose($"[GeneCultivation] 神名念诵：{god.Name} 应召降临（出池，席位流转）");
						return god.Name;
					}
				}
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[GeneCultivation] 神名念诵召唤异常: {e.Message}");
			}
			return null;
		}

		/// <summary>获取回归池全部单位（UI 展示用）</summary>
		public static List<TrueGodData> GetReincarnations()
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				return new List<TrueGodData>(_globalData.Reincarnations);
			}
		}

		/// <summary>删除回归池单个单位（管理功能）</summary>
		public static void DeleteReincarnation(string name, string originWorld)
		{
			lock (_lock)
			{
				if (!_initialized) Init();
				var god = _globalData.Reincarnations.FirstOrDefault(g => g.Name == name && g.OriginWorld == originWorld);
				if (god != null)
				{
					_globalData.Reincarnations.Remove(god);
					SaveGlobalData();
				}
			}
		}

		/// <summary>段位名（11登神/12真神/13超神）</summary>
		private static string GetTierName(int tier)
		{
			if (tier == 13) return "超神级";
			if (tier == 12) return "真神级";
			if (tier == 11) return "登神级";
			return "T" + tier;
		}

		/// <summary>
		/// 放置真神（v0.18.52 移动语义：放置=移动完成）
		/// 整档快照优先：loadObject 全量恢复（所有数据原样，含境界/神名/基因/氏族/文化/语言）；
		/// 旧记录（升级前，无整档）降级：业务字段重建。
		/// </summary>
		public static Actor SpawnTrueGod(TrueGodData godData)
		{
			if (godData == null || World.world == null) return null;

			try
			{
				// 整档快照优先：所有数据完整保留（v0.18.52）
				if (godData.ActorSnapshot != null)
				{
					Actor god = RestoreFullSnapshot(godData);
					if (god != null && god.isAlive())
					{
						// 神名注册表一致性：整档已含神名（custom_data_string），按现存神名登记（同角色跨存档复刻放行）
						string east = ReadDivineName(god, "divine_name");
						string west = ReadDivineName(god, "divine_name_west");
						Code.Realm.RealmMechanics.RestoreSpawnedGodDivineName(god, east, west);

						DSDebug.Verbose($"[TrueGodManager] 真神 {godData.Name} 已整档恢复（移动完成）");
						return god;
					}
					DSDebug.Warning("[TrueGodManager] 整档恢复失败，回退业务字段重建");
				}

				// 旧记录降级：业务字段重建（登神亚种基因+境界+特质+神名）
				return SpawnTrueGodLegacy(godData);
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 生成真神失败: {e.Message}");
				return null;
			}
		}

		/// <summary>
		/// 整档恢复（v0.18.52）：亚种/文化/语言/氏族各自 loadObject 重建（换新id），
		/// 单位 ActorData 注入 units.loadObject——数据 100% 原样，境界/神名/基因等 custom_data 全部保留。
		/// </summary>
		private static Actor RestoreFullSnapshot(TrueGodData godData)
		{
			try
			{
				var subspeciesData = DeepCopy(godData.SubspeciesSnapshot);
				var languageData = DeepCopy(godData.LanguageSnapshot);
				var cultureData = DeepCopy(godData.CultureSnapshot);
				var clanData = DeepCopy(godData.ClanSnapshot);
				var data = DeepCopy(godData.ActorSnapshot);
				if (data == null) return null;

				// 关联数据重建（沿用原版 loadObject 载入；id 全部换新，避免与目标存档冲突）
				if (subspeciesData != null)
				{
					subspeciesData.id = World.world.map_stats.getNextId("subspecies");
					subspeciesData.created_time = World.world.map_stats.world_time;
					data.subspecies = subspeciesData.id;
					World.world.subspecies.loadObject(subspeciesData);
				}
				if (languageData != null)
				{
					languageData.id = World.world.map_stats.getNextId("language");
					languageData.created_time = World.world.map_stats.world_time;
					data.language = languageData.id;
					World.world.languages.loadObject(languageData);
				}
				if (cultureData != null)
				{
					cultureData.id = World.world.map_stats.getNextId("culture");
					cultureData.created_time = World.world.map_stats.world_time;
					data.culture = cultureData.id;
					World.world.cultures.loadObject(cultureData);
				}
				if (clanData != null)
				{
					clanData.id = World.world.map_stats.getNextId("clan");
					clanData.created_time = World.world.map_stats.world_time;
					data.clan = clanData.id;
					World.world.clans.loadObject(clanData);
				}

				// 单位本体：换新id、落到生成点、注入世界
				data.id = World.world.map_stats.getNextId("unit");
				WorldTile tile = FindSpawnTile();
				if (tile == null) return null;
				if (World.world.cities.get(data.cityID) == null) data.cityID = -1;
				data.homeBuildingID = -1;
				data.transportID = -1;
				data.x = tile.pos.x;
				data.y = tile.pos.y;
				World.world.units.loadObject(data);

				return GetActorById(data.id);
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 整档恢复异常: {e.Message}");
				return null;
			}
		}

		/// <summary>按 id 从世界取单位实例（loadObject 注入后拿回 Actor）</summary>
		private static Actor GetActorById(long id)
		{
			try
			{
				if (World.world?.units == null) return null;
				var units = World.world.units.units_only_alive;
				if (units == null) return null;
				for (int i = 0; i < units.Count; i++)
				{
					var a = units[i];
					if (a != null && a.getID() == id) return a;
				}
			}
			catch (Exception) { }
			return null;
		}

		/// <summary>旧记录降级路径（无整档快照）：按业务字段重建（原 v0.18.51 逻辑）</summary>
		private static Actor SpawnTrueGodLegacy(TrueGodData godData)
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

				// 真·复刻：先重建基因序列（专属亚种），再登神升华（升华会基于恢复后的基因做协调效应）
				RestoreGenome(god, godData.Genome);

				// 恢复境界：allowBigJump 跳过跳阶限制（放置=直接恢复 13 阶，不是从 0 突破）；
				// suppressBirth 跳过完整诞生链（切纪元/弹通知/重复存档在 OnGUI 点击帧内执行易原生崩溃）
				CultivationData.SetRealmTier(god, 13, allowBigJump: true, suppressBirth: true);
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

				// 真·复刻：恢复特质快照（登神特质/修炼特质/完美生命）
				RestoreTraits(god, godData.Traits);

				// v0.18.51：恢复独属神名（快照有则写回+登记；旧记录无神名则补授唯一神名）
				// 只恢复神名本身，不触发氏族/神眷/宗教/切纪元等诞生链（放置≠诞生）
				Code.Realm.RealmMechanics.RestoreSpawnedGodDivineName(god, godData.DivineName, godData.DivineNameWest);

				DSDebug.Verbose($"[TrueGodManager] 真神 {godData.Name} 已按原种族 {assetId} 放置（基因+特质+神名已复刻）");
				return god;
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 生成真神失败: {e.Message}");
				return null;
			}
		}

		/// <summary>放置=真·复刻：按快照重建基因序列（专属亚种，不污染共享亚种；super/void 反射恢复）</summary>
		private static void RestoreGenome(Actor god, List<ChromosomeData> genome)
		{
			try
			{
				if (god == null || genome == null || genome.Count == 0) return;
				if (god.current_tile == null) return;

				// 创建专属亚种承载基因（登神单位基因是单位独有，不能写入共享亚种）
				Subspecies sp = World.world.subspecies.newSpecies(god.asset, god.current_tile);
				if (sp == null || sp.nucleus == null) return;

				// 清空默认染色体，按快照逐条重建（Chromosome public 构造 + genes 公开列表 + Nucleus.addChromosome/setDirty）
				sp.nucleus.reset();
				foreach (ChromosomeData cd in genome)
				{
					string chrType = string.IsNullOrEmpty(cd.chromosome_type) ? "chromosome_medium" : cd.chromosome_type;
					Chromosome chr = new Chromosome(chrType, false);
					foreach (string geneId in cd.loci)
					{
						GeneAsset gene = AssetManager.gene_library.get(geneId);
						if (gene != null) chr.genes.Add(gene);
					}
					// 恢复放大器位/空位（反射写 private readonly 列表内容；失败不影响普通基因）
					try
					{
						var ampField = typeof(Chromosome).GetField("_loci_amplifiers",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						if (ampField != null && cd.super_loci != null && cd.super_loci.Count > 0)
						{
							((List<int>)ampField.GetValue(chr)).AddRange(cd.super_loci);
						}
					}
					catch (Exception) { }
					try
					{
						var emptyField = typeof(Chromosome).GetField("_loci_empty",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						if (emptyField != null && cd.void_loci != null && cd.void_loci.Count > 0)
						{
							((List<int>)emptyField.GetValue(chr)).AddRange(cd.void_loci);
						}
					}
					catch (Exception) { }
					sp.nucleus.addChromosome(chr);
				}
				sp.nucleus.setDirty();

				// 切换到专属亚种（原版 public API，与登神升华/维度空间同一套单位亚种切换）
				god.setSubspecies(sp);
				DSDebug.Verbose($"[TrueGodManager] 真神 {god.getName()} 基因序列已复刻（{genome.Count} 条染色体）");
			}
			catch (Exception e)
			{
				DSDebug.Warning($"[TrueGodManager] 恢复基因序列失败: {e.Message}");
			}
		}

		/// <summary>恢复特质快照（addTrait 底层不拦 can_be_given；失败逐个跳过）</summary>
		private static void RestoreTraits(Actor god, List<string> traits)
		{
			if (god == null || traits == null) return;
			foreach (string traitId in traits)
			{
				if (string.IsNullOrEmpty(traitId)) continue;
				try { god.addTrait(traitId, false); }
				catch (Exception) { }
			}
		}

		/// <summary>读取单位特质快照（ActorData.saved_traits 为 public List<string>，反编译 ActorData.cs:30）</summary>
		private static List<string> ReadSavedTraits(Actor actor)
		{
			try
			{
				ActorData data = actor.getData() as ActorData;
				if (data == null || data.saved_traits == null) return null;
				return new List<string>(data.saved_traits);
			}
			catch (Exception) { return null; }
		}

		/// <summary>读取单位神名键（ActorData.custom_data_string）</summary>
		private static string ReadDivineName(Actor actor, string key)
		{
			try
			{
				var data = ActorDataAccessor.GetData(actor);
				if (data == null || data.custom_data_string == null) return null;
				return data.custom_data_string.TryGetValue(key, out string v) ? v : null;
			}
			catch (Exception) { return null; }
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




