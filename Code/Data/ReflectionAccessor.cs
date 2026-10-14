// ============================================================

using System;
using Code.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Data
{
    /// <summary>
    /// SystemManager扩展访问器
    /// </summary>
    public static class SystemManagerExtensions
    {
        /// <summary>通过ID查找Actor（直接访问units字典）</summary>
        public static Actor FindActorById(long id)
        {
            try
            {
                if (World.world == null || World.world.units == null) return null;
                var dict = World.world.units.dict;
                if (dict != null && dict.ContainsKey(id))
                {
                    return dict[id] as Actor;
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[GeneCultivation] FindActorById失败: " + e.Message);
            }
            return null;
        }
    }
}
