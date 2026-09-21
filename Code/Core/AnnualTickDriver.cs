// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Data;

namespace Code.Core
{
    /// <summary>
    /// 全局年度Tick驱动组件。挂在持久化GameObject上，每帧检查时间。
    /// 不遍历单位，仅在时间跨越年度阈值时触发全局结算。
    /// 使用Time.deltaTime累加，不依赖World.world.getCurWorldTime()的纪元重置问题。
    /// </summary>
    public class AnnualTickDriver : MonoBehaviour
    {
        // 游戏时间单位：1年=60天游戏世界时间（getCurWorldTime以游戏天计）
        private const float SECONDS_PER_YEAR = 60f;

        // 累加的游戏时间（天）
        private double _accumulatedTime = 0f;
		// 上次记录的游戏世界时间（用于 getCurWorldTime 差值，接轨游戏速度/暂停）
		private double _lastWorldTime = 0f;
        // 上一次触发年度结算的时间
        private double _lastSettleTime = 0f;
        // 是否初始化
        private bool _initialized = false;

        // 调试计数器
        private int _debugFrameCounter = 0;
        private const int DEBUG_INTERVAL = 600; // 每600帧输出一次

        //  持续生命恢复计时器（按游戏时间累加）
        private static double _regenAccumulator = 0f;
        private const double REGEN_INTERVAL = 1.0; // 每1天游戏时间恢复一次

        //  真实时间节流：高倍速（如x2000）下防止年度结算/生命恢复按游戏时间爆炸触发
        private static double _lastSettleReal = 0f;   // 上次年度结算的真实时间
        private static double _lastRegenReal = 0f;    // 上次生命恢复的真实时间
        private const double SETTLE_REAL_INTERVAL = 0.25; // 年度结算每真实0.25秒最多一次
        private const double REGEN_REAL_INTERVAL = 0.5;   // 生命恢复每真实0.5秒最多一次

        // 挑战战斗期间暂停生命恢复（否则回血比伤害快，杀不死）
        public static bool _challengeDuelActive = false;

        // 战斗中暂停恢复：记录单位上次血量和最近受伤时间
        private static readonly Dictionary<long, float> _lastKnownHealth = new Dictionary<long, float>();
        private static readonly Dictionary<long, float> _lastDamagedTime = new Dictionary<long, float>();

        /// <summary>查询某单位是否处于"战斗压制"状态（挑战战斗中 / 最近10秒内受伤）。
        /// 供 Harmony patch 外部治疗入口（restoreHealth）使用，实现全局恢复总闸。</summary>
        public static bool IsInCombatSuppression(long actorId)
        {
            if (_challengeDuelActive) return true;
            float t;
            if (_lastDamagedTime.TryGetValue(actorId, out t))
                return Time.time - t < 10f;
            return false;
        }

        // 持续生命恢复速度表（每秒恢复最大血量的百分比）
        // 设计：低境界慢恢复，高境界快恢复，战斗中也能持续回血
        private static readonly float[] _regenRatePerSecond =
        {
            0f,       // 0阶（无）
            0.0003f,  // 1阶 觉醒者：0.06%/秒（约28分钟回满）
            0.0004f,  // 2阶 共振者：0.08%/秒
            0.0005f,  // 3阶 强化者：0.1%/秒
            0.0006f,  // 4阶 突变者：0.12%/秒
            0.0007f,  // 5阶 调控者：0.14%/秒
            0.0008f,  // 6阶 场域者：0.16%/秒
            0.001f,   // 7阶 具象者：0.2%/秒（约8分钟回满，接近原版再生特质）
            0.0012f,  // 8阶 干涉者：0.24%/秒
            0.0015f,  // 9阶 解析者：0.3%/秒（约5.6分钟回满）
            0.002f,   // 10阶 使徒级：0.4%/秒
            0.003f,   // 11阶 登神级：0.6%/秒
            0.012f,   // 12阶 真神级：2.4%/秒（约42秒回满）
            0.025f    // 13阶 超神级：5%/秒（约20秒回满，神的恢复速度就是这么快）
        };

