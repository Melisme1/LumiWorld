using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>Optional Editor undo hooks; runtime generation has no UnityEditor dependency.</summary>
    public sealed class BiomeLayoutEditActions
    {
        public Action<GameObject> registerCreatedGroup;
        public Action<GameObject> destroyExistingGroup;
        public Action<Transform, Transform> reparentPreserved;
    }
}
