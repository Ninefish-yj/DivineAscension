// ============================================================

using System;
using Code.Core;
using System.Collections.Generic;
using System.Text;
using Code.Data;
using Code.UI;
using UnityEngine;

namespace Code.Sect
{
    /// <summary>组织职位等级（现代异能体系）</summary>
    public enum SectRank
    {
        None = 0,           // 无组织
        Probationary = 1,   // 见习成员 - 刚加入，考察期
        Member = 2,         // 正式成员 - 通过考察，正式加入
        Senior = 3,         // 资深成员 - 经验丰富，能力较强
        Captain = 4,        // 队长 - 带领小队执行任务
        Commander = 5,      // 指挥官 - 管理多个小队，负责区域
        Leader = 6,         // 领袖 - 组织最高领导人
        DeputyLeader = 7,   // 副领袖 - 纵向管理职位，领袖副手，有实际管理权
        Guardian = 8,       // 守护者 - 专业身份，负责保护组织重要人物/设施
        Advisor = 9,        // 顾问 - 荣誉特邀身份，组织外部专业顾问
        Heritor = 10,       // 继承者 - 荣誉身份，掌握组织核心机密与能力图谱
        Founder = 11        // 创始人 - 特殊身份，组织创建者，永久荣誉
    }

    /// <summary>异能组织数据（运行时）</summary>
    public class SectData
    {
        public string Id;
        public string Name;
        public long MasterId;        // 首领单位ID
        public int FoundingYear;     // 创立年份
        public List<long> MemberIds = new List<long>();
        public float TotalContribution;
        public int HeritageGen;      // 组织届数（首领换届+1）
        public string AncestorName;  // 创始人姓名（含称号，用于组织详情显示）
        public int AncestorTier;     // 创始人境界（用于列表简洁显示）
        public int Breaks;           // 传承断裂次数（首领缺失未换届的次数，随首领/成员存档）
        public int BreakYear;        // 最近一次断裂的年份（防同一断裂重复计数）
    }

    public static class SectManager
    {
        // 运行时组织字典（key=组织ID）
        private static readonly Dictionary<string, SectData> _sects = new Dictionary<string, SectData>();
        // 灭门组织档案（死灰复燃·限时）：组织全灭时留存，窗口期内新组织可概率继承
        private static readonly List<SectArchive> _archives = new List<SectArchive>();
        /// <summary>灭门档案复燃窗口（年）——窗口期内可死灰复燃，超时永久湮灭</summary>
        public const int SECT_ARCHIVE_WINDOW_YEARS = 50;
        /// <summary>创建组织时继承灭门档案的概率</summary>
        public const float SECT_REVIVE_CHANCE = 0.3f;

        /// <summary>设置档案列表（读档时调用）</summary>
        public static void SetArchives(List<SectArchive> list)
        {
            _archives.Clear();
            if (list != null) _archives.AddRange(list);
        }

        /// <summary>获取档案列表（保存时调用）</summary>
        public static List<SectArchive> GetArchives()
        {
            return _archives;
        }

        /// <summary>过滤超时档案（超时永久湮灭）</summary>
        public static List<SectArchive> PruneArchives(List<SectArchive> list)
        {
            var result = new List<SectArchive>();
            if (list == null) return result;
            int currentYear = GetCurrentYear();
            foreach (var a in list)
            {
                if (a == null || string.IsNullOrEmpty(a.Name)) continue;
                if (a.DeathYear > 0 && currentYear - a.DeathYear > SECT_ARCHIVE_WINDOW_YEARS) continue;
                result.Add(a);
            }
            return result;
        }