        // 公共调试属性（供配置tab显示）
        public static double AccumulatedTime { get; private set; }
        public static double LastSettleTime { get; private set; }
        public static bool TickInitialized { get; private set; }
        public static int UpdateCallCount { get; private set; }

        void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // 更新公共调试属性
            UpdateCallCount++;
            AccumulatedTime = _accumulatedTime;
            LastSettleTime = _lastSettleTime;
            TickInitialized = _initialized;

            // 世界未加载时不处理
            if (World.world == null) return;

			// 使用游戏世界时间驱动（接轨游戏速度与暂停：加速时快进、暂停时停走）
			// 世界切换/读档时 getCurWorldTime 可能回退，检测到回退则重置基准、不触发结算
			double worldTime = (double)World.world.getCurWorldTime();
			if (!_initialized)
			{
				_lastWorldTime = worldTime;
				_lastSettleTime = 0.0;
				_initialized = true;
				DSDebug.Verbose($"AnnualTickDriver年度Tick初始化: 游戏时间={_accumulatedTime:F1}天");
				return;
			}
			double timeDiff = (double)worldTime - (double)_lastWorldTime;
			_lastWorldTime = worldTime;
			if (timeDiff < 0.0)
			{
				// 时间回退（读档/世界切换）：重置基准，不结算
				return;
			}
			// 正向跳变（高倍速/加速）不再丢弃：照常累计，由下方真实时间节流控制结算频率
			_accumulatedTime += timeDiff;

