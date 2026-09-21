// ============================================================

using System;
using Code.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Data
{
    /// <summary>
    /// Actor扩展访问器
    /// </summary>
    public static class ActorExtensions
    {
        /// <summary>获取Actor的attackedBy字段（攻击者，直接访问）</summary>
        public static BaseSimObject GetAttackedBy(this Actor actor)
        {
            return actor?.attackedBy;
        }

        /// <summary>获取Actor的data字段（直接访问）</summary>
        public static ActorData GetData(this Actor actor)
        {
            return actor?.data;
        }
    }

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
                DSDebug.Warning("[DivineAscension] FindActorById失败: " + e.Message);
            }
            return null;
        }
    }

    /// <summary>
    /// UnitWindow扩展访问器
    /// </summary>
    public static class UnitWindowExtensions
    {
        /// <summary>获取UnitWindow的actor属性（直接访问）</summary>
        public static Actor GetActor(this UnitWindow window)
        {
            return window?.actor;
        }
    }
}