        /// <summary>组织全灭时留存档案（首领死亡解散/年度清理时调用）</summary>
        public static void RecordSectArchive(SectData sect)
        {
            if (sect == null) return;
            try
            {
                var a = new SectArchive
                {
                    Name = sect.Name,
                    AncestorName = sect.AncestorName,
                    AncestorTier = sect.AncestorTier,
                    Breaks = sect.Breaks,
                    BreakYear = sect.BreakYear,
                    DeathYear = GetCurrentYear()
                };
                // 同名覆盖，防止重复留存
                _archives.RemoveAll(x => x != null && x.Name == a.Name);
                _archives.Add(a);
                DSDebug.Verbose($"组织 {sect.Name} 灭亡，档案已留存（{SECT_ARCHIVE_WINDOW_YEARS}年内可死灰复燃）");
            }
            catch (Exception e)
            {
                DSDebug.Warning("留存组织档案失败: " + e.Message);
            }
        }

        /// <summary>挑选可复燃的灭门档案（窗口期内 + 概率）</summary>
        private static SectArchive PickReviveArchive()
        {
            var candidates = new List<SectArchive>();
            foreach (var a in _archives)
            {
                if (a == null || string.IsNullOrEmpty(a.Name)) continue;
                if (a.DeathYear > 0 && GetCurrentYear() - a.DeathYear <= SECT_ARCHIVE_WINDOW_YEARS)
                    candidates.Add(a);
            }
            if (candidates.Count == 0) return null;
            if (UnityEngine.Random.value > SECT_REVIVE_CHANCE) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>初始化组织系统（世界加载时调用，从单位数据重建组织）</summary>
        public static void Init()
        {
            _sects.Clear();
            RebuildFromActors();
            DSDebug.Verbose("组织系统初始化完成");
        }

        /// <summary>从所有单位的异能组织数据重建运行时异能组织列表</summary>
        public static void RebuildFromActors()
        {
            if (World.world == null || World.world.units == null) return;
            _sects.Clear();

            foreach (var actor in World.world.units.units_only_alive)
            {
                if (actor == null) continue;
                string sectId = CultivationData.GetSectId(actor);
                if (string.IsNullOrEmpty(sectId)) continue;

                if (!_sects.ContainsKey(sectId))
                {
                    _sects[sectId] = new SectData { Id = sectId, Name = sectId };
                }
                SectData sect = _sects[sectId];
                long id = actor.getID();
                if (!sect.MemberIds.Contains(id))
                    sect.MemberIds.Add(id);

                // 首领：恢复组织元数据（组织名/体系/创立年/创始人/贡献/届数，全部随首领单位存档）
                if (CultivationData.GetSectRank(actor) == (int)SectRank.Leader)
                {
                    sect.MasterId = id;
                    sect.Name = CultivationData.GetSectName(actor) ?? sect.Name;
                    sect.FoundingYear = CultivationData.GetSectFoundedYear(actor);
                    sect.AncestorName = CultivationData.GetSectAncestorName(actor);
                    sect.TotalContribution = CultivationData.GetSectContribution(actor);
                    sect.HeritageGen = CultivationData.GetHeritageGen(actor);
                    sect.Breaks = CultivationData.GetSectBreaks(actor);
                    sect.BreakYear = CultivationData.GetSectBreakYear(actor);
                    if (sect.HeritageGen <= 0) sect.HeritageGen = 1;
                }
            }

            // 首领缺失（首领陨落/读档时不在场）兜底：从成员恢复组织名并推举临时首领，防止组织名显示为乱码sectId
            foreach (var sect in _sects.Values)
            {
                if (sect.MasterId > 0) continue;
                Actor heir = null;
                int best = -1;
                foreach (long mid in sect.MemberIds)
                {
                    Actor m = FindActorById(mid);
                    if (m == null || !m.isAlive()) continue;
                    int tier = CultivationData.GetRealmTier(m);
                    if (tier > best) { best = tier; heir = m; }
                }
                if (heir == null) continue;
                sect.MasterId = heir.getID();
                if (string.IsNullOrEmpty(sect.Name) || sect.Name.StartsWith("sect_"))
                {
                    string n = CultivationData.GetSectName(heir);
                    if (!string.IsNullOrEmpty(n) && !n.StartsWith("sect_")) sect.Name = n;
                    else sect.Name = heir.getName() + UILocalization.Get("sect_common_suffix_1");
                }
                if (sect.FoundingYear <= 0) sect.FoundingYear = GetCurrentYear();
                // 创始人名优先从任意成员恢复（不只看临时首领），并写回临时首领保证下次读档仍在
                if (string.IsNullOrEmpty(sect.AncestorName))
                {
                    foreach (long mid in sect.MemberIds)
                    {
                        Actor m = FindActorById(mid);
                        if (m == null) continue;
                        string ancestor = CultivationData.GetSectAncestorName(m);
                        if (!string.IsNullOrEmpty(ancestor)) { sect.AncestorName = ancestor; break; }
                    }
                }
                if (!string.IsNullOrEmpty(sect.AncestorName))
                    CultivationData.SetSectAncestorName(heir, sect.AncestorName);
                if (sect.HeritageGen <= 0) sect.HeritageGen = Mathf.Max(1, CultivationData.GetHeritageGen(heir));
                // 传承断裂计数：首领缺失=一次传承断裂事件。用断裂年份防同一断裂多次读档重复计数。
                int oldBreaks = 0, oldBreakYear = 0;
                foreach (long mid in sect.MemberIds)
                {
                    Actor m = FindActorById(mid);
                    if (m == null) continue;
                    int b = CultivationData.GetSectBreaks(m);
                    int y = CultivationData.GetSectBreakYear(m);
                    if (b > oldBreaks) oldBreaks = b;
                    if (y > oldBreakYear) oldBreakYear = y;
                }
                int currentYear = GetCurrentYear();
                if (oldBreakYear == 0 || oldBreakYear < currentYear)
                {
                    // 新一次传承断裂（此前未计过本次断裂）
                    sect.Breaks = oldBreaks + 1;
                    sect.BreakYear = currentYear;
                }
                else
                {
                    // 同一断裂的重复读档，不重复计数
                    sect.Breaks = oldBreaks;
                    sect.BreakYear = oldBreakYear;
                }
                // 写回临时首领，保证下次读档仍能恢复
                CultivationData.SetSectBreaks(heir, sect.Breaks);
                CultivationData.SetSectBreakYear(heir, sect.BreakYear);
                DSDebug.Verbose($"组织 {sect.Name} 首领缺失，推举 {heir.getName()} 为临时首领（传承断裂第{sect.Breaks}次）");
            }
        }

        /// <summary>
        /// 随机生成组织名称（现代异能风格）
        /// 根据体系不同生成不同风格的名称
        /// </summary>
        /// <summary>
        /// 随机生成组织名称（现代异能风格）
        /// </summary>
        public static string GenerateRandomSectName()
        {
            // 通用名称组件
            string[] commonPrefixes = { UILocalization.Get("sect_common_prefix_1"), UILocalization.Get("sect_common_prefix_2"), UILocalization.Get("sect_common_prefix_3"), UILocalization.Get("sect_common_prefix_4"), UILocalization.Get("sect_common_prefix_5"), UILocalization.Get("sect_common_prefix_6"), UILocalization.Get("sect_common_prefix_7"), UILocalization.Get("sect_common_prefix_8"), UILocalization.Get("sect_common_prefix_9"), UILocalization.Get("sect_common_prefix_10") };
            string[] commonCores = { UILocalization.Get("sect_common_prefix_11"), UILocalization.Get("sect_common_prefix_12"), UILocalization.Get("sect_common_prefix_13"), UILocalization.Get("sect_common_prefix_14"), UILocalization.Get("sect_common_prefix_15"), UILocalization.Get("sect_common_prefix_16"), UILocalization.Get("sect_common_prefix_17"), UILocalization.Get("sect_common_prefix_18"), UILocalization.Get("sect_common_prefix_19"), UILocalization.Get("sect_common_prefix_20") };
            string[] commonSuffixes = { UILocalization.Get("sect_common_suffix_1"), UILocalization.Get("sect_common_suffix_2"), UILocalization.Get("sect_common_suffix_3"), UILocalization.Get("sect_common_suffix_4"), UILocalization.Get("sect_common_suffix_5"), UILocalization.Get("sect_common_suffix_6"), UILocalization.Get("sect_common_suffix_7"), UILocalization.Get("sect_common_suffix_8"), UILocalization.Get("sect_common_suffix_9"), UILocalization.Get("sect_common_suffix_10") };

            // 随机组合：前缀 + 核心 + 后缀
            string prefix = commonPrefixes[UnityEngine.Random.Range(0, commonPrefixes.Length)];
            string core = commonCores[UnityEngine.Random.Range(0, commonCores.Length)];
            string suffix = commonSuffixes[UnityEngine.Random.Range(0, commonSuffixes.Length)];

            // 有30%概率只用前缀+后缀（更简洁）
            if (UnityEngine.Random.value < 0.3f)
            {
                return prefix + suffix;
            }

            return prefix + core + suffix;
        }

        /// <summary>创立组织</summary>
        public static SectData FoundSect(Actor founder, string sectName)
        {
            if (founder == null) return null;
            int tier = CultivationData.GetRealmTier(founder);
            if (tier < 1)
            {
                DSDebug.Verbose("创立组织失败：需要1阶以上异能者");
                return null;
            }
            if (!string.IsNullOrEmpty(CultivationData.GetSectId(founder)))
            {
                DSDebug.Verbose("创立组织失败：已属于其他组织");
                return null;
            }

            // 三人成组织：至少招募2名成员（同体系优先），一个人不算组织
            List<Actor> recruits = FindRecruits(founder, 2);
            if (recruits.Count < 2)
            {
                DSDebug.Verbose("创立组织失败：组织至少需要3人（创始人+2名成员）");
                return null;
            }

            // 如果名称为空，自动生成；若有灭门档案在窗口期内，概率死灰复燃继承旧组织
            SectArchive revive = null;
            if (string.IsNullOrEmpty(sectName))
            {
                revive = PickReviveArchive();
                if (revive != null)
                {
                    sectName = revive.Name;
                    DSDebug.Verbose($"组织 {sectName} 死灰复燃，{founder.getName()} 继承旧传承（断裂累计第{revive.Breaks + 1}次）");
                }
                else
                {
                    sectName = GenerateRandomSectName();
                }
            }

            string sectId = "sect_" + founder.getID() + "_" + DateTime.Now.Ticks;
            var sect = new SectData
            {
                Id = sectId,
                Name = sectName,
                MasterId = founder.getID(),
                FoundingYear = GetCurrentYear(),
                HeritageGen = 1,
                AncestorName = revive != null ? revive.AncestorName : founder.getName(),
                AncestorTier = revive != null ? revive.AncestorTier : tier,
                Breaks = revive != null ? revive.Breaks + 1 : 0,
                BreakYear = revive != null ? GetCurrentYear() : 0
            };
            // 死灰复燃成功：档案使命完成，移除
            if (revive != null) _archives.Remove(revive);
            sect.MemberIds.Add(founder.getID());
            _sects[sectId] = sect;

            CultivationData.SetSectId(founder, sectId);
            CultivationData.SetSectRank(founder, (int)SectRank.Leader);
            CultivationData.SetHeritageGen(founder, 1);
            CultivationData.SetMasterId(founder, "");
            // 组织元数据写首领单位  随单位存档（纯随档，不跨存档）
            CultivationData.SetSectName(founder, sectName);
            CultivationData.SetSectFoundedYear(founder, sect.FoundingYear);
            CultivationData.SetSectAncestorName(founder, founder.getName());
            // 组织系统接轨特质系统：首领=领袖+创始人
            Code.Traits.TraitManager.ApplySectTrait(founder, (int)SectRank.Leader, true);

            // 拉入招募成员（正式成员）
            foreach (Actor r in recruits)
                JoinSect(r, sectId, SectRank.Member);

            DSDebug.Verbose(string.Format("[DivineAscension] {0} 创立组织 [{1}]（{2}人），异能体系={3}",
                founder.getName(), sectName, sect.MemberIds.Count, ""));
            return sect;
        }

        /// <summary>加入组织</summary>
        public static bool JoinSect(Actor actor, string sectId, SectRank rank = SectRank.Probationary)
        {
            if (actor == null || string.IsNullOrEmpty(sectId)) return false;
            if (!_sects.ContainsKey(sectId)) return false;
            if (!string.IsNullOrEmpty(CultivationData.GetSectId(actor))) return false;

            var sect = _sects[sectId];
            long id = actor.getID();
            if (!sect.MemberIds.Contains(id))
                sect.MemberIds.Add(id);

            CultivationData.SetSectId(actor, sectId);
            CultivationData.SetSectRank(actor, (int)rank);
            // 组织系统接轨特质系统：按职位授予组织身份特质
            Code.Traits.TraitManager.ApplySectTrait(actor, (int)rank, false);
            // 组织名冗余到成员（首领陨落后可从成员恢复组织名）
            CultivationData.SetSectName(actor, sect.Name);
            // 创始人名同样冗余到成员（首领陨落/读档首领缺失时，可从任何成员恢复创始人，避免"无创始人"）
            if (!string.IsNullOrEmpty(sect.AncestorName))
                CultivationData.SetSectAncestorName(actor, sect.AncestorName);
            // 断裂次数同样冗余到成员（读档首领缺失时从成员恢复断裂计数）
            CultivationData.SetSectBreaks(actor, sect.Breaks);
            CultivationData.SetSectBreakYear(actor, sect.BreakYear);

            // 组织届数：新成员继承当前届数（首领换届才+1，见OnLeaderDied）修复原逻辑每加入一人届数+1
            if (sect.MasterId > 0)
            {
                Actor master = FindActorById(sect.MasterId);
                if (master != null)
                {
                    int masterGen = CultivationData.GetHeritageGen(master);
                    CultivationData.SetHeritageGen(actor, masterGen);
                    CultivationData.SetMasterId(actor, master.getID().ToString());
                }
            }
            return true;
        }

        /// <summary>
        /// 组织自动归属（3阶起）：已有有效组织则保留，组织失效则重新加入/创建。
        /// 由境界变化与年度结算调用，让组织系统在游戏内真正运转。
        /// 3阶（突变者）起异能者开始寻求组织归属，低阶（1-2阶）仍可被组织主动招募。
        /// </summary>
        public static void EnsureSectMembership(Actor actor)
        {
            if (actor == null || World.world == null) return;
            try
            {
                if (!CultivationData.IsAscended(actor)) return;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 3) return; // 3阶突变者起才需要组织（降低门槛，让组织有人加入）

                string sid = CultivationData.GetSectId(actor);
                if (!string.IsNullOrEmpty(sid))
                {
                    if (_sects.ContainsKey(sid)) return; // 已有有效组织
                    // 组织已失效（读档/变更）：清空旧归属
                    CultivationData.SetSectId(actor, "");
                    CultivationData.SetSectRank(actor, 0);
                }

                // 优先加入已有组织（随机选择，境界越高越容易被接纳）
                var candidates = new List<SectData>();
                foreach (var s in _sects.Values)
                    if (s.MemberIds.Count > 0 && s.MemberIds.Count < 50) candidates.Add(s); // 组织上限50人

                if (candidates.Count > 0)
                {
                    var pool = candidates;
                    // 加入概率：境界越高越容易被组织接纳
                    float joinChance = 0.3f + (tier * 0.07f); // 3阶51%，13阶121%必进
                    if (UnityEngine.Random.value < joinChance && JoinSect(actor, pool[UnityEngine.Random.Range(0, pool.Count)].Id, SectRank.Member))
                        DSDebug.Verbose($"[DivineAscension] {actor.getName()}（{tier}阶）自动加入组织");
                    return;
                }

                // 没有组织则概率创建（首领=创始人），3阶起可创建，境界越高概率越大
                float foundChance = 0.03f + (tier * 0.02f); // 3阶9%，5阶13%，13阶29%
                if (UnityEngine.Random.value < foundChance && FoundSect(actor, null) != null)
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()}（{tier}阶）自动创建组织（概率{foundChance:P0}）");
            }
            catch (Exception e)
            {
                DSDebug.Verbose("组织自动归属失败: " + e.Message);
            }
        }

