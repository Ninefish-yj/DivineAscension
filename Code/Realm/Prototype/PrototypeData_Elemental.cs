// ============================================================
// 异能数据 · 元素系（v0.18.0）
// 大类：元素系 | 小类：火/水/风/雷/冰/土/木/光/暗/金属
// 注意：fire/water/… 10 个 id 为 v0.17.0 旧档异能，禁止改名/删（旧档 ds_prototype 兼容）。
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Elemental
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 火 ----
            new PrototypeDef("fire", "御火", "灼烧敌人，攻击波及周围", "元素系", "火",
                PrototypeTag.Burn, PrototypeTag.Chain, 6),
            new PrototypeDef("el_fire_02", "炎爆", "强化灼烧，攻击溅射连锁", "元素系", "火",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6),
            new PrototypeDef("el_fire_03", "业火", "狂暴当次攻击+25%，附加灼烧", "元素系", "火",
                PrototypeTag.Berserk, PrototypeTag.Burn, 6),
            new PrototypeDef("el_fire_04", "火衣", "受击概率自愈，灼烧反击", "元素系", "火",
                PrototypeTag.Regeneration, PrototypeTag.Burn, 6),
            // ---- 水 ----
            new PrototypeDef("water", "御水", "治疗自身，护盾减伤", "元素系", "水",
                PrototypeTag.Heal, PrototypeTag.Shield, 2),
            new PrototypeDef("el_water_02", "潮汐", "攻击溅射波及周围，减速目标", "元素系", "水",
                PrototypeTag.Chain, PrototypeTag.Slow, 2),
            new PrototypeDef("el_water_03", "洪流", "受击回血，受击概率免伤", "元素系", "水",
                PrototypeTag.Blessing, PrototypeTag.Dodge, 2),
            // ---- 风 ----
            new PrototypeDef("wind", "疾风", "身法闪避，攻击减速目标", "元素系", "风",
                PrototypeTag.Dodge, PrototypeTag.Slow, 1),
            new PrototypeDef("el_wind_02", "龙卷", "攻击溅射连锁，概率震晕敌人", "元素系", "风",
                PrototypeTag.Chain, PrototypeTag.Stun, 1),
            new PrototypeDef("el_wind_03", "御风", "闪避强化，受击概率免伤", "元素系", "风",
                PrototypeTag.Illusion, PrototypeTag.Dodge, 1),
            // ---- 雷 ----
            new PrototypeDef("lightning", "驭雷", "攻击溅射连锁，概率麻痹眩晕", "元素系", "雷",
                PrototypeTag.Chain, PrototypeTag.Stun, 6),
            new PrototypeDef("el_lightning_02", "雷锤", "攻击念力贯穿高伤，麻痹", "元素系", "雷",
                PrototypeTag.Telekinesis, PrototypeTag.Stun, 6),
            new PrototypeDef("el_lightning_03", "磁场", "受击麻痹攻击者，攻击眩晕", "元素系", "雷",
                PrototypeTag.Magnetism, PrototypeTag.Stun, 6),
            // ---- 冰 ----
            new PrototypeDef("ice", "霜降", "减速冻结目标，附加霜冻持续伤害", "元素系", "冰",
                PrototypeTag.Slow, PrototypeTag.Burn, 4),
            new PrototypeDef("el_ice_02", "暴风雪", "深度冰冻目标，当次攻击+5%寒霜微伤", "元素系", "冰",
                PrototypeTag.Frostbite, PrototypeTag.Frost, 4),
            new PrototypeDef("el_ice_03", "冰甲", "护盾减伤，受击反击震慑攻击者", "元素系", "冰",
                PrototypeTag.Shield, PrototypeTag.Vibration, 4),
            // ---- 土 ----
            new PrototypeDef("earth", "地裂", "护盾减伤，攻击波及周围", "元素系", "土",
                PrototypeTag.Shield, PrototypeTag.Petrify, 2),
            new PrototypeDef("el_earth_02", "岩铠", "护盾减伤，受击自愈回血", "元素系", "土",
                PrototypeTag.Shield, PrototypeTag.ElementResist, 2),
            new PrototypeDef("el_earth_03", "山崩", "攻击重力压制目标，溅射波及", "元素系", "土",
                PrototypeTag.Gravity, PrototypeTag.Chain, 2),
            // ---- 木 ----
            new PrototypeDef("wood", "御木", "治疗自身，攻击减速缠绕目标", "元素系", "木",
                PrototypeTag.Heal, PrototypeTag.Slow, 2),
            new PrototypeDef("el_wood_02", "森罗", "受击回血，攻击附加中毒", "元素系", "木",
                PrototypeTag.Regeneration, PrototypeTag.Poison, 2),
            new PrototypeDef("el_wood_03", "缠藤", "攻击眩晕定身，减速目标", "元素系", "木",
                PrototypeTag.Stun, PrototypeTag.Slow, 2),
            // ---- 光 ----
            new PrototypeDef("light", "圣光", "治疗自身，护盾减伤", "光明系", "圣光",
                PrototypeTag.Heal, PrototypeTag.Shield, 3),
            new PrototypeDef("el_light_02", "净化", "攻击概率回复生命，治疗强化", "光明系", "净化",
                PrototypeTag.Soul, PrototypeTag.Cleanse, 3),
            new PrototypeDef("el_light_03", "光罩", "受击免伤强化，受击回血", "光明系", "圣光",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 3),
            // ---- 暗 ----
            new PrototypeDef("dark", "暗影", "攻击吸血，附加暗蚀持续伤害", "黑暗系", "暗影",
                PrototypeTag.Lifesteal, PrototypeTag.Burn, 5),
            new PrototypeDef("el_dark_02", "猎杀", "攻击吸血强化，概率暴击", "黑暗系", "暗影",
                PrototypeTag.Blood, PrototypeTag.Crit, 5),
            new PrototypeDef("el_dark_03", "侵蚀", "攻击附加诅咒持续伤害，恐惧眩晕", "黑暗系", "夜幕",
                PrototypeTag.Curse, PrototypeTag.Weakness, 5),
            // ---- 金属 ----
            new PrototypeDef("metal", "御金", "攻击暴击破甲，护盾硬化", "元素系", "金属",
                PrototypeTag.Crit, PrototypeTag.Shield, 0),
            new PrototypeDef("el_metal_02", "钢铁洪流", "攻击念力贯穿高伤，溅射波及", "元素系", "金属",
                PrototypeTag.Telekinesis, PrototypeTag.Chain, 0),
            new PrototypeDef("el_metal_03", "金身", "护盾减伤强化，受击反震眩晕攻击者", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 0),
        };
    }
}
