// ============================================================
// 异能数据 · 时空系（v0.18.0）
// 大类：时空系 | 小类：时间/空间/次元/引力/速度
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_SpaceTime
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 时间 ----
            new PrototypeDef("st_time_01", "加速", "攻击时缓强减速目标，暴击", "时空系", "时间",
                PrototypeTag.TimeSlow, PrototypeTag.Crit, 4),
            new PrototypeDef("st_time_02", "时停", "攻击深度冰冻目标，概率眩晕", "时空系", "时间",
                PrototypeTag.Frostbite, PrototypeTag.Stun, 4),
            new PrototypeDef("st_time_03", "回溯", "受击自愈回血，攻击治疗", "时空系", "时间",
                PrototypeTag.Regeneration, PrototypeTag.Heal, 4),
            // ---- 空间 ----
            new PrototypeDef("st_space_01", "瞬移", "闪避强化，受击概率免伤", "时空系", "空间",
                PrototypeTag.Illusion, PrototypeTag.Teleport, 4),
            new PrototypeDef("st_space_02", "空间斩", "攻击念力贯穿高伤，溅射波及", "时空系", "空间",
                PrototypeTag.Telekinesis, PrototypeTag.Chain, 4),
            new PrototypeDef("st_space_03", "壁垒", "护盾减伤，受击免伤", "时空系", "空间",
                PrototypeTag.Shield, PrototypeTag.Dodge, 4),
            // ---- 次元 ----
            new PrototypeDef("st_dimension_01", "裂缝", "攻击溅射波及，附加灼烧", "时空系", "次元",
                PrototypeTag.Chain, PrototypeTag.Burn, 6),
            new PrototypeDef("st_dimension_02", "折叠", "攻击重力压制目标，恐惧眩晕", "时空系", "次元",
                PrototypeTag.Gravity, PrototypeTag.Fear, 6),
            new PrototypeDef("st_dimension_03", "相位", "受击免伤最高，受击回血", "时空系", "次元",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 6),
            // ---- 引力 ----
            new PrototypeDef("st_gravity_01", "御重", "攻击重力压制，溅射波及", "时空系", "引力",
                PrototypeTag.Gravity, PrototypeTag.Chain, 6),
            new PrototypeDef("st_gravity_02", "重力场", "攻击眩晕定身目标，减速", "时空系", "引力",
                PrototypeTag.Stun, PrototypeTag.Slow, 6),
            new PrototypeDef("st_gravity_03", "引壁", "护盾减伤，受击反击麻痹攻击者", "时空系", "引力",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 6),
            // ---- 速度 ----
            new PrototypeDef("st_speed_01", "极速", "身法闪避，攻击暴击", "时空系", "速度",
                PrototypeTag.Dodge, PrototypeTag.Crit, 1),
            new PrototypeDef("st_speed_02", "瞬连", "攻击暴击破甲，减速目标", "时空系", "速度",
                PrototypeTag.Crit, PrototypeTag.MultiShot, 1),
            new PrototypeDef("st_speed_03", "音速", "攻击击退眩晕，溅射波及", "时空系", "速度",
                PrototypeTag.Knockback, PrototypeTag.Chain, 1),
        };
    }
}