        /// <summary>
        /// 首领陨落：从组织成员中推举继任者，组织元数据与届数转移（组织延续；纯随档，不跨存档）
        /// 由 EventHooks.OnActorDeath 调用。继任优先级：管理职位(队长/指挥官/副领袖)  职位高  境界高  资深。
        /// </summary>
        public static void OnLeaderDied(Actor deadLeader)
        {
            if (deadLeader == null) return;
            try
            {
                string sectId = CultivationData.GetSectId(deadLeader);
                if (string.IsNullOrEmpty(sectId) || !_sects.ContainsKey(sectId)) return;
                SectData sect = _sects[sectId];
                if (sect.MasterId != deadLeader.getID()) return; // 非现任首领

                Actor heir = null;
                int heirScore = -1;
                foreach (long mid in sect.MemberIds)
                {
                    if (mid == deadLeader.getID()) continue;
                    Actor m = FindActorById(mid);
                    if (m == null || !m.isAlive()) continue;
                    int rank = CultivationData.GetSectRank(m);
                    int tier = CultivationData.GetRealmTier(m);
                    int score = 0;
                    if ((int)SectRank.Captain <= rank && rank <= (int)SectRank.DeputyLeader) score += 10000;
                    score += rank * 100 + tier * 10 + CultivationData.GetHeritageGen(m);
                    if (score > heirScore) { heirScore = score; heir = m; }
                }
                if (heir == null)
                {
                    RecordSectArchive(sect); // 组织无存活成员，留存灭门档案后解散
                    _sects.Remove(sectId);
                    return;
                }

                // 继承组织元数据 + 换届（届数+1）
                int newGen = Mathf.Max(1, CultivationData.GetHeritageGen(deadLeader) + 1);
                CultivationData.SetSectName(heir, sect.Name);
                CultivationData.SetSectFoundedYear(heir, sect.FoundingYear);
                CultivationData.SetSectAncestorName(heir, sect.AncestorName);
                CultivationData.SetSectBreaks(heir, sect.Breaks);
                CultivationData.SetSectBreakYear(heir, sect.BreakYear);
                CultivationData.SetHeritageGen(heir, newGen);
                CultivationData.SetSectRank(heir, (int)SectRank.Leader);
                CultivationData.SetMasterId(heir, "");
                Code.Traits.TraitManager.ApplySectTrait(heir, (int)SectRank.Leader, false);
                sect.MasterId = heir.getID();
                sect.HeritageGen = newGen;
                DSDebug.Verbose($"[DivineAscension] 首领 {deadLeader.getName()} 陨落，{heir.getName()} 继任组织 [{sect.Name}] 第{newGen}任首领");
            }
            catch (Exception e) { DSDebug.Warning("组织换届失败: " + e.Message); }
        }

