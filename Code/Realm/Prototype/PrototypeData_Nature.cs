// ============================================================
// 异能数据 · 自然系（v0.18.0）
// 大类：自然系 | 小类：植物/动物/气象/毒/治愈/野兽/藤蔓
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Nature
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 植物 ----
            new PrototypeDef("na_plant_01", "植物亲和", "治疗自身，攻击减速缠绕目标", "自然系", "植物",
                PrototypeTag.Heal, PrototypeTag.Slow, 2),
            new PrototypeDef("na_plant_02", "荆棘之躯", "受击反击麻痹攻击者，护盾减伤", "自然系", "植物",
                PrototypeTag.Vibration, PrototypeTag.Shield, 2),
            new PrototypeDef("na_plant_03", "花海疗愈", "受击自愈回血，攻击附加中毒", "自然系", "植物",
                PrototypeTag.Regeneration, PrototypeTag.Poison, 2),
            // ---- 动物 ----
            new PrototypeDef("na_beast_01", "野兽直觉", "身法闪避，攻击暴击", "自然系", "动物",
                PrototypeTag.Dodge, PrototypeTag.Crit, 1),
            new PrototypeDef("na_beast_02", "狼群之怒", "攻击狂暴当次+25%，溅射波及", "自然系", "动物",
                PrototypeTag.Berserk, PrototypeTag.Chain, 1),
            new PrototypeDef("na_beast_03", "猛兽扑击", "攻击击退眩晕，减速目标", "自然系", "动物",
                PrototypeTag.Knockback, PrototypeTag.Slow, 0),
            // ---- 气象 ----
            new PrototypeDef("na_weather_01", "呼风唤雨", "攻击溅射波及，治疗自身", "自然系", "气象",
                PrototypeTag.Chain, PrototypeTag.Heal, 4),
            new PrototypeDef("na_weather_02", "雷电风雨", "攻击溅射连锁，麻痹眩晕", "自然系", "气象",
                PrototypeTag.Chain, PrototypeTag.Stun, 4),
            new PrototypeDef("na_weather_03", "沙尘暴", "攻击减速目标，概率眩晕定身", "自然系", "气象",
                PrototypeTag.Slow, PrototypeTag.Stun, 4),
            // ---- 毒 ----
            new PrototypeDef("na_poison_01", "剧毒之触", "攻击附加中毒持续伤害，诅咒加深", "自然系", "毒",
                PrototypeTag.Poison, PrototypeTag.Curse, 5),
            new PrototypeDef("na_poison_02", "万毒之体", "受击反击震慑攻击者，攻击中毒", "自然系", "毒",
                PrototypeTag.Sound, PrototypeTag.Poison, 5),
            new PrototypeDef("na_poison_03", "毒雾弥漫", "攻击溅射波及，附加持续伤害", "自然系", "毒",
                PrototypeTag.Chain, PrototypeTag.Burn, 5),
            // ---- 治愈 ----
            new PrototypeDef("na_heal_01", "自然治愈", "攻击概率回复生命，受击自愈", "自然系", "治愈",
                PrototypeTag.Heal, PrototypeTag.Regeneration, 2),
            new PrototypeDef("na_heal_02", "生命赞歌", "治疗强化，护盾减伤", "自然系", "治愈",
                PrototypeTag.Soul, PrototypeTag.Shield, 2),
            new PrototypeDef("na_heal_03", "回春术", "受击回血强化，闪避免伤", "自然系", "治愈",
                PrototypeTag.Blessing, PrototypeTag.Dodge, 2),
            // ---- 野兽 ----
            new PrototypeDef("na_wild_01", "野性之血", "攻击吸血，暴击破甲", "自然系", "野兽",
                PrototypeTag.Lifesteal, PrototypeTag.Crit, 0),
            new PrototypeDef("na_wild_02", "熊之力", "攻击念力贯穿高伤，击退眩晕", "自然系", "野兽",
                PrototypeTag.Telekinesis, PrototypeTag.Knockback, 0),
            new PrototypeDef("na_wild_03", "豹之迅捷", "闪避强化，攻击暴击", "自然系", "野兽",
                PrototypeTag.Illusion, PrototypeTag.Crit, 1),
            // ---- 藤蔓 ----
            new PrototypeDef("na_vine_01", "藤蔓束缚", "攻击眩晕定身，减速目标", "自然系", "藤蔓",
                PrototypeTag.Stun, PrototypeTag.Slow, 2),
            new PrototypeDef("na_vine_02", "丛林绞杀", "攻击重力压制，附加持续伤害", "自然系", "藤蔓",
                PrototypeTag.Gravity, PrototypeTag.Burn, 2),
            new PrototypeDef("na_vine_03", "绿意屏障", "护盾减伤，受击自愈", "自然系", "藤蔓",
                PrototypeTag.Shield, PrototypeTag.Regeneration, 2),
        };
    }
}
