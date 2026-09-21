using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ai;
using Code.Data;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 变身/重生兼容性补丁。
    /// 原版 ActionLibrary.metamorphInto 调用 ActorTool.copyUnitToOtherUnit 复制单位数据，
    /// 但 copyImportantData 不复制 custom_data（custom_data_int/float/string/bool），
    /// 导致凤凰重生或变身成动物后，境界、基因、能量等模组数据全部丢失。
    /// 本补丁在 copyUnitToOtherUnit 后追加 custom_data 复制。
    /// 注意：Actor.data 和 custom_data_* 都是 private/protected 字段，必须用反射访问。
    /// </summary>
    [HarmonyPatch(typeof(ActorTool), nameof(ActorTool.copyUnitToOtherUnit))]
    public static class MetamorphosisCompatPatch
    {
        private static FieldInfo _dataField;
        private static FieldInfo _customIntField;
        private static FieldInfo _customFloatField;
        private static FieldInfo _customStringField;
        private static FieldInfo _customBoolField;

        [HarmonyPostfix]
        public static void Postfix(Actor pParent, Actor pCloneTarget, bool pCopyAge = true)
        {
            try
            {
                if (pParent == null || pCloneTarget == null) return;

                // 缓存反射字段（只初始化一次）
                if (_dataField == null)
                {
                    _dataField = typeof(Actor).GetField("data",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_dataField == null) return;

                    Type dataType = _dataField.FieldType;
                    _customIntField = dataType.GetField("custom_data_int",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _customFloatField = dataType.GetField("custom_data_float",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _customStringField = dataType.GetField("custom_data_string",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _customBoolField = dataType.GetField("custom_data_bool",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                object parentData = _dataField.GetValue(pParent);
                object cloneData = _dataField.GetValue(pCloneTarget);
                if (parentData == null || cloneData == null) return;

                // 只有当源单位有模组自定义数据时才复制（避免给普通单位创建空容器）
                bool hasDsData = false;
                if (_customIntField != null && _customIntField.GetValue(parentData) != null) hasDsData = true;
                if (_customFloatField != null && _customFloatField.GetValue(parentData) != null) hasDsData = true;
                if (_customStringField != null && _customStringField.GetValue(parentData) != null) hasDsData = true;
                if (_customBoolField != null && _customBoolField.GetValue(parentData) != null) hasDsData = true;

                if (!hasDsData) return;

                // 复制 custom_data（引用赋值，CustomDataContainer 是引用类型）
                if (_customIntField != null)
                {
                    object val = _customIntField.GetValue(parentData);
                    if (val != null) _customIntField.SetValue(cloneData, val);
                }
                if (_customFloatField != null)
                {
                    object val = _customFloatField.GetValue(parentData);
                    if (val != null) _customFloatField.SetValue(cloneData, val);
                }
                if (_customStringField != null)
                {
                    object val = _customStringField.GetValue(parentData);
                    if (val != null) _customStringField.SetValue(cloneData, val);
                }
                if (_customBoolField != null)
                {
                    object val = _customBoolField.GetValue(parentData);
                    if (val != null) _customBoolField.SetValue(cloneData, val);
                }

                // 确保新单位的境界特质与数据同步（变身时特质已被 copyUnitToOtherUnit 复制）
                CultivationData.SyncFromTraits(pCloneTarget);
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] MetamorphosisCompatPatch 异常: " + e.Message);
            }
        }
    }
}