        /// <summary>获取异能组织列表</summary>
        public static List<SectData> GetAllSects()
        {
            return new List<SectData>(_sects.Values);
        }

        /// <summary>获取组织</summary>
        public static SectData GetSect(string sectId)
        {
            if (string.IsNullOrEmpty(sectId)) return null;
            return _sects.ContainsKey(sectId) ? _sects[sectId] : null;
        }

        /// <summary>获取职位名称（中文）</summary>
        public static string GetRankName(int rank)
        {
            switch (rank)
            {
                case 1: return UILocalization.Get("sect_rank_probation");
                case 2: return UILocalization.Get("sect_rank_member");
                case 3: return UILocalization.Get("sect_rank_senior");
                case 4: return UILocalization.Get("sect_rank_captain");
                case 5: return UILocalization.Get("sect_rank_commander");
                case 6: return UILocalization.Get("sect_rank_leader");
                case 7: return UILocalization.Get("sect_rank_deputy");
                case 8: return UILocalization.Get("sect_rank_guardian");
                case 9: return UILocalization.Get("sect_rank_advisor");
                case 10: return UILocalization.Get("sect_rank_heritor");
                case 11: return UILocalization.Get("sect_rank_founder");
                default: return UILocalization.Get("sect_rank_none");
            }
        }

