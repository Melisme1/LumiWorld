using UnityEngine;
namespace LumiWorld.Acs
{
    [DisallowMultipleComponent]
    public sealed class BiomePropLock : MonoBehaviour
    {
        [HideInInspector] public string roleId;
        [HideInInspector] public int slot=-1;
    }
}