			//  每帧更新待播放特效（技能延迟特效 + 虚拟武器延迟特效 + 循环特效停止）
			try { Code.Realm.DSSkillEffectManager.UpdatePendingEffects(Time.deltaTime); } catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] Anonymous 异常: " + dsEx.Message); }
			try { Code.Realm.DSSkillEffectManager.UpdateScheduledStops(Time.deltaTime); } catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] Anonymous 异常: " + dsEx.Message); }
			try { Code.Combat.VirtualWeaponSystem.UpdateDelayedEffects(Time.deltaTime); } catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] Anonymous 异常: " + dsEx.Message); }



			//  持续生命恢复（按游戏时间累加，每1天游戏时间触发一次；真实时间节流防止高倍速下每帧遍历）
			// 解决：之前BasicRegeneration只在年度结算时恢复，战斗中受伤后老半天补不回血
			// 挑战战斗期间暂停生命恢复
		if (!_challengeDuelActive)
		{
			_regenAccumulator += timeDiff;
			if (_regenAccumulator >= REGEN_INTERVAL && Time.realtimeSinceStartup - _lastRegenReal >= REGEN_REAL_INTERVAL)
			{
				_regenAccumulator -= REGEN_INTERVAL;
				_lastRegenReal = Time.realtimeSinceStartup;
				try { ApplyContinuousRegeneration(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] Anonymous 异常: " + dsEx.Message); }
			}
			}

			// 每600帧输出一次调试信息（仅在详细日志模式下）
			_debugFrameCounter++;
			if (_debugFrameCounter % DEBUG_INTERVAL == 0)
			{
				DSDebug.Verbose($"AnnualTickDriver心跳: 帧{_debugFrameCounter}, 累计时间={_accumulatedTime:F1}天 ({_accumulatedTime / SECONDS_PER_YEAR:F1}年), 上次结算={_lastSettleTime:F1}天");
			}

			// 年度结算：真实时间节流（每真实0.25秒最多一次），单次最多补50年防单帧卡顿
			// 高倍速（x2000）下游戏时间快速前进，真实节流保证结算稳定跟随而不逐帧爆炸
			if (Time.realtimeSinceStartup - _lastSettleReal >= SETTLE_REAL_INTERVAL)
			{
				_lastSettleReal = Time.realtimeSinceStartup;
				int yearsToSettle = Mathf.FloorToInt((float)((_accumulatedTime - _lastSettleTime) / SECONDS_PER_YEAR));
				yearsToSettle = Mathf.Clamp(yearsToSettle, 0, 50);

	            if (yearsToSettle > 0)
	            {
	                _lastSettleTime += yearsToSettle * SECONDS_PER_YEAR;
	                for (int i = 0; i < yearsToSettle; i++)
	                {
	                    AnnualTickManager.TriggerGlobalAnnualSettlement();
	                }
	                DSDebug.Verbose($"AnnualTickDriver全局年度Tick触发: {yearsToSettle}年, 累计时间={_accumulatedTime:F1}天");
	            }
			}
        }

        // ============================================================
        //  持续生命恢复系统
        // ============================================================

        /// <summary>
        /// 持续生命恢复：每1秒游戏时间对所有活跃异能者恢复一定比例的最大血量。
        /// 解决：之前BasicRegeneration只在年度结算（每60秒游戏时间）时恢复一次，
        ///       战斗中受伤后要等很久才会回血。
        /// 设计：按境界递增恢复速度，低境界慢恢复，高境界快恢复。
        /// 性能：只遍历活跃异能者列表（不遍历全图单位），严格遵守禁令4。
        /// </summary>
        private static void ApplyContinuousRegeneration()
        {
            var activeIds = AnnualTickManager.GetActiveCultivatorIds();
            if (activeIds == null || activeIds.Count == 0) return;

            int healedCount = 0;
            foreach (long id in activeIds)
            {
                try
                {
                    Actor actor = World.world.units.get(id);
                    if (actor == null || !actor.isAlive()) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier <= 0 || tier >= _regenRatePerSecond.Length) continue;

                    float regenRate = _regenRatePerSecond[tier];
                    if (regenRate <= 0f) continue;

                    // GE生命恢复加成（基于基因能量，上限+50%）
                    float geLifeRegenBonus = GeneEnergyCalculator.GetLifeRegenBonus(actor);
                    // 纪元生命恢复加成（直接读取纪元效果，如生命纪元+25%）
                    float eraLifeRegenBonus = GeneEnergyCalculator.GetEraLifeRegenBonus();
                    // 神创基因治疗加成：独立百分比乘（影响持续生命恢复）
                    float divineHeal = GeneEnergyCalculator.GetDivineHealBonus(actor);
                    // 总恢复速度 = 基础速度 × (1 + GE加成 + 纪元加成 + 神创治疗加成)
                    float totalRegenRate = regenRate * (1f + geLifeRegenBonus + eraLifeRegenBonus + divineHeal);

                    // 使用标准方法获取当前血量和最大血量
                    // 注意：当前血量存储在 actor.data.health（int类型），不是 actor.hp 字段
                    float currentHp = actor.getHealth();
                    float maxHp = actor.getMaxHealth();
                    if (maxHp <= 0f || currentHp >= maxHp) continue;

                    // 战斗压制：最近10秒内受伤过就不恢复（战斗中不准回血）
                    if (!_lastKnownHealth.ContainsKey(id))
                    {
                        _lastKnownHealth[id] = currentHp;
                        _lastDamagedTime[id] = Time.time;
                    }
                    else
                    {
                        float lastHp = _lastKnownHealth[id];
                        if (currentHp < lastHp - 1f)
                        {
                            _lastDamagedTime[id] = Time.time;
                        }
                        _lastKnownHealth[id] = currentHp;

                        // 最近10秒内受伤过，暂停恢复
                        if (Time.time - _lastDamagedTime[id] < 10f)
                        {
                            continue;
                        }
                    }

                    // 恢复：每秒恢复最大血量的totalRegenRate比例，至少恢复1点
                    float healAmount = Mathf.Max(1f, maxHp * totalRegenRate);
                    float newHp = Mathf.Min(currentHp + healAmount, maxHp);

                    // 设置血量（使用 ActorDataAccessor.GetData 获取数据，与原版治疗逻辑一致）
                    // 注意：Actor.data 是私有字段，不能直接访问，必须通过 ActorDataAccessor
                    var actorData = ActorDataAccessor.GetData(actor);
                    if (actorData != null)
                    {
                        actorData.health = (int)newHp;
                    }

                    healedCount++;
                }
                catch { /* 忽略单个单位恢复失败 */ }
            }

            // 生命恢复是正常运行，不输出日志（避免刷屏）

        }
        /// <summary>LateUpdate：已移除EndFrame调用，InputBlocker现在使用帧计数器自动清空</summary>
        void LateUpdate()
        {
            // InputBlocker.EndFrame() 已移除，改为在ReportWindowRect中按帧自动清空
            // 原因：PlayerControl的方法在Update中执行，需要使用上一帧的窗口位置
        }

        /// <summary>重置时间追踪（读档/新世界时调用）</summary>
        public void ResetTracker()
        {
            _initialized = false;
            _accumulatedTime = 0f;
            _lastSettleTime = 0f;
        }

        /// <summary>
        /// 真正的原版战斗协程（不提前算胜负，让原版战斗系统自己打）
        /// 打完后根据谁活下来决定结果
        /// </summary>
        public System.Collections.IEnumerator ChallengeDuelRealCoroutine(Actor challenger, Actor defender, int tier)
        {
            if (challenger == null || defender == null) yield break;

            string tierName = Code.UI.UILocalization.GetTierName(tier);
            string challengerName = challenger.getName();
            string defenderName = defender.getName();

            // 记录原始位置（打完后恢复）
            Vector2 challengerOrigPos = challenger.current_position;
            Vector2 defenderOrigPos = defender.current_position;

            // 在原地播放传送特效
            PlayTeleportEffect(challenger);
            PlayTeleportEffect(defender);

            // 把挑战者传送到被挑战者旁边（相邻tile）
            try
            {
                Vector2 duelPos = new Vector2(defender.current_position.x + 1, defender.current_position.y);
                TeleportActor(challenger, duelPos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Warning($"[DivineAscension] 传送挑战者失败: {e.Message}，保持原位置");
            }

            // 等待一帧，让单位位置更新
            yield return new WaitForSeconds(0.1f);

            // 在新位置播放传送特效
            PlayTeleportEffect(challenger);

            // 强制设置两个单位互相攻击（通过反射设置攻击目标）
            try
            {
                SetAttackTarget(challenger, defender);
                SetAttackTarget(defender, challenger);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Warning($"[DivineAscension] 设置攻击目标失败: {e.Message}");
            }

            // 等待战斗结束：不死不休，一直打到一方死亡为止
            // 但是挑战者血量低于10%且和防御者差距太大时，可以逃跑（逃跑=终身不能突破）
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                if (challenger == null || defender == null) break;
                if (!challenger.isAlive() || !defender.isAlive()) break;

                // 检查挑战者是不是在逃跑（原版逃跑任务：task_unit_flee 逃亡中）
                try
                {
                    if (challenger != null && defender != null)
                    {
                        // 只检测原版的逃跑任务，攻击目标为空不算逃跑
                        bool isFleeing = false;
                        try
                        {
                            isFleeing = challenger.isTask("flee");
                        }
                        catch { }

                        if (isFleeing)
                        {
                            // 只有同阶挑战逃跑才终身禁赛，跨阶挑战逃跑不禁止突破
                            int chTier = Code.Data.CultivationData.GetRealmTier(challenger);
                            int dfTier = Code.Data.CultivationData.GetRealmTier(defender);
                            if (chTier == dfTier)
                            {
                                Code.Core.DSDebug.Verbose($"[DivineAscension] 同阶挑战 {challengerName} 逃跑了！终身不能再突破");
                                // 同阶逃跑了，终身不能再突破
                                Code.Data.CultivationData.SetLifetimeBan(challenger);
                            }
                            else
                            {
                                Code.Core.DSDebug.Verbose($"[DivineAscension] 跨阶挑战 {challengerName} 逃跑了，不禁止突破");
                            }
                            // 停止防御者的攻击
                            defender.clearAttackTarget();
                            break;
                        }
                    }
                }
                catch { }
            }

            // 战斗结束后，根据谁活下来决定结果
            bool challengerWins = challenger != null && challenger.isAlive() && (defender == null || !defender.isAlive());

            // 判断是否同阶挑战（只有同阶才回退/禁赛，跨阶战斗不回退不禁赛）
            bool isSameTierChallenge = false;
            try
            {
                int chTier = Code.Data.CultivationData.GetRealmTier(challenger);
                int dfTier = Code.Data.CultivationData.GetRealmTier(defender);
                isSameTierChallenge = (chTier == dfTier);
            }
            catch { }

            // 检查被挑战者是否逃跑
            bool defenderFled = false;
            try
            {
                if (defender != null && defender.isAlive())
                {
                    defenderFled = defender.isTask("flee");
                }
            }
            catch { }

            // 检查挑战者是否逃跑（可能已经处理过，再查一次用于结果判定）
            bool challengerFled = false;
            try
            {
                if (challenger != null && challenger.isAlive())
                {
                    challengerFled = challenger.isTask("flee");
                }
            }
            catch { }

            // 记录历史事件
            try
            {
                if (challengerWins)
                {
                    // 挑战者赢了，真正执行突破
                    Code.Core.DSDebug.Verbose($"[DivineAscension] {tierName}名额已满，{challengerName} 挑战上位成功，{defenderName} 陨落腾出位置");

                    // 挑战者真正突破到目标境界
                    try
                    {
                        Code.Realm.RealmJudge.DoBreakthrough(challenger);
                    }
                    catch (Exception e)
                    {
                        Code.Core.DSDebug.Warning($"[DivineAscension] 挑战者突破失败: {e.Message}");
                    }

                    Code.Core.DSEventManager.RecordEvent(
                        Code.Core.DSEventType.Warning,
                        string.Format(Code.UI.UILocalization.Get("tier_cap_kill_title"), tierName, challengerName, defenderName),
                        string.Format(Code.UI.UILocalization.Get("tier_cap_kill_desc"), tierName, Code.Realm.RealmJudge.GetTierCap(tier), challengerName, defenderName),
                        defenderName, defender.getID(), tier, defender, true);
                }
                else if (defenderFled && isSameTierChallenge)
                {
                    // 同阶被挑战者逃跑：回退一阶，挑战者上位
                    Code.Core.DSDebug.Verbose($"[DivineAscension] 同阶被挑战者 {defenderName} 逃跑了，回退一阶");
                    Code.Realm.RealmJudge.DemoteActor(defender, tier - 1);

                    // 挑战者成功上位
                    try
                    {
                        Code.Realm.RealmJudge.DoBreakthrough(challenger);
                    }
                    catch (Exception e)
                    {
                        Code.Core.DSDebug.Warning($"[DivineAscension] 挑战者突破失败: {e.Message}");
                    }
                }
                else if (defenderFled)
                {
                    // 跨阶被挑战者逃跑：不回退，但挑战者赢（原版战斗已分出高下）
                    Code.Core.DSDebug.Verbose($"[DivineAscension] 跨阶被挑战者 {defenderName} 逃跑了，不回退");
                    try
                    {
                        Code.Realm.RealmJudge.DoBreakthrough(challenger);
                    }
                    catch (Exception e)
                    {
                        Code.Core.DSDebug.Warning($"[DivineAscension] 挑战者突破失败: {e.Message}");
                    }
                }
                else if (challengerFled)
                {
                    // 挑战者逃跑了：同阶已终身禁赛（前面处理），跨阶不禁止
                    Code.Core.DSDebug.Verbose($"[DivineAscension] 挑战者 {challengerName} 逃跑，挑战失败");
                    if (isSameTierChallenge)
                    {
                        Code.Data.CultivationData.SetLifetimeBan(challenger);
                        Code.Core.DSDebug.Verbose($"[DivineAscension] 同阶挑战逃跑，{challengerName} 终身不能再突破");
                    }
                }
                else if (challenger != null && challenger.isAlive())
                {
                    // 挑战者输了但没死（战斗被中断）：同阶回退一阶，跨阶不回退
                    if (isSameTierChallenge)
                    {
                        Code.Core.DSDebug.Verbose($"[DivineAscension] 同阶挑战 {challengerName} 失败但没死，回退一阶");
                        Code.Realm.RealmJudge.DemoteActor(challenger, tier - 1);
                    }
                    else
                    {
                        Code.Core.DSDebug.Verbose($"[DivineAscension] 跨阶挑战 {challengerName} 失败但没死，不回退不禁止突破");
                    }
                }
                else
                {
                    // 挑战者死了
                    Code.Core.DSDebug.Verbose($"[DivineAscension] {tierName}名额已满，{challengerName} 挑战上位失败，被 {defenderName} 反杀陨落");
                    Code.Core.DSEventManager.RecordEvent(
                        Code.Core.DSEventType.Warning,
                        string.Format(Code.UI.UILocalization.Get("tier_cap_challenge_fail_title"), tierName, challengerName, defenderName),
                        string.Format(Code.UI.UILocalization.Get("tier_cap_challenge_fail_desc"), tierName, Code.Realm.RealmJudge.GetTierCap(tier), challengerName, defenderName),
                        challengerName, challenger.getID(), tier, challenger, true);
                }
            }
            catch { }

            // 移除破境之势状态
            // 状态效果持续5秒后自动消失，不用主动移除

            // 战斗结束后，恢复原始位置
            try
            {
                if (challenger != null && challenger.isAlive())
                {
                    PlayTeleportEffect(challenger);
                    TeleportActor(challenger, challengerOrigPos);
                    PlayTeleportEffect(challenger);
                }
                if (defender != null && defender.isAlive())
                {
                    PlayTeleportEffect(defender);
                    TeleportActor(defender, defenderOrigPos);
                    PlayTeleportEffect(defender);
                }
            }
            catch { }
        }
        private void TeleportActor(Actor actor, Vector2 pos)
        {
            try
            {
                if (actor == null) return;

                // 尝试调用原版teleport方法
                var teleportMethod = actor.GetType().GetMethod("teleport",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (teleportMethod != null)
                {
                    teleportMethod.Invoke(actor, new object[] { pos });
                    return;
                }

                // 备用：尝试调用moveTo方法
                var moveToMethod = actor.GetType().GetMethod("moveTo",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (moveToMethod != null)
                {
                    moveToMethod.Invoke(actor, new object[] { pos });
                    return;
                }

                // 最后备用：直接设置current_position
                var posProperty = actor.GetType().GetProperty("current_position",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (posProperty != null && posProperty.CanWrite)
                {
                    posProperty.SetValue(actor, pos);
                }
            }
            catch { }
        }

        /// <summary>播放传送特效</summary>
        private void PlayTeleportEffect(Actor actor)
        {
            try
            {
                if (actor == null) return;
                // 尝试调用原版teleportEffect方法
                var effectMethod = actor.GetType().GetMethod("teleportEffect",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (effectMethod != null)
                {
                    effectMethod.Invoke(actor, null);
                }
            }
            catch { }
        }

        /// <summary>通过反射设置单位的攻击目标</summary>
        private void SetAttackTarget(Actor attacker, Actor target)
        {
            try
            {
                if (attacker == null || target == null) return;

                // 尝试调用Actor的setAttackTarget方法
                var attackMethod = attacker.GetType().GetMethod("setAttackTarget",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (attackMethod != null)
                {
                    attackMethod.Invoke(attacker, new object[] { target });
                    return;
                }

                // 备用：尝试调用setTarget方法
                var setTargetMethod = attacker.GetType().GetMethod("setTarget",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setTargetMethod != null)
                {
                    setTargetMethod.Invoke(attacker, new object[] { target });
                    return;
                }

                DSDebug.Warning("[DivineAscension] 找不到setAttackTarget/setTarget方法");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 设置攻击目标异常: {e.Message}");
            }
        }
    }
}