        /// <summary>组织贡献：按职位的年度累积量（组织贡献榜/单位窗口显示）</summary>
        public static float GetAnnualContribution(int rank)
        {
            switch (rank)
            {
                case 1: return 5f;    // 见习
                case 2: return 10f;   // 正式
                case 3: return 15f;   // 资深
                case 4: return 20f;   // 队长
                case 5: return 25f;   // 指挥官
                case 6: return 30f;   // 领袖
                case 7: return 28f;   // 副领袖
                case 8: return 18f;   // 守护者
                case 9: return 12f;   // 顾问
                case 10: return 22f;  // 继承者
                case 11: return 35f;  // 创始人
                default: return 0f;
            }
        }

        private static string GetSectName(string sectId)
        {
            if (string.IsNullOrEmpty(sectId)) return UILocalization.Get("sect_rank_none");
            return _sects.ContainsKey(sectId) ? _sects[sectId].Name : sectId;
        }

        private static int GetCurrentYear()
        {
            if (World.world == null) return 0;
            return (int)(World.world.getCurWorldTime() / 365.0);
        }

        /// <summary>招募无组织的异能者入伙（同体系优先，数量不足时任意体系）</summary>
        private static List<Actor> FindRecruits(Actor founder, int count)
        {
            var result = new List<Actor>();
            if (World.world == null || World.world.units == null) return result;
            string dao = "";

            foreach (var actor in World.world.units.units_only_alive)
            {
                if (result.Count >= count) break;
                if (actor == null || actor == founder) continue;
                if (!CultivationData.IsAscended(actor)) continue;
                if (!string.IsNullOrEmpty(CultivationData.GetSectId(actor))) continue;
                if ("" != dao) continue;
                result.Add(actor);
            }
            if (result.Count < count)
            {
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (result.Count >= count) break;
                    if (actor == null || actor == founder || result.Contains(actor)) continue;
                    if (!CultivationData.IsAscended(actor)) continue;
                    if (!string.IsNullOrEmpty(CultivationData.GetSectId(actor))) continue;
                    result.Add(actor);
                }
            }
            return result;
        }

        /// <summary>清理无任何存活成员的空组织（年度结算调用，防"没人组织"残留）</summary>
        public static void CleanupEmptySects()
        {
            var dead = new List<string>();
            foreach (var kv in _sects)
            {
                bool alive = false;
                foreach (long mid in kv.Value.MemberIds)
                {
                    Actor m = FindActorById(mid);
                    if (m != null && m.isAlive()) { alive = true; break; }
                }
                if (!alive) dead.Add(kv.Key);
            }
            foreach (string id in dead)
            {
                if (_sects.TryGetValue(id, out SectData gone)) RecordSectArchive(gone);
                _sects.Remove(id);
                DSDebug.Verbose("已清理空组织: " + id);
            }
        }

        private static Actor FindActorById(long id)
        {
            return SystemManagerExtensions.FindActorById(id);
        }
    }
}



