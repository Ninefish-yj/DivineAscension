using System;
using Newtonsoft.Json;
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
					// v0.18.51：神名跨存档快照（东名+西名），放置时恢复独属神名
					DivineName = ReadDivineName(actor, "divine_name"),
					DivineNameWest = ReadDivineName(actor, "divine_name_west"),
										UnlockedElements = CultivationData.GetUnlockedElements(actor),
					ActiveCombos = CultivationData.GetActiveCombos(actor),
					Qi = CultivationData.GetEnergy(actor),
					Mastery = CultivationData.GetMastery(actor),
					Turbulence = CultivationData.GetTurbulence(actor),
					EnlightenmentExp = CultivationData.GetEnlightenmentExp(actor),
					AscensionTime = DateTime.Now.Ticks,
					OriginWorld = World.world?.name ?? "Unknown",
					KillCount = 0,
					YearsLived = actor.getAge(),
						// 基因/特质快照（放置=真·复刻：登神单位亚种为专属亚种，nucleus 即原单位基因）
						Genome = actor.subspecies?.nucleus != null ? actor.subspecies.nucleus.getListForSave() : null,
						Traits = ReadSavedTraits(actor)
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




